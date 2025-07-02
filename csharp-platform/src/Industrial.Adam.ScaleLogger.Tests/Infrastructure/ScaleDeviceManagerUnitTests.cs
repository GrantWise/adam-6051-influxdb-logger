// Industrial.Adam.ScaleLogger.Tests - Scale Device Manager Unit Tests
// Pure unit tests following ADAM-6051 patterns without external dependencies

using FluentAssertions;
using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Infrastructure;
using Industrial.Adam.ScaleLogger.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Industrial.Adam.ScaleLogger.Tests.Infrastructure;

/// <summary>
/// Pure unit tests for ScaleDeviceManager following proven ADAM-6051 patterns
/// Tests focus on business logic and API contracts without network dependencies
/// Total Tests: 8 (focused on unit testing principles)
/// </summary>
public sealed class ScaleDeviceManagerUnitTests : IDisposable
{
    private readonly ScaleDeviceManager _deviceManager;
    private readonly Mock<ILogger<ScaleDeviceManager>> _mockLogger;

    public ScaleDeviceManagerUnitTests()
    {
        _mockLogger = TestMockFactory.CreateMockLogger<ScaleDeviceManager>();
        _deviceManager = new ScaleDeviceManager(_mockLogger.Object);
    }

    #region Constructor and Basic State Tests

    [Fact]
    public void Constructor_WithValidLogger_ShouldInitializeCorrectly()
    {
        // Act
        using var manager = new ScaleDeviceManager(_mockLogger.Object);

        // Assert
        manager.Should().NotBeNull();
        manager.GetConfiguredDevices().Should().BeEmpty();
        manager.GetProtocolTemplates().Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ScaleDeviceManager(null!));
    }

    #endregion

    #region Protocol Template Management Tests

    [Fact]
    public void AddProtocolTemplate_WithValidTemplate_ShouldAddSuccessfully()
    {
        // Arrange
        var template = TestConfigurationBuilder.ValidProtocolTemplate();

        // Act
        _deviceManager.AddProtocolTemplate(template);

        // Assert
        var templates = _deviceManager.GetProtocolTemplates();
        templates.Should().Contain(t => t.Id == template.Id);
        templates.Should().Contain(t => t.Name == template.Name);
    }

    [Fact]
    public void GetProtocolTemplates_WithMultipleTemplates_ShouldReturnAllTemplates()
    {
        // Arrange
        var templates = new[]
        {
            TestConfigurationBuilder.ValidProtocolTemplate("template1", "Template 1"),
            TestConfigurationBuilder.ValidProtocolTemplate("template2", "Template 2"),
            TestConfigurationBuilder.ValidProtocolTemplate("template3", "Template 3")
        };

        // Act
        foreach (var template in templates)
        {
            _deviceManager.AddProtocolTemplate(template);
        }

        // Assert
        var result = _deviceManager.GetProtocolTemplates();
        result.Should().HaveCount(3);
        result.Should().Contain(t => t.Id == "template1");
        result.Should().Contain(t => t.Id == "template2");
        result.Should().Contain(t => t.Id == "template3");
    }

    [Fact]
    public void GetProtocolTemplates_WithNoTemplates_ShouldReturnEmptyList()
    {
        // Act
        var templates = _deviceManager.GetProtocolTemplates();

        // Assert
        templates.Should().NotBeNull();
        templates.Should().BeEmpty();
    }

    #endregion

    #region Device Configuration Management Tests

    [Fact]
    public void GetConfiguredDevices_WithInitialState_ShouldReturnEmptyList()
    {
        // Act
        var configuredDevices = _deviceManager.GetConfiguredDevices();

        // Assert
        configuredDevices.Should().NotBeNull();
        configuredDevices.Should().BeEmpty();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ShouldClearTemplatesAndDevices()
    {
        // Arrange
        var template = TestConfigurationBuilder.ValidProtocolTemplate();
        _deviceManager.AddProtocolTemplate(template);

        // Act
        _deviceManager.Dispose();

        // Assert
        var templates = _deviceManager.GetProtocolTemplates();
        var devices = _deviceManager.GetConfiguredDevices();
        
        templates.Should().BeEmpty();
        devices.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_Multiple_ShouldNotThrow()
    {
        // Act & Assert
        var act = () =>
        {
            _deviceManager.Dispose();
            _deviceManager.Dispose(); // Second disposal should be safe
        };
        
        act.Should().NotThrow();
    }

    #endregion

    public void Dispose()
    {
        _deviceManager?.Dispose();
    }
}