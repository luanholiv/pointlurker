import { describe, it, expect } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "./server";

describe("MSW server infrastructure", () => {
  it("intercepts fetch to a default-registered handler", async () => {
    const res = await fetch("http://localhost/api/auth/bootstrap");
    expect(res.ok).toBe(true);
  });

  it("server.use() overrides the default handler for one test", async () => {
    server.use(
      http.get("http://localhost/api/auth/bootstrap", () =>
        HttpResponse.json({ userExists: true }),
      ),
    );
    const res = await fetch("http://localhost/api/auth/bootstrap");
    const data = (await res.json()) as { userExists: boolean };
    expect(data.userExists).toBe(true);
  });

  it("handler reverts to default after resetHandlers runs between tests", async () => {
    const res = await fetch("http://localhost/api/auth/bootstrap");
    const data = (await res.json()) as { userExists: boolean };
    expect(data.userExists).toBe(false);
  });
});
