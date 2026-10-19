using Microsoft.AspNetCore.DataProtection;
using PointLurker.Core.Application.Security;

namespace PointLurker.Infrastructure.Database.Security;

/// <summary>
/// Creates <see cref="ISecretProtector"/> instances over the framework
/// <see cref="IDataProtectionProvider"/>. The string API is UTF8+base64 over the byte-oriented
/// Data Protection core; every protected column is <c>text</c>, so a string boundary is the
/// pragmatic surface.
/// </summary>
public sealed class DataProtectorFactory(IDataProtectionProvider provider) : IDataProtectorFactory
{
    public ISecretProtector Create(string purpose) =>
        new Protector(provider.CreateProtector(purpose));

    private sealed class Protector(IDataProtector protector) : ISecretProtector
    {
        public string Protect(string plaintext) => protector.Protect(plaintext);

        public string Unprotect(string ciphertext) => protector.Unprotect(ciphertext);
    }
}
