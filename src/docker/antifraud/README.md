# AntiFraud — API + Worker (imagens Docker)

> **Dev local:** use [Aspire](../../AntiFraud/docs/aspire.md). Esta pasta é referência para build de imagens (ADR 004).

Dockerfiles: `Dockerfile.api`, `Dockerfile.worker`. Contexto de build: pasta `src/` do repositório.

Variáveis típicas: `ConnectionStrings__AntiFraud`, `RabbitMq__*` (ver `.env.example` se existir).

Produção: registry + orchestrator; API e Worker em deployments separados.
