using FluentValidation;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Validation;

/// <summary>
/// The collection itself is optional — the handler reads a missing list as "no changes" — so only
/// its elements are validated, each reported by index so the client can point at the failing row.
/// </summary>
internal sealed class UpsertStoreProfileValuesRequestValidator
    : AbstractValidator<UpsertStoreProfileValuesRequest>
{
    public UpsertStoreProfileValuesRequestValidator()
    {
        RuleForEach(request => request.Values)
            .SetValidator(new StoreProfileValueUpsertRequestValidator());
    }
}
