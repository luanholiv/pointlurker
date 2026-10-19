using FluentValidation;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Validation;

/// <summary>
/// Request shape only. A StreamElements store names its channel, and both fields eventually reach a
/// value-object constructor that throws on blank input; that guard stays as an invariant backstop,
/// and this is what answers the client with a per-field 400 before it can fire.
/// <para>
/// A GauPoints store is one fixed store whose identity the handler resolves itself, so a
/// client-sent identity is refused rather than ignored: accepting it would let a caller label a
/// GauPoints store with an arbitrary external id, and <c>AddWatchedStore</c> trusts its arguments.
/// </para>
/// </summary>
internal sealed class CreateStoreRequestValidator : AbstractValidator<CreateStoreRequest>
{
    public CreateStoreRequestValidator()
    {
        RuleFor(request => request.Provider)
            .Must(provider => provider is null || StoreProviderSlugs.TryParse(provider, out _))
            .WithErrorCode(ErrorCodes.Stores.ProviderUnsupported);

        When(
            request => ProviderOf(request) == StoreProvider.StreamElements,
            () =>
            {
                RuleFor(request => request.ChannelId)
                    .NotEmpty()
                    .WithErrorCode(ErrorCodes.Validation.Required);

                RuleFor(request => request.DisplayName)
                    .NotEmpty()
                    .WithErrorCode(ErrorCodes.Validation.Required);
            }
        );

        When(
            request => ProviderOf(request) == StoreProvider.GauPoints,
            () =>
            {
                RuleFor(request => request.ChannelId)
                    .Null()
                    .WithErrorCode(ErrorCodes.Stores.IdentityNotAccepted);

                RuleFor(request => request.DisplayName)
                    .Null()
                    .WithErrorCode(ErrorCodes.Stores.IdentityNotAccepted);
            }
        );
    }

    /// <summary>Null when the slug does not parse, which the provider rule has already reported.</summary>
    private static StoreProvider? ProviderOf(CreateStoreRequest request)
    {
        if (request.Provider is null)
            return StoreProvider.StreamElements;

        return StoreProviderSlugs.TryParse(request.Provider, out var provider) ? provider : null;
    }
}
