# Teste Técnico — Desenvolvedor Sênior .NET (Antifraude)

> **Objetivo**  
> Avaliar capacidade de **propor soluções arquiteturais**, documentar decisões e estruturar raciocínio técnico para cenários de alta complexidade.

Este repositório contém:

1. **Enunciado do desafio** (o que o candidato deve entregar).
2. **Solução de referência** em `src/AntiFraud/` (.NET 8, **DDD + SOLID**, Clean Architecture).
3. **DDL** em `src/AntiFraud/scripts/ddl.sql` e **ADRs** em `src/AntiFraud/docs/adr/`.

---

## 1) Contexto

A empresa precisa de um **módulo de avaliação antifraude** para processar transações financeiras.  
O módulo recebe transações, aplica regras e devolve uma decisão: `APPROVED`, `REJECTED` ou `REVIEW`.  
O sistema deve ser **idempotente**, **resiliente** e **auditável**.

---

## 2) Escopo do entregável (candidato)

| Item | Descrição |
|------|-----------|
| **A. Documentação** | Visão geral, fluxo E2E, resiliência, idempotência, observabilidade |
| **B. Diagramas (≥2)** | Componentes/containers + sequência ou fluxo |
| **C. ADRs (≥3)** | Mensageria, banco, idempotência (+ deployment opcional) |
| **D. Contrato API** | `POST /transactions` (Idempotency-Key) e `GET /transactions/{id}` |

Formato: **`README.md`** principal, diagramas Mermaid (ou imagens), ADRs no README ou em arquivos dedicados.

---

## 3) Solução de referência (implementação)

### 3.1 Visão geral da arquitetura

Camadas (Clean Architecture + DDD):

| Camada | Projeto | Responsabilidade |
|--------|---------|------------------|
| **Domain** | `AntiFraud.Domain` | Agregado `Transaction`, value objects (`Money`, `IdempotencyKey`), motor de regras (`IFraudRule`, `FraudRuleEngine`), eventos de domínio |
| **Application** | `AntiFraud.Application` | Casos de uso (`ITransactionService`, `IFraudEvaluationService`), orquestração, ports (`IOutboxStore`, `ITransactionQueuePublisher`) |
| **Infrastructure** | `AntiFraud.Infrastructure` | EF Core + PostgreSQL, RabbitMQ, audit trail, outbox |
| **API** | `AntiFraud.Api` | HTTP, contrato, Serilog; com Rabbit: relay outbox → fila |
| **Worker** | `AntiFraud.Worker` | Avaliação antifraude (outbox ou fila Rabbit, conforme config) |

**SOLID na prática**

- **S** — Serviços de aplicação focados (submit vs evaluate).
- **O** — Novas regras implementam `IFraudRule` sem alterar `FraudRuleEngine`.
- **L** — Repositórios substituíveis via `ITransactionRepository`.
- **I** — Ports pequenos (`IOutboxStore`, `IAuditLogger`).
- **D** — Domain não referencia EF/RabbitMQ; infraestrutura depende do domínio.

### 3.2 Diagrama de componentes (containers)

```mermaid
flowchart TB
    Client[Cliente / Gateway] --> API[AntiFraud.Api]
    API --> PG[(PostgreSQL)]
    API -->|UseRabbitMq true| RMQ[(RabbitMQ)]
    RMQ --> Worker[AntiFraud.Worker]
    Worker -->|UseRabbitMq false| PG
    Worker --> PG
    Worker --> Rules[FraudRuleEngine + velocity]
    API --> Logs[Serilog / OpenTelemetry]
    Worker --> Logs
```

**Modo mensageria** (`Features:UseRabbitMq` — **mesmo valor** na API e no Worker):

| Valor | API | Worker |
|-------|-----|--------|
| `true` | `OutboxRabbitRelayWorker` → Rabbit | `RabbitMqTransactionEvaluationConsumer` |
| `false` | Só persiste outbox | `OutboxTransactionDispatchWorker` → lê outbox no Postgres |

### 3.3 Fluxo de ponta a ponta

