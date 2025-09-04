# Connect 4 FPGA + C# Project

This project is a hardware/software hybrid implementation of the classic **Connect 4** game. It combines:

- 🎮 A C# .NET MAUI user interface for human interaction
- ⚙️ An FPGA backend (written in Verilog) that handles some of the AI decisions
- 🔗 SPI communication using an FTDI chip to bridge the C# frontend and Verilog backend

---

## Introduction

This project consists of two main parts. The first of which is a Connect-4 game implemented in .NET MAUI. The game is played between a human player and an AI implemented through the minimax algorithm. In short, the minimax algorithm looks for the best worst-case scenario by looking several moves ahead and deciding whether each combination of moves gives a good or bad outcome. The AI will then make its final move after evaluating those potential outcomes. The more moves ahead the AI looks, the smarter the final move will be, but that is not without a cost. Looking more and more moves ahead will also make the AI decision time increase, which is undesirable. This is where the AI accelerator on the FPGA comes into play. (More details about how the minimax algorithm works can be found here: https://www.freetimelearning.com/artificial-intelligence/minimax-algorithm.)
The role of the FPGA is to speed up the time that is spent determining whether each future combination of moves is good or bad. This is done for each combination of moves within the number-of-moves limit (depth limit), which ends up being an astonishingly large amount. The higher the depth limit, the longer it takes for the AI to make its decision. Because the determination of good or bad moves is done repeatedly, I had the idea of sending each Connect-4 board state to the FPGA with SPI and having it determine whether each board has a good or bad outcome. A good or bad outcome is determined by giving each board a score. The score is calculated by determining how many 2, 3, and 4 in-a-rows there are for both the AI and human. The higher the score, the better that move/board state is for the AI. The lower the score, the better that move/board state is for the human. This score/evaluation determination is implemented in twice: once in the .NET MAUI project if one does not want to use the FPGA and the other in Verilog for the FPGA in which the evaluation is sent back to the project over SPI. The SPI communication on the .NET MAUI side is implemented in two different ways. The first manually changes the pin values (CLK, MOSI, MISO, CS, READY/OUT_ENABLE) on the FTDI chip to communicate data to and from the FPGA. The second uses MPSSE commands to communicate the data which automatically takes care of some of the underlying controls (such as the CLK). In both there are a maximum of 7 boards sent to the FPGA with evaluations computed in parallel to help speed up the process rather then spending those boards over one at a time. Depending on which stage the minimax algorithm is in, the maximum or minimum value of those batch of boards will be sent back to the main program. The amount of boards being sent to the FPGA and whether the minimax algorithm wants a maximum or minimum value are sent over SPI in this format: [is_max_or_min, batch_size, batch].The functionality for each communication implementation and other results are discussed in the Conclusion section.

---

## 🗂 Project Structure

| Folder          | Description                                  |
|-----------------|----------------------------------------------|
| `/Connect4FPGA` | Verilog modules for the Connect 4 game logic |
| `/Connect4`     | C# MAUI UI application + FTDI SPI interface  |

---

## 🔧 Requirements

- **C# Project**
  - .NET 8 SDK
  - MAUI workload installed (`dotnet workload install maui`)
  - FTDI D2XX driver (for SPI)

- **Verilog Project**
  - FPGA Board: Terasic DE0‑Nano (Intel Cyclone IV)
  - FPGA Development Environment: Intel Quartus Prime
  - USB–SPI Bridge Module: FT232H-based adapter (e.g.,-Teyleten Robot FT232H USB to JTAG / SPI / I²C module)

  This module uses the **FT232H** chip with an **MPSSE** engine, enabling USB-to‑SPI communication through FTDI’s D2XX drivers. The adapter is configurable, supports 3.3 V–5 V I/O levels, and works with protocols like SPI, I²C, UART, JTAG, etc. :contentReference[oaicite:0]{index=0}

---

## 🚀 Getting Started

### 1. Build and flash the FPGA
Follow `/verilog/README.md` to synthesize and load the game logic onto your board as well as connect the FTDI pins to the FGPA correctly.

### 2. Build the C# UI
Follow `/csharp/README.md` to build and run the MAUI interface that communicates with the FPGA.

---

## 📡 Communication

The host UI and FPGA communicate over SPI via an FTDI chip in MPSSE mode. The C# code configures the SPI speed, drives the CS line, and reads game results.

---

## 📷 Photos

![Setup](image.jpg)

---

## Conclusion

Ultimately, this project was mostly a success. As was to be expected with constant communication to and from the FPGA, sending data over SPI become the bottleneck for this accelerator. Manually changing the pin values to send and receive data ended up being slower than using the MPSSE commands due to the lack of hardware assistance. However, manually changing the pin values proved to be more reliable, as there was never any error in transmitting data. Unfortunately, the MPSSE commands proved to be less reliable and usually flipped a few of the Connect-4 board bits when being sent to the FPGA. The rest of the communication with MPSSE went perfectly, but due to this error this method of communication usually produced less accurate board evaluations. To conclude, it comes down to a tradeoff between speed and reliability with these two methods of communication. The highest depth limit done within a reasonable time without the FPGA is 9. The highest depth limit done within a reasonable time with the FPGA is 5. While these are not the results I was exactly hoping for, there are further steps that can be taken such as only sending one Connect-4 board that has a depth one less than the depth limit and then building the maximum of 7 boards from that one board on the FPGA to reduce the amount of data sent over SPI. The next step after that would to implement the rest of the minimax algorithm on the FPGA so that only the initial board is sent to the FPGA at the beginning and the final move is sent at the end by the FPGA (essentially moving all the computations to the FPGA). A more reliable FTDI chip would also help reduce the error when using the MPSSE commands. Overall I am satisfied with how this project turned out, as I knew it would be a big undertaking, and it ultimately produces correct results. The future steps I described for this project would further increase the efficiency of this accelerator and give me something to continue working on down the road.