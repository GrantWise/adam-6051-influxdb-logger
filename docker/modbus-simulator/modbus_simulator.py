#!/usr/bin/env python3
"""
Simple Modbus TCP simulator for ADAM-6051 testing
Simulates a counter that increments over time
"""

import time
import threading
from pymodbus.server import StartTcpServer
from pymodbus.device import ModbusDeviceIdentification
from pymodbus.datastore import ModbusSequentialDataBlock, ModbusSlaveContext, ModbusServerContext

def update_counter(context):
    """Update counter values continuously"""
    counter = 0
    while True:
        try:
            # Simulate a 32-bit counter (2 registers)
            # Register 0: Low word, Register 1: High word
            low_word = counter & 0xFFFF
            high_word = (counter >> 16) & 0xFFFF
            
            # Update holding registers 0 and 1
            context[1].setValues(3, 0, [low_word, high_word])
            
            print(f"Counter updated: {counter} (Low: {low_word}, High: {high_word})")
            
            # Increment counter (simulate production count)
            counter = (counter + 1) % (2**32)  # 32-bit counter with rollover
            
            time.sleep(2)  # Update every 2 seconds
            
        except Exception as e:
            print(f"Error updating counter: {e}")
            time.sleep(1)

def main():
    print("Starting ADAM-6051 Modbus TCP Simulator...")
    print("Listening on port 502")
    print("Unit ID: 1")
    print("Counter registers: 0-1 (32-bit counter)")
    
    # Initialize data store with some default values
    store = ModbusSlaveContext(
        di=ModbusSequentialDataBlock(0, [0] * 100),     # Discrete inputs
        co=ModbusSequentialDataBlock(0, [0] * 100),     # Coils  
        hr=ModbusSequentialDataBlock(0, [0] * 100),     # Holding registers
        ir=ModbusSequentialDataBlock(0, [0] * 100)      # Input registers
    )
    
    # Single slave context (Unit ID 1)
    context = ModbusServerContext(slaves={1: store}, single=False)
    
    # Device identification
    identity = ModbusDeviceIdentification()
    identity.VendorName = 'ADAM Simulator'
    identity.ProductCode = '6051'
    identity.VendorUrl = 'http://localhost'
    identity.ProductName = 'ADAM-6051 Counter Simulator'
    identity.ModelName = 'ADAM-6051'
    identity.MajorMinorRevision = '1.0'
    
    # Start counter update thread
    counter_thread = threading.Thread(target=update_counter, args=(context,))
    counter_thread.daemon = True
    counter_thread.start()
    
    # Start Modbus TCP server
    StartTcpServer(
        context=context, 
        identity=identity, 
        address=("0.0.0.0", 502),
        allow_reuse_address=True
    )

if __name__ == "__main__":
    main()