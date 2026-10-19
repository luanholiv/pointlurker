import { test, expect, type Page } from "@playwright/test";

/**
 * These drive the settings 2FA panel through the real `apiClient` and `AuthProvider`, stubbing
 * only HTTP. The component tests mock `useAuth` wholesale, so they would still pass if the
 * frontend read a different field name than the backend writes — nothing else in either suite
 * pins both ends of `twoFactorEnabled` to the same spelling.
 */

/** Mutable so a handler can move the stored state mid-test and the next fetch observes it. */
type Profile = { twoFactorEnabled: boolean };

const signedIn = async (page: Page, profile: Profile) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      "pl.auth.token",
      JSON.stringify({
        accessToken: "token",
        refreshToken: "refresh",
        expiresAt: Date.now() + 3600000,
      }),
    );
  });

  await page.route("**/api/auth/bootstrap", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ userExists: true }),
    }),
  );

  await page.route("**/api/users/me", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        userId: "user",
        email: "admin@example.com",
        credentials: [],
        locale: "en-US",
        twoFactorEnabled: profile.twoFactorEnabled,
      }),
    }),
  );
};

/** The panel must open on the account's stored state, not on "not configured". */
test("an account that already has two-factor opens on the enabled state", async ({
  page,
}) => {
  await signedIn(page, { twoFactorEnabled: true });

  await page.goto("/settings#security");

  await expect(page.getByText("Two-factor is enabled.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Disable" })).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Enable two-factor" }),
  ).toHaveCount(0);
});

test("an account without two-factor is offered enrolment", async ({ page }) => {
  await signedIn(page, { twoFactorEnabled: false });

  await page.goto("/settings#security");

  await expect(
    page.getByRole("button", { name: "Enable two-factor" }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Disable" })).toHaveCount(0);
});

test("disabling asks first, revokes the shared key, then flips the panel", async ({
  page,
}) => {
  const profile: Profile = { twoFactorEnabled: true };
  await signedIn(page, profile);

  let disablePayload: unknown;
  await page.route("**/api/auth/manage/2fa", async (route) => {
    disablePayload = route.request().postDataJSON();
    profile.twoFactorEnabled = false;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        sharedKey: "ROTATEDKEY7777AA",
        recoveryCodes: null,
        recoveryCodesLeft: 0,
        isTwoFactorEnabled: false,
        isMachineRemembered: false,
      }),
    });
  });

  await page.goto("/settings#security");
  await page.getByRole("button", { name: "Disable" }).click();

  await expect(
    page.getByText("Turn off two-factor authentication?"),
  ).toBeVisible();
  expect(disablePayload).toBeUndefined();

  await page.getByRole("button", { name: "Turn it off" }).click();

  // The panel re-reads the profile rather than trusting a local flag, so the flip proves the
  // whole loop: confirm, POST, refetch, render.
  await expect(
    page.getByRole("button", { name: "Enable two-factor" }),
  ).toBeVisible();
  await expect(page.getByText("Two-factor is enabled.")).toHaveCount(0);

  // `enable: false` alone would leave the revoked authenticator producing valid codes.
  expect(disablePayload).toEqual({ enable: false, resetSharedKey: true });
});

test("enrolment renders a named QR and shows the recovery codes once", async ({
  page,
}) => {
  const profile: Profile = { twoFactorEnabled: false };
  await signedIn(page, profile);

  await page.route("**/api/auth/manage/2fa", async (route) => {
    const body = route.request().postDataJSON() as { enable?: boolean };

    if (body?.enable === true) {
      profile.twoFactorEnabled = true;
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          sharedKey: "JBSWY3DPEHPK3PXP",
          recoveryCodes: ["aaaa-1111", "bbbb-2222"],
          recoveryCodesLeft: 2,
          isTwoFactorEnabled: true,
          isMachineRemembered: false,
        }),
      });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        sharedKey: "JBSWY3DPEHPK3PXP",
        recoveryCodes: null,
        recoveryCodesLeft: 0,
        isTwoFactorEnabled: false,
        isMachineRemembered: false,
      }),
    });
  });

  await page.goto("/settings#security");
  await page.getByRole("button", { name: "Enable two-factor" }).click();

  await expect(
    page.getByRole("img", { name: "Scan this key in your authenticator app." }),
  ).toBeVisible();
  await expect(page.getByText("JBSWY3DPEHPK3PXP")).toBeVisible();

  await page.getByLabel("Verification code").fill("123456");
  await page.getByRole("button", { name: "Verify and enable" }).click();

  // Identity issues these exactly once, so the panel is the only place they will ever appear.
  const codes = page.getByRole("list", { name: "Recovery codes" });
  await expect(codes).toContainText("aaaa-1111");
  await expect(codes).toContainText("bbbb-2222");
  await expect(page.getByText("Two-factor is enabled.")).toBeVisible();
});
