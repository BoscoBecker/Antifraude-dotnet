# Credenciais Postgres / pgAdmin (dev)

> **Infra local:** subida pelo [AntiFraud.AppHost](../../AntiFraud/docs/aspire.md) (Postgres + pgAdmin em http://localhost:15433).

Esta pasta guarda **`.env.example`** e **`scripts/ddl.sql`** (espelho do canônico em `AntiFraud/scripts/ddl.sql`). Use os valores para configurar [User Secrets](../../AntiFraud/docs/user-secrets.md).

Copie `.env.example` → `.env` (não commitar). Campos usados pelos scripts: `POSTGRES_*`, `POSTGRES_CONNECTION_STRING_HOST`, `PGADMIN_*`.
