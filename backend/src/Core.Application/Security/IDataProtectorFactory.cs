namespace PointLurker.Core.Application.Security;

/// <summary>
/// Creates purpose-bound <see cref="ISecretProtector"/> instances. Keeps the
/// <c>Microsoft.AspNetCore.DataProtection</c> dependency out of the application and domain
/// layers (arch-test enforced); the concrete factory lives in the infrastructure layer.
/// </summary>
public interface IDataProtectorFactory
{
    ISecretProtector Create(string purpose);
}
