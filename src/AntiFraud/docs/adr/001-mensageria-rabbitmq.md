# ADR 001 — Mensageria com RabbitMQ

## Status

Aceito

## Contexto

O módulo antifraude precisa desacoplar a ingestão HTTP da avaliação assíncrona, absorver picos de tráfego e permitir reprocessamento controlado.

## Decisão

Adotar **RabbitMQ** (exchange topic + fila durable) como broker principal, com **outbox transacional** no PostgreSQL para garantir publicação at-least-once consistente com a gravação da transação.

## Alternativas consideradas

| Opção | Prós | Contras |
|-------|------|---------|
| **Kafka** | Alto throughput, retenção longa | Operação mais pesada para volume moderado de fraude |
| **AWS SQS/SNS** | Gerenciado, DLQ nativa | Lock-in cloud, latência cross-region |
| **RabbitMQ** | Routing flexível, operação madura on-prem | Throughput menor que Kafka |

## Consequências

- Worker consome `antifraud.transactions` com ack manual após persistir decisão.
- Falhas de publish caem no **outbox**; job de relay aplica retry exponencial e envia para **DLQ** (`dead_letter_messages`) após N tentativas.
- Idempotência no consumer via `MessageId = transactionId`.
