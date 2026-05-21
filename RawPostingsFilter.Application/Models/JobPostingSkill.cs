using System.ComponentModel;

namespace RawPostingsFilter.Application.Models;

[Description("Профессиональный навык, указанный в вакансии или заказе.")]
public sealed record JobPostingSkill
{
    [Description("Краткое каноничное название навыка.")]
    public required string DisplayName { get; init; }

    [Description("Короткое, но информативное описание навыка в контексте этой вакансии, пригодное для embeddings и матчинга.")]
    public required string Description { get; init; }

    [Description("Альтернативные названия, синонимы, сокращения и распространенные варианты формулировки этого же навыка.")]
    public IReadOnlyList<string> AlternativeNames { get; init; } = [];
}
