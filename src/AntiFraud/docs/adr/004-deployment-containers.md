# ADR 004 — Deployment em containers (Docker)

## Status

Aceito

## Contexto

API e Worker escalam de forma independente; ambiente local utiliza **Docker Compose** em `src/docker/`.

## Decisão

Empacotar **AntiFraud.Api** e **AntiFraud.Worker** em imagens OCI (Dockerfiles em `src/docker/antifraud/`), orquestradas via **Docker Compose** no desenvolvimento. PostgreSQL e RabbitMQ em stacks separadas (`src/docker/pgadmin/`, `src/docker/rabbitmq/`), rede compartilhada `antifraud-net`.

Produção pode evoluir para **Kubernetes** (HPA no worker, serviços gerenciados de Postgres/Rabbit) — fora do escopo mínimo deste repositório.

## Consequências

- API **stateless** — escala horizontal sem sticky session.
- Worker escala consumindo a mesma fila Rabbit (várias réplicas com prefetch/ack manual).
- **DDL** aplicado via init do Postgres (`src/docker/pgadmin/scripts/ddl.sql`) ou script manual; aplicação **não** roda migrations nem `EnsureCreated`.
- Credenciais via `.env` nos compose (não commitar); host local via **User Secrets** — ver [user-secrets.md](../user-secrets.md).

## Referências

- [README raiz — seção 7.1](../../../../README.md)
- [src/docker/README.md](../../../docker/README.md)
- [src/docker/antifraud/README.md](../../../docker/antifraud/README.md)
