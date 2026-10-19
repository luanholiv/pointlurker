import { test, expect, type APIRequestContext } from "@playwright/test";
import {
  EMAIL,
  ensureSeToken,
  forbidPageRoute,
  loginAsAdmin,
  readAccessToken,
} from "./realStack";

// The thin, real-stack half of the availability-notification proof. It asserts only that the
// SHIPPED backend image reaches a real SMTP server and that the mail arrives. What the message
// says — MIME shape, localized subject, the rows written alongside it — belongs to the backend
// integration test, which pins all of that far more cheaply than a browser can.
//
// What nothing else can see: the Smtp:* binding and the MailKit transport as they are wired in a
// published image, the backend container's route to the mail service, and the sync hosted service
// firing on its cron in that image rather than being called directly from a test.
//
// The whole run, through the real stack:
//   drop this spec's store and empty the mailbox -> reset WireMock to the sold-out payload ->
//   create the store, which imports the item unavailable at create time, outside any cycle ->
//   wishlist it -> flip WireMock to in-stock -> one sync cycle sees the transition -> the backend
//   mails the single user -> Mailpit holds it.
//
// Deliberately NOT check-or-create like its four siblings. Their subject is a steady state; this
// one's is a transition, which a retry against a warm database cannot observe a second time. Every
// attempt therefore starts by deleting the store and resetting the scenario, so the path is always
// the cold one.

// Published by the compose overlay on E2E_MAILPIT_PORT. nginx proxies only /api, so reading the
// mailbox is the one hop the browser cannot make on the spec's behalf.
const MAILPIT_URL = process.env.E2E_MAILPIT_URL ?? "http://localhost:8026";

// The channel is this store's identity, not its name. The name given at creation does not survive:
// the channel-info refresh overwrites every store's display name with whatever the channel lookup
// returns, and the WireMock catch-all answers every channel with the same fixture, so they all
// converge on one name. Uniqueness — and the 409 on a repeat create — live on the channel id.
const CHANNEL_ID = "e2e-notify-channel-id";
const STORE_DISPLAY_NAME = "E2E Notify Store";
// Must match the item in mappings/store-items-notify-*.json.
const ITEM_NAME = "E2E Notify Item";

// Not channels. Looking either of these up flips the WireMock scenario that decides which payload
// the notify store's items endpoint serves. Both answer 404, which is also how the spec knows the
// priority-1 control stub matched rather than the catch-all, since that one answers 200 for any
// username at all.
const SET_AVAILABLE = "e2e_notify_set_available";
const SET_UNAVAILABLE = "e2e_notify_set_unavailable";

// The cron is five-field, so a cycle is at worst a wall-clock minute away; two minutes is two
// boundaries of headroom.
const ONE_SYNC_CYCLE = 120_000;
// The send happens in the same cycle, a moment after the item row lands. This budget is really the
// retry policy's worst case rather than the cron's.
const MAIL_DELIVERY = 30_000;

type MailpitList = {
  messages: { ID: string; To: { Address: string }[] }[];
};

type StoreListItem = { id: string; channelId: string };
type StoreItemRow = { id: string; name: string; isAvailable: boolean };

test.beforeEach(async ({ page }) => {
  // Two cron waits plus the sign-in preamble. The longest of the real specs, by design.
  test.setTimeout(240_000);
  forbidPageRoute(page, "sync-notify-email.real.spec.ts");
});

