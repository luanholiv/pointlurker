namespace PointLurker.Core.Application.Security;

/// <summary>
/// Purpose-bound, string-in/string-out data protector. Each instance is bound to a single
/// protection purpose (see <see cref="ProtectionPurposes"/>); ciphertext produced under one
/// purpose cannot be unprotected under another. Resolve a purpose-bound instance via keyed DI.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
