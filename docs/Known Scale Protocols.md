# Known Scale Protocols and Templates
## Industrial Weight Scale Communication Standards

**Version:** 1.0  
**Date:** July 2025  
**Purpose:** Reference guide for common industrial scale communication protocols

---

## Overview

This document catalogs known communication protocols used by major industrial scale manufacturers. These templates can be used for rapid integration without requiring the protocol discovery process.

## Major Manufacturer Protocols

### 1. Mettler Toledo - MT-SICS Protocol

**Template:** `mettler_toledo_mt_standard.json`

**Characteristics:**
- **Framing:** STX/ETX (Start/End of Text) characters
- **Format:** Binary control characters + ASCII data
- **Example:** `\x02S     12.345 kg \x03\r\n`
- **Stability:** Single character (S=stable, D=dynamic)
- **Precision:** Typically 3 decimal places
- **Command Support:** Full SICS command set (SI, SI T, etc.)

**Common Models:**
- NewClassic Series (ML, MS)
- Excellence Series (XS, XP, XPR)
- Precision Series (PL, PM)
- Adventure Series (AX)

**Integration Notes:**
- Most reliable and well-documented protocol
- Excellent for laboratory applications
- Supports bidirectional communication
- Built-in error checking

---

### 2. A&D Weighing - FX Series

**Template:** `and_weighing_fx_series.json`

**Characteristics:**
- **Format:** CSV-style comma-separated values
- **Example:** `ST,GS,+00123.5,kg`
- **Stability:** Two-character codes (ST, US, QT, etc.)
- **Net/Gross:** Explicit gross/net indication
- **Precision:** Variable, typically 1-2 decimal places

**Common Models:**
- FX-120i, FX-200i, FX-300i
- GF-K Series
- GX-K Series

**Integration Notes:**
- Very consistent across A&D product line
- Easy to parse due to CSV format
- Good for industrial applications
- Reliable and robust

---

### 3. Ohaus - Defender/Ranger Series

**Template:** `ohaus_defender_series.json`

**Characteristics:**
- **Format:** Simple space-padded ASCII
- **Example:** `   123.4 g S\r\n`
- **Alignment:** Right-aligned weight with leading spaces
- **Stability:** Single character at end (S, ?, space)
- **Simplicity:** Minimal formatting, easy to parse

**Common Models:**
- Defender 3000, 5000, 7000
- Ranger 3000, 4000, 7000
- Scout Pro Series
- Navigator Series

**Integration Notes:**
- Simplest protocol to implement
- Good for basic weight reading
- Limited status information
- Widely compatible

---

### 4. Sartorius - Laboratory Standard

**Template:** `sartorius_standard.json`

**Characteristics:**
- **Format:** Mode + Status + Signed Weight + Unit
- **Example:** `N +0001.2345 g \r\n`
- **Precision:** High precision (3-4 decimal places)
- **Mode Indication:** Normal, Tare, Percent modes
- **Laboratory Focus:** Designed for analytical applications

**Common Models:**
- Entris Series
- Practum Series  
- Quintix Series
- Secura Series

**Integration Notes:**
- Excellent for laboratory environments
- High precision measurements
- Comprehensive status reporting
- Good documentation

---

### 5. Avery Weigh-Tronix - Industrial Standard

**Template:** `avery_weigh_tronix_standard.json`

**Characteristics:**
- **Format:** Status + Signed Weight + Unit
- **Example:** `N+0012.34 kg\r\n`
- **Status Codes:** N=normal, O=overload, U=underload, M=motion
- **Industrial Focus:** Robust for harsh environments
- **Simplicity:** Straightforward parsing

**Common Models:**
- ZM Series (truck scales)
- BSQ Series (bench scales)
- PC Series (counting scales)

**Integration Notes:**
- Robust industrial protocol
- Good for heavy-duty applications
- Clear status indication
- Reliable in harsh environments

---

### 6. Generic Chinese Scales

**Template:** `generic_chinese_scales.json`

**Characteristics:**
- **Format:** Prefix + Weight + Unit
- **Example:** `=12.34kg\r\n` or `+12.34kg\r\n`
- **Variability:** Highly inconsistent implementations
- **Simplicity:** Basic weight reporting
- **Documentation:** Often undocumented

**Common Implementations:**
- Various OEM manufacturers
- Generic industrial scales
- Cost-optimized solutions

