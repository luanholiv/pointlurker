namespace PointLurker.Core.Domain.Errors;

/// <summary>
/// Canonical source-of-truth for all backend error codes.
/// Every code emitted in an HTTP response MUST reference a constant from this file.
/// Codes follow the pattern <c>&lt;bounded_context&gt;.&lt;snake_case&gt;</c> with at least two segments.
/// The regex is: <c>^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$</c>
///
/// <para>
/// <b>How to add a new code:</b>
/// <list type="number">
///   <item>Declare a constant here in the appropriate nested class.</item>
///   <item>Add the matching <c>errors.&lt;code&gt;</c> key to both <c>en-US.json</c> and <c>pt-BR.json</c>.</item>
///   <item>If the code maps to a <see cref="Core.Domain.Stores.Enums.SubmissionFailureCode"/> value, append the enum (do NOT reorder existing values).</item>
///   <item>Document the code in <c>docs/ERROR-CODES.md</c> as an inline code span — enforced in both directions by <c>Architecture.Tests/ErrorCodeDocumentationTests</c>.</item>
///   <item>If it carries params, fill the reference's <b>Params</b> column and repeat those names in a <c>Params:</c> summary comment on the constant — <c>Architecture.Tests/ErrorCodePlaceholderTests</c> requires the comment wherever the column names anything, and requires the two to agree.</item>
/// </list>
/// </para>
/// </summary>
public static class ErrorCodes
{
    /// <summary>
    /// Per-field codes. Three producers emit them: <c>IdentityErrorMapper</c> for the framework
    /// Identity endpoints, the request-validation endpoint filter for every custom endpoint that
    /// binds a body or a query contract, and <c>SearchQuery</c> for the list endpoints' filters. All
    /// three wrap them in <c>validation.failed</c> with a <c>params.errors</c> array, built by
    /// <c>FieldErrorProblem</c>.
    /// </summary>
    public static class Validation
    {
        // Generic validation codes

        /// <summary>
        /// The wrapper. Params: <c>errors</c> — one entry per broken rule, each an object with the
        /// field it belongs to, the code naming why, and that code's own params.
        /// </summary>
        public const string Failed = "validation.failed";

        /// <summary>Params: <c>field</c>.</summary>
        public const string Required = "validation.required";

        /// <summary>Params: <c>field</c>, <c>max</c> (int) — the cap the value exceeded.</summary>
        public const string TooLong = "validation.too_long";

        /// <summary>Params: <c>field</c>, <c>min</c> (int) — the cap the value fell short of.</summary>
        public const string TooShort = "validation.too_short";

        /// <summary>
        /// A number outside the range its rule allows. Params: <c>field</c>, <c>min</c> (int),
        /// <c>max</c> (int) — the inclusive bounds the value fell outside.
        /// </summary>
        public const string OutOfRange = "validation.out_of_range";

        /// <summary>
        /// The fallback code: a FluentValidation rule that named none, and any Identity error the
        /// mapping table does not recognise. Params: <c>field</c>, <c>format</c> — the latter only
        /// on the Identity path, where it carries that error's own description.
        /// </summary>
        public const string InvalidFormat = "validation.invalid_format";

        // ASP.NET Identity password rules → 1:1 mapping

        /// <summary>
        /// Params: <c>min</c> (int) — recovered from Identity's English description, the only place
        /// the configured length appears.
        /// </summary>
        public const string PasswordTooShort = "validation.password_too_short";
        public const string PasswordRequiresDigit = "validation.password_requires_digit";
        public const string PasswordRequiresUpper = "validation.password_requires_upper";
        public const string PasswordRequiresLower = "validation.password_requires_lower";
        public const string PasswordRequiresNonAlphanumeric =
            "validation.password_requires_non_alphanumeric";

        /// <summary>
        /// Params: <c>min</c> (int) — the distinct-character count required, recovered the same way
        /// as <see cref="PasswordTooShort"/>'s.
        /// </summary>
        public const string PasswordRequiresUniqueChars =
            "validation.password_requires_unique_chars";
        public const string PasswordMismatch = "validation.password_mismatch";

