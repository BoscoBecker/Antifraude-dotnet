# ADR 001 — Mensageria com RabbitMQ

## Status

Aceito

## Contexto

O módulo antifraude precisa desacoplar a ingestão HTTP da avaliação assíncrona, absorver picos de tráfego e permitir reprocessamento controlado.

## Decisão

Adotar **RabbitMQ** (exchange topic + fila durable) como broker **opcional** (`Features:UseRabbitMq`), com **outbox transacional** no PostgreSQL:

- **POST** grava transação + outbox no **mesmo commit** (sem publish direto no Rabbit).
- **`UseRabbitMq: true`:** `OutboxRabbitRelayWorker` na **API** publica na fila; **Worker** consome (`RabbitMqTransactionEvaluationConsumer`).
- **`UseRabbitMq: false`:** **Worker** lê outbox no Postgres (`OutboxTransactionDispatchWorker`) — sem broker.

API e Worker devem usar o **mesmo** valor de `UseRabbitMq`.

## Alternativas consideradas

| Opção | Prós | Contras |
|-------|------|---------|
| **Kafka** | Alto throughput, retenção longa | Operação mais pesada para volume moderado de fraude |
| **AWS SQS/SNS** | Gerenciado, DLQ nativa | Lock-in cloud, latência cross-region |
| **RabbitMQ** | Routing flexível, operação madura on-prem | Throughput menor que Kafka |
| **Só outbox (sem broker)** | Menos infra local | Polling no Postgres; escala horizontal via fila fica limitada |

## Consequências (implementação atual)

- Fila: `antifraud.transactions`, exchange `antifraud.events`, routing key `transaction.received`.
- Consumer com **ack manual** após avaliação; **nack** sem requeue em falhas duras.
- **MessageId** na publicação = `transactionId` (dedup lógica).
- Outbox: `attempts` / `last_error` em falha de relay; mensagens pendentes são reprocessadas pelo loop do relay (intervalo ~2 s).
- **DLQ dedicada (`dead_letter_messages`)** — evolução futura; hoje não há tabela DLQ no DDL.

## Referências no código

- `OutboxRabbitRelayWorker`, `RabbitMqTransactionQueuePublisher` (API)
- `RabbitMqTransactionEvaluationConsumer` (Worker)
- `OutboxTransactionDispatchWorker` (Worker, modo sem Rabbit)
- `DependencyInjection.AddMessaging`
