# ADR 002 — PostgreSQL como store relacional

## Status

Aceito

## Contexto

Transações financeiras exigem integridade referencial, auditoria imutável, consultas analíticas (velocity, blocklist) e consistência forte na criação + outbox.

## Decisão

Usar **PostgreSQL** relacional como system of record. Detalhes de regras e scores ficam normalizados em `fraud_evaluations`; agregados de leitura podem evoluir para projeções/materialized views.

## Alternativas consideradas

| Opção | Prós | Contras |
|-------|------|---------|
| **MongoDB / DynamoDB** | Schema flexível | Joins/auditoria/consistência transacional mais complexos |
| **PostgreSQL** | ACID, índices compostos, JSONB se necessário | Escala write vertical + read replicas |
| **Híbrido (PG + Redis)** | Cache de velocity | Mais componentes operacionais |

## Consequências

- Índice único em `idempotency_key`.
- Índice `(customer_id, created_at_utc)` para regra de velocity.
- Event sourcing completo **não** adotado; audit trail via `audit_logs` + domínio.
- Schema via DDL (`src/AntiFraud/scripts/ddl.sql`); sem EF migrations.

## Referências

- [DDL canônico](../../scripts/ddl.sql)
- [ADR 001 — Outbox / RabbitMQ](001-mensageria-rabbitmq.md)
