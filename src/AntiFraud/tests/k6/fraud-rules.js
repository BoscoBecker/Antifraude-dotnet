/**
 * Cenários funcionais — regras de fraude (1 VU, sequencial).
 * Requer API + Worker (+ Rabbit se UseRabbitMq=true).
 *
 * k6 run -e BASE_URL=http://localhost:5080 fraud-rules.js
 */
import { group } from "k6";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import {
  buildPayload,
  newIdempotencyKey,
  submitTransaction,
  waitForFinalDecision,
} from "./helpers/antifraud.js";

export const options = {
  vus: 1,
  iterations: 1,
  thresholds: {
    checks: ["rate>0.95"],
  },
};

export default function () {
  const runId = uuidv4().slice(0, 8);

  group("APPROVED — valor baixo, cliente único", () => {
    const customerId = `K6-OK-${runId}`;
    const { transactionId } = submitTransaction(
      buildPayload({ customerId, amount: 250.0 }),
      newIdempotencyKey("approved"),
      { scenario: "approved" },
    );
    waitForFinalDecision(transactionId, "APPROVED");
  });

  group("REVIEW — HIGH_AMOUNT (>= 10000)", () => {
    const customerId = `K6-HIGH-${runId}`;
    const { transactionId } = submitTransaction(
      buildPayload({ customerId, amount: 10000.0 }),
      newIdempotencyKey("review-high"),
      { scenario: "review_high_amount" },
    );
    waitForFinalDecision(transactionId, "REVIEW");
  });

  group("REVIEW — VELOCITY (5 txs mesmo cliente)", () => {
    const customerId = `K6-VEL-${runId}`;
    let lastId = null;
    for (let i = 0; i < 5; i++) {
      const { transactionId } = submitTransaction(
        buildPayload({
          customerId,
          amount: 50.0,
          externalReference: `VEL-${runId}-${i}`,
        }),
        newIdempotencyKey(`velocity-${i}`),
        { scenario: "review_velocity" },
      );
      lastId = transactionId;
    }
    waitForFinalDecision(lastId, "REVIEW");
  });

  group("REJECTED — HIGH_AMOUNT + VELOCITY (4 baixas + 1 >= 10000)", () => {
    const customerId = `K6-REJ-${runId}`;
    for (let i = 0; i < 4; i++) {
      const { transactionId } = submitTransaction(
        buildPayload({
          customerId,
          amount: 100.0,
          externalReference: `REJ-PREP-${runId}-${i}`,
        }),
        newIdempotencyKey(`reject-prep-${i}`),
        { scenario: "rejected_prep" },
      );
      waitForFinalDecision(transactionId, "APPROVED");
    }

    const { transactionId } = submitTransaction(
      buildPayload({
        customerId,
        amount: 15000.0,
        externalReference: `REJ-FINAL-${runId}`,
      }),
      newIdempotencyKey("reject-final"),
      { scenario: "rejected" },
    );
    waitForFinalDecision(transactionId, "REJECTED");
  });
}
