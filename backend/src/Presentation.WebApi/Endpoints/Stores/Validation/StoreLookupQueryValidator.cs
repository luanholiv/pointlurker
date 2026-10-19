using FluentValidation;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Validation;

/// <summary>
/// Request shape only. The query rule names <c>store_lookup.query_required</c> rather than the
/// generic required code, so the sentence says what to type. It applies to StreamElements alone:
/// GauPoints is one fixed store, so there is nothing to search for.
/// </summary>
internal sealed class StoreLookupQueryValidator : AbstractValidator<StoreLookupQuery>
{
    public StoreLookupQueryValidator()
    {
        RuleFor(query => query.Provider)
            .Must(provider => StoreProviderSlugs.TryParse(provider, out _))
            .WithErrorCode(ErrorCodes.Stores.ProviderUnsupported);

        // NotEmpty covers absent, empty and whitespace alike.
        RuleFor(query => query.Query)
            .NotEmpty()
            .When(query => IsStreamElements(query.Provider))
            .WithErrorCode(ErrorCodes.StoreLookup.QueryRequired);
    }

    private static bool IsStreamElements(string? provider) =>
        StoreProviderSlugs.TryParse(provider, out var parsed)
        && parsed == StoreProvider.StreamElements;
}
