# AntiFraud — solução de referência

Documentação completa do desafio e arquitetura: **[../../README.md](../../README.md)**

## Configuração local (User Secrets)

Senhas e connection string **não** vão no Git. Configure uma vez:

- Guia: **[docs/user-secrets.md](docs/user-secrets.md)**
- Template de chaves: **[secrets.template.json](secrets.template.json)**
- Script exemplo: **`scripts/setup-user-secrets.example.cmd`** (copie, edite senhas, execute)

## Banco de dados (sem EF migrations)

O schema **não** é criado pela aplicação. Use o DDL:

- **Canônico:** `src/docker/pgadmin/scripts/ddl.sql`
- **Cópia:** `scripts/ddl.sql`

Na subida, API e Worker validam conexão e presença das tabelas (`DatabaseSchemaBootstrap`).

## Executar localmente

1. Subir Postgres (+ DDL): `docker compose` em `src/docker/pgadmin/`
2. (Opcional) RabbitMQ: `src/docker/rabbitmq/`
3. Configurar **User Secrets** (API + Worker) — ver acima
4. Build e processos:

```bash
cd src/AntiFraud
dotnet build AntiFraud.sln
dotnet run --project src/AntiFraud.Api
dotnet run --project src/AntiFraud.Worker
```

**Sem RabbitMQ** (`UseRabbitMq: false` em `appsettings.json`): Worker processa outbox no Postgres.

**Com RabbitMQ** (`UseRabbitMq: true`): API relay outbox → Rabbit; Worker consome a fila.

Docker app: `src/docker/antifraud/docker-compose.yaml` (variáveis de ambiente, não User Secrets).
