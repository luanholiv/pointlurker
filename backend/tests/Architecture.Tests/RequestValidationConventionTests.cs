using System.Collections;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// Request-shape validation lives in the presentation layer and speaks only in declared error
/// codes. Both halves matter: a code the catalog cannot resolve renders as the identifier itself,
/// and validation leaking into the domain layers would make the aggregates depend on an HTTP
/// concern.
/// </summary>
public class RequestValidationConventionTests
{
    /// <summary>
    /// FluentValidation defaults a rule's <c>ErrorCode</c> to the failing validator's type name, so
    /// a rule that forgets <c>WithErrorCode</c> would answer with something like
    /// <c>NotEmptyValidator</c>. The mapper degrades an unrecognised code to
    /// <c>validation.invalid_format</c> rather than leaking it; this is what keeps that fallback
    /// unreachable, and what stops a rule inventing a code with no catalog key behind it.
    /// </summary>
    [Fact]
    public void Every_validator_rule_names_a_declared_error_code()
    {
        var validators = DiscoverValidators();
        var failing = new List<string>();

        foreach (var validatorType in validators)
        {
            var validator = (IEnumerable)Activator.CreateInstance(validatorType)!;

            foreach (var rule in validator.Cast<IValidationRule>())
            {
                foreach (var component in rule.Components)
                {
                    // A child-validator component never fails on its own account — the failures
                    // come from the nested validator's own rules, which are checked in their turn.
                    if (component.Validator is IChildValidatorAdaptor)
                    {
                        continue;
                    }

                    if (!ErrorCodeCatalog.Codes.Contains(component.ErrorCode))
                    {
                        failing.Add(
                            $"{validatorType.Name}.{rule.PropertyName}: error code "
                                + $"'{component.ErrorCode}' is not declared in ErrorCodes"
                        );
                    }
                }
            }
        }

        Assert.True(
            failing.Count == 0,
            $"""
            {nameof(Every_validator_rule_names_a_declared_error_code)}: {failing.Count} rule(s) name
            an error code that ErrorCodes does not declare. Add .WithErrorCode(ErrorCodes.X) to the
            rule, or declare the code (constant + docs/ERROR-CODES.md + both locale catalogs).
            Violations:
            {string.Join("\n", failing)}
            """
        );
    }

    /// <summary>
    /// Guards the discovery: an empty set would make the check above pass vacuously.
    /// </summary>
    [Fact]
    public void The_scan_actually_finds_validators()
    {
        Assert.True(
            DiscoverValidators().Count > 5,
            "Expected the presentation assembly to declare a validator per request contract."
        );
    }

    /// <summary>
    /// Request shape is an HTTP concern. The domain layers keep their own invariants in their own
    /// constructors and must not reach for a validation library to do it.
    /// </summary>
    [Fact]
    public void The_domain_layers_do_not_reference_the_validation_library()
    {
        var offenders = new[] { ArchitectureAssemblies.Domain, ArchitectureAssemblies.Application }
            .Where(assembly =>
                assembly
                    .GetReferencedAssemblies()
                    .Any(reference =>
                        reference.Name?.StartsWith("FluentValidation", StringComparison.Ordinal)
                        == true
                    )
            )
            .Select(assembly => assembly.GetName().Name ?? "<unnamed>")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"{nameof(The_domain_layers_do_not_reference_the_validation_library)}: "
                + $"{string.Join(", ", offenders)} reference FluentValidation. Request-shape rules "
                + "belong to Presentation.WebApi; domain invariants stay in the domain."
        );
    }

    private static IReadOnlyList<Type> DiscoverValidators() =>
        ArchitectureAssemblies
            .Presentation.GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false }
                && type.GetConstructor(Type.EmptyTypes) is not null
                && InheritsFromAbstractValidator(type)
            )
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

    private static bool InheritsFromAbstractValidator(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (
                current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(AbstractValidator<>)
            )
            {
                return true;
            }
        }

        return false;
    }
}
