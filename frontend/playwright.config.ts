import { defineConfig, devices } from "@playwright/test";

/**
 * Minimal Playwright config for the CMS smoke suite. Expects the full stack to be reachable at
 * CMS_BASE_URL (default http://localhost:5173). Run `docker compose up -d --build` first, then
 * `npm run e2e`.
 */
const baseURL = process.env.CMS_BASE_URL ?? "http://localhost:5173";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  retries: 1,
  workers: 1,
  reporter: [["list"]],
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    ignoreHTTPSErrors: true,
    actionTimeout: 10_000,
    navigationTimeout: 20_000,
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
  ],
});
