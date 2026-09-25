/**
 * Stress — mix ponderado: maioria APPROVED + picos REVIEW/REJECTED.
 *
 * k6 run -e BASE_URL=http://localhost:5080 stress-mixed.js
 */
import { check, sleep } from "k6";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import {
  buildPayload,
  newIdempotencyKey,
  submitTransaction,
  waitForFinalDecision,
} from "./helpers/antifraud.js";

export const options = {
  scenarios: {
    mixed_load: {
      executor: "ramping-vus",
      startVUs: 0,
      stages: [
        { duration: "20s", target: 5 },
        { duration: "1m", target: 20 },
        { duration: "20s", target: 0 },
      ],
      gracefulRampDown: "15s",
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.08"],
    http_req_duration: ["p(95)<4000"],
    checks: ["rate>0.90"],
  },
};

const verifyDecision = __ENV.VERIFY_DECISION === "true";

export default function () {
  const roll = Math.random();
  const bucket = __VU % 20;
  const customerId =
    roll < 0.15 ? `K6-STRESS-VEL-${bucket}` : `K6-STRESS-${__VU}-${__ITER}`;

  let amount = 100 + (__ITER % 500);
  let expected = "APPROVED";
  let scenario = "approved";

  if (roll >= 0.92) {
    amount = 12000;
    expected = "REVIEW";
    scenario = "review_high";
  } else if (roll >= 0.85) {
    amount = 15000;
    expected = bucket < 4 ? "REJECTED" : "REVIEW";
    scenario = "high_risk";
  }

  const { transactionId } = submitTransaction(
    buildPayload({ customerId, amount }),
    newIdempotencyKey("stress"),
    { scenario },
  );

  if (verifyDecision) {
    const body = waitForFinalDecision(transactionId, null);
    check(body, {
      "decision not PENDING": (b) => b.decision !== "PENDING",
    });
  } else {
    sleep(0.2);
  }
}
