-- AntiFraud schema — idempotente (IF NOT EXISTS)

-- Fonte canônica: docker/pgadmin/scripts/ddl.sql

-- Deploy automático: docker/pgadmin/docker-compose.yaml (docker-entrypoint-initdb.d)

-- Banco: POSTGRES_DB (antifraud).



CREATE TABLE IF NOT EXISTS transactions

(

    id                   UUID PRIMARY KEY,

    idempotency_key      VARCHAR(128) NOT NULL,

    external_reference   VARCHAR(64) NOT NULL,

    merchant_id          VARCHAR(64) NOT NULL,

    customer_id          VARCHAR(64) NOT NULL,

    amount               NUMERIC(18, 2) NOT NULL,

    currency             VARCHAR(3) NOT NULL,

    payment_method       VARCHAR(32) NOT NULL,

    ip_address           VARCHAR(64),

    device_fingerprint   VARCHAR(128),

    status               INT NOT NULL,

    decision             INT NOT NULL,

    decision_reason      VARCHAR(512),

    processing_attempts  INT NOT NULL DEFAULT 0,

    created_at_utc       TIMESTAMPTZ NOT NULL,

    completed_at_utc     TIMESTAMPTZ

);



CREATE UNIQUE INDEX IF NOT EXISTS ux_transactions_idempotency_key

    ON transactions (idempotency_key);



CREATE INDEX IF NOT EXISTS ix_transactions_customer_created

    ON transactions (customer_id, created_at_utc DESC);



CREATE INDEX IF NOT EXISTS ix_transactions_external_reference

    ON transactions (external_reference);





CREATE TABLE IF NOT EXISTS fraud_evaluations

(

    id               UUID PRIMARY KEY,

    transaction_id   UUID NOT NULL,

    rule_code        VARCHAR(64) NOT NULL,

    passed           BOOLEAN NOT NULL,

    score            INT NOT NULL,

    message          VARCHAR(512),

    evaluated_at_utc TIMESTAMPTZ NOT NULL,

    CONSTRAINT fk_fraud_evaluations_transaction

        FOREIGN KEY (transaction_id)

        REFERENCES transactions (id)

        ON DELETE CASCADE

);



CREATE INDEX IF NOT EXISTS ix_fraud_evaluations_transaction

    ON fraud_evaluations (transaction_id);





CREATE TABLE IF NOT EXISTS outbox_messages

(

    id               UUID PRIMARY KEY,

    type             VARCHAR(128) NOT NULL,

    payload          JSONB NOT NULL,

    created_at_utc   TIMESTAMPTZ NOT NULL,

    processed_at_utc TIMESTAMPTZ,

    attempts         INT NOT NULL DEFAULT 0,

    last_error       TEXT

);



CREATE INDEX IF NOT EXISTS ix_outbox_messages_pending

    ON outbox_messages (created_at_utc)

    WHERE processed_at_utc IS NULL;





CREATE TABLE IF NOT EXISTS audit_logs

(

    id               UUID PRIMARY KEY,

    transaction_id   UUID,

    action           VARCHAR(64) NOT NULL,

    payload          JSONB NOT NULL,

    created_at_utc   TIMESTAMPTZ NOT NULL,

    CONSTRAINT fk_audit_logs_transaction

        FOREIGN KEY (transaction_id)

        REFERENCES transactions (id)

        ON DELETE SET NULL

);



CREATE INDEX IF NOT EXISTS ix_audit_logs_transaction_created

    ON audit_logs (transaction_id, created_at_utc DESC);

