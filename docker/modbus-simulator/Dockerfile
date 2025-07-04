# Simple Modbus TCP simulator for testing
FROM python:3.11-slim

WORKDIR /app

# Install required packages with specific version
RUN pip install pymodbus==3.5.4

# Copy simulator script
COPY modbus_simulator.py .

# Expose Modbus TCP port
EXPOSE 502

# Run the simulator
CMD ["python", "modbus_simulator.py"]