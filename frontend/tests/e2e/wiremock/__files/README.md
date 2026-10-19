# WireMock response bodies

- `channel-response.json` — stub for `GET /kappa/v2/channels/{username}`. The channel
  id uses JSON key `_id` (the backend's `ChannelLookupResponse` reads `_id`, not `id`).
- `channel-response-profile.json` — a **second** channel with a distinct `_id`, served only
  for username `e2e_profile` (`mappings/channels-lookup-profile.json`, priority 1, ahead of
  the priority-5 catch-all). Without it every lookup resolves to the same `_id`, so a second
  `POST /api/stores` fails `stores.already_exists` and the profile spec would have to share
  `smoke.real.spec.ts`'s store — including its destructive delete steps.
- `store-items-response.json` — **not committed here.** It is bind-mounted at runtime from
  `backend/tests/Infrastructure.ExternalServices.ContractTests/Fixtures/store-items-response.json`
  by `docker-compose.e2e.yml`, so the StreamElements contract tests and this real-smoke
  stub share one source of truth. WireMock resolves `bodyFileName` per request, so the file
  being absent at container start is fine.
