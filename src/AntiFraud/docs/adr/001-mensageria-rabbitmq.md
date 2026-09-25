# ADR 001 — Mensageria com RabbitMQ

## Status

Aceito

## Contexto

O módulo antifraude precisa desacoplar a ingestão HTTP da avaliação assíncrona, absorver picos de tráfego e permitir reprocessamento controlado.

## Decisão

Adotar **RabbitMQ** (exchange topic + fila durable) como broker **obrigatório**, com **outbox transacional** no PostgreSQL:

- **POST** grava transação + outbox no **mesmo commit** (sem publish direto no Rabbit).
- **API:** `OutboxRabbitRelayWorker` lê outbox pendente e publica na fila.
- **Worker:** `RabbitMqTransactionEvaluationConsumer` consome a fila e chama `ProcessAsync`.

## Alternativas consideradas

| Opção | Prós | Contras |
|-------|------|---------|
| **Kafka** | Alto throughput, retenção longa | Operação mais pesada para volume moderado de fraude |
| **AWS SQS/SNS** | Gerenciado, DLQ nativa | Lock-in cloud, latência cross-region |
| **RabbitMQ** | Routing flexível, operação madura on-prem | Throughput menor que Kafka |
| **Só outbox (polling Postgres)** | Menos moving parts | Polling no banco; escala horizontal pior — **removido** para simplificar o projeto |

## Consequências (implementação atual)

- Fila: `antifraud.transactions`, exchange `antifraud.events`, routing key `transaction.received`.
- Consumer com **ack manual** após avaliação; **nack** sem requeue em falhas duras.
- **MessageId** na publicação = `transactionId` (dedup lógica).
- Outbox: `attempts` / `last_error` em falha de relay; mensagens pendentes são reprocessadas pelo loop do relay (intervalo ~2 s).
- **DLQ dedicada (`dead_letter_messages`)** — evolução futura; hoje não há tabela DLQ no DDL.

## Referências no código

- `OutboxRabbitRelayWorker`, `RabbitMqTransactionQueuePublisher` (API)
- `RabbitMqTransactionEvaluationConsumer` (Worker)
- `DependencyInjection.AddRabbitMqMessaging`
