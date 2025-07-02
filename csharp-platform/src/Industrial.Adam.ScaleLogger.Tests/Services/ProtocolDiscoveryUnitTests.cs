// Industrial.Adam.ScaleLogger.Tests - Protocol Discovery Unit Tests
// Pure unit tests following ADAM-6051 patterns without external dependencies

using FluentAssertions;
using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Models;
using Industrial.Adam.ScaleLogger.Services;
using Industrial.Adam.ScaleLogger.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.RegularExpressions;
using Xunit;

namespace Industrial.Adam.ScaleLogger.Tests.Services;

/// <summary>
/// Pure unit tests for ProtocolDiscoveryService following proven ADAM-6051 patterns
/// Tests focus on template management and configuration logic without network dependencies
/// Total Tests: 9 (focused on unit testing principles)
/// </summary>
public sealed class ProtocolDiscoveryUnitTests : IDisposable
{
    private readonly ProtocolDiscoveryService _discoveryService;
    private readonly Mock<ILogger<ProtocolDiscoveryService>> _mockLogger;
    private readonly ProtocolDiscoveryConfig _config;

    public ProtocolDiscoveryUnitTests()
    {
        _mockLogger = TestMockFactory.CreateMockLogger<ProtocolDiscoveryService>();
        _config = TestConfigurationBuilder.ValidProtocolDiscoveryConfig();
        _discoveryService = new ProtocolDiscoveryService(_config, _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        using var service = new ProtocolDiscoveryService(_config, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        var templates = service.GetAvailableTemplates();
        templates.Should().NotBeEmpty(); // Should have built-in templates
    }

    [Fact]
    public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ProtocolDiscoveryService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ProtocolDiscoveryService(_config, null!));
    }

    #endregion

    #region Built-in Template Tests

    [Fact]
    public void GetAvailableTemplates_WithDefaultConfiguration_ShouldReturnBuiltInTemplates()
    {
        // Act
        var templates = _discoveryService.GetAvailableTemplates();

        // Assert
        templates.Should().NotBeEmpty();
        templates.Should().Contain(t => t.Id == "adam-4571-basic");
        templates.Should().Contain(t => t.Id == "generic-scale");
        templates.Should().OnlyContain(t => !string.IsNullOrEmpty(t.Name));
        templates.Should().OnlyContain(t => t.Commands.Any());
        templates.Should().OnlyContain(t => t.ExpectedResponses.Any());
    }

    [Fact]
    public void BuiltInTemplates_ShouldHaveValidStructure()
    {
        // Act
        var templates = _discoveryService.GetAvailableTemplates();

        // Assert
        templates.Should().NotBeEmpty();
        foreach (var template in templates)
        {
            template.Id.Should().NotBeNullOrEmpty();
            template.Name.Should().NotBeNullOrEmpty();
            template.Commands.Should().NotBeEmpty();
            template.ExpectedResponses.Should().NotBeEmpty();
            template.WeightPattern.Should().NotBeNullOrEmpty();
            
            // Validate regex patterns don't throw
            foreach (var pattern in template.ExpectedResponses)
            {
                var act = () => new Regex(pattern);
                act.Should().NotThrow();
            }
            
            // Validate weight pattern
            var weightPatternAct = () => new Regex(template.WeightPattern);
            weightPatternAct.Should().NotThrow();
        }
    }

    #endregion

    #region Custom Template Management Tests

    [Fact]
    public void AddTemplate_WithValidTemplate_ShouldAddSuccessfully()
    {
        // Arrange
        var customTemplate = TestConfigurationBuilder.ValidProtocolTemplate("custom-scale", "Custom Scale");
        var initialCount = _discoveryService.GetAvailableTemplates().Count;

        // Act
        _discoveryService.AddTemplate(customTemplate);

        // Assert
        var templates = _discoveryService.GetAvailableTemplates();
        templates.Should().HaveCount(initialCount + 1);
        templates.Should().Contain(t => t.Id == "custom-scale");
        templates.Should().Contain(t => t.Name == "Custom Scale");
    }

    [Fact]
    public void AddTemplate_WithMultipleTemplates_ShouldAddAllTemplates()
    {
        // Arrange
        var customTemplates = new[]
        {
            TestConfigurationBuilder.ValidProtocolTemplate("mettler-toledo", "Mettler Toledo"),
            TestConfigurationBuilder.ValidProtocolTemplate("ohaus", "Ohaus Scale"),
            TestConfigurationBuilder.ValidProtocolTemplate("sartorius", "Sartorius Balance")
        };
        var initialCount = _discoveryService.GetAvailableTemplates().Count;

        // Act
        foreach (var template in customTemplates)
        {
            _discoveryService.AddTemplate(template);
        }

        // Assert
        var templates = _discoveryService.GetAvailableTemplates();
        templates.Should().HaveCount(initialCount + 3);
        templates.Should().Contain(t => t.Id == "mettler-toledo");
        templates.Should().Contain(t => t.Id == "ohaus");
        templates.Should().Contain(t => t.Id == "sartorius");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ShouldClearTemplatesAndPreventFurtherOperations()
    {
        // Arrange
        var customTemplate = TestConfigurationBuilder.ValidProtocolTemplate("test-dispose", "Test Dispose");
        _discoveryService.AddTemplate(customTemplate);

        // Act
        _discoveryService.Dispose();

        // Assert
        var templates = _discoveryService.GetAvailableTemplates();
        templates.Should().BeEmpty();
        
        // Further operations should throw
        Assert.Throws<ObjectDisposedException>(() =>
            _discoveryService.AddTemplate(customTemplate));
    }

    #endregion

    public void Dispose()
    {
        _discoveryService?.Dispose();
    }
}