import { test, expect, type Page } from "@playwright/test";
import { fulfillJson } from "./apiEnvelopes";

/**
 * The catalog tests prove a key exists in both locales. They cannot prove it reaches the screen —
 * that needs the real page, the real error path, and a real locale.
 *
 * Both halves of the auth surface are covered because the locale comes from a different place on
 * each. A signed-in user has one stored on their profile; a visitor at the login screen has no
 * profile yet, so the only signal is the browser's own language. Stubbing `/api/users/me` would
 * prove nothing about the second case.
 */

const problem = (body: Record<string, unknown>) => ({
  status: (body.status as number) ?? 400,
  contentType: "application/problem+json",
  body: JSON.stringify(body),
});

const usePortugueseBrowser = async (page: Page) => {
  await page.addInitScript(() => {
    Object.defineProperty(navigator, "language", {
      get: () => "pt-BR",
      configurable: true,
    });
    Object.defineProperty(navigator, "languages", {
      get: () => ["pt-BR"],
      configurable: true,
    });
  });
};

const signedIn = async (page: Page, locale: string) => {
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
    fulfillJson(route, { userExists: true }),
  );

  await page.route("**/api/users/me", (route) =>
    fulfillJson(route, {
      userId: "user-1",
      email: "admin@example.com",
      credentials: [
        {
          provider: "streamelements",
          accountName: null,
          expiresAt: null,
          lastValidatedAt: null,
          lastValidationError: null,
        },
      ],
      locale,
    }),
  );
};

test("a rejected sign-in renders the code in the browser's locale", async ({
  page,
}) => {
  await usePortugueseBrowser(page);

  await page.route("**/api/auth/bootstrap", (route) =>
    fulfillJson(route, { userExists: true }),
  );

  // What the backend sends for wrong credentials: a code, and a title that is developer copy.
  await page.route("**/api/auth/login", (route) =>
    route.fulfill(
      problem({
        status: 401,
        title: "Unauthorized",
        code: "auth.unauthorized",
        params: {},
      }),
    ),
  );

  await page.goto("/login");
  await page.getByLabel("E-mail").fill("admin@example.com");
  await page.getByLabel("Senha", { exact: true }).fill("wrong-password");
  await page.getByRole("button", { name: "Entrar" }).click();

  await expect(page.getByText("Credenciais inválidas.")).toBeVisible();
  await expect(page.getByText("Unauthorized")).toHaveCount(0);
});

/**
 * The sentence for a refused authenticator code was authored in both locales and reached no screen,
 * because nothing emitted the code for it. This drives the two-step challenge the real login uses:
 * credentials first, then the code, each answered by the backend on its own terms.
 */
test("a refused authenticator code renders its own sentence, not the credentials one", async ({
  page,
}) => {
  await usePortugueseBrowser(page);

  await page.route("**/api/auth/bootstrap", (route) =>
    fulfillJson(route, { userExists: true }),
  );

  let attempt = 0;
  await page.route("**/api/auth/login", (route) => {
    attempt += 1;
    // The first attempt carries no code, so the second factor is still owed; the second carries a
    // wrong one. Both are a 401 whose `detail` reads the same — the code is what separates them.
    const body =
      attempt === 1
        ? {
            status: 401,
            title: "Unauthorized",
            detail: "RequiresTwoFactor",
            code: "auth.two_factor_required",
            params: {},
          }
        : {
            status: 401,
            title: "Unauthorized",
            detail: "Failed",
            code: "auth.two_factor_invalid",
            params: {},
          };
    return route.fulfill(problem(body));
  });

  await page.goto("/login");
  await page.getByLabel("E-mail").fill("admin@example.com");
  await page.getByLabel("Senha", { exact: true }).fill("correct-password");
  await page.getByRole("button", { name: "Entrar" }).click();

  const code = page.getByLabel("Código do autenticador");
  await expect(code).toBeVisible();
  await code.fill("000000");
  await page.getByRole("button", { name: "Entrar" }).click();

  await expect(
    page.getByText("Código de autenticação de dois fatores inválido."),
  ).toBeVisible();
  // The wrong sentence for this failure.
  await expect(page.getByText("Credenciais inválidas.")).toHaveCount(0);
  await expect(page.getByText("Unauthorized")).toHaveCount(0);
  // The field stays on screen so the code can be retyped.
  await expect(code).toBeVisible();
});

