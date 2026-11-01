using FluentAssertions;
using VocalEye.Desktop.Core.Approvals;

namespace VocalEye.Desktop.UnitTests;

public class ApprovalSummaryBuilderTests
{
    [Fact]
    public void FileGenerator_describes_name_type_and_previews_content()
    {
        var s = ApprovalSummaryBuilder.Build("file_generator",
            """{"filename":"aibasics","filetype":"pdf","content":"# AI basics\nHello"}""");

        s.ArgumentsReadable.Should().BeTrue();
        s.Headline.Should().Contain("aibasics.pdf");
        s.Fields.Should().Contain(f => f.Label == "Type" && f.Value == "pdf");
        s.Previews.Should().ContainSingle().Which.Text.Should().Contain("AI basics");
    }

    [Fact]
    public void CodeBuilder_lists_every_file_with_size_and_collapsed_previews()
    {
        var s = ApprovalSummaryBuilder.Build("code_builder",
            """{"projectName":"todo","files":[{"path":"src/a.cs","content":"class A {}"},{"path":"README.md","content":"hi"}]}""");

        s.Headline.Should().Contain("todo").And.Contain("2 files").And.Contain("not run");
        s.Previews.Select(p => p.Label).Should().Equal("src/a.cs", "README.md");
        s.Previews.Should().OnlyContain(p => !p.ExpandedByDefault && p.Caption != null);
    }

    [Theory]
    [InlineData("open_url", "https://example.com", "web page")]
    [InlineData("open_app", "notepad", "Launch")]
    public void DesktopCommand_shows_exact_target(string action, string target, string expectedWord)
    {
        var s = ApprovalSummaryBuilder.Build("desktop_command", $$"""{"action":"{{action}}","target":"{{target}}"}""");
        s.Headline.Should().Contain(target).And.Contain(expectedWord);
        s.Fields.Should().Contain(f => f.Label == "Target" && f.Value == target);
    }

    [Fact]
    public void SendEmail_shows_recipient_subject_and_full_body_expanded()
    {
        var s = ApprovalSummaryBuilder.Build("send_email",
            """{"to":"a@b.com","subject":"Hello","body":"Line1\nLine2"}""");

        s.Headline.Should().Contain("a@b.com").And.Contain("can't be taken back");
        s.Fields.Should().Contain(f => f.Label == "Subject" && f.Value == "Hello");
        var body = s.Previews.Should().ContainSingle().Which;
        body.Text.Should().Be("Line1\nLine2");
        body.ExpandedByDefault.Should().BeTrue();
    }

    [Fact]
    public void Long_content_is_shortened_with_a_caption_but_never_hidden_silently()
    {
        var content = new string('x', ApprovalSummaryBuilder.PreviewCharLimit + 500);
        var s = ApprovalSummaryBuilder.Build("send_email", $$"""{"to":"a@b.com","subject":"s","body":"{{content}}"}""");
        s.Previews[0].Caption.Should().Contain("first");
        s.Previews[0].Text.Should().Contain("preview shortened");
    }

    [Fact]
    public void Unknown_tool_lists_arguments_generically()
    {
        var s = ApprovalSummaryBuilder.Build("mystery", """{"a":"b","n":3}""");
        s.Headline.Should().Contain("mystery");
        s.Fields.Should().Contain(f => f.Label == "a" && f.Value == "b").And.Contain(f => f.Label == "n" && f.Value == "3");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Unreadable_arguments_warn_the_user_instead_of_throwing(string json)
    {
        var s = ApprovalSummaryBuilder.Build("send_email", json);
        s.ArgumentsReadable.Should().BeFalse();
        s.Headline.Should().Contain("could not be read");
    }

    [Fact]
    public void PrettyJson_indents_valid_json_and_passes_through_invalid_text()
    {
        ApprovalSummaryBuilder.PrettyJson("""{"a":1}""").Should().Contain("\n");
        ApprovalSummaryBuilder.PrettyJson("oops").Should().Be("oops");
    }
}
