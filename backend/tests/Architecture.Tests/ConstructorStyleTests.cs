using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PointLurker.Architecture.Tests;

public class ConstructorStyleTests
{
    /// <summary>
    /// Dependency injection uses primary constructors. The tell-tale of the old
    /// style is a readonly instance field whose type is also one of the type's own constructor
    /// parameter types — i.e. a parameter copied verbatim into a backing field.
    ///
    /// A field holding a value *derived* from a parameter is still fine and deliberately passes:
    /// SmtpEmailService takes IOptions&lt;SmtpSettings&gt; and keeps a SmtpSettings field, so the
    /// types differ. This complements IDE0290 in backend/.editorconfig, which only fires when
    /// every constructor statement is a plain parameter-to-field assignment.
    ///
    /// Core.Domain is exempt: its entities keep a private EF constructor plus a public
    /// state-setting constructor, and its value objects validate in theirs.
    /// </summary>
    [Fact]
    public void Constructor_parameters_are_not_copied_into_backing_fields()
    {
        var failing = new List<string>();

        foreach (var asm in ArchitectureAssemblies.SourceAssemblies)
        {
            if (asm == ArchitectureAssemblies.Domain)
            {
                continue;
            }

            foreach (var t in asm.GetTypes())
            {
                if (!t.IsClass || IsCompilerGenerated(t) || IsGeneratedPersistenceType(t))
                {
                    continue;
                }

                var parameterTypes = t.GetConstructors(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    )
                    .SelectMany(c => c.GetParameters())
                    .Select(p => p.ParameterType)
                    .ToHashSet();

                if (parameterTypes.Count == 0)
                {
                    continue;
                }

                var fields = t.GetFields(
                    BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.Instance
                        | BindingFlags.DeclaredOnly
                );

                foreach (var f in fields)
                {
                    // Not readonly: mutable state, not a captured dependency.
                    if (!f.IsInitOnly)
                    {
                        continue;
                    }

                    // Primary-constructor captures and auto-property backing fields.
                    if (
                        f.Name.StartsWith('<')
                        || f.GetCustomAttribute<CompilerGeneratedAttribute>() is not null
                    )
                    {
                        continue;
                    }

                    if (parameterTypes.Contains(f.FieldType))
                    {
                        failing.Add($"{t.FullName}.{f.Name} ({f.FieldType.Name})");
                    }
                }
            }
        }

        Assert.True(
            failing.Count == 0,
            $"{nameof(Constructor_parameters_are_not_copied_into_backing_fields)}: use a primary constructor instead of copying the parameter into a field: {string.Join(", ", failing)}"
        );
    }

    private static bool IsCompilerGenerated(Type t)
    {
        if (t.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
        {
            return true;
        }
        if (t.Name.Contains('<', StringComparison.Ordinal))
        {
            return true;
        }
        return false;
    }

    private static bool IsGeneratedPersistenceType(Type t)
    {
        if (t.Namespace?.Contains(".Migrations", StringComparison.Ordinal) == true)
        {
            return true;
        }
        if (t.Namespace?.StartsWith("Refit.Implementation", StringComparison.Ordinal) == true)
        {
            return true;
        }
        return InheritsFrom(t, typeof(Migration)) || InheritsFrom(t, typeof(ModelSnapshot));
    }

    private static bool InheritsFrom(Type t, Type baseType)
    {
        var cursor = t.BaseType;
        while (cursor is not null)
        {
            if (cursor == baseType)
            {
                return true;
            }
            cursor = cursor.BaseType;
        }
        return false;
    }
}