test("a real sync cycle mails the availability notification over SMTP", async ({
  page,
}) => {
  await loginAsAdmin(page);
  await ensureSeToken(page);

  const api = page.request;
  const auth = { Authorization: `Bearer ${await readAccessToken(page)}` };

  // 1. Cold start. Deleting the store cascades to its items and wishlist entries, so a retry sees
  //    the same empty ground as a first attempt.
  const listed = await api.get("/api/stores", { headers: auth });
  expect(listed.status(), "GET /api/stores").toBe(200);
  const { items: stores } = (await listed.json()) as { items: StoreListItem[] };
  const stale = stores.find((s) => s.channelId === CHANNEL_ID);
  if (stale) {
    const removed = await api.delete(`/api/stores/${stale.id}`, {
      headers: auth,
    });
    expect(removed.status(), "DELETE /api/stores/{id}").toBe(204);
  }

  await flipScenario(api, auth, SET_UNAVAILABLE);

  const emptied = await api.delete(`${MAILPIT_URL}/api/v1/messages`);
  expect(
    emptied.ok(),
    "Mailpit must be reachable on its published host port",
  ).toBeTruthy();

  // 2. Create the store. Items are imported by the create handler, not by a sync cycle, so the row
  //    is persisted unavailable without the cron having been involved at all.
  const created = await api.post("/api/stores", {
    headers: auth,
    data: { channelId: CHANNEL_ID, displayName: STORE_DISPLAY_NAME },
  });
  expect(created.status(), "POST /api/stores").toBe(201);
  const { id: storeId } = (await created.json()) as { id: string };

  // 3. Wishlist the sold-out item. Without an entry that notifies, the cycle drops the availability
  //    event in silence, so this is a precondition rather than a detail. autoRedeem stays off: the
  //    redeem path would call a points endpoint WireMock does not stub.
  const [item] = await readItems(api, auth, storeId);
  expect(item.name).toBe(ITEM_NAME);
  expect(item.isAvailable, "the item must start unavailable").toBe(false);

  const wishlisted = await api.post("/api/wishlist", {
    headers: auth,
    data: {
      storeItemId: item.id,
      notifyOnAvailable: true,
      autoRedeem: false,
      redemptionMessage: null,
    },
  });
  expect(wishlisted.status(), "POST /api/wishlist").toBe(201);

  // 4. Restock upstream. The next cycle diffs this against the row above and raises the transition.
  await flipScenario(api, auth, SET_AVAILABLE);

  // 5. Wait for the cycle. Split from the mailbox wait on purpose: this failing means the cron never
  //    fired or WireMock never flipped, which is a different fault from a send that never left the
  //    container — and one combined timeout could not tell them apart.
  await expect
    .poll(async () => (await readItems(api, auth, storeId))[0]?.isAvailable, {
      message: "a sync cycle must observe the item as available",
      timeout: ONE_SYNC_CYCLE,
      intervals: [2_000],
    })
    .toBe(true);

  // 6. The point of the spec: the container's SMTP client reached the server and the message was
  //    accepted.
  let deliveredId: string | undefined;
  await expect
    .poll(
      async () => {
        deliveredId = await findNotificationId(api);
        return deliveredId;
      },
      {
        message: `Mailpit must hold an availability notification addressed to ${EMAIL}`,
        timeout: MAIL_DELIVERY,
        intervals: [1_000],
      },
    )
    .toBeTruthy();

  // Identity, not shape: enough to know this is that notification and not some other mail.
  const message = await api.get(`${MAILPIT_URL}/api/v1/message/${deliveredId}`);
  expect(message.ok()).toBeTruthy();
  const { HTML } = (await message.json()) as { HTML: string };
  expect(HTML).toContain(ITEM_NAME);
});

/** Flips the WireMock scenario through the backend's own StreamElements path. */
async function flipScenario(
  api: APIRequestContext,
  auth: Record<string, string>,
  username: string,
): Promise<void> {
  const response = await api.get(
    `/api/stores/lookup?provider=streamelements&query=${username}`,
    {
      headers: auth,
    },
  );
  // 404 is the control stub answering. A 200 would mean the catch-all matched and the scenario
  // never moved — which would otherwise surface two minutes later as an unexplained timeout.
  expect(response.status(), `control stub for ${username}`).toBe(404);
}

async function findNotificationId(
  api: APIRequestContext,
): Promise<string | undefined> {
  const response = await api.get(`${MAILPIT_URL}/api/v1/messages?limit=50`);
  if (!response.ok()) {
    return undefined;
  }
  const { messages } = (await response.json()) as MailpitList;
  return messages.find((m) => m.To.some((to) => to.Address === EMAIL))?.ID;
}

async function readItems(
  api: APIRequestContext,
  auth: Record<string, string>,
  storeId: string,
): Promise<StoreItemRow[]> {
  const response = await api.get(`/api/stores/${storeId}/items?avail=0`, {
    headers: auth,
  });
  expect(response.status(), `GET /api/stores/${storeId}/items`).toBe(200);
  const { items } = (await response.json()) as { items: StoreItemRow[] };
  return items;
}
