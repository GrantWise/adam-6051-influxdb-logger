# ADAM Scale Logger - Quick Start Guide

Get your industrial scale monitoring running in 5 minutes!

## 🚀 One-Command Deployment

```bash
cd docker-scale
cp .env.template .env
# Edit .env with your ADAM device IP
docker-compose up -d
```

## 📋 Required Configuration

Edit `.env` file:
```bash
ADAM_HOST=192.168.1.101  # ← Your ADAM-4571 IP address
DEVICE_NAME=Scale_Line_1 # ← Descriptive name for your scale
DEVICE_LOCATION=Plant_A  # ← Location identifier
```

## 🔗 Access Points

After startup (wait 30 seconds):

- **Scale Dashboard**: http://localhost:3000 (admin/admin)
- **Health Status**: http://localhost:8080/health  
- **Database**: localhost:5432 (adam_user/adam_secure_password)

## ✅ Verify Installation

1. **Check services are running**:
   ```bash
   docker-compose ps
   ```

2. **Test scale connectivity**:
   ```bash
   docker-compose run --rm adam-scale-logger --test
   ```

3. **View live data**:
   ```bash
   docker-compose logs -f adam-scale-logger
   ```

## 🔧 Quick Commands

```bash
# View logs
docker-compose logs adam-scale-logger

# Restart logger only
docker-compose restart adam-scale-logger

# Stop everything
docker-compose down

# Full cleanup (removes data!)
docker-compose down -v
```

## 🆘 Common Issues

**Scale not connecting?**
- Check IP address in `.env` file
- Verify network connectivity: `ping 192.168.1.101`
- Check ADAM device TCP port (usually 4001)

**No data in dashboard?**
- Wait 60 seconds for data collection
- Check logger logs: `docker-compose logs adam-scale-logger`
- Verify Grafana datasource connection

**Dashboard not loading?**
- Restart Grafana: `docker-compose restart grafana`
- Check Grafana logs: `docker-compose logs grafana`

## 📞 Need Help?

1. Check the full README.md for detailed documentation
2. Review logs: `docker-compose logs [service-name]`
3. Test connectivity: `docker-compose run --rm adam-scale-logger --test`

---

**Next Steps**: See `README.md` for advanced configuration, multiple devices, and production deployment guidance.