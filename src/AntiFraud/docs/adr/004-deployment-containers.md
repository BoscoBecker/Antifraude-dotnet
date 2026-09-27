# ADR 004 — Deployment em containers (Docker)

## Status

Aceito

## Contexto

API e Worker escalam de forma independente. **Desenvolvimento local** usa **.NET Aspire** (`AntiFraud.AppHost`) para Postgres, RabbitMQ, pgAdmin, API e Worker.

## Decisão

Empacotar **AntiFraud.Api** e **AntiFraud.Worker** em imagens OCI (Dockerfiles em `src/docker/antifraud/`). Manifests em `src/docker/` servem como **referência** de variáveis e rede para produção ou testes de imagem.

Produção pode evoluir para **Kubernetes** (HPA no worker, serviços gerenciados de Postgres/Rabbit) — fora do escopo mínimo deste repositório.

## Consequências

- API **stateless** — escala horizontal sem sticky session.
- Worker escala consumindo a mesma fila Rabbit (várias réplicas com prefetch/ack manual).
- **DDL** via `src/AntiFraud/scripts/ddl.sql` (init Postgres Aspire + `DatabaseSchemaBootstrap`); aplicação **não** roda migrations nem `EnsureCreated`.
- Credenciais locais: **User Secrets** — ver [user-secrets.md](../user-secrets.md) e [aspire.md](../aspire.md).

## Referências

- [README raiz — seção 7](../../../../README.md)
- [aspire.md](../aspire.md)
- [src/docker/README.md](../../../docker/README.md)