        /// <summary>Params: <c>userName</c> — the name Identity rejected.</summary>
        public const string DuplicateUserName = "validation.duplicate_user_name";

        /// <summary>Params: <c>userName</c>.</summary>
        public const string InvalidUserName = "validation.invalid_user_name";

        /// <summary>Params: <c>email</c> — the address Identity rejected.</summary>
        public const string DuplicateEmail = "validation.duplicate_email";

        /// <summary>Params: <c>email</c>.</summary>
        public const string InvalidEmail = "validation.invalid_email";
    }

    /// <summary>
    /// Request-level failures raised by the framework before an endpoint handler runs, i.e. the
    /// request itself could not be read. Distinct from <see cref="Validation"/>, whose
    /// <c>validation.failed</c> wrapper always carries a per-field <c>params.errors</c> array.
    /// </summary>
    public static class Request
    {
        public const string MalformedBody = "request.malformed_body";
        public const string BodyTooLarge = "request.body_too_large";
    }

    /// <summary>
    /// Query-string failures on the searchable list endpoints. Unlike <see cref="Request"/> these
    /// are raised by the handler, not the framework: the parameters bind as strings and the
    /// endpoint rejects the ones it cannot interpret rather than substituting a default. A silently
    /// defaulted sort returns correctly-shaped, wrongly-ordered data, which is indistinguishable
    /// from success.
    /// </summary>
    /// <remarks>
    /// These are per-field codes like <see cref="Validation"/>'s: they travel as
    /// <c>params.errors</c> entries inside <c>validation.failed</c>, never as a response's own
    /// <c>code</c>, so one client branch resolves a bad filter and a bad request body alike.
    /// </remarks>
    public static class Search
    {
        /// <summary>
        /// Params: <c>field</c> and <c>parameter</c> (both the query-string name), <c>value</c>
        /// (the rejected value), <c>allowed</c> (string[]).
        /// </summary>
        public const string InvalidEnumValue = "search.invalid_enum_value";

        /// <summary>
        /// Params: <c>field</c> and <c>parameter</c> (both the query-string name), <c>value</c>
        /// (the rejected value).
        /// </summary>
        public const string InvalidFilterValue = "search.invalid_filter_value";
    }

    public static class Users
    {
        public const string RegisterAlreadyExists = "users.register_already_exists";
        public const string NotFound = "users.not_found";

        /// <summary>
        /// Params: <c>locale</c> (the rejected value), <c>allowed</c> (string[]).
        /// </summary>
        public const string LocaleUnsupported = "users.locale_unsupported";
    }

    public static class Auth
    {
        public const string Unauthorized = "auth.unauthorized";
        public const string TwoFactorRequired = "auth.two_factor_required";
        public const string TwoFactorInvalid = "auth.two_factor_invalid";

        /// <summary>
        /// A store is being added for a provider the owner holds no credential for. Scoped to that
        /// provider: a GauPoints store never asks for the StreamElements token, nor the reverse.
        /// Params: <c>provider</c>.
        /// </summary>
        public const string ProviderCredentialNotConfigured =
            "auth.provider_credential_not_configured";
    }

    public static class Credentials
    {
        /// <summary>
        /// A per-field code on the credential body, named rather than the generic
        /// <see cref="Validation.Required"/> so the sentence says a credential is what is missing.
        /// Params: <c>field</c>.
        /// </summary>
        public const string SecretRequired = "credentials.secret_required";

        /// <summary>
        /// The provider refused the credential when it was checked on save. Params:
        /// <c>provider</c>.
        /// </summary>
        public const string Rejected = "credentials.rejected";

        /// <summary>
        /// The route names no provider a credential can be stored for. Params: <c>provider</c>
        /// (the rejected value), <c>allowed</c> (string[]).
        /// </summary>
        public const string ProviderUnsupported = "credentials.provider_unsupported";

        /// <summary>
        /// The credential carries its own expiry, and it has already passed, so it is refused
        /// without asking the provider. Params: <c>provider</c>.
        /// </summary>
        public const string Expired = "credentials.expired";
    }

