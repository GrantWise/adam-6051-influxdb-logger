# 🚀 ADAM-6051 Counter Logger - 5-Minute Quick Start

Get your ADAM-6051 industrial counter monitoring running in 5 minutes!

## ⚡ Super Quick Setup

```bash
# 1. Clone or download the project
cd adam-6051-influxdb-logger/docker

# 2. Start everything (uses default ADAM device IP: 192.168.1.100)
docker-compose up -d

# 3. Wait 30 seconds for initialization, then access:
# - Grafana Dashboard: http://localhost:3000 (admin/admin)
# - InfluxDB UI: http://localhost:8086 (admin/admin123)
```

## 🔧 Configure Your ADAM Device

If your ADAM-6051 is NOT at `192.168.1.100`:

```bash
# Option 1: Environment variable (easiest)
ADAM_HOST=192.168.1.50 docker-compose up -d

# Option 2: Edit config file
nano config/adam_config.json  # Change "host": "192.168.1.50"
docker-compose restart adam-logger
```

## ✅ Verify It's Working

```bash
# Check all services are running
docker-compose ps

# Check logger is connecting to your ADAM device
docker-compose logs adam-logger

# You should see:
# ✓ Successfully connected to ADAM-6051 at 192.168.1.100
# ✓ Connected to InfluxDB 2.x bucket: adam_counters
# Channel 0: count=1234, rate=5.2/s
```

## 📊 View Your Data

1. **Open Grafana**: http://localhost:3000 (admin/admin)
2. **Dashboard**: "🏭 ADAM-6051 Industrial Counter Monitor" 
3. **Real-time data**: Updates every 5 seconds

## 🛠️ Common Issues

**No data in dashboard?**
```bash
# Check ADAM device connectivity
ping 192.168.1.100  # Replace with your device IP

# Check logs for connection errors
docker-compose logs adam-logger | grep ERROR
```

**ADAM device at different IP?**
```bash
# Update and restart
echo "ADAM_HOST=192.168.1.50" > .env
docker-compose restart adam-logger
```

**Want different channels?**
```bash
# Edit config/adam_config.json
"channels": [0, 1, 2, 3]  # Monitor channels 0-3
docker-compose restart adam-logger
```

## 🎯 What You Get

- **Real-time counter monitoring** with 5-second updates
- **Professional dashboard** with trends and rates
- **365-day data retention** in InfluxDB
- **Device health monitoring** and alerts
- **One-command setup** and teardown

## 📞 Quick Help

**Stop everything:**
```bash
docker-compose down
```

**Start fresh (removes all data):**
```bash
docker-compose down -v
docker-compose up -d
```

**Update to latest versions:**
```bash
docker-compose pull
docker-compose up -d
```

That's it! Your industrial counter monitoring is now running! 🎉