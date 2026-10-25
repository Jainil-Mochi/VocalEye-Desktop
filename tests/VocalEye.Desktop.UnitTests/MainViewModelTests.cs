using FluentAssertions;
using Xunit;
using VocalEye.Desktop.App.ViewModels;

namespace VocalEye.Desktop.UnitTests;

public sealed class MainViewModelTests
{
    [Fact]
    public void Constructor_ShouldSetInitialTitleAndStatus()
    {
        MainViewModel viewModel = new();

        viewModel.Title.Should().Be("VocalEye Desktop");
        viewModel.Status.Should().Be("Phase 0 - Foundations");
    }
}
