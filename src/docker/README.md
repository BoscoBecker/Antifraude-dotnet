# Docker — referência (ADR 004)

**Desenvolvimento local:** use **[.NET Aspire](../AntiFraud/docs/aspire.md)** (`AntiFraud.AppHost`) — Postgres, RabbitMQ, pgAdmin, API e Worker.

Esta pasta mantém **Dockerfiles** e manifests de referência para empacotamento OCI (API/Worker) e exemplos de variáveis. Não é o fluxo principal de dev documentado no [README](../../README.md) §7.

Credenciais locais de referência: `pgadmin/.env` e `rabbitmq/.env` (copiar de `.env.example`, não commitar) — ver [user-secrets.md](../AntiFraud/docs/user-secrets.md).
