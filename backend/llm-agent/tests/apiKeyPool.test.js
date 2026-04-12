import test from "node:test";
import assert from "node:assert/strict";

import { withGoogleGenAiClient } from "../src/apiKeyPool.js";

function clearApiKeyPoolState() {
  globalThis.__cursivisApiKeyPools?.clear?.();
  delete process.env.GOOGLE_API_KEY;
  delete process.env.GEMINI_API_KEY;
  delete process.env.GOOGLE_API_KEYS;
  delete process.env.GEMINI_API_KEYS;
}

test("rotates to the next configured API key when the current key is quota-limited", async () => {
  clearApiKeyPoolState();
  process.env.GOOGLE_API_KEYS = "key-one,key-two,key-three";

  const attemptedKeys = [];

  const result = await withGoogleGenAiClient(async (_client, entry) => {
    attemptedKeys.push(entry.apiKey);

    if (entry.apiKey === "key-one") {
      throw new Error("RESOURCE_EXHAUSTED: retry in 12s");
    }

    return entry.apiKey;
  });

  assert.equal(result, "key-two");
  assert.deepEqual(attemptedKeys, ["key-one", "key-two"]);
});

test("keeps exhausted keys on cooldown for the next request and skips them immediately", async () => {
  clearApiKeyPoolState();
  process.env.GOOGLE_API_KEYS = "alpha,beta";

  const firstPass = [];
  const secondPass = [];

  const firstResult = await withGoogleGenAiClient(async (_client, entry) => {
    firstPass.push(entry.apiKey);

    if (entry.apiKey === "alpha") {
      throw new Error("RESOURCE_EXHAUSTED: retry in 60s");
    }

    return entry.apiKey;
  });

  const secondResult = await withGoogleGenAiClient(async (_client, entry) => {
    secondPass.push(entry.apiKey);
    return entry.apiKey;
  });

  assert.equal(firstResult, "beta");
  assert.deepEqual(firstPass, ["alpha", "beta"]);
  assert.equal(secondResult, "beta");
  assert.deepEqual(secondPass, ["beta"]);
});

