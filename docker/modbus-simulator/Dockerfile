# Simple Modbus TCP simulator for testing
FROM python:3.11-slim

WORKDIR /app

# Install required packages
RUN pip install pymodbus

# Copy simulator script
COPY modbus_simulator.py .

# Expose Modbus TCP port
EXPOSE 502

# Run the simulator
CMD ["python", "modbus_simulator.py"]