test("a rejected password change names the rule in Portuguese", async ({
  page,
}) => {
  await signedIn(page, "pt-BR");

  // A validation failure leaves the outer code generic and names the rule in params.errors[].
  await page.route("**/api/auth/manage/info", (route) =>
    route.fulfill(
      problem({
        status: 400,
        title: "One or more validation errors occurred.",
        code: "validation.failed",
        params: {
          errors: [
            {
              field: "password",
              code: "validation.password_requires_digit",
              params: {},
            },
          ],
        },
      }),
    ),
  );

  await page.goto("/settings#security");
  await page.getByLabel("Senha atual").fill("current-password");
  await page.getByLabel("Nova senha", { exact: true }).fill("newpassword");
  await page.getByLabel("Confirmar nova senha").fill("newpassword");
  await page.getByRole("button", { name: "Atualizar senha" }).click();

  await expect(
    page.getByText("A senha deve conter pelo menos um número."),
  ).toBeVisible();
  await expect(
    page.getByText("One or more validation errors occurred."),
  ).toHaveCount(0);
});

/**
 * Two rules breaking at once must both appear, each against the input it belongs to. Reading only
 * `params.errors[0]` renders one sentence for the pair.
 */
test("every broken password rule reaches the screen in Portuguese", async ({
  page,
}) => {
  await signedIn(page, "pt-BR");

  await page.route("**/api/auth/manage/info", (route) =>
    route.fulfill(
      problem({
        status: 400,
        title: "One or more validation errors occurred.",
        code: "validation.failed",
        params: {
          errors: [
            {
              field: "password",
              code: "validation.password_requires_digit",
              params: {},
            },
            {
              field: "password",
              code: "validation.password_requires_upper",
              params: {},
            },
          ],
        },
      }),
    ),
  );

  await page.goto("/settings#security");
  await page.getByLabel("Senha atual").fill("current-password");
  await page.getByLabel("Nova senha", { exact: true }).fill("newpassword");
  await page.getByLabel("Confirmar nova senha").fill("newpassword");
  await page.getByRole("button", { name: "Atualizar senha" }).click();

  await expect(
    page.getByText("A senha deve conter pelo menos um número."),
  ).toBeVisible();
  await expect(
    page.getByText("A senha deve conter pelo menos uma letra maiúscula."),
  ).toBeVisible();
  await expect(page.getByLabel("Nova senha", { exact: true })).toHaveAttribute(
    "aria-invalid",
    "true",
  );
});

/**
 * The add-store dialog has no input for either validated field, so both sentences are composed
 * through `validation.fieldMessage`. This is the case that proves the composed form reads naturally
 * in Portuguese — the colon shape needs no gender or number agreement, which is why it was chosen
 * over interpolating the field name into the sentence itself.
 */
test("a store creation that breaks two fields names both in Portuguese", async ({
  page,
}) => {
  await signedIn(page, "pt-BR");

  await page.route("**/api/stores", (route) => {
    if (route.request().method() !== "POST") {
      return fulfillJson(route, { items: [], total: 0 });
    }
    return route.fulfill(
      problem({
        status: 400,
        title: "One or more validation errors occurred.",
        code: "validation.failed",
        params: {
          errors: [
            {
              field: "channelId",
              code: "validation.required",
              params: { field: "channelId" },
            },
            {
              field: "displayName",
              code: "validation.required",
              params: { field: "displayName" },
            },
          ],
        },
      }),
    );
  });

  await page.route("**/api/stores/lookup**", (route) =>
    fulfillJson(route, {
      externalId: "ch-1",
      handle: "xqc",
      displayName: "xQc",
      avatarUrl: null,
      itemCount: null,
    }),
  );

  await page.goto("/stores");
  await page.getByRole("button", { name: "Adicionar loja" }).click();
  await page.getByPlaceholder("ex.: xqc").fill("xqc");
  await page.getByRole("button", { name: "Buscar" }).click();
  await page.getByRole("button", { name: "Continuar" }).click();

  await expect(
    page.getByText("ID do canal: Este campo é obrigatório."),
  ).toBeVisible();
  await expect(
    page.getByText("Nome da loja: Este campo é obrigatório."),
  ).toBeVisible();
});
