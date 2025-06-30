// Industrial.Adam.Logger.Tests - DefaultDataTransformer Unit Tests
// Comprehensive tests for data transformation implementation (12 tests as per TESTING_PLAN.md)

using FluentAssertions;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Services;
using Industrial.Adam.Logger.Tests.TestHelpers;
using Xunit;

namespace Industrial.Adam.Logger.Tests.Services;

/// <summary>
/// Unit tests for DefaultDataTransformer (12 tests planned)
/// </summary>
public class DefaultDataTransformerTests
{
    #region TransformValue Tests (6 tests)

    [Fact]
    public void TransformValue_DefaultScaling_ShouldReturnRawValue()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.ScaleFactor = 1.0;
        channel.Offset = 0.0;
        channel.DecimalPlaces = 0;

        // Act
        var result = transformer.TransformValue(1000, channel);

        // Assert
        result.Should().Be(1000.0);
    }

    [Fact]
    public void TransformValue_WithScaling_ShouldApplyScaleFactor()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.ScaleFactor = 0.1;
        channel.Offset = 0.0;
        channel.DecimalPlaces = 1;

        // Act
        var result = transformer.TransformValue(1000, channel);

        // Assert
        result.Should().Be(100.0);
    }

    [Fact]
    public void TransformValue_WithOffset_ShouldApplyOffset()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.ScaleFactor = 1.0;
        channel.Offset = 273.15; // Celsius to Kelvin conversion
        channel.DecimalPlaces = 2;

        // Act
        var result = transformer.TransformValue(0, channel);

        // Assert
        result.Should().Be(273.15);
    }

    [Fact]
    public void TransformValue_WithScalingAndOffset_ShouldApplyBoth()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.ScaleFactor = 0.01; // Convert from centi-units
        channel.Offset = -273.15; // Kelvin to Celsius conversion
        channel.DecimalPlaces = 2;

        // Act
        var result = transformer.TransformValue(30000, channel); // 300.00 K

        // Assert
        result.Should().Be(26.85); // (30000 * 0.01) - 273.15 = 26.85°C
    }

    [Fact]
    public void TransformValue_WithRounding_ShouldRoundToDecimalPlaces()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.ScaleFactor = 1.0 / 3.0; // Creates repeating decimal
        channel.Offset = 0.0;
        channel.DecimalPlaces = 3;

        // Act
        var result = transformer.TransformValue(1, channel);

        // Assert
        result.Should().Be(0.333); // 1/3 rounded to 3 decimal places
    }

    [Fact]
    public void TransformValue_NegativeValues_ShouldHandleCorrectly()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var channel = TestConfigurationBuilder.ValidChannelConfig();
        channel.ScaleFactor = -0.5;
        channel.Offset = 100.0;
        channel.DecimalPlaces = 1;

        // Act
        var result = transformer.TransformValue(200, channel);

        // Assert
        result.Should().Be(0.0); // (200 * -0.5) + 100 = 0.0
    }

    #endregion

    #region EnrichTags Tests (6 tests)

    [Fact]
    public void EnrichTags_BasicEnrichment_ShouldAddStandardTags()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var baseTags = new Dictionary<string, object>
        {
            { "original_tag", "original_value" }
        };
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig();
        var channelConfig = TestConfigurationBuilder.ValidChannelConfig(0, "TestChannel");

        // Act
        var result = transformer.EnrichTags(baseTags, deviceConfig, channelConfig);

        // Assert
        result.Should().ContainKey("original_tag");
        result.Should().ContainKey("data_source");
        result.Should().ContainKey("channel_name");
        result.Should().ContainKey("device_type");
        result.Should().ContainKey("timestamp_utc");
        result["data_source"].Should().Be("adam_logger");
        result["channel_name"].Should().Be("TestChannel");
        result["device_type"].Should().Be("adam_6051");
    }

    [Fact]
    public void EnrichTags_WithChannelMetadata_ShouldIncludeChannelInfo()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var baseTags = new Dictionary<string, object>();
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig();
        var channelConfig = TestConfigurationBuilder.ValidChannelConfig(2, "PressureSensor");
        channelConfig.Description = "Main line pressure sensor";
        channelConfig.StartRegister = 100;
        channelConfig.RegisterCount = 2;
        channelConfig.ScaleFactor = 0.01;
        channelConfig.Offset = -10.0;

        // Act
        var result = transformer.EnrichTags(baseTags, deviceConfig, channelConfig);

        // Assert
        result.Should().ContainKey("channel_description");
        result.Should().ContainKey("register_start");
        result.Should().ContainKey("register_count");
        result.Should().ContainKey("scale_factor");
        result.Should().ContainKey("offset");
        result["channel_description"].Should().Be("Main line pressure sensor");
        result["register_start"].Should().Be(100);
        result["register_count"].Should().Be(2);
        result["scale_factor"].Should().Be(0.01);
        result["offset"].Should().Be(-10.0);
    }

    [Fact]
    public void EnrichTags_WithoutDescription_ShouldSkipDescription()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var baseTags = new Dictionary<string, object>();
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig();
        var channelConfig = TestConfigurationBuilder.ValidChannelConfig(0, "TestChannel");
        channelConfig.Description = null; // No description

        // Act
        var result = transformer.EnrichTags(baseTags, deviceConfig, channelConfig);

        // Assert
        result.Should().NotContainKey("channel_description");
    }

    [Fact]
    public void EnrichTags_WithZeroOffset_ShouldSkipOffset()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var baseTags = new Dictionary<string, object>();
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig();
        var channelConfig = TestConfigurationBuilder.ValidChannelConfig();
        channelConfig.Offset = 0.0; // Zero offset

        // Act
        var result = transformer.EnrichTags(baseTags, deviceConfig, channelConfig);

        // Assert
        result.Should().NotContainKey("offset");
    }

    [Fact]
    public void EnrichTags_WithDeviceTags_ShouldPrefixAndInclude()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var baseTags = new Dictionary<string, object>();
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig();
        deviceConfig.Tags.Clear();
        deviceConfig.Tags.Add("location", "building_a");
        deviceConfig.Tags.Add("line", "production_1");
        var channelConfig = TestConfigurationBuilder.ValidChannelConfig();

        // Act
        var result = transformer.EnrichTags(baseTags, deviceConfig, channelConfig);

        // Assert
        result.Should().ContainKey("device_location");
        result.Should().ContainKey("device_line");
        result["device_location"].Should().Be("building_a");
        result["device_line"].Should().Be("production_1");
    }

    [Fact]
    public void EnrichTags_ConflictingTags_ShouldUseOriginal()
    {
        // Arrange
        var transformer = new DefaultDataTransformer();
        var baseTags = new Dictionary<string, object>
        {
            { "device_type", "custom_device" } // Conflicts with standard tag
        };
        var deviceConfig = TestConfigurationBuilder.ValidDeviceConfig();
        var channelConfig = TestConfigurationBuilder.ValidChannelConfig();

        // Act
        var result = transformer.EnrichTags(baseTags, deviceConfig, channelConfig);

        // Assert
        result["device_type"].Should().Be("custom_device"); // Should keep original value
    }

    #endregion
}