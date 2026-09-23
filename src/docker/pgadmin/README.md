# Postgres + pgAdmin (local)

Stack: [`docker-compose.yaml`](docker-compose.yaml) · credenciais: [`.env`](.env) (copiar de [`.env.example`](.env.example)).

## Subir

```powershell
cd D:\Tecnica
docker compose -f docker/pgadmin/docker-compose.yaml up -d
docker compose -f docker/pgadmin/docker-compose.yaml ps
```

| Serviço | Acesso |
|---------|--------|
| Postgres (host) | `localhost:5432`, DB `antifraud`, user `postgres`, senha = `POSTGRES_PASSWORD` |
| Postgres (container) | Host `my-postgres`, port `5432` |
| pgAdmin | http://localhost:15432/login |

### Logins

1. **pgAdmin (web)** — `PGADMIN_DEFAULT_EMAIL` + `PGADMIN_DEFAULT_PASSWORD`
2. **Servidor AntiFraud (my-postgres)** — senha = `POSTGRES_PASSWORD` (1ª conexão na UI)

| De onde conecta | Host |
|-----------------|------|
| pgAdmin (container) | **`my-postgres`** |
| Windows / `dotnet run` | **`localhost`** |

## `servers.json`

Pré-cadastra o servidor PostgreSQL (host `my-postgres`). Senha do banco não fica no Git — informe na UI.

## Schema AntiFraud

Na **primeira** criação do volume `postgres_data`, o Postgres executa automaticamente [`scripts/ddl.sql`](scripts/ddl.sql) (`IF NOT EXISTS` — idempotente).

Se o volume **já existia** antes deste mount, rode manualmente:

```powershell
Get-Content D:\Tecnica\docker\pgadmin\scripts\ddl.sql | docker exec -i my-postgres psql -U postgres -d antifraud
```

Para forçar init de novo (apaga dados): `docker compose -f docker/pgadmin/docker-compose.yaml down` e `docker volume rm <projeto>_postgres_data`.

## Senhas

No `.env`, use **aspas** se a senha tiver `#` (senão o resto vira comentário).

Trocar só o `.env` não altera senha já gravada no volume `postgres_data` — use `ALTER USER` ou recrie o volume.

## Parar

```powershell
docker compose -f docker/pgadmin/docker-compose.yaml down
```

Cria a rede Docker **`antifraud-net`** (usada pelo [RabbitMQ](../rabbitmq/README.md)).
