# AntiFraud — API + Worker (Docker Linux)

Um **único** `docker-compose.yaml` com **dois serviços** (API e Worker):

| Abordagem | Quando usar |
|-----------|-------------|
| **Um compose (este)** | Dev local, mesmo ciclo de vida, mesma rede e config — recomendado aqui. |
| Composes separados | Escala/deploy independente (ex.: K8s com Deployments distintos); em Docker local costuma ser overhead. |

Imagens base: **`mcr.microsoft.com/dotnet/*:8.0-jammy`** (Linux).

## Pré-requisitos

1. Infra no ar (rede `antifraud-net`):

   ```powershell
   docker compose -f docker/pgadmin/docker-compose.yaml up -d
   docker compose -f docker/rabbitmq/docker-compose.yaml up -d
   ```

2. DDL aplicado no Postgres (uma vez).

3. [`.env`](.env) a partir de [`.env.example`](.env.example) — **mesmas senhas** que `docker/pgadmin/.env` e `docker/rabbitmq/.env`.

## Build e subir

```powershell
cd D:\Tecnica
docker compose -f docker/antifraud/docker-compose.yaml build
docker compose -f docker/antifraud/docker-compose.yaml up -d
docker compose -f docker/antifraud/docker-compose.yaml ps
```

| Serviço | URL / notas |
|---------|-------------|
| API | http://localhost:5080/swagger |
| Worker | sem porta publicada (background) |

Dentro da rede Docker: Postgres `my-postgres`, RabbitMQ `my-rabbitmq` (não `localhost`).

## Variáveis

| Variável | Descrição |
|----------|-----------|
| `POSTGRES_*` | Connection string da API/Worker |
| `RABBITMQ_*` / `RABBITMQ_DEFAULT_*` | Broker (obrigatório para API relay + Worker consumer) |

## Parar

```powershell
docker compose -f docker/antifraud/docker-compose.yaml down
```

Não remove imagens; `--rmi local` se quiser limpar tags `:local`.

## Produção

Este compose é **referência local**. Em produção: imagens no registry, secrets no orchestrator, API e Worker em Deployments separados (ADR 004).
