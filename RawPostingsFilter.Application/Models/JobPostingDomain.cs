using System.ComponentModel;

namespace RawPostingsFilter.Application.Models;

[Description("Доменная область бизнеса, продукта или индустрии, к которой относится вакансия.")]
public sealed record JobPostingDomain
{
    [Description("Каноничное название доменной области, например FinTech, E-commerce, Healthcare, EdTech или Game Development.")]
    public required string Name { get; init; }

    [Description("Альтернативные названия, синонимы и близкие формулировки этой же доменной области.")]
    public IReadOnlyList<string> AlternativeNames { get; init; } = [];
}
