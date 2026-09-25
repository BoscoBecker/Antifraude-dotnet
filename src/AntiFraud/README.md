# AntiFraud — solução de referência

Documentação completa do desafio e arquitetura: **[../../README.md](../../README.md)**

## Configuração local (User Secrets)

Senhas e connection string **não** vão no Git. Configure uma vez:

- Guia: **[docs/user-secrets.md](docs/user-secrets.md)**
- Template: **[secrets.template.json](secrets.template.json)**
- Script exemplo: **`scripts/setup-user-secrets.example.cmd`**

## Banco de dados (sem EF migrations)

- **DDL canônico:** `../docker/pgadmin/scripts/ddl.sql`
- **Cópia:** `scripts/ddl.sql`

Na subida, API e Worker validam conexão e tabelas (`DatabaseSchemaBootstrap`).

## Executar

### Docker (stack completa)

Ver **[README raiz — §7.1](../../README.md)** e **[../docker/README.md](../docker/README.md)**.

### Host (API + Worker)

1. Postgres + RabbitMQ: compose em `src/docker/`
2. User Secrets na API e Worker (Postgres + `RabbitMq`)
3. `dotnet build AntiFraud.sln`
4. `dotnet run --project src/AntiFraud.Worker` e `dotnet run --project src/AntiFraud.Api`

**Fluxo:** POST → outbox (Postgres) → relay na **API** → Rabbit → consumer no **Worker** → decisão no Postgres.

## Testes

| Tipo | Pasta |
|------|--------|
| **Unitários (xUnit)** | [tests/AntiFraud.UnitTests/](tests/AntiFraud.UnitTests/) — `dotnet test` |
| **Stress / fraude (k6)** | [tests/k6/README.md](tests/k6/README.md) |

## ADRs

| ADR | Arquivo |
|-----|---------|
| 001 Mensageria | [docs/adr/001-mensageria-rabbitmq.md](docs/adr/001-mensageria-rabbitmq.md) |
| 002 PostgreSQL | [docs/adr/002-banco-postgresql.md](docs/adr/002-banco-postgresql.md) |
| 003 Idempotência | [docs/adr/003-idempotencia.md](docs/adr/003-idempotencia.md) |
| 004 Deployment | [docs/adr/004-deployment-containers.md](docs/adr/004-deployment-containers.md) |
