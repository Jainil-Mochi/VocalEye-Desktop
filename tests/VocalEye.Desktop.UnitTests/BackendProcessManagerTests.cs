using FluentAssertions;
using VocalEye.Desktop.Hosting.Services;
using Xunit;

namespace VocalEye.Desktop.UnitTests;

public class BackendProcessManagerTests
{
    [Fact]
    public async Task StartBackendAsync_ShouldGenerateAuthTokenAndSetPort()
    {
        // Arrange
        var manager = new BackendProcessManager();

        // Act
        var result = await manager.StartBackendAsync();

        // Assert
        result.Should().BeTrue();
        manager.AuthToken.Should().NotBeNullOrEmpty();
        manager.Port.Should().Be(5055);

        await manager.StopBackendAsync();
    }
}
