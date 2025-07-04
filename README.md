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
├── EXAMPLES.md                  # Comprehensive C# usage examples
├── Industrial.Adam.Logger.sln   # Main C# solution
├── src/                         # C# implementation (main)
│   ├── Industrial.Adam.Logger/           # Core library with InfluxDB integration
│   ├── Industrial.Adam.Logger.Tests/     # Unit tests (183 tests)
│   ├── Industrial.Adam.Logger.IntegrationTests/  # Integration tests
│   └── Industrial.Adam.Logger.Examples/  # Usage examples
├── docs/                        # Documentation
│   ├── adam-6051-influxdb-logger.md      # Technical documentation
│   └── TESTING_PLAN.md                   # Testing strategy
├── docker/                      # Docker deployment (InfluxDB + Grafana)
└── python/                      # Python implementation (alternative)
    ├── adam_counter_logger.py   # Lightweight Python version
    └── adam_config_json.json    # Configuration example
```

## Quick Start

### 🐳 Docker Deployment (Recommended)

**Complete monitoring stack with InfluxDB + Grafana:**

```bash
# 1. Clone the repository
git clone https://github.com/GrantWise/adam-6051-counter-logger.git
cd adam-6051-counter-logger

# 2. Start the monitoring infrastructure
cd docker
docker-compose up -d

# 3. Verify services are running
docker-compose ps

# 4. Access the dashboards
# - Grafana: http://localhost:3000 (admin/admin)
# - InfluxDB: http://localhost:8086 (admin/admin123)
```

**The C# logger runs automatically in Docker! 🎉**

```bash
# 5. Configure your ADAM device IP (optional)
echo "ADAM_HOST=192.168.1.100" > docker/.env
echo "ADAM_UNIT_ID=1" >> docker/.env
echo "POLL_INTERVAL=2000" >> docker/.env

# 6. Restart with new configuration
cd docker
docker-compose restart adam-logger

# 7. View C# logger logs
docker-compose logs -f adam-logger
```

### 💻 Local Development

#### Python Implementation

```bash
cd python/
pip install pymodbus influxdb-client
python adam_counter_logger.py
```

#### C# Implementation (Primary)

```bash
dotnet build
dotnet run --project src/Industrial.Adam.Logger.Examples
```

## Language-Specific Features

### Python Implementation
- **Lightweight**: Single-file implementation with minimal dependencies
- **Rapid Deployment**: Quick setup for smaller installations
- **Cross-Platform**: Runs on Windows, Linux, and macOS
- **Dependencies**: PyModbus, InfluxDB Client

### C# Implementation (Primary)
- **Enterprise-Ready**: Full solution architecture with comprehensive testing
- **InfluxDB Integration**: Native InfluxDB client with batching and retry logic
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

## 🐳 Docker Deployment

### Prerequisites

- Docker and Docker Compose installed
- ADAM-6051 device accessible on your network
- Ports 3000 (Grafana) and 8086 (InfluxDB) available

### Infrastructure Components

The Docker stack includes:

- **InfluxDB 2.7**: Time-series database for counter data storage
- **Grafana 12.0**: Real-time dashboard and visualization  
- **ADAM Logger**: C# .NET 8 application with InfluxDB integration

### Setup Instructions

1. **Clone and Navigate:**
   ```bash
   git clone https://github.com/GrantWise/adam-6051-counter-logger.git
   cd adam-6051-counter-logger/docker
   ```

2. **Configure Environment (Optional):**
   ```bash
   # Create environment file for custom settings
   cp .env.template .env
   
   # Edit with your device settings
   echo "ADAM_HOST=192.168.1.100" >> .env
   echo "ADAM_UNIT_ID=1" >> .env
   echo "POLL_INTERVAL=5.0" >> .env
   ```

3. **Start the Stack:**
   ```bash
   # Start all services
   docker-compose up -d
   
   # View logs
   docker-compose logs -f
   
   # Check service status
   docker-compose ps
   ```

4. **Access Services:**
   - **Grafana Dashboard**: http://localhost:3000
     - Username: `admin`
     - Password: `admin`
   - **InfluxDB Console**: http://localhost:8086
     - Username: `admin`
     - Password: `admin123`
     - Organization: `adam_org`
     - Bucket: `adam_counters`

### Data Flow

```
ADAM-6051 Device → C# Logger (Docker) → InfluxDB → Grafana Dashboard
                       ↓                    ↓
              Modbus TCP/502        Time-series DB
                                   adam_counters
                      
              Real-time monitoring with:
              • Counter values & rates
              • Device health status  
              • System performance metrics
              • Data quality indicators
```

### Configuration

The Docker stack automatically configures the C# logger via environment variables:

```bash
# Device configuration
ADAM_HOST=192.168.1.100        # Your ADAM device IP
ADAM_UNIT_ID=1                 # Modbus unit ID  
POLL_INTERVAL=2000             # Polling interval in ms
LOG_LEVEL=Information          # Logging level

# InfluxDB connection (auto-configured)
# - URL: http://influxdb:8086
# - Token: adam-super-secret-token
# - Organization: adam_org
# - Bucket: adam_counters
```

**For external C# applications**, use this configuration:

```csharp
config.InfluxDb = new InfluxDbConfig
{
    Url = "http://localhost:8086",
    Token = "adam-super-secret-token",
    Organization = "adam_org",
    Bucket = "adam_counters", 
    Measurement = "counter_data"
};
```

### Docker Commands

```bash
# Start services
docker-compose up -d

# Stop services  
docker-compose down

# View logs
docker-compose logs grafana
docker-compose logs influxdb

# Restart a service
docker-compose restart adam-logger

# Update and rebuild
docker-compose pull && docker-compose up -d
```

### Troubleshooting

**Services won't start:**
```bash
# Check port conflicts
netstat -tulpn | grep -E ':(3000|8086)'

# View detailed logs
docker-compose logs
```

**Can't connect to ADAM device:**
```bash
# Test network connectivity
docker-compose exec adam-logger ping 192.168.1.100

# Check device configuration
docker-compose exec adam-logger cat /app/config/adam_config.json
```

**Dashboard shows no data:**
1. Verify InfluxDB has data: http://localhost:8086/orgs/adam_org/data-explorer
2. Check C# logger is writing to InfluxDB
3. Verify Grafana datasource connection

### Persistent Data

Data is automatically persisted in Docker volumes:
- `influxdb_data`: Time-series data storage
- `grafana_data`: Dashboard configurations and user settings

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
dotnet test
```

## Documentation

- **[README.md](README.md)**: This overview document
- **[EXAMPLES.md](EXAMPLES.md)**: Comprehensive C# usage examples
- **[docs/adam-6051-influxdb-logger.md](docs/adam-6051-influxdb-logger.md)**: Detailed technical documentation
- **[docs/TESTING_PLAN.md](docs/TESTING_PLAN.md)**: Comprehensive testing strategy
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