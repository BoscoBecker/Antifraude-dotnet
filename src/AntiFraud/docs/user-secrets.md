# User Secrets (desenvolvimento local)

Senhas **não** ficam em `appsettings.json`. Em Development:

| Projeto | `UserSecretsId` |
|---------|-----------------|
| **AntiFraud.Api** | `antifraud-api-local-dev-8f4e2c1a-5b3d-4e6f-9a0b-1c2d3e4f5a6b` |
| **AntiFraud.AppHost** | **o mesmo da API** (Postgres + Rabbit nos containers Aspire) |
| **AntiFraud.Worker** | id próprio — **mesmos valores** de chaves |

Valores de referência podem ficar em `src/docker/pgadmin/.env` e `src/docker/rabbitmq/.env` (não commitados) — o AppHost lê credenciais via User Secrets.

**Aspire:** se mudar a senha nos secrets após a 1ª subida do volume Postgres, apague os volumes Aspire — ver [aspire.md](aspire.md).

## 1. API (e AppHost Aspire)

```bash
cd src/AntiFraud/src/AntiFraud.Api

# Senha com #: Password='...' ou %23 — senão tudo após # é ignorado na connection string
dotnet user-secrets set "ConnectionStrings:AntiFraud" "Host=localhost;Port=5432;Database=antifraud;Username=postgres;Password='SUA_SENHA_POSTGRES'"

dotnet user-secrets set "RabbitMq:UserName" "antifraud"
dotnet user-secrets set "RabbitMq:Password" "SUA_SENHA_RABBIT"

dotnet user-secrets set "PgAdmin:DefaultEmail" "seu-email@example.com"
dotnet user-secrets set "PgAdmin:DefaultPassword" "SUA_SENHA_POSTGRES"
```

Com o **AppHost** no ar: `Host=localhost`, Postgres **5432**, Rabbit **5672** (AMQP).

## 2. Worker

```bash
cd src/AntiFraud/src/AntiFraud.Worker

dotnet user-secrets set "ConnectionStrings:AntiFraud" "Host=localhost;Port=5432;Database=antifraud;Username=postgres;Password=SUA_SENHA_POSTGRES"

dotnet user-secrets set "RabbitMq:UserName" "antifraud"
dotnet user-secrets set "RabbitMq:Password" "SUA_SENHA_RABBIT"
```

## 3. Conferir

```bash
dotnet user-secrets list
```

Para limpar: `dotnet user-secrets clear` (na pasta da API e do Worker).

## 4. Ambiente

- **API:** `ASPNETCORE_ENVIRONMENT=Development`
- **Worker:** `DOTNET_ENVIRONMENT=Development`

## 5. Produção

Variáveis de ambiente no orchestrator (sem User Secrets). Template: [`secrets.template.json`](../secrets.template.json).
