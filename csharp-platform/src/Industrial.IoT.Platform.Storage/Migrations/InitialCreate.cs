// Industrial.IoT.Platform.Storage - Initial Database Migration
// Creates the initial SQL Server schema for Industrial IoT Platform following existing ADAM logger patterns

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Industrial.IoT.Platform.Storage.Migrations;

/// <summary>
/// Initial database migration for Industrial IoT Platform
/// Creates tables for scale data, protocol templates, and device configurations
/// Following existing ADAM logger database patterns for consistency
/// </summary>
public partial class InitialCreate : Migration
{
    /// <summary>
    /// Apply the migration - create all tables and indexes
    /// </summary>
    /// <param name="migrationBuilder">Migration builder for database operations</param>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create ScaleData table
        migrationBuilder.CreateTable(
            name: "ScaleData",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DeviceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Channel = table.Column<int>(type: "int", nullable: false),
                Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                WeightKg = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                RawWeight = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Unit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Quality = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Good"),
                AcquisitionTime = table.Column<TimeSpan>(type: "time", nullable: false),
                StabilityScore = table.Column<double>(type: "float", nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Manufacturer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Model = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                SerialNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                ProtocolTemplate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScaleData", x => x.Id);
                table.CheckConstraint("CK_ScaleData_Channel", "[Channel] >= 1 AND [Channel] <= 8");
                table.CheckConstraint("CK_ScaleData_StabilityScore", "[StabilityScore] IS NULL OR ([StabilityScore] >= 0 AND [StabilityScore] <= 100)");
            });

