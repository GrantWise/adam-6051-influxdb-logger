// Industrial.Adam.Logger - Default Data Transformation Implementation
// Default implementation for transforming values and enriching metadata

using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Interfaces;

namespace Industrial.Adam.Logger.Services;

/// <summary>
/// Default implementation of data transformation including scaling and metadata enrichment
/// </summary>
public class DefaultDataTransformer : IDataTransformer
{
    /// <summary>
    /// Transform raw values using scaling, offset, and rounding
    /// </summary>
    /// <param name="rawValue">Raw value from the device</param>
    /// <param name="channelConfig">Channel configuration with transformation parameters</param>
    /// <returns>Transformed value ready for application use</returns>
    public double? TransformValue(long rawValue, ChannelConfig channelConfig)
    {
        // Apply scaling and offset transformation
        var scaled = rawValue * channelConfig.ScaleFactor + channelConfig.Offset;
        
        // Round to the specified number of decimal places
        return Math.Round(scaled, channelConfig.DecimalPlaces);
    }

    /// <summary>
    /// Enrich metadata tags with additional context and information
    /// </summary>
    /// <param name="baseTags">Base tags from the channel configuration</param>
    /// <param name="deviceConfig">Device configuration for additional context</param>
    /// <param name="channelConfig">Channel configuration for specific metadata</param>
    /// <returns>Enriched tags dictionary with additional metadata</returns>
    public Dictionary<string, object> EnrichTags(
        Dictionary<string, object> baseTags, 
        AdamDeviceConfig deviceConfig, 
        ChannelConfig channelConfig)
    {
        var enrichedTags = new Dictionary<string, object>(baseTags);
        
        // Add channel metadata
        enrichedTags[StandardTags.DataSource] = "adam_logger";
        enrichedTags["channel_name"] = channelConfig.Name;
        
        if (!string.IsNullOrWhiteSpace(channelConfig.Description))
            enrichedTags["channel_description"] = channelConfig.Description;

        // Add channel-specific metadata
        enrichedTags["register_start"] = channelConfig.StartRegister;
        enrichedTags["register_count"] = channelConfig.RegisterCount;
        enrichedTags["scale_factor"] = channelConfig.ScaleFactor;
        
        if (channelConfig.Offset != 0)
            enrichedTags["offset"] = channelConfig.Offset;

        // Add device metadata if provided
        if (deviceConfig?.Tags != null)
        {
            foreach (var tag in deviceConfig.Tags)
            {
                // Prefix device tags to avoid conflicts
                enrichedTags.TryAdd($"device_{tag.Key}", tag.Value);
            }
        }

        // Add standard industrial tags if not already present
        enrichedTags.TryAdd(StandardTags.DeviceType, "adam_6051");
        enrichedTags.TryAdd("timestamp_utc", DateTimeOffset.UtcNow.ToString("O"));

        return enrichedTags;
    }
}