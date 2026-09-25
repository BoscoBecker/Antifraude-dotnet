/**
 * Carga simples — só POST (não valida decisão final).
 * Para regras de fraude use fraud-rules.js; stress com mix use stress-mixed.js.
 */
import http from "k6/http";
import { check, sleep } from "k6";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { baseUrl } from "./helpers/antifraud.js";

export const options = {
  scenarios: {
    steady_load: {
      executor: "ramping-vus",
      startVUs: 0,
      stages: [
        { duration: "30s", target: 10 },
        { duration: "1m", target: 25 },
        { duration: "30s", target: 0 },
      ],
      gracefulRampDown: "10s",
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.05"],
    http_req_duration: ["p(95)<3000"],
  },
};

export default function () {
  const payload = JSON.stringify({
    externalReference: `ORD-${__VU}-${__ITER}`,
    merchantId: "MRC-K6",
    customerId: `CUS-${__VU % 8}`,
    amount: 150,
    currency: "BRL",
    paymentMethod: "PIX",
  });

  const res = http.post(`${baseUrl}/transactions`, payload, {
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": `k6-${uuidv4()}`,
    },
    tags: { name: "POST /transactions" },
  });

  check(res, {
    "status 202 or 200": (r) => r.status === 202 || r.status === 200,
  });

  sleep(0.3);
}
