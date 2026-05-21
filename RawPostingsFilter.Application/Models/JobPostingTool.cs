using System.ComponentModel;

namespace RawPostingsFilter.Application.Models;

[Description("Инструмент, технология, платформа, библиотека, сервис, рабочая программа или среда, указанные в вакансии.")]
public sealed record JobPostingTool
{
    [Description("Каноничное или стандартное название инструмента.")]
    public required string ToolStandardName { get; init; }

    [Description("Альтернативные названия, синонимы, сокращения, русские варианты и распространенные варианты написания инструмента.")]
    public IReadOnlyList<string> ToolAltNames { get; init; } = [];
}
