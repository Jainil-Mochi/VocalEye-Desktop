namespace VocalEye.Desktop.Core.Approvals;

public static class ToolRiskExtensions
{
    public static ToolRisk Parse(string? value) =>
        Enum.TryParse<ToolRisk>(value?.Trim(), ignoreCase: true, out var risk) ? risk : ToolRisk.Unknown;

    /// <summary>Fallback used until the tool catalog has loaded. Unknown tools are treated as Write (never "safe").</summary>
    public static ToolRisk DefaultFor(string toolName) => toolName switch
    {
        "get_current_time" => ToolRisk.ReadOnly,
        "send_email" => ToolRisk.Destructive,
        _ => ToolRisk.Write
    };

    public static string Label(this ToolRisk risk) => risk switch
    {
        ToolRisk.ReadOnly => "Read only",
        ToolRisk.Write => "Makes changes",
        ToolRisk.Destructive => "Sends / irreversible",
        _ => "Unknown risk"
    };
}
