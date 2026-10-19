namespace PointLurker.Core.Domain.Stores.Enums;

/// <summary>
/// Persisted as <see langword="int"/> in <c>Redemption.FailureCode</c>.
/// <para>
/// <b>APPEND-ONLY:</b> New values get the next unused int.
/// NEVER reorder, renumber, or remove existing values —
/// historical redemption rows store the integer.
/// A unit test (<c>SubmissionFailureCodeTests.SubmissionFailureCode_int_to_name_mapping_is_unchanged</c>)
/// snapshots the int→name map and fails the build on any violation.
/// </para>
/// </summary>
public enum SubmissionFailureCode
{
    /// <summary>Catch-all for unknown/unmapped failures. <c>FailureContext</c> carries the raw detail.</summary>
    Unknown = 0,

    /// <summary>The provider credential is missing, or could not be decrypted from storage.</summary>
    TokenDecryptionFailed = 1,

    /// <summary>A required user input value was not found when the redemption was submitted. <c>FailureContext</c> = label.</summary>
    InputCoverageLost = 2,

    /// <summary>A user input value could not be decrypted.</summary>
    InputDecryptionFailed = 3,

    /// <summary>The provider responded with 400 Bad Request.</summary>
    ProviderApiBadRequest = 4,

    /// <summary>The provider responded with 401 Unauthorized (credential expired or invalid).</summary>
    ProviderApiUnauthorized = 5,

    /// <summary>The provider responded with 403 Forbidden.</summary>
    ProviderApiForbidden = 6,

    /// <summary>The provider responded with 404 Not Found (item or store removed).</summary>
    ProviderApiNotFound = 7,

    /// <summary>The provider responded with any 5xx server error.</summary>
    ProviderApiServerError = 8,

    /// <summary>The provider refused the redemption because the owner has too few points. Each client decides what that looks like on its wire.</summary>
    InsufficientPoints = 9,

    /// <summary>
    /// The provider answered the redemption with a refusal of its own that no more specific code
    /// covers — for GauPoints, any <c>ok: false</c> that is not an authentication failure.
    /// <c>FailureContext</c> carries the status and the provider's message.
    /// </summary>
    ProviderRejected = 10,

    /// <summary>
    /// The credential had already expired when the redemption was due, so it was never sent.
    /// Known from the credential itself, without a call.
    /// </summary>
    CredentialExpired = 11,
}
