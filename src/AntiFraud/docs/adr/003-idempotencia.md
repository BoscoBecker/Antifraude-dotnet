# ADR 003 — Idempotência e deduplicação

## Status

Aceito

## Contexto

Clientes podem reenviar a mesma transação por timeout, retry HTTP ou entrega duplicada na fila.

## Decisão

1. **API**: header obrigatório `Idempotency-Key` (8–128 chars), persistido com **unique constraint** em `transactions.idempotency_key`.
2. **Application**: lookup antes de criar agregado; replay retorna `200 OK` com mesmo corpo (criação retorna `202 Accepted`).
3. **Consumer**: processamento idempotente — se `status = Completed`, ignorar reentrega.
4. **Dedup de mensagens**: `MessageId` = `transactionId` + verificação de estado no worker.

## Alternativas consideradas

- Cache Redis SETNX TTL: rápido porém volátil; usado apenas como otimização futura.
- Chave composta `(merchant_id, external_reference)`: complementar, não substitui idempotency key do cliente.

## Consequências

- Colisão de chave com payload diferente → `409 Conflict` (evolução futura via hash do body).
- Outbox relay deve ser idempotente ao republicar.
