using System.ComponentModel.DataAnnotations;

namespace PointLurker.Core.Application.Stores;

public sealed class InputResolutionOptions
{
    public const string SectionName = "InputResolution";

    [Range(0, 100)]
    public int MatchThreshold { get; set; } = 85;
}