```mermaid
sequenceDiagram
    participant C as Cliente
    participant API as AntiFraud.Api
    participant DB as PostgreSQL
    participant Q as RabbitMQ
    participant W as AntiFraud.Worker
    participant EV as FraudEvaluationService

    C->>API: POST /transactions + Idempotency-Key
    API->>DB: Lookup idempotency_key
    alt Nova transação
        API->>DB: INSERT transaction + audit + outbox (1 txn)
        Note over API,DB: POST não publica no Rabbit diretamente
        API-->>C: 202 Accepted + id
    else Replay idempotente
        API-->>C: 200 OK + mesmo payload
    end

    alt UseRabbitMq true
        API->>DB: Relay lê outbox pendente
        API->>Q: Publish transactionId
        Q->>W: Deliver message
    else UseRabbitMq false
        W->>DB: Poll outbox pendente
    end

    W->>EV: ProcessAsync (regras + velocity)
    W->>DB: UPDATE decision + fraud_evaluations + audit
    C->>API: GET /transactions/{id}
    API->>DB: SELECT
    API-->>C: status + APPROVED|REJECTED|REVIEW
```

### 3.4 Resiliência (implementado vs. evolução)

| Mecanismo | Onde | Comportamento |
|-----------|------|----------------|
| **Outbox transacional** | POST + `outbox_messages` | Transação e evento pendente no mesmo commit |
| **Retry outbox** | Relay / dispatch | `attempts` + `last_error`; pendências reprocessadas no loop (~2 s) |
| **Publish failure** | `RabbitMqTransactionQueuePublisher` | Exceção propagada; outbox **não** marcada processada |
| **At-least-once** | Rabbit + outbox | `ProcessAsync` ignora se `status = Completed`; ack/nack no consumer |
| **Recovery** | `OutboxRabbitRelayWorker` | Republica transações QUEUED sem outbox pendente (cooldown) |
| **DLQ / backoff exponencial** | — | **Não implementado** (sem tabela `dead_letter_messages` no DDL) |
| **Circuit breaker** | — | **Futuro** (blocklist/geo) |

### 3.5 Idempotência e deduplicação

- Header **`Idempotency-Key`** obrigatório no `POST /transactions`.
- **Unique index** `transactions.idempotency_key`.
- Replay HTTP retorna a mesma representação (`200 OK`); primeira submissão `202 Accepted`.
- Consumer ignora reprocessamento se decisão já finalizada.
- Outbox republication usa `transactionId` como chave lógica.

Detalhes: [ADR 003](src/AntiFraud/docs/adr/003-idempotencia.md).

### 3.6 Observabilidade

| Pilar | Implementação |
|-------|----------------|
| **Logs** | Serilog na API e Worker (`Application`, contexto de transação) |
| **Tracing** | OpenTelemetry (ASP.NET Core + HttpClient) na API |
| **Auditoria** | Tabela `audit_logs` (`TRANSACTION_RECEIVED`, `TRANSACTION_EVALUATED`) |
| **Métricas Prometheus customizadas** | **Não implementadas** (evolução futura) |

### 3.7 Regras de risco (referência)

| Regra | Condição (resumo) | Score se falhar |
|-------|-------------------|-----------------|
| `HIGH_AMOUNT` | Valor ≥ 10.000 | 60 |
| `VELOCITY` | ≥ 5 transações do mesmo `customerId` em 10 min | 70 (aplicada no `FraudEvaluationService`) |

Soma dos scores das regras que falharam: **≥ 100** → `REJECTED`; **≥ 50** → `REVIEW`; caso contrário → `APPROVED`.

---

## 4) Contrato de API

Base URL (local): `http://localhost:5080`

### POST `/transactions`

**Headers**

| Header | Obrigatório | Descrição |
|--------|-------------|-----------|
| `Idempotency-Key` | Sim | 8–128 caracteres, único por intenção de pagamento |
| `Content-Type` | Sim | `application/json` |

**Body**

```json
{
  "externalReference": "ORD-998877",
  "merchantId": "MRC-001",
  "customerId": "CUS-4421",
  "amount": 1500.50,
  "currency": "BRL",
  "paymentMethod": "CREDIT_CARD",
  "ipAddress": "203.0.113.10",
  "deviceFingerprint": "fp_abc123"
}
```

**Respostas**

| Código | Situação |
|--------|----------|
| `202 Accepted` | Nova transação enfileirada |
| `200 OK` | Replay idempotente |
| `400 Bad Request` | Payload ou header inválido |

**Exemplo**

```bash
curl -X POST http://localhost:5080/transactions \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: demo-key-001" \
  -d "{\"externalReference\":\"ORD-1\",\"merchantId\":\"M1\",\"customerId\":\"C1\",\"amount\":100,\"currency\":\"BRL\",\"paymentMethod\":\"PIX\"}"
```

### GET `/transactions/{id}`

