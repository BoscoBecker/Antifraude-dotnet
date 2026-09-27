# AntiFraud — solução de referência

Documentação completa do desafio e arquitetura: **[../../README.md](../../README.md)**

## Configuração local (User Secrets)

Senhas e connection string **não** vão no Git. Configure uma vez:

- Guia: **[docs/user-secrets.md](docs/user-secrets.md)**
- Template: **[secrets.template.json](secrets.template.json)**

## Banco de dados (sem EF migrations)

- **DDL canônico:** [scripts/ddl.sql](scripts/ddl.sql)
- Init no Postgres Aspire (1ª subida) + `DatabaseSchemaBootstrap` na API/Worker

## Executar (Aspire)

```bash
dotnet run --project AntiFraud.AppHost/AntiFraud.AppHost.csproj --launch-profile https
```

Detalhes: [docs/aspire.md](docs/aspire.md) e README raiz §7.1.

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
