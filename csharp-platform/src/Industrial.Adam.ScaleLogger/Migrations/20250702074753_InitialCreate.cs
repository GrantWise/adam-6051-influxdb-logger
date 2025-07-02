using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Industrial.Adam.ScaleLogger.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScaleDevices",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Configuration = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScaleDevices", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "SystemEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemEvents_ScaleDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "ScaleDevices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WeighingTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    WeightValue = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    IsStable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Quality = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "NOW()"),
                    OperatorId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ProductCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BatchNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    WorkOrder = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RawValue = table.Column<string>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeighingTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeighingTransactions_ScaleDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "ScaleDevices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScaleDevices_IsActive",
                table: "ScaleDevices",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ScaleDevices_Location",
                table: "ScaleDevices",
                column: "Location");

            migrationBuilder.CreateIndex(
                name: "IX_ScaleDevices_Name",
                table: "ScaleDevices",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_DeviceId",
                table: "SystemEvents",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_EventId",
                table: "SystemEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_EventType",
                table: "SystemEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_EventType_Timestamp",
                table: "SystemEvents",
                columns: new[] { "EventType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_Severity",
                table: "SystemEvents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEvents_Timestamp",
                table: "SystemEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_WeighingTransactions_BatchNumber",
                table: "WeighingTransactions",
                column: "BatchNumber");

            migrationBuilder.CreateIndex(
                name: "IX_WeighingTransactions_DeviceId",
                table: "WeighingTransactions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_WeighingTransactions_DeviceId_Timestamp",
                table: "WeighingTransactions",
                columns: new[] { "DeviceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_WeighingTransactions_ProductCode",
                table: "WeighingTransactions",
                column: "ProductCode");

            migrationBuilder.CreateIndex(
                name: "IX_WeighingTransactions_Timestamp",
                table: "WeighingTransactions",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_WeighingTransactions_TransactionId",
                table: "WeighingTransactions",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeighingTransactions_WorkOrder",
                table: "WeighingTransactions",
                column: "WorkOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemEvents");

            migrationBuilder.DropTable(
                name: "WeighingTransactions");

            migrationBuilder.DropTable(
                name: "ScaleDevices");
        }
    }
}
