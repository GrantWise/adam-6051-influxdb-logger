# ADAM-6051 InfluxDB Logger

A comprehensive data acquisition and logging solution for ADAM-6051 industrial I/O modules. This repository provides both Python and C# implementations with identical functionality for reading Modbus counter values and logging them to InfluxDB.

## Overview

The ADAM-6051 InfluxDB Logger connects to ADAM-6051 devices via Modbus TCP, reads counter values from configured channels, and logs the data to InfluxDB with comprehensive error handling, data validation, and monitoring capabilities.

### Key Features

- **Dual Language Support**: Complete implementations in both Python and C#
- **Modbus TCP Communication**: Robust connection handling with automatic retry and recovery
- **Data Validation**: Comprehensive validation including range checks, rate limiting, and overflow detection
- **InfluxDB Integration**: Optimized data storage with configurable batching and retention
- **Health Monitoring**: Built-in health checks and performance metrics
- **Industrial Grade**: Designed for 24/7 operation with comprehensive error handling
- **Configurable**: JSON-based configuration for devices, channels, and operational parameters

## Repository Structure

```
adam-6051-influxdb-logger/
├── README.md                    # This file - overview and getting started
├── CLAUDE.md                    # AI assistant development guidelines
├── TESTING_PLAN.md             # Comprehensive testing strategy (C# only)
├── adam-6051-influxdb-logger.md # Detailed technical documentation
├── csharp/                     # C# implementation
│   ├── Industrial.Adam.Logger.sln
│   └── src/
│       ├── Industrial.Adam.Logger/           # Main library
│       ├── Industrial.Adam.Logger.Tests/     # Unit tests (183 tests)
│       ├── Industrial.Adam.Logger.IntegrationTests/  # Integration tests
│       └── Industrial.Adam.Logger.Examples/  # Usage examples
└── python/                     # Python implementation
    ├── adam_counter_logger.py  # Main application
    └── adam_config_json.json   # Configuration example
```

## Quick Start

### Python Implementation

```bash
cd python/
pip install pymodbus influxdb-client
python adam_counter_logger.py
```

### C# Implementation

```bash
cd csharp/
dotnet build
dotnet run --project src/Industrial.Adam.Logger.Examples
```

## Language-Specific Features

### Python Implementation
- **Lightweight**: Single-file implementation with minimal dependencies
- **Rapid Deployment**: Quick setup for smaller installations
- **Cross-Platform**: Runs on Windows, Linux, and macOS
- **Dependencies**: PyModbus, InfluxDB Client

### C# Implementation
- **Enterprise-Ready**: Full solution architecture with comprehensive testing
- **Performance**: Optimized for high-throughput industrial environments
- **Testing**: 183 unit and integration tests providing 100% coverage
- **Architecture**: Clean Architecture with SOLID principles
- **Scalability**: Designed for large-scale industrial deployments
- **Health Monitoring**: ASP.NET Core health checks integration
- **Dependency Injection**: Full DI container support
- **Observability**: Structured logging and metrics

## Configuration

Both implementations use JSON configuration files with identical schema:

```json
{
  "device_ip": "192.168.1.100",
  "device_port": 502,
  "unit_id": 1,
  "poll_interval": 1000,
  "channels": [
    {
      "channel_number": 0,
      "name": "ProductionCounter",
      "register_address": 0,
      "register_count": 2,
      "enabled": true
    }
  ],
  "influxdb": {
    "url": "http://localhost:8086",
    "token": "your-token",
    "org": "your-org", 
    "bucket": "adam-data"
  }
}
```

## Key Capabilities

### Data Acquisition
- **Multi-Channel Support**: Configure multiple counters per device
- **Register Flexibility**: Support for 16-bit, 32-bit, and 64-bit counters
- **Rate Calculation**: Automatic rate-of-change calculation with configurable windows
- **Data Validation**: Range validation, overflow detection, and rate limiting

### Reliability
- **Connection Management**: Automatic reconnection with exponential backoff
- **Error Handling**: Comprehensive error handling with detailed logging
- **Data Integrity**: Validation and quality tracking for all readings
- **Recovery**: Automatic recovery from communication failures

### Monitoring
- **Health Checks**: Built-in health monitoring for devices and services
- **Performance Metrics**: Connection statistics, read rates, and error tracking
- **Alerting**: Configurable alerting for device failures and data quality issues
- **Diagnostics**: Detailed diagnostic information for troubleshooting

## Testing (C# Implementation)

The C# implementation includes comprehensive testing infrastructure:

- **183 Total Tests**: Complete coverage of all components
- **Unit Tests**: 110 tests covering individual components
- **Integration Tests**: 73 tests covering end-to-end scenarios
- **Test Categories**: Configuration, services, data processing, error handling
- **Continuous Integration**: Automated testing on build

Run tests:
```bash
cd csharp/
dotnet test
```

## Documentation

- **[README.md](README.md)**: This overview document
- **[adam-6051-influxdb-logger.md](adam-6051-influxdb-logger.md)**: Detailed technical documentation
- **[TESTING_PLAN.md](TESTING_PLAN.md)**: Comprehensive testing strategy
- **[CLAUDE.md](CLAUDE.md)**: Development guidelines and architectural principles

## Architecture Highlights

### Python Implementation
- **Procedural Design**: Straightforward, easy-to-understand flow
- **Single Responsibility**: Each function has a clear, focused purpose
- **Error Handling**: Comprehensive exception handling with logging

### C# Implementation
- **Clean Architecture**: Separation of concerns with clear boundaries
- **SOLID Principles**: Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion
- **Design Patterns**: Repository, Service, Factory, Observer patterns
- **Dependency Injection**: Full IoC container support
- **Reactive Streams**: Observable data streams for real-time processing

## Support

For questions, issues, or contributions:

1. **Issues**: Use the repository issue tracker
2. **Documentation**: Refer to the comprehensive documentation files
3. **Examples**: Check the examples folder for implementation patterns
4. **Tests**: Review the test files for usage examples and edge cases

## License

This project is designed for industrial automation and logging applications. Please ensure compliance with your organization's security and operational requirements.