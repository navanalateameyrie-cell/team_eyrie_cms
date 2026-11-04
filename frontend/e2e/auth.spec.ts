import { test, expect } from "@playwright/test";

const ADMIN_EMAIL = process.env.CMS_ADMIN_EMAIL ?? "admin@cms.local";
const ADMIN_PASS  = process.env.CMS_ADMIN_PASS  ?? "Admin!23";

test.describe("Auth smoke", () => {
  test("login page renders and bad credentials show an error", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByRole("button", { name: /sign in/i })).toBeVisible();

    await page.getByLabel(/email/i).fill("nope@example.com");
    await page.getByLabel(/password/i).fill("wrong-password");
    await page.getByRole("button", { name: /sign in/i }).click();

    // Any visible error message is sufficient — exact copy is UX-churn risk.
    await expect(page.locator("body")).toContainText(/invalid|failed|unauthor/i);
  });

  test("admin sign-in reaches the home page without a server error", async ({ page }) => {
    await page.goto("/login");
    await page.getByLabel(/email/i).fill(ADMIN_EMAIL);
    await page.getByLabel(/password/i).fill(ADMIN_PASS);
    await page.getByRole("button", { name: /sign in/i }).click();

    // After login the app lands on "/" (or /dashboard). Assert we're no longer on /login.
    await expect(page).not.toHaveURL(/\/login/);
    // Header user chip carries the signed-in email.
    await expect(page.locator("body")).toContainText(ADMIN_EMAIL.split("@")[0]);
  });
});
