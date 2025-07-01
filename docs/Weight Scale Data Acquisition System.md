# Weight Scale Data Acquisition System
## Product Requirements Document & Technical Specification

**Version:** 2.0  
**Date:** July 2025  
**Platform:** ADAM-4571 Serial-to-Ethernet Converter

---

## 1. Executive Summary

### 1.1 Purpose
Develop Python and C# applications to acquire weight data from industrial scales via ADAM-4571 RS232-to-Ethernet converters. The system features an innovative protocol discovery mechanism that automatically detects scale data formats through guided interaction with the operator.

### 1.2 Key Features
- Automatic protocol discovery through weight testing
- Transport-agnostic design (TCP/IP, RS232, USB)
- Real-time weight monitoring and stability detection
- Automated data capture and logging
- Service-oriented architecture with separate UI and backend

### 1.3 System Architecture
```
[Industrial Scale] --RS232--> [ADAM-4571] --TCP/IP--> [Backend Service] --API--> [UI Applications]
```

**Important Note:** This system performs protocol discovery and data communication only. It does not calibrate or adjust scale accuracy.

---

## 2. Technical Architecture

### 2.1 ADAM-4571 Configuration
- **Mode:** TCP Server (Raw Socket Mode)
- **Default Port:** 4001
- **Serial Settings:** Match scale configuration
  - Baud Rate: 9600/19200 (scale-dependent)
  - Data Bits: 7 or 8
  - Parity: None/Even/Odd
  - Stop Bits: 1 or 2
- **TCP Settings:**
  - Packing Length: 0 (immediate forwarding)
  - Force Transmit: 10ms
  - Delimiter: None (handled in application)

### 2.2 Service-Oriented Architecture

**Backend Service Components:**
```
Core Services:
├── TcpConnectionManager      # ADAM-4571 communication
├── ProtocolDiscoveryEngine   # Automatic format detection
├── StreamParser              # Real-time data parsing
├── TemplateManager           # Protocol template storage
├── DataRepository            # Weight data persistence
└── ApiGateway               # REST/WebSocket server
```

**Frontend Applications:**
- Web UI (React/Vue)
- Desktop (WPF/WinForms)
- Mobile (React Native)
- Terminal CLI

**API Interface:**
- REST endpoints for configuration and data
- WebSocket for real-time weight streaming
- Transport-agnostic protocol discovery

---

## 3. Core Functionality

### 3.1 Automated Protocol Discovery

**Intelligent Format Detection:**
- No manual protocol configuration required
- Operator-guided weight testing process
- Confidence-based iterative approach
- Transport-agnostic implementation

**Discovery Process:**
1. **Initial Baseline** - Capture empty scale data
2. **Iterative Weight Testing**
   - Add/remove weights as needed
   - System suggests optimal test weights
   - Confidence scores update in real-time
   - Continue until high confidence achieved
3. **Manual Override** - Adjust any detected field
4. **Validation** - Confirm parsing accuracy
5. **Template Storage** - Save for future use

### 3.2 Connection Management
- Configurable IP address and port
- Automatic reconnection with exponential backoff
- Connection status monitoring
- Support for multiple data sources (TCP/Serial/USB)

### 3.3 Real-Time Data Processing
- Continuous weight monitoring
- Stability/motion detection
- Automatic capture on stable weight
- Duplicate capture prevention
- Configurable capture thresholds

### 3.4 Data Features
- Weight value with units
- Stability status
- Tare/Net/Gross indicators
- Counting mode support (if available)
- Timestamped logging
- CSV export functionality

---

## 4. Protocol Discovery System

### 4.1 How It Works

**Ground Truth Approach:**
Instead of blind pattern matching, the system correlates known weight values with data streams:

1. **Operator places known weight** (e.g., 2kg)
2. **System captures data stream**
3. **Operator confirms display reading**
4. **System correlates data with weight**
5. **Process repeats until confident**

**What Gets Detected:**
- Frame boundaries and delimiters
- Weight field location and format
- Decimal position and number encoding
- Stability indicators
- Unit fields
- Update frequency

### 4.2 Confidence Algorithm

**Field-Level Confidence Scoring:**
```
Weight Field: 
- Correlation with user input (40%)
- Decimal position consistency (20%)
- Numeric pattern stability (20%)
- Format validation (20%)

Stability Indicator:
- State change detection (50%)
- Pattern consistency (30%)
- Timing correlation (20%)
```

### 4.3 Transport-Agnostic Design

**Data Stream Providers:**
```
DataStreamProvider (Interface)
├── TcpStreamProvider     # ADAM-4571 connection
├── SerialPortProvider    # Direct RS232
├── UsbSerialProvider     # USB-Serial adapters
├── FileStreamProvider    # Testing/simulation
└── MockStreamProvider    # Development
```

The discovery engine works identically regardless of data source.

---

## 5. User Interface Specifications

### 5.1 Protocol Discovery UI

**Main Discovery Screen:**
```
┌─────────────────────────────────────┐
│ Protocol Detection Progress         │
│                                     │
│ Weight Field:    ████████░░ 85%     │
│ Stability:       ██████░░░░ 60%     │
│ Delimiters:      ██████████ 100%    │
│ Format:          ████████░░ 80%     │
│                                     │
│ Detected: 12.45 kg (Stable)         │
│                                     │
│ Suggestion: Add weight 15-20kg      │
│                                     │
│ [Add Weight] [Remove] [Manual Edit] │
│ [Accept Current Configuration]      │
└─────────────────────────────────────┘
```

### 5.2 Discovery Workflow

