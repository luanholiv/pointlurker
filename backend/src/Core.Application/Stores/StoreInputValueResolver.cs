using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// Resolves an item's user-input definitions against its store's input profile and decrypts the
/// values behind them. The rules live here once; auto-redeem uses the outcome as a gate and
/// submission turns it into a failure code.
/// </summary>
public sealed class StoreInputValueResolver(
    IInputLabelResolver labelResolver,
    [FromKeyedServices(ProtectionPurposes.StoreInputProfile)] ISecretProtector protector
) : IStoreInputValueResolver
{
    public StoreInputResolution Resolve(StoreItem item, WatchedStore store)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(store);

        var definitions = item.UserInputDefinitions;
        var values = new ProviderInputValue[definitions.Count];
        var missing = new List<string>();

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];

            if (labelResolver.Resolve(definition, store) is not ResolutionResult.Resolved resolved)
            {
                missing.Add(definition);
                continue;
            }

            var ciphertext = store.GetCiphertext(resolved.Canonical);
            if (ciphertext is null)
            {
                missing.Add(definition);
                continue;
            }

            try
            {
                values[i] = new ProviderInputValue(definition, protector.Unprotect(ciphertext));
            }
            catch (Exception ex)
            {
                // A value the store holds but cannot be read back is a fault, not a coverage gap:
                // it outranks anything already collected in `missing`.
                return new StoreInputResolution.DecryptionFailed(definition, ex);
            }
        }

        return missing.Count > 0
            ? new StoreInputResolution.Missing(missing)
            : new StoreInputResolution.Resolved(values);
    }
}
