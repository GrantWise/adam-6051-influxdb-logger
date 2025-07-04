// Industrial.Adam.Logger.IntegrationTests - Basic Integration Test
// Simple integration test to verify infrastructure works

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Industrial.Adam.Logger.IntegrationTests;

/// <summary>
/// Basic integration tests to verify test infrastructure
/// </summary>
public class BasicIntegrationTest
{
    [Fact]
    public void ServiceProvider_ShouldResolveBasicServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        var provider = services.BuildServiceProvider();

        // Act
        var logger = provider.GetService<ILogger<BasicIntegrationTest>>();

        // Assert
        logger.Should().NotBeNull();
    }

    [Fact]
    public void HostBuilder_ShouldCreateHost()
    {
        // Arrange & Act
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
            })
            .Build();

        // Assert
        host.Should().NotBeNull();
        host.Services.Should().NotBeNull();
    }

    [Fact]
    public void Integration_BasicAssertion_ShouldWork()
    {
        // Arrange
        const int expected = 42;

        // Act
        const int actual = 42;

        // Assert
        actual.Should().Be(expected);
    }
}