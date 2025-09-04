# Verilog FPGA Logic – Connect 4 AI

This folder contains Verilog source files implementing the core Connect 4 game engine, including:

- Connect 4 game board state evaluation
- SPI interface for communication with host

---


## ⛓ Files Overview

| File                | Description                                      |
|---------------------|--------------------------------------------------|
| DE0_Nano.v          | Top-level module                                 |
| SPI.v               | SPI interface module                             |
| Evaluation.v        | Board state evaluation                           |
| FinalEvaluation.v   | Integrates SPI and evaluation processes          |

---

## 🛠 How to Build

1. Open Quartus
2. Create a new project and add all `.v` files
3. Assign correct pins for:
   - SPI SCLK, MOSI, MISO, CS
   - Optional debug LEDs
4. Synthesize, compile, and flash to your FPGA

---

## 🔌 FPGA to FTDI SPI Wiring

The FPGA communicates with the host PC via SPI using an FTDI chip configured in MPSSE mode. Below is the wiring scheme for connecting the FPGA’s top-level signals (`DE0_Nano` module) to the FTDI pins:

| FPGA Signal          | FPGA Pin (`GPIO_0`) | FTDI Signal      | Description                                        |
|---------------------|----------------------|------------------|----------------------------------------------------|
| `master_clk`        | `GPIO_0[0]`          | SCLK             | SPI Clock (Master Clock)                           |
| `d_in`              | `GPIO_0[1]`          | MOSI             | Master Out, Slave In (FPGA receives data)          |
| `d_out`             | `GPIO_0[2]`          | MISO             | Master In, Slave Out (FPGA sends data)             |
| `cs`                | `GPIO_0[3]`          | CS (Chip Select) | SPI Chip Select signal                             |
| `out_enable`        | `GPIO_0[4]`          | -                | Output Enable for SPI data line (optional control) |

### Notes:

- The FTDI chip is set up in MPSSE mode, handling SPI communication with the FPGA.
- `master_clk` is driven by the FTDI to clock data transfers.
- `d_in` and `d_out` form the SPI MOSI and MISO lines, respectively.
- `cs` is the chip select signal from FTDI to FPGA, indicating active SPI communication.
- The `out_enable` signal signals to the host that the FPGA is ready to send the evaluation.

### FTDI Pin Mapping:

| FTDI Pin | Signal             |
|----------|--------------------|
| Pin 0    | SCLK               |
| Pin 1    | MOSI               |
| Pin 2    | MISO               |
| Pin 3    | CS                 |
| Pin 4    | READY (out_enable) |

This wiring ensures smooth SPI communication between the PC host and the FPGA evaluation engine, enabling hardware-accelerated Connect 4 AI evaluations.

---

## 🧪 Testing

You can simulate the logic using:
- ModelSim / Questa (Intel/Altera)
- Or test directly on hardware using the C# host

---

## 🧠 Notes

- All evaluation logic runs in hardware
- SPI protocol uses a simple command/data format — see C# host code for command sequence