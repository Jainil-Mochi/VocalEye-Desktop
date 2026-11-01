namespace VocalEye.Desktop.Core.Approvals;

public static class ToolDisplay
{
    public static string Name(string toolName) => toolName switch
    {
        "file_generator" => "Create a file",
        "code_builder" => "Create a code project",
        "desktop_command" => "Open on this PC",
        "send_email" => "Send an email",
        "get_current_time" => "Check the time",
        _ => toolName
    };
}
