# Error Codes

Every error code the backend emits. All HTTP errors are [RFC 7807 ProblemDetails](https://www.rfc-editor.org/rfc/rfc7807) with two extensions: `code` (stable, language-free) and `params` (interpolation values).

> **Framework Identity endpoints** (`MapIdentityApi` under `/api/auth`: `register`, `login`, `refresh`, `manage/2fa`, `manage/info`) follow the contract too. `UseIdentityErrorContract` **adds** `code` and `params` to their 4xx `application/problem+json` bodies; built-in fields stay, so a `ValidationProblem` carries both `errors` and `params.errors[]`. A dead refresh token stays a bodyless `401`.

**Source of truth:** `backend/src/Core.Domain/Errors/ErrorCodes.cs`  
**Frontend catalog:** `frontend/src/i18n/locales/en-US.json` (keys under `errors.*`)

---

## How to Add a New Code

1. Declare a constant in `Core.Domain/Errors/ErrorCodes.cs`, in the right nested class.
2. Add `errors.<code>` to both `en-US.json` and `pt-BR.json`, interpolating the same `{{placeholders}}` (`placeholderParity.test.ts`).
3. For a `SubmissionFailureCode`: **append** the enum value (never reorder or renumber), add its arm to `RedemptionFailureCodes.For`, and give the enum member and `ErrorCodes.RedemptionFailure` constant the same name (`RedemptionFailureCodesTests`).
4. Document it here under its namespace as an inline code span (`` `stores.not_found` ``). `Architecture.Tests/ErrorCodeDocumentationTests` fails on a constant missing here and on an entry with no constant.
5. Fill the **Params** column and repeat it in the constant's `Params:` summary comment. `Architecture.Tests/ErrorCodePlaceholderTests` requires every catalog `{{placeholder}}` to be listed, and the column and comment to agree.

Codes reach a response only through `ErrorCodes.*` constants. `Architecture.Tests/PresentationConventionTests` fails on an inline code literal, a hand-written `detail`, error text passed to a `Results` helper, or a `DomainError` not built from `ErrorCodes`.

---

## Namespace: `validation`

Per-field request validation and ASP.NET Identity password rules.

`validation.failed` is the wrapper; each broken rule is a `params.errors[]` entry with its own `field`, `code` and `params`. Several entries may share a field (Identity reports one per unmet password rule). Clients render every entry and fall back to the wrapper's text for an unknown code.

**Three producers**, none of which parses English:

- **Request bodies and `[AsParameters]` query contracts** — a FluentValidation validator per contract (`Presentation.WebApi/Validation/`), run by `ValidationEndpointFilter<TRequest>` via `ValidatesBody<TRequest>()` / `ValidatesQuery<TQuery>()`. Codes come from `WithErrorCode(...)`; params from FluentValidation placeholders, allowlisted so a rejected value is never echoed.
- **Query-string filters** — `Presentation.WebApi/Search/SearchQuery.cs` (see [`search`](#namespace-search)).
- **Framework Identity endpoints** — `Presentation.WebApi/Errors/IdentityErrorMapper.cs`, which also recovers `min` from Identity's English description.

The first two build the envelope in `Validation/FieldErrorProblem.cs`; `IdentityErrorMapper` builds the same shape itself, because it rewrites a buffered framework response. A rule may use a code from another namespace when a more specific sentence exists (e.g. `credentials.secret_required`).

| Code | HTTP Status | Params | Use Case | Frontend Key |
|------|------------|--------|----------|--------------|
| `validation.failed` | 400 | `errors: [{field, code, params}]` | Wrapper for per-field entries | `errors.validation.failed` |
| `validation.required` | — (inside `errors[]`) | `field` | Missing or blank | `errors.validation.required` |
| `validation.too_long` | — | `field`, `max` (int) | Over max length | `errors.validation.too_long` |
| `validation.too_short` | — | `field`, `min` (int) | Under min length | `errors.validation.too_short` |
| `validation.out_of_range` | — | `field`, `min` (int), `max` (int) | Outside inclusive range | `errors.validation.out_of_range` |
| `validation.invalid_format` | — | `field`, `format` | Bad format; fallback when a rule names no code | `errors.validation.invalid_format` |
| `validation.password_too_short` | — | `min` (int) | Identity: password length | `errors.validation.password_too_short` |
| `validation.password_requires_digit` | — | — | Identity: needs digit | `errors.validation.password_requires_digit` |
| `validation.password_requires_upper` | — | — | Identity: needs uppercase | `errors.validation.password_requires_upper` |
| `validation.password_requires_lower` | — | — | Identity: needs lowercase | `errors.validation.password_requires_lower` |
| `validation.password_requires_non_alphanumeric` | — | — | Identity: needs special char | `errors.validation.password_requires_non_alphanumeric` |
| `validation.password_requires_unique_chars` | — | `min` (int) | Identity: unique char count | `errors.validation.password_requires_unique_chars` |
| `validation.password_mismatch` | — | — | Identity: passwords don't match | `errors.validation.password_mismatch` |
| `validation.duplicate_user_name` | — | `userName` | Identity: username taken | `errors.validation.duplicate_user_name` |
| `validation.invalid_user_name` | — | `userName` | Identity: invalid username | `errors.validation.invalid_user_name` |
| `validation.duplicate_email` | — | `email` | Identity: email taken | `errors.validation.duplicate_email` |
| `validation.invalid_email` | — | `email` | Identity: invalid email | `errors.validation.invalid_email` |

Only the wrapper has an HTTP status; nested codes have none.

**Frontend rendering.** `tApiFieldErrors` (`frontend/src/i18n/fieldErrors.ts`) resolves every entry; `splitFieldErrors` puts each under its input, or at form level as `validation.fieldMessage` (`"{{label}}: {{message}}"`) when the form has no such input. Catalog sentences stay field-agnostic; the user-facing field name comes from `frontend/src/i18n/fieldLabels.ts`. `tApiError` returns one string from the first entry, for shapes with no entries (a bodyless 500, a dropped connection).

**Producer differences:**

- `ValidationFailureMapper` and `SearchQuery` repeat `field` inside each entry's `params` (via `FieldErrorProblem.ToEntryParams`); `IdentityErrorMapper` does not. Read `field` from the entry.
- `validation.invalid_format` carries `format` only from `IdentityErrorMapper`, for an unrecognised Identity error: `format` holds Identity's English description and `field` its code. Developer-facing only; never render it.

`validation.too_short` has **no producer** yet; it is reserved for a future minimum-length rule.

---

## Namespace: `request`

The request could not be read, before any handler runs. ASP.NET Core throws `BadHttpRequestException` with its own status, which `GlobalExceptionHandler` honours and logs at `Warning`.

| Code | HTTP Status | Params | Use Case | Frontend Key |
|------|------------|--------|----------|--------------|
| `request.malformed_body` | 4xx | — | Body not bindable (invalid JSON, wrong type, missing required property); status from the exception, usually 400 | `errors.request.malformed_body` |
| `request.body_too_large` | 413 | — | Body over the server's size limit | `errors.request.body_too_large` |

No params: the exception exposes no field detail, and its message is never sent. A body that binds but breaks a rule gets `validation.failed` instead; an unparseable one never reaches a validator. `IntegrationTests/ErrorHandling/RequestValidationSweepTests` covers both across every custom write endpoint and the list endpoints' query strings. These codes can also come from the framework Identity endpoints.

Query parameters are `string?` properties on an `[AsParameters]` record, so `?offset=abc` answers in this contract rather than as a code-less framework `400` (`Architecture.Tests/QueryParameterConventionTests`).

---

## Namespace: `search`

Unreadable filters on the list endpoints (`GET /api/stores`, `GET /api/stores/{id}/items`, `GET /api/wishlist`, `GET /api/redemptions`).

**Per-field codes:** they arrive as `params.errors[]` entries of `validation.failed`, never as a response's own `code`. `field` is the query-string parameter name; the params below are the entry's.

| Code | HTTP Status | Params | Use Case | Frontend Key |
|------|------------|--------|----------|--------------|
| `search.invalid_enum_value` | 400 | `field` (string), `parameter` (string), `value` (string), `allowed` (string[]) | `sort`, `dir` or `status` outside its allowed set | `errors.search.invalid_enum_value` |
| `search.invalid_filter_value` | 400 | `field` (string), `parameter` (string), `value` (string) | Unparseable number, boolean or date, e.g. `min=abc` | `errors.search.invalid_filter_value` |

- `field` and `parameter` hold the same value; the catalog sentences interpolate `parameter`.
- Every unreadable parameter is reported, in read order.
- An unrecognized value is **refused, never defaulted** — a silently defaulted `sort` would return wrongly ordered data that looks like success.
- An omitted parameter is not an error: it means "do not narrow", or the endpoint default.
- Exception: an out-of-range `offset` or `limit` is **clamped** (`SearchQuery.ClampOffset`, `SearchQuery.ClampLimit`). A non-integer one is still `search.invalid_filter_value`.

---

## Namespace: `users`

| Code | HTTP Status | Params | Endpoint | Frontend Key |
|------|------------|--------|----------|--------------|
| `users.register_already_exists` | 400 | — | `POST /api/auth/register` (second call). A per-field entry (`field` = `email`) of `validation.failed`, never the response's own `code` | `errors.users.register_already_exists` |
| `users.not_found` | 404 | — | Owner lookup failed | `errors.users.not_found` |
| `users.locale_unsupported` | 400 | `locale` (string), `allowed` (string[]) | `PATCH /api/users/me/locale` | `errors.users.locale_unsupported` |

---

## Namespace: `credentials`

| Code | HTTP Status | Params | Endpoint | Frontend Key |
|------|------------|--------|----------|--------------|
| `credentials.secret_required` | 400 | `field` (string) | `PUT /api/users/me/credentials/{provider}`, blank `secret` | `errors.credentials.secret_required` |
| `credentials.rejected` | 422 | `provider` (slug) | `PUT /api/users/me/credentials/{provider}` — provider answered 401/403; nothing stored | `errors.credentials.rejected` |
| `credentials.provider_unsupported` | 404 | `provider` (the rejected value), `allowed` (string[]) | `PUT`/`DELETE /api/users/me/credentials/{provider}` with an unknown provider | `errors.credentials.provider_unsupported` |
| `credentials.expired` | 422 | `provider` (slug) | `PUT /api/users/me/credentials/{provider}` — the credential's own expiry (GauPoints `exp`) has passed; refused without calling the provider | `errors.credentials.expired` |

> A check with no verdict (provider unreachable, rate-limited, failing) stores the credential unconfirmed; the account refresh re-checks it. A refresh that is later refused records `credentials.rejected` in `ProviderCredential.LastValidationError`.

---

## Namespace: `auth`

| Code | HTTP Status | Params | Endpoint | Frontend Key |
|------|------------|--------|----------|--------------|
| `auth.provider_credential_not_configured` | 403 | `provider` (slug) | `POST /api/stores` with no credential for that store's provider | `errors.auth.provider_credential_not_configured` |
| `auth.unauthorized` | 401 | — | `POST /api/auth/login` — wrong credentials, locked out, or not allowed | `errors.auth.unauthorized` |
| `auth.two_factor_required` | 401 | — | `POST /api/auth/login` — password accepted, second factor owed | `errors.auth.two_factor_required` |
| `auth.two_factor_invalid` | 401, 400 | — | `POST /api/auth/login` — authenticator code refused; `POST /api/auth/manage/2fa` — enrolment code refused | `errors.auth.two_factor_invalid` |

> `UseIdentityErrorContract` maps the framework's `SignInResult` (in `detail`) to these codes. Lockout and not-allowed share `auth.unauthorized` so an unauthenticated caller learns nothing about the account. A refused authenticator code reads `Failed` in `detail` like a wrong password, so `TwoFactorOutcomeSignInManager` records the attempt it made and the middleware reads that. Enrolment needs no help: its `ValidationProblem` names `InvalidTwoFactorCode`.

---

## Namespace: `stores`

| Code | HTTP Status | Params | Endpoint | Frontend Key |
|------|------------|--------|----------|--------------|
| `stores.not_found` | 404 | — | Any `/api/stores/{id}…` route with an unknown id | `errors.stores.not_found` |
| `stores.already_exists` | 409 | `channelId` (string) | `POST /api/stores` — already watched (for GauPoints, `channelId` is its fixed id) | `errors.stores.already_exists` |
| `stores.provider_unsupported` | 400 | `field` (string) | `GET /api/stores/lookup`, `POST /api/stores` — `provider` is not a valid slug (numbers refused) | `errors.stores.provider_unsupported` |
| `stores.identity_not_accepted` | 400 | `field` (string) | `POST /api/stores` — `channelId`/`displayName` sent for a single-store provider | `errors.stores.identity_not_accepted` |

`stores.provider_unsupported` and `stores.identity_not_accepted` are per-field entries of `validation.failed`. An absent `provider` on `POST /api/stores` means StreamElements. The profile `DELETE` routes for values and aliases are idempotent (`204` whether or not the target existed) and fail only with `stores.not_found`. An over-length profile value is `validation.too_long`.

---

## Namespace: `wishlist`

| Code | HTTP Status | Params | Endpoint | Frontend Key |
|------|------------|--------|----------|--------------|
| `wishlist.entry_not_found` | 404 | — | Wishlist CRUD with an unknown entry | `errors.wishlist.entry_not_found` |
| `wishlist.item_not_found` | 404 | — | Add with an unknown item; `GET /api/wishlist/{id}` when the item row is gone | `errors.wishlist.item_not_found` |
| `wishlist.already_exists` | 409 | — | Duplicate entry | `errors.wishlist.already_exists` |
| `wishlist.missing_input_coverage` | 422 | `missingLabels` (string[]) | Enable auto-redeem without full input coverage | `errors.wishlist.missing_input_coverage` |
| `wishlist.messages_not_supported` | 422 | — | Non-empty `redemptionMessage` for an item that accepts none (an empty string means no message) | `errors.wishlist.messages_not_supported` |

An over-length redemption message is `validation.too_long` (cap: `WishlistEntry.RedemptionMessageMaxLength`).

---

## Namespace: `store_lookup`

| Code | HTTP Status | Params | Endpoint | Frontend Key |
|------|------------|--------|----------|--------------|
| `store_lookup.query_required` | 400 | `field` (string) | `GET /api/stores/lookup?provider=streamelements` with no `query` | `errors.store_lookup.query_required` |
| `store_lookup.not_found` | 404 | `provider` (slug), `query` (string) | `GET /api/stores/lookup` — provider answered 404 | `errors.store_lookup.not_found` |

`store_lookup.query_required` is a per-field entry of `validation.failed` from `StoreLookupQueryValidator` (StreamElements only; GauPoints takes no query). `store_lookup.not_found` is a plain response with no `errors[]`.

---

## Namespace: `provider_api`

| Code | HTTP Status | Params | Use Case | Frontend Key |
|------|------------|--------|----------|--------------|
| `provider_api.unreachable` | 502 | `provider` (slug) | `GET /api/stores/lookup` — no response from the provider | `errors.provider_api.unreachable` |
| `provider_api.fetch_failed` | 502 | `provider` (slug) | `GET /api/stores/lookup` — any other provider error; `POST /api/stores` — identity or items unreadable | `errors.provider_api.fetch_failed` |

---

## Namespace: `redemption.failure`

**Not HTTP codes.** `Redemption.FailureCode` stores the `SubmissionFailureCode` enum as an int; the API sends the code string (`Core.Domain/Errors/RedemptionFailureCodes.For`), which the history page resolves with `tError()`.

- Int values are **append-only** (on-disk representation); `SubmissionFailureCodeTests` snapshots the map.
- `RedemptionFailureCodes.For` ends in a catch-all so one unreadable row cannot fail a page; `RedemptionFailureCodesTests` asserts every declared member maps to its own constant instead.

| Code | Stored int | Enum name | FailureContext | Frontend Key |
|------|------------|-----------|----------------|--------------|
| `redemption.failure.unknown` | 0 | `Unknown` | **Never written.** Only an undeclared stored int or the catch-all reaches it. | `errors.redemption.failure.unknown` |
| `redemption.failure.token_decryption_failed` | 1 | `TokenDecryptionFailed` | `<Provider> credential not configured`, or the decryption exception message | `errors.redemption.failure.token_decryption_failed` |
| `redemption.failure.input_coverage_lost` | 2 | `InputCoverageLost` | Missing input label | `errors.redemption.failure.input_coverage_lost` |
| `redemption.failure.input_decryption_failed` | 3 | `InputDecryptionFailed` | — | `errors.redemption.failure.input_decryption_failed` |
| `redemption.failure.provider_api_bad_request` | 4 | `ProviderApiBadRequest` | `HTTP <status>: <reason phrase>` | `errors.redemption.failure.provider_api_bad_request` |
| `redemption.failure.provider_api_unauthorized` | 5 | `ProviderApiUnauthorized` | `HTTP <status>: <reason phrase>` | `errors.redemption.failure.provider_api_unauthorized` |
| `redemption.failure.provider_api_forbidden` | 6 | `ProviderApiForbidden` | `HTTP <status>: <reason phrase>` | `errors.redemption.failure.provider_api_forbidden` |
| `redemption.failure.provider_api_not_found` | 7 | `ProviderApiNotFound` | `HTTP <status>: <reason phrase>` | `errors.redemption.failure.provider_api_not_found` |
| `redemption.failure.provider_api_server_error` | 8 | `ProviderApiServerError` | `HTTP <status>: <reason phrase>` | `errors.redemption.failure.provider_api_server_error` |
| `redemption.failure.insufficient_points` | 9 | `InsufficientPoints` | `HTTP <status>: <provider response body>` (the provider client classifies it) | `errors.redemption.failure.insufficient_points` |
| `redemption.failure.provider_rejected` | 10 | `ProviderRejected` | `HTTP <status>: <provider message>` — any other refusal (GauPoints `ok: false`) | `errors.redemption.failure.provider_rejected` |
| `redemption.failure.credential_expired` | 11 | `CredentialExpired` | `<Provider> credential expired at <instant>` — not sent | `errors.redemption.failure.credential_expired` |

---

## Namespace: `system`

| Code | HTTP Status | Params | Use Case | Frontend Key |
|------|------------|--------|----------|--------------|
| `system.internal_error` | 500 | — | Unhandled exception (global exception handler) | `errors.system.internal_error` |
| `system.upstream_unavailable` | 502 | — | **No producer**; 502s use the `provider_api` codes. Declared only. | `errors.system.upstream_unavailable` |

---

## ProblemDetails Shape

Every error response uses `Content-Type: application/problem+json`:

```json
{
  "type": "https://errors.app/stores/not_found",
  "title": "Store not found.",
  "status": 404,
  "code": "stores.not_found",
  "params": {}
}
```

Validation errors carry per-field entries:

```json
{
  "type": "https://errors.app/validation/failed",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "code": "validation.failed",
  "params": {
    "errors": [
      { "field": "password", "code": "validation.password_too_short", "params": { "min": 8 } }
    ]
  }
}
```

`title` is always English and developer-facing; the frontend renders `code` + `params` through its catalog. `Accept-Language` is ignored.
