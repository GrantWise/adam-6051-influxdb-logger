// Industrial.Adam.ScaleLogger.Tests - Weighing Repository Tests
// Following proven ADAM-6051 testing patterns with Arrange-Act-Assert

using FluentAssertions;
using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Data;
using Industrial.Adam.ScaleLogger.Data.Entities;
using Industrial.Adam.ScaleLogger.Data.Repositories;
using Industrial.Adam.ScaleLogger.Models;
using Industrial.Adam.ScaleLogger.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Industrial.Adam.ScaleLogger.Tests.Data.Repositories;

/// <summary>
/// Unit tests for WeighingRepository following proven ADAM-6051 patterns
/// Tests cover happy path, error scenarios, and edge cases
/// Total Tests: 15 (planned)
/// </summary>
public sealed class WeighingRepositoryTests : IDisposable
{
    private readonly ScaleLoggerDbContext _context;
    private readonly WeighingRepository _repository;
    private readonly DatabaseConfig _databaseConfig;

    public WeighingRepositoryTests()
    {
        // Arrange - Set up in-memory database for testing
        _databaseConfig = TestConfigurationBuilder.ValidDatabaseConfig(DatabaseProvider.SQLite);
        _databaseConfig.ConnectionString = $"Data Source=:memory:";
        
        var options = new DbContextOptionsBuilder<ScaleLoggerDbContext>()
            .UseSqlite(_databaseConfig.ConnectionString)
            .Options;
            
        var configOptions = Options.Create(_databaseConfig);
        _context = new ScaleLoggerDbContext(options, configOptions);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        
        var mockLogger = TestMockFactory.CreateMockLogger<WeighingRepository>();
        _repository = new WeighingRepository(_context, mockLogger.Object);
    }

    #region SaveWeighingAsync Tests (4 tests)

    [Fact]
    public async Task SaveWeighingAsync_WithValidReading_ShouldSaveSuccessfully()
    {
        // Arrange
        var reading = TestConfigurationBuilder.ValidScaleDataReading(
            deviceId: "TEST_SCALE_001", 
            weight: 12.34);

        // Act
        var result = await _repository.SaveWeighingAsync(reading);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.TransactionId.Should().NotBeEmpty();
        result.DeviceId.Should().Be("TEST_SCALE_001");
        result.WeightValue.Should().Be(12.34m);
        result.Unit.Should().Be("kg");
        result.IsStable.Should().BeTrue();
        result.Quality.Should().Be(DataQuality.Good.ToString());
        result.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task SaveWeighingAsync_WithUnstableReading_ShouldSaveWithCorrectQuality()
    {
        // Arrange
        var reading = TestConfigurationBuilder.ValidScaleDataReading(
            deviceId: "TEST_SCALE_002", 
            weight: 15.67,
            quality: DataQuality.Uncertain);

        // Act
        var result = await _repository.SaveWeighingAsync(reading);

        // Assert
        result.Should().NotBeNull();
        result.DeviceId.Should().Be("TEST_SCALE_002");
        result.WeightValue.Should().Be(15.67m);
        result.IsStable.Should().BeFalse();
        result.Quality.Should().Be(DataQuality.Uncertain.ToString());
    }

    [Fact]
    public async Task SaveWeighingAsync_WithNullReading_ShouldThrowArgumentNullException()
    {
        // Arrange
        ScaleDataReading? nullReading = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => _repository.SaveWeighingAsync(nullReading!));
        
        exception.ParamName.Should().Be("reading");
    }

    #endregion

    #region GetWeighingsAsync Tests (5 tests)

