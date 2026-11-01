using FluentAssertions;
using VocalEye.Desktop.Core.Artifacts;
using VocalEye.Desktop.Core.DTOs;

namespace VocalEye.Desktop.UnitTests;

public class ArtifactTests
{
    [Theory]
    [InlineData(@"C:\Users\me\Documents\VocalEye\report.pdf", true)]
    [InlineData(@"C:\Users\me\Documents\VocalEye\notes.md", true)]
    [InlineData(@"C:\Users\me\Documents\VocalEye\setup.exe", false)]
    [InlineData(@"C:\Users\me\Documents\VocalEye\run.BAT", false)]
    [InlineData(@"C:\Users\me\Documents\VocalEye\x.ps1", false)]
    [InlineData(@"\\server\share\a.pdf", false)]
    [InlineData(@"C:\Users\me\..\other\a.pdf", false)]
    [InlineData(@"relative\a.pdf", false)]
    [InlineData("", false)]
    public void File_policy(string path, bool expected) =>
        ArtifactPolicy.IsSafeFilePath(path, out _).Should().Be(expected);

    [Theory]
    [InlineData(@"C:\Users\me\Documents\VocalEye\projects\todo", true)]
    [InlineData(@"\\server\share", false)]
    [InlineData(@"projects\todo", false)]
    public void Folder_policy(string path, bool expected) =>
        ArtifactPolicy.IsSafeFolderPath(path, out _).Should().Be(expected);

    [Theory]
    [InlineData("https://example.com/a", true)]
    [InlineData("http://example.com", true)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("mailto:a@b.com", false)]
    public void Url_policy(string url, bool expected) =>
        ArtifactPolicy.IsSafeUrl(url, out _, out _).Should().Be(expected);

    [Fact]
    public void Structured_artifacts_win_over_text_parsing()
    {
        var call = new ToolCallDto
        {
            Name = "file_generator", Success = true, Output = @"Saved to C:\other\x.txt",
            Artifacts = [new ToolArtifactDto { Kind = "file", Value = @"C:\real\a.pdf" }]
        };
        ArtifactExtractor.Resolve(call).Should().ContainSingle().Which.Value.Should().Be(@"C:\real\a.pdf");
    }

    [Fact]
    public void File_path_is_found_in_plain_text_output()
    {
        var call = new ToolCallDto { Name = "file_generator", Success = true, Output = @"File saved: C:\Users\Jainil\Documents\VocalEye\aibasics.pdf." };
        var a = ArtifactExtractor.Resolve(call).Should().ContainSingle().Which;
        a.Kind.Should().Be("file");
        a.Value.Should().Be(@"C:\Users\Jainil\Documents\VocalEye\aibasics.pdf");
    }

    [Fact]
    public void Code_builder_output_resolves_to_a_folder()
    {
        var call = new ToolCallDto { Name = "code_builder", Success = true, Output = @"Project created at C:\Users\me\Documents\VocalEye\projects\todo" };
        ArtifactExtractor.Resolve(call).Should().ContainSingle().Which.Kind.Should().Be("folder");
    }

    [Fact]
    public void Failed_or_unrelated_tools_produce_no_artifacts()
    {
        ArtifactExtractor.Resolve(new ToolCallDto { Name = "file_generator", Success = false, Output = @"C:\x\a.pdf failed" }).Should().BeEmpty();
        ArtifactExtractor.Resolve(new ToolCallDto { Name = "send_email", Success = true, Output = @"C:\x\a.pdf" }).Should().BeEmpty();
    }

    [Fact]
    public void Artifact_value_field_is_read_from_json_and_legacy_path_still_works()
    {
        var opts = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var modern = System.Text.Json.JsonSerializer.Deserialize<ToolArtifactDto>("""{"kind":"url","value":"https://example.com"}""", opts)!;
        var legacy = System.Text.Json.JsonSerializer.Deserialize<ToolArtifactDto>("""{"kind":"file","path":"C:\\x\\a.pdf"}""", opts)!;

        modern.ResolvedValue.Should().Be("https://example.com");
        legacy.ResolvedValue.Should().Be(@"C:\x\a.pdf");
    }

    [Fact]
    public void Empty_artifacts_are_ignored_and_output_text_is_used()
    {
        var call = new ToolCallDto
        {
            Name = "file_generator", Success = true, Output = @"Saved C:\Docs\a.pdf",
            Artifacts = [new ToolArtifactDto { Kind = "file", Value = "" }]
        };
        ArtifactExtractor.Resolve(call).Should().ContainSingle().Which.Value.Should().Be(@"C:\Docs\a.pdf");
    }

    [Fact]
    public void Tool_name_alias_is_resolved_for_history_results()
    {
        new ToolCallDto { ToolName = "code_builder" }.ResolvedName.Should().Be("code_builder");
    }
}
