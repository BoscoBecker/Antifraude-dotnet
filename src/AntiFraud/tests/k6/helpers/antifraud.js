import http from "k6/http";
import { check, fail } from "k6";
import { sleep } from "k6";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

export const baseUrl = __ENV.BASE_URL || "http://localhost:5080";

const pollIntervalSec = Number(__ENV.POLL_INTERVAL_SEC || "0.5");
const pollMaxAttempts = Number(__ENV.POLL_MAX_ATTEMPTS || "60");

export function newIdempotencyKey(prefix = "k6") {
  return `${prefix}-${uuidv4()}`;
}

export function submitTransaction(payload, idempotencyKey, tags = {}) {
  const res = http.post(`${baseUrl}/transactions`, JSON.stringify(payload), {
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": idempotencyKey,
    },
    tags: { name: "POST /transactions", ...tags },
  });

  check(res, {
    "POST accepted (202) or idempotent (200)": (r) =>
      r.status === 202 || r.status === 200,
  });

  if (res.status !== 202 && res.status !== 200) {
    fail(`POST failed: ${res.status} ${res.body}`);
  }

  const body = JSON.parse(res.body);
  return { response: res, body, transactionId: body.id };
}

export function getTransaction(transactionId) {
  return http.get(`${baseUrl}/transactions/${transactionId}`, {
    tags: { name: "GET /transactions/{id}" },
  });
}

/**
 * Aguarda decisão final (não PENDING). Requer Worker em execução.
 */
export function waitForFinalDecision(transactionId, expectedDecision = null) {
  let lastBody = null;

  for (let attempt = 0; attempt < pollMaxAttempts; attempt++) {
    const res = getTransaction(transactionId);
    if (res.status !== 200) {
      sleep(pollIntervalSec);
      continue;
    }

    lastBody = JSON.parse(res.body);
    if (
      lastBody.decision &&
      lastBody.decision !== "PENDING" &&
      lastBody.status === "COMPLETED"
    ) {
      if (expectedDecision) {
        check(lastBody, {
          [`decision is ${expectedDecision}`]: (b) =>
            b.decision === expectedDecision,
        });
      }
      return lastBody;
    }

    sleep(pollIntervalSec);
  }

  fail(
    `Timeout waiting for decision on ${transactionId}. Last: ${JSON.stringify(lastBody)}`,
  );
}

export function buildPayload({
  customerId,
  amount,
  externalReference,
  merchantId = "MRC-K6",
  currency = "BRL",
  paymentMethod = "PIX",
}) {
  return {
    externalReference: externalReference || `ORD-${uuidv4()}`,
    merchantId,
    customerId,
    amount,
    currency,
    paymentMethod,
  };
}
