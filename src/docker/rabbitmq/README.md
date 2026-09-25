# RabbitMQ + Management (local)

Stack: [`docker-compose.yaml`](docker-compose.yaml).

Imagem **`rabbitmq:3-management`**: broker AMQP + console web (plugin management).

## Pré-requisitos

- Rede **`antifraud-net`** já criada (suba o Postgres antes):

  ```powershell
  docker compose -f docker/pgadmin/docker-compose.yaml up -d
  ```

- Arquivo [`.env`](.env) (copiar de [`.env.example`](.env.example)):

  | Variável | Uso |
  |----------|-----|
  | `RABBITMQ_DEFAULT_USER` | Login AMQP + Management UI |
  | `RABBITMQ_DEFAULT_PASS` | Senha (use aspas se tiver `#`) |

## Subir

```powershell
cd D:\Tecnica
docker compose -f docker/rabbitmq/docker-compose.yaml up -d
docker compose -f docker/rabbitmq/docker-compose.yaml ps
```

| Acesso | URL / host |
|--------|------------|
| **Management UI** | http://localhost:15672 |
| **AMQP (AntiFraud no Windows)** | `localhost:5672` |
| **AMQP (outro container na rede)** | Host `my-rabbitmq`, port `5672` |

Login da UI = `RABBITMQ_DEFAULT_USER` / `RABBITMQ_DEFAULT_PASS` do `.env`.

## AntiFraud

Em `appsettings.json` (Api e Worker):

```json
"RabbitMq": {
  "HostName": "localhost",
  "Port": 5672,
  "UserName": "<RABBITMQ_DEFAULT_USER>",
  "Password": "<RABBITMQ_DEFAULT_PASS>",
  "ExchangeName": "antifraud.events",
  "QueueName": "antifraud.transactions",
  "RoutingKey": "transaction.received"
}
```

Exchange/fila são criadas pela aplicação na publicação/consumo (referência).

## Parar

```powershell
docker compose -f docker/rabbitmq/docker-compose.yaml down
```

Volume `rabbitmq_data` permanece até `docker volume rm`.
