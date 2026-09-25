# Testes k6 — AntiFraud

Pasta de **stress** e **cenários de regras** contra `POST /transactions` e `GET /transactions/{id}`.

**Pré-requisitos:** API + **Worker** no ar (Postgres; Rabbit se `UseRabbitMq: true`).

## Instalar k6

| Plataforma | Comando / link |
|------------|----------------|
| **Windows (winget)** | `winget install Grafana.k6` |
| **Windows (Chocolatey)** | `choco install k6` |
| **Documentação** | [Install k6](https://grafana.com/docs/k6/latest/set-up/install-k6/) |
| **Download binários** | [GitHub — grafana/k6 releases](https://github.com/grafana/k6/releases) |

```bash
k6 version
```

## Scripts

| Arquivo | Objetivo |
|---------|----------|
| **`fraud-rules.js`** | Valida **APPROVED**, **REVIEW** (valor alto / velocity), **REJECTED** (60+70) — 1 VU, sequencial |
| **`stress-mixed.js`** | Rampa de VUs; mix ~85% aprovados + picos de valor alto / clientes “quentes” |
| **`transactions.js`** | Carga só no POST (202/200), sem poll de decisão |

Helpers: **`helpers/antifraud.js`** (submit, poll até `COMPLETED`).

### Regras exercitadas (`fraud-rules.js`)

| Grupo | Condição | Decisão esperada |
|-------|----------|------------------|
| APPROVED | amount 250, cliente único | `APPROVED` |
| REVIEW — HIGH_AMOUNT | amount ≥ 10_000 | `REVIEW` (score 60) |
| REVIEW — VELOCITY | 5× amount 50, mesmo `customerId` | `REVIEW` (score 70 na 5ª) |
| REJECTED | 4× APPROVED + 1× 15_000 mesmo cliente | `REJECTED` (60+70) |

## Executar

Na pasta `src/AntiFraud/tests/k6`:

```bash
# Cenários de fraude (recomendado antes do stress)
k6 run -e BASE_URL=http://localhost:5080 fraud-rules.js

# Stress misto
k6 run -e BASE_URL=http://localhost:5080 stress-mixed.js

# Stress validando decisão (mais lento — poll GET)
k6 run -e BASE_URL=http://localhost:5080 -e VERIFY_DECISION=true stress-mixed.js

# Só throughput POST
k6 run -e BASE_URL=http://localhost:5080 transactions.js
```

Smoke:

```bash
k6 run -e BASE_URL=http://localhost:5080 --vus 1 --duration 5s transactions.js
```

### Variáveis de ambiente

| Variável | Default | Descrição |
|----------|---------|-----------|
| `BASE_URL` | `http://localhost:5080` | API |
| `POLL_INTERVAL_SEC` | `0.5` | Intervalo entre GETs no poll |
| `POLL_MAX_ATTEMPTS` | `60` | Tentativas (~30 s) |
| `VERIFY_DECISION` | `false` | `stress-mixed.js` aguarda decisão final |

## Windows (PowerShell)

```powershell
cd D:\Tecnica\src\AntiFraud\tests\k6
k6 run -e BASE_URL=http://localhost:5080 fraud-rules.js
```
