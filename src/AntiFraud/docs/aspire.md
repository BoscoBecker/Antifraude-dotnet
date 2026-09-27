# .NET Aspire — AntiFraud

Orquestração local: **PostgreSQL**, **RabbitMQ**, **pgAdmin**, **API** e **Worker** + dashboard Aspire.

## Projetos

| Projeto | Papel |
|---------|--------|
| `AntiFraud.AppHost` | Orquestrador Aspire |
| `AntiFraud.ServiceDefaults` | OTel, health, service discovery (Api + Worker) |
| Domain / Application / Infrastructure | Sem referência ao AppHost |

## Credenciais = User Secrets da API

O AppHost usa o **mesmo** `UserSecretsId` da `AntiFraud.Api`:

- `ConnectionStrings:AntiFraud`
- `RabbitMq:UserName` / `RabbitMq:Password`
- `PgAdmin:DefaultEmail` / `PgAdmin:DefaultPassword` (login pgAdmin Aspire)

Ver [user-secrets.md](user-secrets.md).

Postgres e Rabbit usam `AddParameter` com os valores dos User Secrets.

### Persistência de dados

- Volumes Docker: `antifraud-aspire-postgres-data`, `antifraud-aspire-rabbitmq-data`
- A senha Postgres fica fixada na **primeira** init do volume — se mudar secrets, apague os volumes e suba de novo
- Evite `docker volume prune` sem rever estes nomes

## DDL

- Init Postgres (1ª subida do volume): `scripts/ddl.sql` no build do AppHost (`postgres-init/ddl.sql`)
- Fallback: `DatabaseSchemaBootstrap` na API/Worker (`src/AntiFraud/scripts/ddl.sql`)

## Executar

Pré-requisitos: **.NET 8**, **Docker Desktop**, **User Secrets** configurados.

```bash
dotnet run --project src/AntiFraud/AntiFraud.AppHost/AntiFraud.AppHost.csproj --launch-profile https
```

No Visual Studio, perfil **https** no AppHost. Dashboard abre no browser; URLs da API aparecem no dashboard.

### Logs estruturados (`/structuredlogs`)

Abra **Structured logs** no dashboard ou `{base-url-do-dashboard}/structuredlogs`.

- **OTLP:** `AntiFraud.ServiceDefaults` exporta logs/métricas/traces quando existe `OTEL_EXPORTER_OTLP_ENDPOINT` (o AppHost injeta ao correr Api/Worker).
- **Serilog:** Api/Worker usam `writeToProviders: true` para os eventos chegarem ao OpenTelemetry (não ficam só no console).
- Filtre por recurso **antifraud-api** / **antifraud-worker** (Worker: `Application=AntiFraud.Worker`, `ServiceRole=fraud-evaluation-consumer`).
- Api e Worker: Serilog `writeToProviders: true` + `ILogger` com parâmetros/`BeginScope` (ex.: `TransactionId` no consumer Rabbit).

### URLs típicas (host)

| Serviço | Endereço |
|---------|----------|
| **Swagger (API)** | http://localhost:5080/swagger (ver dashboard) |
| **pgAdmin Aspire** | http://localhost:15433/login |
| **RabbitMQ Management** | ver dashboard Aspire |
| **PostgreSQL** | `localhost:5432`, database `antifraud` |

## NuGet

Se `Aspire.AppHost.Sdk` não restaurar, use `nuget.config` na raiz e em `src/AntiFraud`.

## `password authentication failed for user "postgres"`

1. Senha com **`#`**: na connection string use `Password='sua#senha'` ou `%23` nos User Secrets
2. Confira: `dotnet user-secrets list` na pasta da API
3. Se mudou a senha após a 1ª subida do volume, **reset** (AppHost parado):

```bash
docker volume rm antifraud-aspire-postgres-data antifraud-aspire-rabbitmq-data
```

Reaplique User Secrets se necessário e suba o AppHost outra vez.

## Testes k6

Com o AppHost no ar, use a URL da API do dashboard (geralmente `http://localhost:5080`). Ver [tests/k6/README.md](../tests/k6/README.md).
