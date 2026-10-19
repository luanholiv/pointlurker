using System.ComponentModel.DataAnnotations;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

public sealed class StreamElementsSettings
{
    public const string SectionName = "StreamElements";

    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;
}
