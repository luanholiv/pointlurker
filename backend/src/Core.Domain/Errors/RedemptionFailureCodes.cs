using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Domain.Errors;

/// <summary>
/// Turns a stored <see cref="SubmissionFailureCode"/> into the error code the frontend resolves
/// through its catalog. The enum is a storage detail — an append-only integer in
/// <c>Redemption.FailureCode</c> — and the code is the contract, so the translation belongs here
/// rather than in the reader, which would otherwise keep its own hand-copied table of the integers
/// with nothing holding it to this enum. The <see cref="ErrorCodes.RedemptionFailure"/> constants are
/// the single written record of the mapping.
/// </summary>
public static class RedemptionFailureCodes
{
    /// <summary>
    /// The code for <paramref name="code"/>, or <see cref="ErrorCodes.RedemptionFailure.Unknown"/>
    /// for a value this build does not declare.
    /// <para>
    /// The fallback is not defensive padding. The column is an integer that EF casts back without
    /// validation, so a row written by a newer build — or by hand — can hold a member that does not
    /// exist here, and throwing would fail the whole redemption page over one unreadable row.
    /// <c>Unknown</c> is documented as exactly this catch-all. What it must not do is hide a member
    /// added without a case here, so <c>RedemptionFailureCodesTests</c> asserts every declared
    /// member maps to its own constant and never reaches the fallback.
    /// </para>
    /// </summary>
    public static string For(SubmissionFailureCode code) =>
        code switch
        {
            SubmissionFailureCode.Unknown => ErrorCodes.RedemptionFailure.Unknown,
            SubmissionFailureCode.TokenDecryptionFailed => ErrorCodes
                .RedemptionFailure
                .TokenDecryptionFailed,
            SubmissionFailureCode.InputCoverageLost => ErrorCodes
                .RedemptionFailure
                .InputCoverageLost,
            SubmissionFailureCode.InputDecryptionFailed => ErrorCodes
                .RedemptionFailure
                .InputDecryptionFailed,
            SubmissionFailureCode.ProviderApiBadRequest => ErrorCodes
                .RedemptionFailure
                .ProviderApiBadRequest,
            SubmissionFailureCode.ProviderApiUnauthorized => ErrorCodes
                .RedemptionFailure
                .ProviderApiUnauthorized,
            SubmissionFailureCode.ProviderApiForbidden => ErrorCodes
                .RedemptionFailure
                .ProviderApiForbidden,
            SubmissionFailureCode.ProviderApiNotFound => ErrorCodes
                .RedemptionFailure
                .ProviderApiNotFound,
            SubmissionFailureCode.ProviderApiServerError => ErrorCodes
                .RedemptionFailure
                .ProviderApiServerError,
            SubmissionFailureCode.InsufficientPoints => ErrorCodes
                .RedemptionFailure
                .InsufficientPoints,
            SubmissionFailureCode.ProviderRejected => ErrorCodes.RedemptionFailure.ProviderRejected,
            SubmissionFailureCode.CredentialExpired => ErrorCodes
                .RedemptionFailure
                .CredentialExpired,
            _ => ErrorCodes.RedemptionFailure.Unknown,
        };
}
