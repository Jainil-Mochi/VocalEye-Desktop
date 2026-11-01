using System.Globalization;
using System.Text;
using System.Text.Json;

namespace VocalEye.Desktop.Core.Approvals;

/// <summary>
/// Turns (toolName, argumentsJson) into something a person can judge. It describes the actual
/// arguments the backend will run, never the model's own explanation of them.
/// </summary>
public static class ApprovalSummaryBuilder
{
    public const int PreviewCharLimit = 4000;

    public static ApprovalSummary Build(string toolName, string? argumentsJson)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        }
        catch (JsonException)
        {
            return Unreadable(toolName);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Unreadable(toolName);

            return toolName switch
            {
                "file_generator" => FileGenerator(root),
                "code_builder" => CodeBuilder(root),
                "desktop_command" => DesktopCommand(root),
                "send_email" => SendEmail(root),
                _ => Generic(toolName, root)
            };
        }
    }

    /// <summary>Indented JSON for the "show raw arguments" view; returns the input unchanged if it is not valid JSON.</summary>
    public static string PrettyJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "{}";
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static ApprovalSummary FileGenerator(JsonElement a)
    {
        var name = Str(a, "filename");
        var type = Str(a, "filetype");
        var content = Str(a, "content") ?? string.Empty;
        var file = name is null ? "a file" : type is null ? $"\"{name}\"" : $"\"{name}.{type}\"";

        return new ApprovalSummary(
            $"Create {file} in your VocalEye output folder.",
            [
                new("File name", name ?? "(not given)"),
                new("Type", type ?? "(not given)"),
                new("Size", $"{content.Length.ToString("N0", CultureInfo.CurrentCulture)} characters")
            ],
            [new("Content", Trim(content), Caption(content), ExpandedByDefault: content.Length <= 600)],
            true);
    }

    private static ApprovalSummary CodeBuilder(JsonElement a)
    {
        var project = Str(a, "projectName") ?? "(unnamed)";
        var previews = new List<ApprovalPreview>();
        long totalBytes = 0;
        var count = 0;

        if (a.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in files.EnumerateArray())
            {
                if (f.ValueKind != JsonValueKind.Object) continue;
                var path = Str(f, "path") ?? "(no path)";
                var text = Str(f, "content") ?? string.Empty;
                var bytes = Encoding.UTF8.GetByteCount(text);
                totalBytes += bytes;
                count++;
                previews.Add(new ApprovalPreview(path, Trim(text), FormatBytes(bytes), ExpandedByDefault: false));
            }
        }

        return new ApprovalSummary(
            $"Create the code project \"{project}\" with {count} file{(count == 1 ? string.Empty : "s")}. The code is saved, not run.",
            [
                new("Project", project),
                new("Files", count.ToString(CultureInfo.CurrentCulture)),
                new("Total size", FormatBytes(totalBytes))
            ],
            previews,
            true);
    }

    private static ApprovalSummary DesktopCommand(JsonElement a)
    {
        var action = Str(a, "action");
        var target = Str(a, "target") ?? "(not given)";

        var headline = action switch
        {
            "open_url" => $"Open this web page in your browser: {target}",
            "open_app" => $"Launch this app: {target}",
            _ => $"Run a desktop action ({action ?? "unknown"}) on: {target}"
        };

        return new ApprovalSummary(
            headline,
            [new("Action", action ?? "(not given)"), new("Target", target)],
            [],
            true);
    }

    private static ApprovalSummary SendEmail(JsonElement a)
    {
        var to = Str(a, "to") ?? "(no recipient)";
        var subject = Str(a, "subject") ?? "(no subject)";
        var body = Str(a, "body") ?? string.Empty;

        return new ApprovalSummary(
            $"Send an email to {to}. Once sent, it can't be taken back.",
            [new("To", to), new("Subject", subject)],
            [new("Message", Trim(body), Caption(body), ExpandedByDefault: true)],
            true);
    }

    private static ApprovalSummary Generic(string toolName, JsonElement a)
    {
        var fields = new List<ApprovalField>();
        foreach (var p in a.EnumerateObject())
        {
            var value = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? string.Empty : p.Value.GetRawText();
            fields.Add(new ApprovalField(p.Name, value.Length > 300 ? value[..300] + "…" : value));
        }
        return new ApprovalSummary($"Run the tool \"{toolName}\" with the arguments below.", fields, [], true);
    }

    private static ApprovalSummary Unreadable(string toolName) =>
        new($"The assistant wants to run \"{toolName}\", but its details could not be read. Check the raw arguments before approving.",
            [], [], false);

    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string Trim(string text) =>
        text.Length <= PreviewCharLimit ? text : text[..PreviewCharLimit] + "\n… (preview shortened)";

    private static string? Caption(string text) =>
        text.Length > PreviewCharLimit ? $"Showing the first {PreviewCharLimit:N0} of {text.Length:N0} characters" : null;

    private static string FormatBytes(long bytes) =>
        bytes < 1024 ? $"{bytes} B" : $"{bytes / 1024.0:0.#} KB";
}
