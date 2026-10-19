using FluentValidation;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Validation;
using PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Users.Validation;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Validation;

namespace PointLurker.Presentation.WebApi.Validation;

public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers a validator per request contract, consumed by <c>ValidatesBody&lt;TRequest&gt;()</c>
    /// on the routes that bind a body and by <c>ValidatesQuery&lt;TQuery&gt;()</c> on the ones whose
    /// query string has a rule behind it.
    /// </summary>
    /// <remarks>
    /// Listed explicitly rather than discovered by assembly scanning: the contracts are
    /// <c>internal</c>, so scanning would need <c>includeInternalTypes</c> and the reflection that
    /// comes with it, and the published image is a self-contained single file. A missing line here
    /// fails <c>RequestValidatorRegistrationTests</c>, which enumerates the contracts itself.
    /// Validators hold no state, so one instance each is enough.
    /// </remarks>
    public static IServiceCollection AddRequestValidation(this IServiceCollection services)
    {
        services.AddSingleton<IValidator<CreateStoreRequest>, CreateStoreRequestValidator>();
        services.AddSingleton<
            IValidator<UpsertStoreProfileAliasRequest>,
            UpsertStoreProfileAliasRequestValidator
        >();
        services.AddSingleton<
            IValidator<UpsertStoreProfileValuesRequest>,
            UpsertStoreProfileValuesRequestValidator
        >();
        services.AddSingleton<
            IValidator<StoreProfileValueUpsertRequest>,
            StoreProfileValueUpsertRequestValidator
        >();
        services.AddSingleton<IValidator<SetLocaleRequest>, SetLocaleRequestValidator>();
        services.AddSingleton<IValidator<SetCredentialRequest>, SetCredentialRequestValidator>();
        services.AddSingleton<
            IValidator<CreateWishlistEntryRequest>,
            CreateWishlistEntryRequestValidator
        >();
        services.AddSingleton<
            IValidator<UpdateWishlistEntryRequest>,
            UpdateWishlistEntryRequestValidator
        >();
        services.AddSingleton<IValidator<StoreLookupQuery>, StoreLookupQueryValidator>();

        return services;
    }
}
