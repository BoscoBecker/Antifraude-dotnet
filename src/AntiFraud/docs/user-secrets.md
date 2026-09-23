# User Secrets (desenvolvimento local)

Senhas **não** ficam em `appsettings.json`. Em Development, a API e o Worker carregam **User Secrets** (via `UserSecretsId` no `.csproj`).

Alinhe com `src/docker/pgadmin/.env` e `src/docker/rabbitmq/.env`.

## 1. API

```bash
cd src/AntiFraud/src/AntiFraud.Api

dotnet user-secrets set "ConnectionStrings:AntiFraud" "Host=localhost;Port=5432;Database=antifraud;Username=postgres;Password=SUA_SENHA_POSTGRES"

dotnet user-secrets set "RabbitMq:UserName" "antifraud"
dotnet user-secrets set "RabbitMq:Password" "SUA_SENHA_RABBIT"
```

## 2. Worker

```bash
cd src/AntiFraud/src/AntiFraud.Worker

dotnet user-secrets set "ConnectionStrings:AntiFraud" "Host=localhost;Port=5432;Database=antifraud;Username=postgres;Password=SUA_SENHA_POSTGRES"

dotnet user-secrets set "RabbitMq:UserName" "antifraud"
dotnet user-secrets set "RabbitMq:Password" "SUA_SENHA_RABBIT"
```

Use os **mesmos** valores na API e no Worker.

## 3. Conferir

```bash
dotnet user-secrets list
```

## 4. Ambiente

User Secrets só entram automaticamente com:

- **API:** `ASPNETCORE_ENVIRONMENT=Development` (padrão do `dotnet run` no template Web)
- **Worker:** `DOTNET_ENVIRONMENT=Development` (padrão do `dotnet run` no Worker SDK)

## 5. Docker / produção

Use variáveis de ambiente (ex.: `ConnectionStrings__AntiFraud`, `RabbitMq__Password`) — ver `src/docker/antifraud/docker-compose.yaml`.

Template de chaves: [`secrets.template.json`](../secrets.template.json).
