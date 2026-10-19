namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

internal sealed record UpsertStoreProfileValuesRequest(
    IReadOnlyList<StoreProfileValueUpsertRequest>? Values
);
