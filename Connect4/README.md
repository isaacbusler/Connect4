# C# MAUI Frontend – Connect 4 UI

This is the user interface layer of the project. It allows a human player to play Connect 4 against an AI opponent via an optional FTDI-based SPI connection.

---

## 📋 Features

- Fully interactive 7x6 Connect 4 grid
- Color-coded game pieces
- Game status display (turns, win/loss)
- SPI communication with FPGA via FTDI (MPSSE mode)
- Reset functionality

---

## 🧰 Requirements

- .NET 8 SDK
- MAUI workload installed:
  ```bash
  dotnet workload install maui
  ```

---

## ⚙️ AI Search Depth Configuration (`DEPTH_LIMIT`)

The AI's decision-making strength and speed are controlled by a parameter called `DEPTH_LIMIT`, which determines how many moves ahead the AI explores using the Minimax algorithm.

### Where is `DEPTH_LIMIT` set?

The `DEPTH_LIMIT` constant is located inside the `GetNextMove` method of the `AI` class:

```csharp
const int DEPTH_LIMIT = 5;
```

---

## ⚙️ AI Evaluation Mode Configuration (`useFPGA`)

The AI’s evaluation function can run either on the CPU (software-based) or be accelerated using an FPGA hardware module. This behavior is controlled by the `useFPGA` boolean parameter passed to the AI’s `GetNextMove` method.

### Where is `useFPGA` used?

The `useFPGA` parameter is passed into the `GetNextMove` method of the `AI` class:

```csharp
public void GetNextMove(Board board, int[] bestMove, bool useFPGA)
```

---

## 🧠 Notes

- SPI communication with manually changing the pins is used by default.
- To change the communication from manually changing the pins to MPSSE, use the function `SendSpiPacketMPSSE` instead of `SendSpiPacket`.
- To change the speed of communication within `SendSpiPacket`, change the parameter value within each `DelayMicroseconds` function call.