        // Create ProtocolTemplates table
        migrationBuilder.CreateTable(
            name: "ProtocolTemplates",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TemplateName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Manufacturer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Model = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "1.0.0"),
                CommunicationSettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CommandTemplatesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ResponsePatternsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ValidationRulesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ErrorHandlingJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 50),
                ConfidenceThreshold = table.Column<double>(type: "float", nullable: false, defaultValue: 75.0),
                TimeoutMs = table.Column<int>(type: "int", nullable: false, defaultValue: 5000),
                MaxRetries = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                IsBuiltIn = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                SupportedBaudRates = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                EnvironmentalOptimization = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Author = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                LastUsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UsageCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                SuccessRate = table.Column<double>(type: "float", nullable: false, defaultValue: 0.0)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProtocolTemplates", x => x.Id);
                table.CheckConstraint("CK_ProtocolTemplates_Priority", "[Priority] >= 1 AND [Priority] <= 100");
                table.CheckConstraint("CK_ProtocolTemplates_ConfidenceThreshold", "[ConfidenceThreshold] >= 0 AND [ConfidenceThreshold] <= 100");
                table.CheckConstraint("CK_ProtocolTemplates_TimeoutMs", "[TimeoutMs] >= 100 AND [TimeoutMs] <= 30000");
                table.CheckConstraint("CK_ProtocolTemplates_MaxRetries", "[MaxRetries] >= 0 AND [MaxRetries] <= 10");
                table.CheckConstraint("CK_ProtocolTemplates_SuccessRate", "[SuccessRate] >= 0 AND [SuccessRate] <= 100");
            });

        // Create DeviceConfigurations table
        migrationBuilder.CreateTable(
            name: "DeviceConfigurations",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DeviceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                DeviceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                DeviceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Port = table.Column<int>(type: "int", nullable: true),
                SerialPort = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                BaudRate = table.Column<int>(type: "int", nullable: true),
                DataBits = table.Column<int>(type: "int", nullable: true),
                Parity = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                StopBits = table.Column<int>(type: "int", nullable: true),
                FlowControl = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                ProtocolTemplate = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ChannelConfigurationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AcquisitionIntervalMs = table.Column<int>(type: "int", nullable: false, defaultValue: 1000),
                ConnectionTimeoutMs = table.Column<int>(type: "int", nullable: false, defaultValue: 10000),
                ReadTimeoutMs = table.Column<int>(type: "int", nullable: false, defaultValue: 5000),
                MaxRetries = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                RetryDelayMs = table.Column<int>(type: "int", nullable: false, defaultValue: 1000),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                EnableHealthMonitoring = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                EnableStabilityMonitoring = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                StabilityThreshold = table.Column<double>(type: "float", nullable: false, defaultValue: 80.0),
                EnvironmentalOptimization = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Manufacturer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Model = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                SerialNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                FirmwareVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                TagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastConnectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastDataReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                TotalConnections = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                SuccessfulConnections = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                TotalDataReads = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                SuccessfulDataReads = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "GETUTCDATE()"),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeviceConfigurations", x => x.Id);
                table.CheckConstraint("CK_DeviceConfigurations_Port", "[Port] IS NULL OR ([Port] >= 1 AND [Port] <= 65535)");
                table.CheckConstraint("CK_DeviceConfigurations_BaudRate", "[BaudRate] IS NULL OR ([BaudRate] >= 300 AND [BaudRate] <= 115200)");
                table.CheckConstraint("CK_DeviceConfigurations_DataBits", "[DataBits] IS NULL OR ([DataBits] >= 7 AND [DataBits] <= 8)");
                table.CheckConstraint("CK_DeviceConfigurations_StopBits", "[StopBits] IS NULL OR ([StopBits] >= 1 AND [StopBits] <= 2)");
                table.CheckConstraint("CK_DeviceConfigurations_AcquisitionIntervalMs", "[AcquisitionIntervalMs] >= 100 AND [AcquisitionIntervalMs] <= 3600000");
                table.CheckConstraint("CK_DeviceConfigurations_ConnectionTimeoutMs", "[ConnectionTimeoutMs] >= 1000 AND [ConnectionTimeoutMs] <= 60000");
                table.CheckConstraint("CK_DeviceConfigurations_ReadTimeoutMs", "[ReadTimeoutMs] >= 100 AND [ReadTimeoutMs] <= 30000");
                table.CheckConstraint("CK_DeviceConfigurations_MaxRetries", "[MaxRetries] >= 0 AND [MaxRetries] <= 10");
                table.CheckConstraint("CK_DeviceConfigurations_RetryDelayMs", "[RetryDelayMs] >= 100 AND [RetryDelayMs] <= 10000");
                table.CheckConstraint("CK_DeviceConfigurations_StabilityThreshold", "[StabilityThreshold] >= 0 AND [StabilityThreshold] <= 100");
            });

        // Create indexes for ScaleData table
        migrationBuilder.CreateIndex(
            name: "IX_ScaleData_DeviceId_Timestamp",
            table: "ScaleData",
            columns: new[] { "DeviceId", "Timestamp" });

        migrationBuilder.CreateIndex(
            name: "IX_ScaleData_Timestamp",
            table: "ScaleData",
            column: "Timestamp");

        migrationBuilder.CreateIndex(
            name: "IX_ScaleData_WeightKg",
            table: "ScaleData",
            column: "WeightKg");

        migrationBuilder.CreateIndex(
            name: "IX_ScaleData_Quality",
            table: "ScaleData",
            column: "Quality");

        migrationBuilder.CreateIndex(
            name: "IX_ScaleData_Manufacturer_Model",
            table: "ScaleData",
            columns: new[] { "Manufacturer", "Model" });

        // Create indexes for ProtocolTemplates table
        migrationBuilder.CreateIndex(
            name: "IX_ProtocolTemplates_TemplateName_Unique",
            table: "ProtocolTemplates",
            column: "TemplateName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProtocolTemplates_Manufacturer_Model",
            table: "ProtocolTemplates",
            columns: new[] { "Manufacturer", "Model" });

        migrationBuilder.CreateIndex(
            name: "IX_ProtocolTemplates_IsActive",
            table: "ProtocolTemplates",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_ProtocolTemplates_Priority",
            table: "ProtocolTemplates",
            column: "Priority");

        migrationBuilder.CreateIndex(
            name: "IX_ProtocolTemplates_UsageCount_SuccessRate",
            table: "ProtocolTemplates",
            columns: new[] { "UsageCount", "SuccessRate" });

        // Create indexes for DeviceConfigurations table
        migrationBuilder.CreateIndex(
            name: "IX_DeviceConfigurations_DeviceId_Unique",
            table: "DeviceConfigurations",
            column: "DeviceId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DeviceConfigurations_DeviceType",
            table: "DeviceConfigurations",
            column: "DeviceType");

        migrationBuilder.CreateIndex(
            name: "IX_DeviceConfigurations_IsActive",
            table: "DeviceConfigurations",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_DeviceConfigurations_IpAddress_Port",
            table: "DeviceConfigurations",
            columns: new[] { "IpAddress", "Port" });

        migrationBuilder.CreateIndex(
            name: "IX_DeviceConfigurations_Location_Department",
            table: "DeviceConfigurations",
            columns: new[] { "Location", "Department" });

        // Insert default protocol templates
        InsertDefaultProtocolTemplates(migrationBuilder);
    }

    /// <summary>
    /// Rollback the migration - drop all tables
    /// </summary>
    /// <param name="migrationBuilder">Migration builder for database operations</param>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ScaleData");
        migrationBuilder.DropTable(name: "ProtocolTemplates");
        migrationBuilder.DropTable(name: "DeviceConfigurations");
    }

    /// <summary>
    /// Insert default protocol templates during migration
    /// Based on existing Python implementation templates
    /// </summary>
    /// <param name="migrationBuilder">Migration builder for insert operations</param>
    private static void InsertDefaultProtocolTemplates(MigrationBuilder migrationBuilder)
    {
        // Mettler Toledo Standard Template
        migrationBuilder.InsertData(
            table: "ProtocolTemplates",
            columns: new[]
            {
                "TemplateName", "DisplayName", "Manufacturer", "Model", "Description", "Version",
                "CommunicationSettingsJson", "CommandTemplatesJson", "ResponsePatternsJson",
                "Priority", "ConfidenceThreshold", "TimeoutMs", "MaxRetries", "IsActive", "IsBuiltIn",
                "SupportedBaudRates", "EnvironmentalOptimization", "Author", "CreatedAt", "ModifiedAt"
            },
            values: new object[]
            {
                "mettler_toledo_standard",
                "Mettler Toledo Standard Protocol",
                "Mettler Toledo",
                null,
                "Standard Mettler Toledo scale protocol with SI/SIR commands",
                "1.0.0",
                "{\"BaudRate\":9600,\"DataBits\":8,\"Parity\":\"None\",\"StopBits\":1,\"FlowControl\":\"None\"}",
                "{\"RequestWeight\":\"SI\\r\\n\",\"RequestWeightImmediate\":\"SIR\\r\\n\",\"Reset\":\"Z\\r\\n\"}",
                "{\"WeightPattern\":\"S\\\\s+S\\\\s+([\\\\d\\\\.-]+)\\\\s*(\\\\w*)\",\"StablePattern\":\"S\\\\s+S\\\\s+\",\"UnstablePattern\":\"S\\\\s+D\\\\s+\",\"OverloadPattern\":\"S\\\\s+\\\\+\\\\s+\",\"UnderloadPattern\":\"S\\\\s+-\\\\s+\"}",
                90,
                80.0,
                5000,
                3,
                true,
                true,
                "9600,19200,38400",
                "CleanRoom",
                "System",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            });

        // Sartorius Standard Template
        migrationBuilder.InsertData(
            table: "ProtocolTemplates",
            columns: new[]
            {
                "TemplateName", "DisplayName", "Manufacturer", "Model", "Description", "Version",
                "CommunicationSettingsJson", "CommandTemplatesJson", "ResponsePatternsJson",
                "Priority", "ConfidenceThreshold", "TimeoutMs", "MaxRetries", "IsActive", "IsBuiltIn",
                "SupportedBaudRates", "EnvironmentalOptimization", "Author", "CreatedAt", "ModifiedAt"
            },
            values: new object[]
            {
                "sartorius_standard",
                "Sartorius Standard Protocol",
                "Sartorius",
                null,
                "Standard Sartorius scale protocol",
                "1.0.0",
                "{\"BaudRate\":9600,\"DataBits\":8,\"Parity\":\"None\",\"StopBits\":1,\"FlowControl\":\"None\"}",
                "{\"RequestWeight\":\"P\\r\\n\",\"Tare\":\"T\\r\\n\",\"Zero\":\"Z\\r\\n\"}",
                "{\"WeightPattern\":\"([\\\\d\\\\.-]+)\\\\s*(\\\\w*)\",\"StablePattern\":\"[\\\\d\\\\.-]+\\\\s*\\\\w*\\\\s*$\",\"ErrorPattern\":\"Err\"}",
                85,
                75.0,
                5000,
                3,
                true,
                true,
                "9600,19200",
                "Laboratory",
                "System",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            });

        // Ohaus Standard Template
        migrationBuilder.InsertData(
            table: "ProtocolTemplates",
            columns: new[]
            {
                "TemplateName", "DisplayName", "Manufacturer", "Model", "Description", "Version",
                "CommunicationSettingsJson", "CommandTemplatesJson", "ResponsePatternsJson",
                "Priority", "ConfidenceThreshold", "TimeoutMs", "MaxRetries", "IsActive", "IsBuiltIn",
                "SupportedBaudRates", "EnvironmentalOptimization", "Author", "CreatedAt", "ModifiedAt"
            },
            values: new object[]
            {
                "ohaus_standard",
                "Ohaus Standard Protocol",
                "Ohaus",
                null,
                "Standard Ohaus scale protocol",
                "1.0.0",
                "{\"BaudRate\":9600,\"DataBits\":8,\"Parity\":\"None\",\"StopBits\":1,\"FlowControl\":\"None\"}",
                "{\"RequestWeight\":\"IP\\r\\n\",\"Print\":\"P\\r\\n\",\"Zero\":\"Z\\r\\n\"}",
                "{\"WeightPattern\":\"([\\\\d\\\\.-]+)\\\\s*(\\\\w*)\",\"StablePattern\":\"ST,NT,([\\\\d\\\\.-]+)\\\\s*(\\\\w*)\",\"MotionPattern\":\"US,NT\"}",
                80,
                70.0,
                5000,
                3,
                true,
                true,
                "9600,19200",
                "Factory",
                "System",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            });

        // Generic Scale Template
        migrationBuilder.InsertData(
            table: "ProtocolTemplates",
            columns: new[]
            {
                "TemplateName", "DisplayName", "Manufacturer", "Model", "Description", "Version",
                "CommunicationSettingsJson", "CommandTemplatesJson", "ResponsePatternsJson",
                "Priority", "ConfidenceThreshold", "TimeoutMs", "MaxRetries", "IsActive", "IsBuiltIn",
                "SupportedBaudRates", "EnvironmentalOptimization", "Author", "CreatedAt", "ModifiedAt"
            },
            values: new object[]
            {
                "generic_scale",
                "Generic Scale Protocol",
                "Generic",
                null,
                "Generic scale protocol for unknown manufacturers",
                "1.0.0",
                "{\"BaudRate\":9600,\"DataBits\":8,\"Parity\":\"None\",\"StopBits\":1,\"FlowControl\":\"None\"}",
                "{\"RequestWeight\":\"\\r\\n\",\"Print\":\"P\\r\\n\"}",
                "{\"WeightPattern\":\"([\\\\d\\\\.-]+)\\\\s*(\\\\w*)\",\"GenericPattern\":\".*([\\\\d\\\\.-]+).*\"}",
                10,
                50.0,
                10000,
                5,
                true,
                true,
                "2400,4800,9600,19200,38400",
                "Generic",
                "System",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            });
    }
}