1. **Connection Setup**
   - Select data source (TCP/Serial/USB)
   - Enter connection parameters
   - Verify data stream

2. **Interactive Testing**
   - Follow system suggestions
   - Add/remove weights freely
   - Monitor confidence progress
   - Override as needed

3. **Validation**
   - Preview parsed data
   - Test with various weights
   - Save as template

### 5.3 Operational UI

**Weight Monitoring Screen:**
- Large, readable weight display
- Stability indicator (color-coded)
- Connection status
- Capture history
- Export functions

---

## 6. API Specification

### 6.1 Protocol Discovery API

```
POST /api/protocol/discovery/start
Body: { "streamSource": "tcp", "connectionParams": {...} }
Response: { "sessionId": "uuid", "status": "connected" }

POST /api/protocol/discovery/weight
Body: { 
  "sessionId": "uuid",
  "action": "add|remove",
  "displayValue": "12.34",
  "unit": "kg"
}
Response: {
  "confidence": {
    "weight": 85,
    "stability": 70,
    "format": 90,
    "overall": 78
  },
  "suggestion": "Add weight between 20-30kg",
  "canAccept": true,
  "parsedData": {
    "weight": 12.34,
    "stable": true,
    "unit": "kg"
  }
}

POST /api/protocol/discovery/accept
Body: { "sessionId": "uuid", "templateName": "Custom_Scale_001" }
Response: { "templateId": "uuid", "success": true }
```

### 6.2 Operational API

```
GET /api/weight/current
Response: { "weight": 12.34, "unit": "kg", "stable": true }

WebSocket: ws://host/api/weight/stream
Message: { "weight": 12.34, "unit": "kg", "stable": true, "timestamp": "..." }

POST /api/weight/capture
Body: { "manual": false }
Response: { "id": "uuid", "weight": 12.34, "timestamp": "..." }

GET /api/weight/history?from=date&to=date
Response: [ { "id": "uuid", "weight": 12.34, ... }, ... ]
```

---

## 7. Technical Implementation

### 7.1 Protocol Discovery Engine

**Core Algorithm:**
1. Collect baseline (empty scale)
2. For each weight test:
   - Capture transition patterns
   - Identify changed fields
   - Correlate with user input
   - Update confidence scores
3. Suggest optimal next test
4. Generate parser template

**Smart Suggestions:**
- Different magnitudes (1, 10, 100kg)
- Fractional values for decimals
- Near-capacity for field width
- Negative values (if tare available)

### 7.2 Parser Template Format

```json
{
  "protocolId": "uuid",
  "name": "Custom_Scale_XYZ",
  "version": "1.0",
  "transport": {
    "frameDelimiter": "\\r\\n",
    "encoding": "ASCII"
  },
  "fields": [
    {
      "name": "stability",
      "type": "lookup",
      "position": { "start": 0, "length": 2 },
      "values": {
        "ST": "stable",
        "US": "unstable"
      }
    },
    {
      "name": "weight",
      "type": "numeric",
      "position": { "start": 3, "length": 8 },
      "format": {
        "decimal": 2,
        "padding": "space",
        "alignment": "right"
      }
    }
  ]
}
```

### 7.3 Performance Requirements

- Connection establishment: < 5 seconds
- Weight display latency: < 200ms
- Discovery process: < 2 minutes typical
- Memory usage: < 100MB
- CPU usage: < 5% during operation
- Support 24/7 continuous operation

---

## 8. Testing Strategy

### 8.1 Scale Simulator
Develop a simulator that:
- Generates realistic weight data streams
- Simulates various protocols
- Introduces network delays
- Creates error conditions
- Supports automated testing

### 8.2 Test Scenarios
- Protocol discovery validation
- Network interruption recovery
- Long-term stability (24+ hours)
- High-frequency updates
- Edge cases (overflow, negative)
- Multi-client connections

---

## 9. Deployment

### 9.1 Deliverables
- Backend service executable
- Web UI application
- Desktop client (optional)
- Configuration templates
- Documentation suite

### 9.2 System Requirements
- **OS:** Windows 10/11, Linux (Ubuntu 20.04+)
- **Runtime:** .NET 6.0+ or Python 3.8+
- **Memory:** 4GB RAM minimum
- **Network:** Ethernet connection
- **Display:** 1280x720 minimum

---

## 10. Future Enhancements

### Phase 2 Features
- Multi-scale simultaneous monitoring
- Database backend integration
- Advanced analytics and reporting
- Machine learning optimization
- Cloud template repository
- Mobile native applications
- MQTT/OPC UA integration
- Bluetooth/WiFi scale support

---

## Appendix A: Common Scale Formats

| Manufacturer | Format Example | Notes |
|--------------|----------------|-------|
| Mettler Toledo | `\x02S     12.34 kg \x03\r\n` | STX/ETX framing |
| AND | `ST,GS,+00123.5 kg\r\n` | CSV-style |
| Ohaus | `   123.4 g S\r\n` | Space-padded |
| Generic Chinese | `=12.34kg<CR><LF>` | Varies widely |

---

## Appendix B: Setup Best Practices

| Test Type | Purpose | Recommendation |
|-----------|---------|----------------|
| Empty scale | Baseline format | Always start here |
| Small weight | Basic parsing | Use 1-5% of capacity |
| Large weight | Field overflow | Use 50-80% of capacity |
| Fractional | Decimal handling | Use precise value |
| Tare test | Net weighing | If scale supports |
| Motion test | Stability detection | Gentle disturbance |

**Important:** This process configures communication protocol only. For scale calibration or accuracy adjustment, consult your scale manufacturer's documentation and use certified weights.