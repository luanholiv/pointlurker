using System.Reflection;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// Absolute paths the source-scanning architecture tests read. They are baked into this test
/// assembly at build time by <c>Architecture.Tests.csproj</c> (<c>AssemblyMetadata</c> items)
/// rather than rediscovered at run time by walking up from <c>AppContext.BaseDirectory</c>.
/// <para>
/// A walk fails silently: a scanner handed no directory finds no violations, which reads as a
/// pass, so a moved folder or a different working directory switches a guard off with no signal.
/// Every member here throws instead — naming the metadata item to fix — so drift fails the build.
/// </para>
/// </summary>
internal static class SourcePaths
{
    private const string RepositoryRootKey = "RepositoryRoot";
    private const string BackendRootKey = "BackendRoot";
    private const string BackendSourceRootKey = "BackendSourceRoot";

    /// <summary>Absolute path to the repository root.</summary>
    public static string RepositoryRoot => ResolveDirectory(RepositoryRootKey);

    /// <summary>Absolute path to <c>backend/</c>.</summary>
    public static string BackendRoot => ResolveDirectory(BackendRootKey);

    /// <summary>Absolute path to <c>backend/src/</c>.</summary>
    public static string BackendSourceRoot => ResolveDirectory(BackendSourceRootKey);

    /// <summary>Absolute path to <c>backend/src/&lt;projectName&gt;/</c>.</summary>
    public static string ProjectSourceRoot(string projectName) =>
        RequireDirectory(
            Path.Combine(BackendSourceRoot, projectName),
            $"{BackendSourceRootKey} + '{projectName}'"
        );

    /// <summary>Absolute path to a file below the repository root, e.g. <c>docs/FOO.md</c>.</summary>
    public static string RepositoryFile(string relativePath) =>
        RequireFile(Combine(RepositoryRoot, relativePath), relativePath);

    /// <summary>Absolute path to a file below <c>backend/</c>.</summary>
    public static string BackendFile(string relativePath) =>
        RequireFile(Combine(BackendRoot, relativePath), relativePath);

    private static string Combine(string root, string relativePath) =>
        Path.Combine([root, .. relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)]);

    private static string ResolveDirectory(string key)
    {
        var value = typeof(SourcePaths)
            .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(a => a.Key == key)
            ?.Value;

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Assembly metadata '{key}' is missing. Architecture.Tests.csproj must declare "
                    + $"<AssemblyMetadata Include=\"{key}\" Value=\"...\" /> — without it the "
                    + "source-scanning guards have nothing to scan and would pass vacuously."
            );
        }

        return RequireDirectory(value, key);
    }

    private static string RequireDirectory(string path, string describedBy) =>
        System.IO.Directory.Exists(path)
            ? path
            : throw new InvalidOperationException(
                $"Directory '{path}' ({describedBy}) does not exist. The repository layout moved: "
                    + "update Architecture.Tests.csproj so the guards keep scanning real source."
            );

    private static string RequireFile(string path, string describedBy) =>
        File.Exists(path)
            ? path
            : throw new InvalidOperationException(
                $"File '{path}' ({describedBy}) does not exist. The repository layout moved: "
                    + "update Architecture.Tests.csproj so the guards keep reading a real file."
            );
}