Retorna status do processamento e decisão antifraude.

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "COMPLETED",
  "decision": "APPROVED",
  "decisionReason": "All rules passed within acceptable risk.",
  "createdAtUtc": "2025-09-22T12:00:00Z",
  "completedAtUtc": "2025-09-22T12:00:01Z",
  "evaluations": [
    { "ruleCode": "HIGH_AMOUNT", "passed": true, "score": 0, "message": null }
  ]
}
```

| Campo `decision` | Significado |
|------------------|-------------|
| `PENDING` | Aguardando worker |
| `APPROVED` | Aprovado |
| `REJECTED` | Rejeitado |
| `REVIEW` | Revisão manual |

---

## 5) ADRs (decisões arquiteturais)

| ADR | Tema |
|-----|------|
| [001 — Mensageria RabbitMQ](src/AntiFraud/docs/adr/001-mensageria-rabbitmq.md) | Outbox + Rabbit opcional |
| [002 — PostgreSQL](src/AntiFraud/docs/adr/002-banco-postgresql.md) | Store relacional |
| [003 — Idempotência](src/AntiFraud/docs/adr/003-idempotencia.md) | Dedup API e consumer |
| [004 — Deployment](src/AntiFraud/docs/adr/004-deployment-containers.md) | Docker Compose local |

---

## 6) Modelo de dados (DDL)

DDL canônico: `[src/docker/pgadmin/scripts/ddl.sql](src/docker/pgadmin/scripts/ddl.sql)` (cópia em `[src/AntiFraud/scripts/ddl.sql](src/AntiFraud/scripts/ddl.sql)`).  
A aplicação **não usa EF migrations** nem `EnsureCreated`; API e Worker só validam conexão e tabelas.

**Tabelas principais**

- `transactions` — agregado raiz (status, decisão, idempotency).
- `fraud_evaluations` — resultado por regra aplicada.
- `audit_logs` — trilha auditável append-only (`payload` JSONB).
- `outbox_messages` — outbox transacional (`payload` JSONB).

**Enums (aplicação)**

- `status`: Received=0, Queued=1, Processing=2, Completed=3, Failed=4  
- `decision`: Pending=0, Approved=1, Rejected=2, Review=3  

---

## 7) Como executar localmente

### Pré-requisitos

- .NET 8 SDK  
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Compose v2)  
- Detalhes das stacks: `[src/docker/README.md](src/docker/README.md)`

---

### 7.1 Subir **todos** os containers (Postgres + pgAdmin + RabbitMQ + API + Worker)

Execute na **raiz do repositório** . Ordem: Postgres cria a rede `antifraud-net`; RabbitMQ e AntiFraud usam essa rede.

#### Primeira vez — arquivos `.env`

```powershell
cd src\docker\pgadmin
copy .env.example .env
# Edite .env: POSTGRES_PASSWORD, PGADMIN_DEFAULT_PASSWORD (use aspas se a senha tiver #)

cd src\docker\rabbitmq
@"
RABBITMQ_DEFAULT_USER=antifraud
RABBITMQ_DEFAULT_PASS="SuaSenhaRabbitAqui"
"@ | Out-File -Encoding utf8 .env

cd src\docker\antifraud
@"
POSTGRES_HOST=my-postgres
POSTGRES_PORT=5432
POSTGRES_DB=antifraud
POSTGRES_USER=postgres
POSTGRES_PASSWORD="MesmaSenhaDoPgAdmin"
RABBITMQ_HOST=my-rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_DEFAULT_USER=antifraud
RABBITMQ_DEFAULT_PASS="MesmaSenhaDoRabbit"
USE_RABBITMQ=true
"@ | Out-File -Encoding utf8 .env
```

No **antifraud**, `POSTGRES_PASSWORD` e credenciais Rabbit devem ser **iguais** às de `pgadmin` e `rabbitmq`.

#### Subir tudo (PowerShell)

```powershell

docker compose -f src/docker/pgadmin/docker-compose.yaml up -d
docker compose -f src/docker/rabbitmq/docker-compose.yaml up -d
docker compose -f src/docker/antifraud/docker-compose.yaml up -d --build

docker ps --filter "name=my-postgres" --filter "name=my-pgadmin" --filter "name=my-rabbitmq" --filter "name=antifraud-"
```

#### Subir tudo (CMD)

```cmd

docker compose -f src\docker\pgadmin\docker-compose.yaml up -d
docker compose -f src\docker\rabbitmq\docker-compose.yaml up -d
docker compose -f src\docker\antifraud\docker-compose.yaml up -d --build
```

#### URLs e portas

| Serviço | Endereço |
|---------|----------|
| **Swagger (API)** | http://localhost:5080/swagger |
| **pgAdmin** | http://localhost:15432 |
| **RabbitMQ Management** | http://localhost:15672 |
| **PostgreSQL (host)** | `localhost:5432`, database `antifraud` |

O **DDL** roda automaticamente na **primeira** inicialização do volume Postgres (`src/docker/pgadmin/scripts/ddl.sql`). Se o volume já existia sem tabelas, execute o script manualmente no pgAdmin ou recrie o volume.

#### Parar todos os containers

```powershell

