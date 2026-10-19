/**
 * The providers a store can come from, as the API names them: the slug in a URL, in a request
 * body and in an error's params. The badge label is the catalog entry `providers.<slug>`.
 */
export const STORE_PROVIDERS = ["streamelements", "gaupoints"] as const;

export type StoreProvider = (typeof STORE_PROVIDERS)[number];

/**
 * A provider with one fixed store: nothing to search for, and it can be added once. Held here
 * rather than asked of the server because the add dialog decides its layout before any request.
 */
export const SINGLE_STORE_PROVIDERS: readonly StoreProvider[] = ["gaupoints"];

export const isSingleStoreProvider = (provider: StoreProvider) =>
  SINGLE_STORE_PROVIDERS.includes(provider);

/** The catalog key of a provider's display name. */
export const providerLabelKey = (provider: StoreProvider) =>
  `providers.${provider}` as const;