    public static class Stores
    {
        public const string NotFound = "stores.not_found";

        /// <summary>Params: <c>channelId</c>.</summary>
        public const string AlreadyExists = "stores.already_exists";

        /// <summary>
        /// A per-field code on the store lookup query and the create-store body: the value names no
        /// provider a store can be added for. Params: <c>field</c>.
        /// </summary>
        public const string ProviderUnsupported = "stores.provider_unsupported";

        /// <summary>
        /// A per-field code on the create-store body: the provider has one fixed store, whose
        /// identity the server resolves itself, so a client-sent one is refused rather than
        /// trusted. Params: <c>field</c>.
        /// </summary>
        public const string IdentityNotAccepted = "stores.identity_not_accepted";
    }

    public static class Wishlist
    {
        public const string EntryNotFound = "wishlist.entry_not_found";
        public const string ItemNotFound = "wishlist.item_not_found";
        public const string AlreadyExists = "wishlist.already_exists";

        /// <summary>Params: <c>missingLabels</c> (string[]).</summary>
        public const string MissingInputCoverage = "wishlist.missing_input_coverage";

        /// <summary>
        /// A non-empty redemption message for an item that accepts none. An empty one is treated
        /// as no message, so a form posting the whole body is never refused for it.
        /// </summary>
        public const string MessagesNotSupported = "wishlist.messages_not_supported";
    }

    public static class StoreLookup
    {
        /// <summary>
        /// A per-field code, named by the store lookup query validator rather than the generic
        /// <see cref="Validation.Required"/> so the sentence says what to type. Only a provider with
        /// many stores needs a query. Params: <c>field</c>.
        /// </summary>
        public const string QueryRequired = "store_lookup.query_required";

        /// <summary>Params: <c>provider</c>, <c>query</c>.</summary>
        public const string NotFound = "store_lookup.not_found";
    }

    public static class ProviderApi
    {
        /// <summary>The provider could not be reached. Params: <c>provider</c>.</summary>
        public const string Unreachable = "provider_api.unreachable";

        /// <summary>The provider answered with an error while a store was being added. Params: <c>provider</c>.</summary>
        public const string FetchFailed = "provider_api.fetch_failed";
    }

    /// <summary>
    /// Codes for DB-persisted redemption failure reasons.
    /// These are NOT HTTP response codes — they are values stored in
    /// <see cref="Core.Domain.Stores.Enums.SubmissionFailureCode"/>
    /// and resolved by the frontend catalog under <c>errors.redemption.failure.*</c>.
    /// </summary>
    public static class RedemptionFailure
    {
        public const string Unknown = "redemption.failure.unknown";
        public const string TokenDecryptionFailed = "redemption.failure.token_decryption_failed";

        /// <summary>FailureContext: the label of the input whose value went missing.</summary>
        public const string InputCoverageLost = "redemption.failure.input_coverage_lost";
        public const string InputDecryptionFailed = "redemption.failure.input_decryption_failed";
        public const string ProviderApiBadRequest = "redemption.failure.provider_api_bad_request";
        public const string ProviderApiUnauthorized =
            "redemption.failure.provider_api_unauthorized";
        public const string ProviderApiForbidden = "redemption.failure.provider_api_forbidden";
        public const string ProviderApiNotFound = "redemption.failure.provider_api_not_found";
        public const string ProviderApiServerError = "redemption.failure.provider_api_server_error";
        public const string InsufficientPoints = "redemption.failure.insufficient_points";

        /// <summary>FailureContext: <c>HTTP &lt;status&gt;: &lt;provider message&gt;</c>.</summary>
        public const string ProviderRejected = "redemption.failure.provider_rejected";

        /// <summary>FailureContext: the provider and the instant its credential expired.</summary>
        public const string CredentialExpired = "redemption.failure.credential_expired";
    }

    public static class System
    {
        public const string InternalError = "system.internal_error";
        public const string UpstreamUnavailable = "system.upstream_unavailable";
    }
}