    [Fact]
    public async Task GetWeighingsAsync_WithValidDeviceId_ShouldReturnCorrectReadings()
    {
        // Arrange
        var deviceId = "TEST_SCALE_003";
        var readings = new[]
        {
            TestConfigurationBuilder.ValidScaleDataReading(deviceId, 10.11),
            TestConfigurationBuilder.ValidScaleDataReading(deviceId, 20.22),
            TestConfigurationBuilder.ValidScaleDataReading(deviceId, 30.33)
        };

        foreach (var reading in readings)
        {
            await _repository.SaveWeighingAsync(reading);
        }

        // Act
        var result = await _repository.GetWeighingsAsync(deviceId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().OnlyContain(w => w.DeviceId == deviceId);
        result.Should().BeInDescendingOrder(w => w.Timestamp); // Most recent first
        
        var weights = result.Select(w => w.WeightValue).ToArray();
        weights.Should().Contain(new[] { 10.11m, 20.22m, 30.33m });
    }

    [Fact]
    public async Task GetWeighingsAsync_WithDateRange_ShouldReturnFilteredResults()
    {
        // Arrange
        var deviceId = "TEST_SCALE_004";
        var now = DateTimeOffset.UtcNow;
        
        // Create readings with different timestamps
        var oldReading = TestConfigurationBuilder.ValidScaleDataReading(deviceId, 5.55);
        var recentReading = TestConfigurationBuilder.ValidScaleDataReading(deviceId, 6.66);
        
        await _repository.SaveWeighingAsync(oldReading);
        await Task.Delay(100); // Ensure different timestamps
        await _repository.SaveWeighingAsync(recentReading);

        var fromTime = now.AddMinutes(-1);
        var toTime = now.AddMinutes(1);

        // Act
        var result = await _repository.GetWeighingsAsync(deviceId, fromTime, toTime);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.Should().OnlyContain(w => w.DeviceId == deviceId);
        result.Should().OnlyContain(w => w.Timestamp >= fromTime && w.Timestamp <= toTime);
    }

    [Fact]
    public async Task GetWeighingsAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        var deviceId = "TEST_SCALE_005";
        var readingCount = 10;
        var limit = 3;

        // Create multiple readings
        for (int i = 0; i < readingCount; i++)
        {
            var reading = TestConfigurationBuilder.ValidScaleDataReading(deviceId, i * 1.11);
            await _repository.SaveWeighingAsync(reading);
        }

        // Act
        var result = await _repository.GetWeighingsAsync(deviceId, limit: limit);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(limit);
        result.Should().OnlyContain(w => w.DeviceId == deviceId);
    }

    [Fact]
    public async Task GetWeighingsAsync_WithNonExistentDevice_ShouldReturnEmptyList()
    {
        // Arrange
        var nonExistentDeviceId = "NON_EXISTENT_SCALE";

        // Act
        var result = await _repository.GetWeighingsAsync(nonExistentDeviceId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task GetWeighingsAsync_WithInvalidDeviceId_ShouldThrowArgumentException(string? invalidDeviceId)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _repository.GetWeighingsAsync(invalidDeviceId!));
        
        exception.ParamName.Should().Be("deviceId");
    }

    #endregion

    #region GetLatestWeighingsAsync Tests (1 test)

    [Fact]
    public async Task GetLatestWeighingsAsync_WithMultipleDevices_ShouldReturnLatestForEach()
    {
        // Arrange
        var device1 = "TEST_SCALE_006";
        var device2 = "TEST_SCALE_007";
        
        // Create readings for both devices
        await _repository.SaveWeighingAsync(TestConfigurationBuilder.ValidScaleDataReading(device1, 1.11));
        await _repository.SaveWeighingAsync(TestConfigurationBuilder.ValidScaleDataReading(device2, 2.22));
        
        await Task.Delay(100);
        
        await _repository.SaveWeighingAsync(TestConfigurationBuilder.ValidScaleDataReading(device1, 3.33));

        // Act
        var result = await _repository.GetLatestWeighingsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2); // One latest reading per device
        result.Should().Contain(w => w.DeviceId == device1 && w.WeightValue == 3.33m);
        result.Should().Contain(w => w.DeviceId == device2 && w.WeightValue == 2.22m);
    }

    #endregion

    #region Concurrent Operations Tests (1 test)

    [Fact]
    public async Task ConcurrentSaveOperations_ShouldHandleMultipleThreads()
    {
        // Arrange
        var deviceId = "TEST_SCALE_CONCURRENT";
        var concurrentOperations = 10;
        var tasks = new List<Task<WeighingTransaction>>();

        // Act - Create concurrent save operations
        for (int i = 0; i < concurrentOperations; i++)
        {
            var reading = TestConfigurationBuilder.ValidScaleDataReading(deviceId, i * 1.0);
            tasks.Add(_repository.SaveWeighingAsync(reading));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(concurrentOperations);
        results.Should().OnlyContain(r => r != null);
        results.Should().OnlyContain(r => r.DeviceId == deviceId);
        
        // Verify all were saved to database
        var savedReadings = await _repository.GetWeighingsAsync(deviceId);
        savedReadings.Should().HaveCount(concurrentOperations);
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}