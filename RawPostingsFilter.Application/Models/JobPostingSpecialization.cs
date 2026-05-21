using System.ComponentModel;

namespace RawPostingsFilter.Application.Models;

[Description("Профессиональная специализация, которую ищут в вакансии или заказе.")]
public sealed record JobPostingSpecialization
{
    [Description("Каноничное название специализации, например Backend Developer, UI Designer, Motion Designer или Project Manager.")]
    public required string Name { get; init; }

    [Description("Альтернативные названия, синонимы, сокращения и близкие формулировки этой же специализации.")]
    public IReadOnlyList<string> AlternativeNames { get; init; } = [];
}