**Integration Notes:**
- Use as last resort template
- High variability between manufacturers
- Limited status information
- Protocol discovery recommended

---

## Protocol Selection Guide

### By Application Type

| Application | Recommended Protocol | Alternative |
|-------------|---------------------|-------------|
| Laboratory | Mettler Toledo, Sartorius | A&D Weighing |
| Industrial Production | A&D Weighing, Avery W-T | Ohaus |
| Quality Control | Mettler Toledo, Sartorius | A&D Weighing |
| Shipping/Receiving | Avery Weigh-Tronix | Ohaus |
| General Purpose | Ohaus, A&D Weighing | Generic |

### By Precision Requirements

| Precision Level | Recommended Manufacturers |
|-----------------|---------------------------|
| High (0.0001g) | Mettler Toledo, Sartorius |
| Medium (0.01g) | A&D Weighing, Ohaus |
| Standard (0.1g) | Ohaus, Avery Weigh-Tronix |
| Basic (1g) | Generic, Ohaus |

### By Integration Complexity

| Complexity | Protocol | Notes |
|------------|----------|-------|
| Simple | Ohaus, Generic Chinese | Basic weight reading |
| Medium | A&D Weighing, Avery W-T | Good status information |
| Advanced | Mettler Toledo, Sartorius | Full bidirectional communication |

---

## Communication Parameters

### Common Serial Settings

| Manufacturer | Baud Rate | Data Bits | Parity | Stop Bits |
|--------------|-----------|-----------|--------|-----------|
| Mettler Toledo | 9600 | 7 | Even | 1 |
| A&D Weighing | 9600 | 8 | None | 1 |
| Ohaus | 9600 | 8 | None | 1 |
| Sartorius | 9600 | 8 | None | 1 |
| Avery W-T | 9600 | 8 | None | 1 |

### ADAM-4571 Configuration

For all protocols, configure ADAM-4571 as:
- **Mode:** TCP Server (Raw Socket)
- **Port:** 4001 (default)
- **Packing Length:** 0 (immediate forwarding)
- **Force Transmit:** 10ms
- **Delimiter:** None (handled by application)

---

## Template Usage

### 1. Direct Template Application
```bash
# Copy known template to active protocol
cp protocol_templates/mettler_toledo_mt_standard.json active_protocol.json

# Configure application to use template
python adam_scale_discovery.py --template active_protocol.json
```

### 2. Template-Guided Discovery
```bash
# Use template as starting point for discovery
python adam_scale_discovery.py --discover --hint mettler_toledo
```

### 3. Verification Mode
```bash
# Test known template against actual scale
python adam_scale_discovery.py --verify mettler_toledo_mt_standard
```

---

## Integration Best Practices

### 1. Template Validation
- Always verify template against actual hardware
- Test with multiple weight values
- Confirm stability detection works correctly
- Validate unit conversions

### 2. Fallback Strategy
```
1. Try manufacturer-specific template
2. Fall back to generic manufacturer template  
3. Use protocol discovery as last resort
4. Manual configuration if all else fails
```

### 3. Production Deployment
- Test template thoroughly in development
- Validate with full range of expected weights
- Test error conditions (overload, underload)
- Document any customizations made

### 4. Troubleshooting
- Check serial communication parameters first
- Verify ADAM-4571 configuration
- Use oscilloscope/serial monitor for diagnosis
- Compare actual data to template expectations

---

## Future Template Additions

### Planned Additions
- **Rice Lake Weighing Systems** - Industrial truck scales
- **Cardinal Scale** - Various industrial applications  
- **Digi International** - Retail and industrial scales
- **Fairbanks Scales** - Heavy industrial applications
- **PCE Instruments** - Laboratory and industrial

### Custom Template Development
- Document protocol discovery process
- Share templates with community
- Validate with multiple device models
- Maintain version compatibility

---

## Support and Documentation

### Manufacturer Resources
- **Mettler Toledo:** MT-SICS Interface Manual
- **A&D Weighing:** FX-i Series Communication Manual
- **Ohaus:** RS232 Interface Documentation
- **Sartorius:** Data Interface Technical Manual
- **Avery Weigh-Tronix:** Serial Interface Guide

### Community Resources
- Template sharing repository
- Protocol discovery success stories
- Troubleshooting guides
- Integration examples

For additional templates or protocol support, consult manufacturer documentation or use the protocol discovery feature to automatically detect unknown formats.