docker compose -f src/docker/antifraud/docker-compose.yaml down
docker compose -f src/docker/rabbitmq/docker-compose.yaml down
docker compose -f src/docker/pgadmin/docker-compose.yaml down
```

Para remover também os volumes Postgres/pgAdmin/Rabbit (apaga dados):

```powershell
docker compose -f src/docker/pgadmin/docker-compose.yaml down -v
docker compose -f src/docker/rabbitmq/docker-compose.yaml down -v
```

---

### 7.2 Rodar API + Worker no host (sem container da app)

Com Postgres no Docker (`localhost:5432`):

1. **User Secrets** (senhas fora do Git): guia `[src/AntiFraud/docs/user-secrets.md](src/AntiFraud/docs/user-secrets.md)` ou script exemplo `src/AntiFraud/scripts/setup-user-secrets.example.cmd` (API + Worker, mesmos valores).
2. Build e execução:

```powershell

dotnet build src\AntiFraud\AntiFraud.sln

dotnet run --project src\AntiFraud\src\AntiFraud.Worker
dotnet run --project src\AntiFraud\src\AntiFraud.Api
```

Swagger (processo local): `http://localhost:5080/swagger` (`launchSettings.json` da API).

`Features:UseRabbitMq` nos `appsettings.json` (API e Worker devem coincidir):

- **`false`:** Worker processa a **outbox** no Postgres (Rabbit opcional).
- **`true`:** API faz relay outbox → Rabbit; Worker consome a fila (credenciais Rabbit via User Secrets ou env).

Documentação da solução: [src/AntiFraud/README.md](src/AntiFraud/README.md).

---

### 7.3 Testes de stress e regras (k6)

Pasta: **[src/AntiFraud/tests/k6/](src/AntiFraud/tests/k6/)** — instale o [k6](https://grafana.com/docs/k6/latest/set-up/install-k6/) (`winget install Grafana.k6` ou [releases](https://github.com/grafana/k6/releases)).

Com **API + Worker** no ar:

```powershell
cd src\AntiFraud\tests\k6

# Valida APPROVED / REVIEW / REJECTED (regras HIGH_AMOUNT + VELOCITY)
k6 run -e BASE_URL=http://localhost:5080 fraud-rules.js

# Stress com mix de transações aceitas e picos de risco
k6 run -e BASE_URL=http://localhost:5080 stress-mixed.js
```

Guia completo: [src/AntiFraud/tests/k6/README.md](src/AntiFraud/tests/k6/README.md).

### 7.4 Testes unitários (xUnit)

```powershell
cd src\AntiFraud
dotnet test tests\AntiFraud.UnitTests\AntiFraud.UnitTests.csproj
```

Detalhes: [src/AntiFraud/tests/AntiFraud.UnitTests/README.md](src/AntiFraud/tests/AntiFraud.UnitTests/README.md).

---

## 8) Critérios de avaliação sugeridos (RH/Tech Lead)

| Critério | Peso |
|----------|------|
| Clareza arquitetural e trade-offs | 25% |
| Idempotência, resiliência e auditoria | 25% |
| Qualidade do contrato API e modelagem | 20% |
| Observabilidade e operação | 15% |
| ADRs e diagramas | 15% |

---

## 9) Estrutura do repositório

```text
├── README.md
└── src/
    ├── docker/                    ← pgadmin/, rabbitmq/, antifraud/
    └── AntiFraud/
        ├── AntiFraud.sln
        ├── docs/                  ← adr/, user-secrets.md
        ├── scripts/               ← ddl.sql, setup-user-secrets.example.cmd
        ├── tests/
        │   ├── AntiFraud.UnitTests/  ← xUnit
        │   └── k6/                   ← stress / fraud-rules
        └── src/
            ├── AntiFraud.Domain/
            ├── AntiFraud.Application/
            ├── AntiFraud.Infrastructure/
            ├── AntiFraud.Api/
            └── AntiFraud.Worker/
```

---

## 10) Notas para o time interno

A pasta `AntiFraud/` é uma **referência** para calibrar entrevistas; o candidato **não** precisa entregar código idêntico, mas deve cobrir os tópicos do escopo.  
Para avaliar apenas documentação, peça ao candidato um repositório separado e compare com as seções 3–6 deste README.
