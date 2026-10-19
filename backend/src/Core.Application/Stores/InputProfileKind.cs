namespace PointLurker.Core.Application.Stores;

/// <summary>Where a provider's item inputs are defined. Never persisted.</summary>
public enum InputProfileKind
{
    /// <summary>Each item declares its own input labels (StreamElements' <c>userInput</c>).</summary>
    PerItemDeclared,

    /// <summary>Every item asks for the same fixed set of inputs, decided by the provider.</summary>
    FixedSchema,
}
