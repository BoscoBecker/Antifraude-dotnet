# Docker — stacks locais

Cada stack tem **compose + `.env` próprio** (não commitar `.env`).

| Pasta | Serviços | Documentação |
|-------|----------|----------------|
| [`pgadmin/`](pgadmin/README.md) | Postgres 16 + pgAdmin | [pgadmin/README.md](pgadmin/README.md) |
| [`rabbitmq/`](rabbitmq/README.md) | RabbitMQ + Management UI | [rabbitmq/README.md](rabbitmq/README.md) |
| [`antifraud/`](antifraud/README.md) | API + Worker (.NET 8 Linux) | [antifraud/README.md](antifraud/README.md) |

Comandos completos (primeira vez + subir/parar tudo): **[README na raiz do repo](../../README.md#71-subir-todos-os-containers-postgres--pgadmin--rabbitmq--api--worker)**.

## Ordem recomendada (raiz `Tecnica/`)

1. Postgres/pgAdmin (cria a rede `antifraud-net`):

   ```powershell
   cd D:\Tecnica
   docker compose -f src/docker/pgadmin/docker-compose.yaml up -d
   ```

2. RabbitMQ (rede externa `antifraud-net`):

   ```powershell
   docker compose -f src/docker/rabbitmq/docker-compose.yaml up -d
   ```

3. AntiFraud API + Worker:

   ```powershell
   docker compose -f src/docker/antifraud/docker-compose.yaml up -d --build
   ```

## Senhas

- **`src/docker/pgadmin/.env`** — `POSTGRES_*`, `PGADMIN_*` (copiar de `.env.example`)
- **`src/docker/rabbitmq/.env`** — `RABBITMQ_DEFAULT_USER`, `RABBITMQ_DEFAULT_PASS`
- **`src/docker/antifraud/.env`** — mesmas credenciais Postgres + Rabbit para os containers

Gere senhas fortes (`openssl rand -base64 32`). Caracteres `#` exigem **aspas duplas** no `.env`.

Alinhe `src/AntiFraud/.../appsettings.json` com `POSTGRES_PASSWORD` e, se usar fila, `RabbitMq`.
