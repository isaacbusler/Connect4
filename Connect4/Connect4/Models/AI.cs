using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FTD2XX_NET;

namespace Connect4.Models
{

    /// <summary>
    /// Implements the AI logic for playing Connect 4 using the Minimax algorithm
    /// with optional alpha-beta pruning and FPGA-accelerated evaluation.
    /// </summary>
    /// <remarks>
    /// This class provides methods to calculate the optimal move for the AI player by
    /// exploring game states to a fixed depth using the Minimax search algorithm. It
    /// supports both software-based and hardware-accelerated (FPGA) evaluation modes.
    ///
    /// Key features:
    /// <list type="bullet">
    ///   <item>
    ///     <description>Minimax with alpha-beta pruning for performance optimization</description>
    ///   </item>
    ///   <item>
    ///     <description>Evaluation function based on heuristics (center control, window scoring)</description>
    ///   </item>
    ///   <item>
    ///     <description>Batch evaluation support for efficient FPGA usage</description>
    ///   </item>
    ///   <item>
    ///     <description>SPI communication with FPGA via FTDI interface</description>
    ///   </item>
    ///   <item>
    ///     <description>Timing statistics for performance profiling</description>
    ///   </item>
    /// </list>
    ///
    /// The search depth limit (DEPTH_LIMIT) controlling how far ahead the Minimax algorithm explores
    /// can be found and adjusted in the <see cref="GetNextMove"/> method.
    /// </remarks>
    public class AI
    {

        private long totalEvalTime = 0; // Total time spent evaluating positions
        private long totalSearchTime = 0; // Total time AI spends determining next move
        private long evalCalls = 0; // Number of times evaluation method is called

        private List<Board> batch = new(); // Initializes the list of board batches to be evaluated

        /// <summary>
        /// Determines the best next move for the current player using the Minimax algorithm,
        /// optionally offloading evaluation to an FPGA. Also records timing and node exploration statistics.
        /// </summary>
        /// <param name="board">The current game board state.</param>
        /// <param name="bestMove">
        /// An array where the best move's coordinates (e.g., row and column) will be stored upon completion.
        /// </param>
        /// <param name="useFPGA">
        /// A flag indicating whether the evaluation function should be accelerated using an FPGA.
        /// </param>
        public void GetNextMove(Board board, int[] bestMove, bool useFPGA)
        {
            // DEPTH_LIMIT controls how many moves ahead the Minimax algorithm searches.
            // A higher value results in a deeper search and potentially stronger play,
            // but increases computation time exponentially.
            // Here, it's set to 5 for a balance between performance and search depth when using the FPGA.
            // Use a depth 9 for a balance between performance and search depth when not using the FPGA.
            const int DEPTH_LIMIT = 9;

            long[] numNodesExplored = new long[1]; // Tracks the number of nodes explored during the search.

            totalEvalTime = 0;   // Total time spent in evaluation functions (e.g., board scoring).
            totalSearchTime = 0; // Total time spent performing the full Minimax search.
            evalCalls = 0;       // Number of evaluation calls made.
            batch.Clear();       // Clear any batch data from previous runs (if applicable).

            var sw = Stopwatch.StartNew();
            Minimax(board, DEPTH_LIMIT, true, bestMove, numNodesExplored, useFPGA);
            sw.Stop();
            totalSearchTime = sw.ElapsedTicks * (1000000000L / Stopwatch.Frequency); // ns

            double percent = (100.0 * totalEvalTime) / totalSearchTime;

            //for debugging
            Debug.WriteLine($"Nodes explored: {numNodesExplored[0]}");
            Debug.WriteLine($"Eval calls: {evalCalls}");
            Debug.WriteLine($"Total search time: {totalSearchTime / 1_000_000.0:F2} ms");
            Debug.WriteLine($"Total eval time: {totalEvalTime / 1_000_000.0:F2} ms");
            Debug.WriteLine($"Evaluation time as % of total: {percent:F2}%");
        }

        /// <summary>
        /// Calculates the score for a specific position on the board in a given direction 
        /// (horizontal, vertical, or diagonal).
        /// </summary>
        /// <param name="board">The board that contains the position being evaluated.</param>
        /// <param name="startRow">The row index of the position being evaluated.</param>
        /// <param name="startCol">The column index of the position being evaluated.</param>
        /// <param name="deltaRow">The row step to determine the direction of evaluation 
        /// (e.g., 0 for horizontal, 1 for diagonal or vertical).</param>
        /// <param name="deltaCol">The column step to determine the direction of evaluation 
        /// (e.g., 1 for horizontal or diagonal, 0 for vertical).</param>
        /// <returns>The calculated score of the specified position in the given direction.</returns>
        private int EvaluateWindow(Board board, int startRow, int startCol, int deltaRow, int deltaCol)
        {
            int aiCount = 0, humanCount = 0, emptyCount = 0; // How many of each peice are in the given 4-piece window

            for (int i = 0; i < 4; i++)
            {
                var cell = board.GetCell(startRow + i * deltaRow, startCol + i * deltaCol);
                switch (cell)
                {
                    case Board.Value.AI:
                        aiCount++; break;
                    case Board.Value.Human:
                        humanCount++; break;
                    case Board.Value.Empty:
                        emptyCount++; break;
                }
            }

            if (aiCount == 4) return 100000; // Winning move for AI
            if (humanCount == 4) return -100000; // Winning move for Human
            if (aiCount == 3 && emptyCount == 1) return 100; // 3 in a row for AI
            if (humanCount == 3 && emptyCount == 1) return -100; // 3 in a row for Human
            if (aiCount == 2 && emptyCount == 2) return 10; // 2 in a row for AI
            if (humanCount == 2 && emptyCount == 2) return -10; // 2 in a row for Human

            return 0;
        }


        /// <summary>
        /// Evaluates a batch of board states and returns the best score depending on the player's turn.
        /// Applies scoring heuristics such as center column preference and window-based pattern analysis.
        /// Also logs timing and debugging info, and optionally prepares data for FPGA evaluation.
        /// </summary>
        /// <param name="batch">A list of board states to evaluate.</param>
        /// <param name="isMax">
        /// A boolean indicating whether the evaluation is for the maximizing player (e.g., AI).
        /// If true, returns the highest score; otherwise, returns the lowest.
        /// </param>
        /// <returns>
        /// The evaluation score: either the maximum or minimum value from the batch depending on <paramref name="isMax"/>.
        /// </returns>
        public double Evaluate(List<Board> batch, bool isMax)
        {
            // Increment evaluation counter (used for performance tracking or debugging)
            evalCalls++;

            // Start timing the evaluation duration
            var sw = Stopwatch.StartNew();

            // List to hold evaluation scores for each board in the batch
            List<int> scores = new();

            // Store board dimensions
            int ROWS = Board.Rows, COLS = Board.Cols;

            // Evaluate each board in the batch
            foreach (var board in batch)
            {
                int score = 0; // Initialize score for current board

                // --- Center Column Bonus ---
                // Favor control of the center column (commonly beneficial in games like Connect Four)
                int centerCol = COLS / 2;
                for (int i = 0; i < ROWS; i++)
                {
                    var cell = board.GetCell(i, centerCol);
                    score += cell switch
                    {
                        Board.Value.AI => 3,     // +3 for AI occupying center
                        Board.Value.Human => -3, // -3 for Human occupying center
                        _ => 0
                    };
                }

                // --- Sliding Window Evaluation ---
                // Analyze all possible 4-cell "windows" in all directions
                for (int row = 0; row < ROWS; row++)
                {
                    for (int col = 0; col < COLS; col++)
                    {
                        if (col + 3 < COLS)
                            score += EvaluateWindow(board, row, col, 0, 1); // Horizontal window
                        if (row + 3 < ROWS)
                            score += EvaluateWindow(board, row, col, 1, 0); // Vertical window
                        if (row + 3 < ROWS && col + 3 < COLS)
                            score += EvaluateWindow(board, row, col, 1, 1); // Diagonal (\) window
                        if (row - 3 >= 0 && col + 3 < COLS)
                            score += EvaluateWindow(board, row, col, -1, 1); // Diagonal (/) window
                    }
                }

                // Add final score for this board to the scores list
                scores.Add(score);
            }

            // Stop the timer and add elapsed ticks (converted to nanoseconds) to totalEvalTime
            sw.Stop();
            totalEvalTime += sw.ElapsedTicks * (1000000000L / Stopwatch.Frequency);

            /*
            // --- Debugging Information ---
            Debug.WriteLine($"batch_size: {batch.Count}");
            Debug.WriteLine($"is_max: {isMax}");

            // Build the byte packet representing this batch for potential SPI communication/logging
            byte[] dataToSend = BuildPacket(batch, isMax);
            Debug.WriteLine("SPI TX: " + BitConverter.ToString(dataToSend));

            // Output the best/worst score, depending on isMax
            if (isMax)
            {
                Debug.WriteLine("result: 0x" + scores.Max().ToString("X")); // Hex format
                Debug.WriteLine("result: " + scores.Max());                 // Decimal
            }
            else
            {
                Debug.WriteLine("result: 0x" + scores.Min().ToString("X")); // Hex format
                Debug.WriteLine("result: " + scores.Min());                 // Decimal
            }

            // Duplicate Min output (optional, maybe an oversight or intentional)
            Debug.WriteLine("result: 0x" + scores.Min().ToString("X"));
            Debug.WriteLine("result: " + scores.Min());

            // Optional: Print each board in the batch for visual inspection
            foreach (Board board in batch)
            {
                PrintBoard(board);
            }
            */

            // Return the best (max) or worst (min) score depending on who's evaluating
            return isMax ? scores.Max() : scores.Min();
        }


        /// <summary>
        /// Initiates the Minimax search algorithm to determine the best move for the current player.
        /// This method starts the recursive evaluation by calling either MaxValue or MinValue,
        /// depending on whose turn it is.
        /// </summary>
        /// <param name="board">The current game board state to evaluate.</param>
        /// <param name="depthLimit">The maximum depth the Minimax search should explore.</param>
        /// <param name="useAlphaBetaPruning">Whether to apply alpha-beta pruning to optimize the search.</param>
        /// <param name="bestMove">
        /// An array to store the best move found. Will be populated with column and row indices.
        /// </param>
        /// <param name="numNodesExplored">
        /// A reference to a counter tracking how many nodes the algorithm has evaluated (for debugging/performance).
        /// </param>
        /// <param name="useFPGA">Whether to use FPGA hardware acceleration for evaluation.</param>
        /// <returns>The evaluation score of the best move found for the current player.</returns>
        public double Minimax(Board board, int depthLimit, bool useAlphaBetaPruning,
                      int[] bestMove, long[] numNodesExplored, bool useFPGA)
        {
            // Clear the current batch used for FPGA or batch evaluation mode
            batch.Clear();

            // Alpha and beta are initialized for alpha-beta pruning
            // Alpha = best score that the maximizer can guarantee so far
            // Beta = best score that the minimizer can guarantee so far
            double alpha = double.NegativeInfinity;
            double beta = double.PositiveInfinity;

            // Decide whether to start with MaxValue or MinValue
            // depending on whose turn it is on the current board
            return board.CurrentTurn == Board.Value.AI
                ? MaxValue(
                    board,
                    depthLimit,              // Depth limit for recursion
                    useAlphaBetaPruning,     // Whether to use pruning
                    bestMove,                // Output: best move [row, col]
                    numNodesExplored,        // Output: tracks how many nodes were searched
                    alpha,
                    beta,
                    0,                       // Initial depth
                    useFPGA                  // Whether to use FPGA-accelerated evaluation
                )
                : MinValue(
                    board,
                    depthLimit,
                    useAlphaBetaPruning,
                    bestMove,
                    numNodesExplored,
                    alpha,
                    beta,
                    0,
                    useFPGA
                );
        }


        /// <summary>
        /// Recursively evaluates the game tree from the minimizing player's perspective (typically the human player),
        /// using the Minimax algorithm with optional alpha-beta pruning and optional FPGA acceleration.
        /// </summary>
        /// <param name="board">The current state of the game board.</param>
        /// <param name="depthLimit">The maximum search depth for the Minimax algorithm.</param>
        /// <param name="useAlphaBetaPruning">Whether to apply alpha-beta pruning to reduce search space.</param>
        /// <param name="bestMove">
        /// An array to store the best move found at the top level (depth 0). Only modified at the root call.
        /// </param>
        /// <param name="numNodesExplored">
        /// A reference to a counter that keeps track of the total number of nodes evaluated in the search.
        /// </param>
        /// <param name="alpha">The current best value that the maximizing player can guarantee.</param>
        /// <param name="beta">The current best value that the minimizing player can guarantee.</param>
        /// <param name="currentDepth">The current depth in the recursive Minimax tree.</param>
        /// <param name="useFPGA">Whether to use FPGA acceleration for evaluating positions in batches.</param>
        /// <returns>The minimum score achievable from the current board state within the given depth limit.</returns>
        private double MinValue(Board board, int depthLimit, bool useAlphaBetaPruning,
                        int[] bestMove, long[] numNodesExplored,
                        double alpha, double beta, int currentDepth, bool useFPGA)
        {
            // --- TERMINAL CASES (Leaf nodes or early stopping) ---
            if (depthLimit == 0 || currentDepth == depthLimit || board.GetWinner() != Board.Value.Empty || board.IsFull())
            {
                if (currentDepth == depthLimit)
                {
                    // If we hit depth limit, defer evaluation for batch processing
                    batch.Add(board);
                    return 0; // Placeholder score until batch is evaluated later
                }

                // If the game has ended or depth = 0, evaluate this board immediately
                return Evaluate(new List<Board> { board }, false); // false = Min's perspective
            }

            // Count node visit for performance profiling
            numNodesExplored[0]++;

            // Initialize value to +infinity (we're minimizing)
            double value = double.PositiveInfinity;

            // Local batch to accumulate boards at depthLimit - 1 for deferred evaluation
            List<Board> localBatch = new();

            // --- Generate all possible moves for Human player ---
            for (int col = 0; col < Board.Cols; col++)
            {
                int row = GetAvailableRow(board, col);
                if (row == -1) continue; // Column is full, skip

                // Clone the board and make the move
                var newBoard = board.GetClone();
                newBoard.MakeMove(row, col, Board.Value.Human);

                // Recurse: either MaxValue or MinValue depending on the turn
                double temp = newBoard.CurrentTurn == Board.Value.AI
                    ? MaxValue(newBoard, depthLimit, useAlphaBetaPruning, bestMove, numNodesExplored, alpha, beta, currentDepth + 1, useFPGA)
                    : MinValue(newBoard, depthLimit, useAlphaBetaPruning, bestMove, numNodesExplored, alpha, beta, currentDepth + 1, useFPGA);

                // Collect boards at depthLimit - 1 for batch processing
                if (currentDepth == depthLimit - 1)
                {
                    localBatch.Add(newBoard);
                }

                // Update value if a lower score is found
                if (temp < value)
                {
                    value = temp;

                    // If we're at the root, store best move
                    if (currentDepth == 0)
                        bestMove[0] = col;
                }

                // Alpha-beta pruning: prune if value is already worse than alpha
                if (useAlphaBetaPruning && value <= alpha)
                    return value;

                // Update beta value
                beta = Math.Min(beta, value);
            }

            // --- Deferred Batch Evaluation at depthLimit - 1 ---
            if (currentDepth == depthLimit - 1 && localBatch.Any())
            {
                value = useFPGA
                    ? GetNextFPGA(localBatch, false) // Offload to FPGA
                    : Evaluate(localBatch, false);   // CPU-based batch evaluation
            }

            return value;
        }





        /// <summary>
        /// Recursively evaluates the game tree from the maximizing player's perspective (typically the AI),
        /// using the Minimax algorithm with optional alpha-beta pruning and optional FPGA acceleration.
        /// </summary>
        /// <param name="board">The current state of the game board.</param>
        /// <param name="depthLimit">The maximum search depth for the Minimax algorithm.</param>
        /// <param name="useAlphaBetaPruning">Whether to apply alpha-beta pruning to reduce the number of nodes explored.</param>
        /// <param name="bestMove">
        /// An array used to store the best move found at the top level (depth 0). Only modified at the root level.
        /// </param>
        /// <param name="numNodesExplored">
        /// A reference to a counter tracking the total number of game states evaluated during the search.
        /// </param>
        /// <param name="alpha">The current best score that the maximizing player (AI) can guarantee.</param>
        /// <param name="beta">The current best score that the minimizing player (Human) can guarantee.</param>
        /// <param name="currentDepth">The current depth in the Minimax recursion tree.</param>
        /// <param name="useFPGA">Whether to use FPGA hardware acceleration to evaluate positions in batches.</param>
        /// <returns>The maximum score achievable from the current board state within the given depth limit.</returns>
        private double MaxValue(Board board, int depthLimit, bool useAlphaBetaPruning,
                        int[] bestMove, long[] numNodesExplored,
                        double alpha, double beta, int currentDepth, bool useFPGA)
        {
            // --- TERMINAL CASES (Leaf nodes or stopping conditions) ---
            if (depthLimit == 0 || currentDepth == depthLimit ||
                board.GetWinner() != Board.Value.Empty || board.IsFull())
            {
                if (currentDepth == depthLimit)
                {
                    // Defer evaluation to batch processing
                    batch.Add(board);
                    return 0; // Placeholder score
                }

                // Game is over or depth = 0, so evaluate immediately
                return Evaluate(new List<Board> { board }, true); // true = Max's perspective
            }

            // Track how many nodes we explore (useful for debugging and performance)
            numNodesExplored[0]++;

            // Initialize to negative infinity since we are maximizing
            double value = double.NegativeInfinity;

            // Local batch for deferred evaluation at depthLimit - 1
            List<Board> localBatch = new();

            // --- Generate all possible moves for the AI player (Maximizer) ---
            for (int col = 0; col < Board.Cols; col++)
            {
                int row = GetAvailableRow(board, col);
                if (row == -1) continue; // Skip full columns

                // Clone the board and make the AI move
                var newBoard = board.GetClone();
                newBoard.MakeMove(row, col, Board.Value.AI);

                // Recurse: decide whether next turn is Max or Min
                double temp = newBoard.CurrentTurn == Board.Value.AI
                    ? MaxValue(newBoard, depthLimit, useAlphaBetaPruning, bestMove, numNodesExplored,
                               alpha, beta, currentDepth + 1, useFPGA)
                    : MinValue(newBoard, depthLimit, useAlphaBetaPruning, bestMove, numNodesExplored,
                               alpha, beta, currentDepth + 1, useFPGA);

                // Collect boards at depthLimit - 1 for deferred evaluation
                if (currentDepth == depthLimit - 1)
                {
                    localBatch.Add(newBoard);
                }

                // Update the best value found so far
                if (temp > value)
                {
                    value = temp;

                    // If we're at the root of the tree, store best move (column)
                    if (currentDepth == 0)
                        bestMove[0] = col;
                }

                // Alpha-beta pruning: prune this branch if it's worse than what Min can already guarantee
                if (useAlphaBetaPruning && value >= beta)
                    return value;

                // Update alpha to the best (highest) value we've seen so far
                alpha = Math.Max(alpha, value);
            }

            // --- Deferred Evaluation at depthLimit - 1 ---
            if (currentDepth == depthLimit - 1 && localBatch.Any())
            {
                value = useFPGA
                    ? GetNextFPGA(localBatch, true)  // Offload to FPGA
                    : Evaluate(localBatch, true);    // CPU-based batch evaluation
            }

            return value;
        }





        /// <summary>
        /// Finds the next available (empty) row in a specified column of the board.
        /// </summary>
        /// <param name="board">The current game board to search within.</param>
        /// <param name="col">The column index to check for available space.</param>
        /// <returns>
        /// The row index of the lowest available position in the specified column,
        /// or -1 if the column is full.
        /// </returns>
        private int GetAvailableRow(Board board, int col)
        {
            for (int r = Board.Rows - 1; r >= 0; r--)
            {
                if (board.GetCell(r, col) == Board.Value.Empty) return r;
            }
            return -1;
        }

        /// <summary>
        /// Prints statistics related to the search and evaluation phases of the AI algorithm,
        /// including the number of evaluation calls, total search time, total evaluation time,
        /// and the percentage of time spent on evaluation.
        /// </summary>
        public void PrintTimingStats()
        {
            double percent = (100.0 * totalEvalTime) / totalSearchTime;
            Console.WriteLine($"Eval calls: {evalCalls}");
            Console.WriteLine($"Total search time: {totalSearchTime / 1_000_000.0:F2} ms");
            Console.WriteLine($"Total eval time: {totalEvalTime / 1_000_000.0:F2} ms");
            Console.WriteLine($"Evaluation time as % of total: {percent:F2}%");
        }



        /// <summary>
        /// Builds a byte array packet containing evaluation information for a batch of boards.
        /// The packet includes whether the evaluation is for a maximizing player, the batch size,
        /// and the serialized board data.
        /// </summary>
        /// <param name="batch">The list of board states to be evaluated.</param>
        /// <param name="isMax">Indicates if the evaluation is for the maximizing player (true) or minimizing player (false).</param>
        /// <returns>A byte array representing the constructed packet to send over SPI or other communication.</returns>
        public byte[] BuildPacket(List<Board> batch, bool isMax)
        {
            // Maximum number of boards expected in a batch
            const int maxBoards = 7;

            // Number of bytes used to represent a single Board object
            const int bytesPerBoard = 11;

            // Total number of bytes required for the full batch (padded to maxBoards)
            const int totalBatchBytes = maxBoards * bytesPerBoard;

            // Convert the current batch of Board objects to a byte array
            byte[] batchData = BatchToByteArray(batch);

            // Calculate how many boards need to be padded (if fewer than maxBoards)
            int boardsToPad = maxBoards - batch.Count;

            // Create a padded byte array large enough to hold maxBoards, filled with 0s by default
            byte[] paddedBatch = new byte[totalBatchBytes];

            // Copy the actual batch data into the correct position of the padded array,
            // leaving the beginning empty (zeroed) for padding
            Array.Copy(batchData, 0, paddedBatch, boardsToPad * bytesPerBoard, batchData.Length);

            // Initialize the final packet:
            // [0] = isMax flag (1 for max, 0 for min)
            // [1] = actual number of boards in this batch
            // [2..] = padded board data
            byte[] packet = new byte[2 + totalBatchBytes];
            packet[0] = (byte)(isMax ? 1 : 0);         // 1st byte indicates max or min mode
            packet[1] = (byte)batch.Count;             // 2nd byte indicates number of actual boards in the batch
            Array.Copy(paddedBatch, 0, packet, 2, totalBatchBytes); // Copy padded data into the packet
            
            /*
            // Debug logs for diagnostics and inspection
            Debug.WriteLine($"batch_size: {batch.Count}");
            Debug.WriteLine($"is_max_or_min: {isMax}");
            Debug.WriteLine($"packet bytes: {BitConverter.ToString(packet)}");
            Debug.WriteLine($"packet binary: {ToBinaryString(packet)}");

            // Optionally print each board in the batch for debugging
            foreach (Board board in batch)
            {
                PrintBoard(board);
            }
            */

            // Return the constructed packet
            return packet;
        }



        /// <summary>
        /// Converts an array of bytes into a string representation of their binary values.
        /// Each byte is represented as an 8-character binary string.
        /// </summary>
        /// <param name="data">The byte array to convert.</param>
        /// <returns>A string containing the binary representation of the input bytes.</returns>
        private string ToBinaryString(byte[] data)
        {
            var sb = new System.Text.StringBuilder(data.Length * 8);
            foreach (byte b in data)
            {
                sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
            }
            return sb.ToString();
        }


        /// <summary>
        /// Converts a list of <see cref="Board"/> instances into a compact byte array representation.
        /// Each cell in the board is encoded using 2 bits, with 4 bits of padding at the start of each board's data.
        /// </summary>
        /// <param name="batch">The list of <see cref="Board"/> objects to convert.</param>
        /// <returns>A byte array representing the batch of boards in a packed binary format.</returns>
        public byte[] BatchToByteArray(List<Board> batch)
        {
            // Dimensions of each board
            int rows = Board.Rows;
            int cols = Board.Cols;

            // Total number of cells on a board
            int cellsPerBoard = rows * cols;

            // Each cell is encoded using 2 bits (00: Empty, 01: AI, 10: Human)
            // Add 4 extra bits (possibly for metadata or padding)
            int bitsPerBoard = (cellsPerBoard * 2) + 4;

            // Calculate number of bytes required to store the bits for each board
            // +7 ensures rounding up to the nearest whole byte
            int bytesPerBoard = (bitsPerBoard + 7) / 8;

            // Total bytes for the full batch
            int totalBytes = batch.Count * bytesPerBoard;
            byte[] data = new byte[totalBytes]; // Final output byte array

            int bitIndex = 0; // Bit index to keep track of where we are writing in the byte array

            foreach (var board in batch)
            {
                // Skip the first 4 bits for each board (reserved/unused/padding)
                bitIndex += 4;

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        // Get the value of the cell at (r, c)
                        Board.Value cell = board.GetCell(r, c);

                        // Encode cell value as 2-bit binary
                        int cellBits = cell switch
                        {
                            Board.Value.Empty => 0b00,
                            Board.Value.AI => 0b01,
                            Board.Value.Human => 0b10,
                            _ => 0b00 // Fallback to empty if unexpected value
                        };

                        // Determine which byte and which bit position to write into
                        int bytePos = bitIndex / 8;         // Target byte index
                        int bitPosInByte = bitIndex % 8;    // Bit offset within the byte

                        // Determine how to shift the 2-bit value into the correct position
                        int shift = 6 - bitPosInByte; // (8 - 2 - bitPosInByte)

                        if (shift >= 0)
                        {
                            // Fits within the current byte
                            data[bytePos] |= (byte)(cellBits << shift);
                        }
                        else
                        {
                            // Spans across two bytes
                            // Write the first part into the current byte
                            data[bytePos] |= (byte)(cellBits >> (-shift));

                            // Write the remaining bits into the next byte
                            data[bytePos + 1] |= (byte)(cellBits << (8 + shift));
                        }

                        // Move to the next 2-bit position for the next cell
                        bitIndex += 2;
                    }
                }
            }

            return data; // Return the encoded byte array for the entire batch
        }





        /// <summary>
        /// Sends a byte array packet to the FPGA via SPI and receives the response.
        /// </summary>
        /// <param name="data">The byte array data packet to send to the FPGA.</param>
        /// <returns>
        /// The byte array response from the FPGA, or <c>null</c> if no response is received or an error occurs.
        /// </returns>
        public byte[] SendByteArrayToFPGA(byte[] data)
        {
            try
            {
                byte[] response = SendSpiPacket(data); // Response from the FPGA
                if (response == null || response.Length == 0)
                {
                    Debug.WriteLine("No response received from FPGA.");
                    return null;
                }
                return response;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SPI communication error: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// Sends a batch of boards to the FPGA for evaluation and retrieves the computed score.
        /// </summary>
        /// <param name="batch">The list of board states to be evaluated by the FPGA.</param>
        /// <param name="isMax">Indicates whether to evaluate for the maximizing player (true) or minimizing player (false).</param>
        /// <returns>
        /// The score returned by the FPGA evaluation. Returns <see cref="double.NegativeInfinity"/> if <paramref name="isMax"/> is true and no valid response is received,
        /// otherwise returns <see cref="double.PositiveInfinity"/> on invalid response.
        /// </returns>
        public double GetNextFPGA(List<Board> batch, bool isMax)
        {
            evalCalls++;
            var sw = Stopwatch.StartNew();

            byte[] dataToSend = BuildPacket(batch, isMax); // Data sent to the FPGA
            byte[] response = SendByteArrayToFPGA(dataToSend); // Response from the FGPA

            if (response == null || response.Length < 4)
            {
                Debug.WriteLine("No valid response from FPGA.");
                return isMax ? double.NegativeInfinity : double.PositiveInfinity;
            }

            Array.Reverse(response);
            int score = BitConverter.ToInt32(response, 0);


            sw.Stop();
            totalEvalTime += sw.ElapsedTicks * (1000000000L / Stopwatch.Frequency);

            /*
            //for debugging
            Debug.WriteLine($"score: {score}");
            */

            return score;
        }

        /// <summary>
        /// Sends an SPI packet to an FPGA using an FTDI device in MPSSE mode, and reads a 33-bit response.
        /// </summary>
        /// <param name="dataToSend">The byte array to transmit over SPI.</param>
        /// <returns>
        /// A 4-byte array containing the realigned 33-bit response from the FPGA (MSB-aligned),
        /// or <c>null</c> if communication failed or the FPGA did not respond in time.
        /// </returns>
        /// <remarks>
        /// This method initializes the FTDI device, sets it into MPSSE mode, configures SPI settings (50 kHz),
        /// and manually toggles chip select (CS) and clock lines as required by the target FPGA protocol.
        /// The FPGA's READY signal is polled before receiving the response.
        /// The response consists of 5 raw bytes (containing 33 bits), which are then realigned to a 4-byte output,
        /// with the first dummy bit discarded and remaining bits left-shifted by 1.
        /// </remarks>
        public byte[] SendSpiPacketMPSSE(byte[] dataToSend)
        {
            var ftdi = new FTDI();
            uint deviceCount = 0;
            ftdi.GetNumberOfDevices(ref deviceCount);
            if (deviceCount == 0) return null;

            var deviceList = new FTDI.FT_DEVICE_INFO_NODE[deviceCount];
            ftdi.GetDeviceList(deviceList);
            if (ftdi.OpenBySerialNumber(deviceList[0].SerialNumber) != FTDI.FT_STATUS.FT_OK) return null;

            ftdi.ResetDevice();
            ftdi.Purge(FTDI.FT_PURGE.FT_PURGE_RX | FTDI.FT_PURGE.FT_PURGE_TX);
            ftdi.SetTimeouts(5000, 5000);
            ftdi.SetLatency(2);
            ftdi.SetBitMode(0x00, FTDI.FT_BIT_MODES.FT_BIT_MODE_MPSSE);
            Thread.Sleep(50);

            // MPSSE setup
            byte[] setup = new byte[] {
                0x8A, 0x97, 0x8D,       // disable loopback, adaptive & 3-phase
                0x86, 0x57, 0x02,       // clock divisor (60MHz / ((1 + 0x0257) * 2) = 50000 Hz) (slow for debugging)
                0x80, 0x08, 0x0B        // CS HIGH idle
            };
            uint bw = 0;
            ftdi.Write(setup, setup.Length, ref bw);

            // Helper to set CS pin
            void SetCS(bool low)
            {
                byte pins = low ? (byte)0x00 : (byte)0x08;
                ftdi.Write(new byte[] { 0x80, pins, 0x0B }, 3, ref bw);
            }

            // 1. Pull CS LOW to start transaction
            SetCS(true);

            // 2. Send 2 dummy clock cycles for correct synchronization
            ftdi.Write(new byte[] { 0x8E, 0x01, 0x00 }, 3, ref bw);

            // 3. Send data
            ushort len = (ushort)(dataToSend.Length - 1);
            byte[] writeCmd = new byte[3 + dataToSend.Length];
            writeCmd[0] = 0x11;
            writeCmd[1] = (byte)(len & 0xFF);
            writeCmd[2] = (byte)(len >> 8);
            Array.Copy(dataToSend, 0, writeCmd, 3, dataToSend.Length);
            ftdi.Write(writeCmd, writeCmd.Length, ref bw);

            // Send 2 more dummy clock cycles needed by the FPGA to finish processing
            ftdi.Write(new byte[] { 0x8E, 0x01, 0x00 }, 3, ref bw);

            // 4. Pull CS HIGH after sending data
            SetCS(false);

            

            // 5. Wait for FPGA READY, toggling clock while CS HIGH
            const int maxWait = 5000;
            int waited = 0;
            bool ready = false;
            while (waited < maxWait)
            {
                ftdi.Write(new byte[] { 0x80, 0x00, 0x0B }, 3, ref bw);
                Thread.Sleep(1);
                ftdi.Write(new byte[] { 0x80, 0x04, 0x0B }, 3, ref bw);
                Thread.Sleep(1);

                byte ps = 0;
                ftdi.GetPinStates(ref ps);
                if ((ps & 0x10) != 0) { ready = true; break; }
                waited += 2;
            }
            if (!ready) { ftdi.Close(); return null; }

            //used for debugging
            //Thread.Sleep(60000);

            ftdi.Purge(FTDI.FT_PURGE.FT_PURGE_RX | FTDI.FT_PURGE.FT_PURGE_TX);

            // 6. Pull CS LOW before clocking in
            SetCS(true);

            // 7. Clock in 33 bits from FPGA
            ftdi.Write(new byte[] {
                0x20, 0x20, 0x00,  // Clock in 33 bits (32 good + 1 dummy)
                0x87              // Send Immediate
            }, 4, ref bw);

            // *** Read 5 bytes from FTDI RX queue ***
            const int R = 5;
            byte[] raw = new byte[R];
            uint br = 0;

            uint rxq = 0;
            int waitR = 0;
            while (waitR < 100 && rxq < R)
            {
                ftdi.GetRxBytesAvailable(ref rxq);
                Thread.Sleep(1);
                waitR++;
            }
            if (rxq < R)
            {
                ftdi.Close();
                return null;
            }

            ftdi.Read(raw, R, ref br);
            if (br != R)
            {
                ftdi.Close();
                return null;
            }

            SetCS(false);

            ftdi.SetBitMode(0x00, FTDI.FT_BIT_MODES.FT_BIT_MODE_RESET);
            ftdi.Close();


            // Re-align bits: drop first bit, shift the rest left by 1 for correct alignment
            byte[] realigned = new byte[4];
            realigned[0] = (byte)((raw[0] << 1) | (raw[1] >> 7));
            realigned[1] = (byte)((raw[1] << 1) | (raw[2] >> 7));
            realigned[2] = (byte)((raw[2] << 1) | (raw[3] >> 7));
            realigned[3] = (byte)((raw[3] << 1) | (raw[4] >> 7));

            /*
            //for dubgging
            Debug.WriteLine("Raw 5-byte FPGA response: " + BitConverter.ToString(raw));
            Debug.WriteLine("Realigned FPGA response: " + BitConverter.ToString(realigned));
            */
            
            return realigned;

        }

        /// <summary>
        /// Sends a byte array to an FPGA device over SPI using an FTDI interface and reads the response.
        /// </summary>
        /// <param name="dataToSend">The byte array containing data to transmit to the FPGA.</param>
        /// <returns>
        /// A byte array containing the FPGA's response. Returns <c>null</c> if no FTDI device is found,
        /// if the device fails to open, or if a timeout or communication error occurs.
        /// </returns>
        /// <remarks>
        /// This method handles SPI communication by manually toggling clock and chip select pins,
        /// sending data MSB first, and reading the response from the FPGA while respecting the READY signal.
        /// </remarks>
        public byte[] SendSpiPacket(byte[] dataToSend)
        {
            FTDI ftdi = new FTDI();
            uint deviceCount = 0;

            // Get FTDI device count
            ftdi.GetNumberOfDevices(ref deviceCount);
            if (deviceCount == 0)
            {
                Debug.WriteLine("No FTDI devices found.");
                return null;
            }

            // Get device list and open first device
            FTDI.FT_DEVICE_INFO_NODE[] deviceList = new FTDI.FT_DEVICE_INFO_NODE[deviceCount];
            ftdi.GetDeviceList(deviceList);
            FTDI.FT_STATUS status = ftdi.OpenBySerialNumber(deviceList[0].SerialNumber);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                Debug.WriteLine("Failed to open FTDI device.");
                return null;
            }

            // Reset and configure device
            ftdi.ResetDevice();
            ftdi.SetTimeouts(5000, 5000);
            ftdi.SetLatency(2);
            ftdi.SetBitMode(0x00, 0x00);
            ftdi.SetBitMode(0x00, 0x02);

            // Pin assignments
            const byte SCLK = 1 << 0;
            const byte MOSI = 1 << 1;
            const byte MISO = 1 << 2;
            const byte CS = 1 << 3;
            const byte READY = 1 << 4;

            // Direction: output pins are CS, SCLK, MOSI
            byte direction = (byte)(CS | SCLK | MOSI);

            // Start with CS high (idle), CLK low, MOSI low
            byte pinState = CS;
            uint bytesWritten = 0;
            byte pins = 0;

            // Helper to write pins state
            void WritePins(byte state)
            {
                ftdi.Write(new byte[] { 0x80, state, direction }, 3, ref bytesWritten);
            }

            // Microsecond delay helper
            void DelayMicroseconds(double microseconds)
            {
                var sw = Stopwatch.StartNew();
                long ticks = (long)(microseconds * Stopwatch.Frequency / 1_000_000.0);
                while (sw.ElapsedTicks < ticks) { }
            }

            // Initialize pins: CS HIGH, CLK LOW, MOSI LOW
            pinState = (byte)(pinState & ~SCLK & ~MOSI);
            WritePins(pinState);

            // Pull CS LOW to start transaction
            pinState = (byte)(pinState & ~CS);
            WritePins(pinState);

            // 2 clock cycles delay (clock toggling, MOSI low)
            for (int i = 0; i < 2; i++)
            {
                // Clock LOW
                pinState = (byte)(pinState & ~SCLK & ~MOSI);
                WritePins(pinState);
                DelayMicroseconds(10);

                // Clock HIGH
                pinState = (byte)(pinState | SCLK);
                WritePins(pinState);
                DelayMicroseconds(10);
            }

            // Send data bits MSB first, clock toggling every 50us
            foreach (byte b in dataToSend)
            {
                for (int bit = 7; bit >= 0; bit--)
                {
                    byte bitVal = (byte)((b >> bit) & 1);
                    // Clock LOW phase: set MOSI bit (Fixed shift to bit 1)
                    pinState = (byte)((pinState & ~SCLK & ~MOSI) | (bitVal << 1));
                    WritePins(pinState);
                    DelayMicroseconds(10);

                    // Clock HIGH phase: MOSI stable
                    pinState = (byte)(pinState | SCLK);
                    WritePins(pinState);
                    DelayMicroseconds(10);
                }
            }

            // Pull CS HIGH immediately after sending data
            pinState = (byte)(pinState | CS);
            WritePins(pinState);

            

            // Wait for FPGA READY to go HIGH, while toggling clock continuously with MOSI low
            const int maxWaitMs = 5000;
            int waited = 0;
            bool isReady = false;

            while (waited < maxWaitMs)
            {
                // Clock LOW phase MOSI LOW
                pinState = (byte)(pinState & ~SCLK & ~MOSI);
                WritePins(pinState);
                DelayMicroseconds(10);

                // Clock HIGH phase MOSI LOW
                pinState = (byte)(pinState | SCLK);
                WritePins(pinState);
                DelayMicroseconds(10);

                ftdi.GetPinStates(ref pins);
                if ((pins & READY) != 0)
                {
                    isReady = true;
                    break;
                }

                Thread.Sleep(1); // Sleep 1 ms to avoid busy wait
                waited += 1;     // Increment waited in milliseconds
            }

            if (!isReady)
            {
                Debug.WriteLine("Timeout waiting for FPGA READY.");
                ftdi.Close();
                return null;
            }

            

            // Pull CS LOW again to start reading response
            pinState = (byte)(pinState & ~CS);
            WritePins(pinState);

            // 1 clock cycle delay (clock toggling, MOSI low)
            for (int i = 0; i < 1; i++)
            {
                // Clock LOW
                pinState = (byte)(pinState & ~SCLK & ~MOSI);
                WritePins(pinState);
                DelayMicroseconds(10);

                // Clock HIGH
                pinState = (byte)(pinState | SCLK);
                WritePins(pinState);
                DelayMicroseconds(10);
            }

            //used for debugging
            //Thread.Sleep(60000);

            const int RESPONSE_SIZE = 4;
            byte[] response = new byte[RESPONSE_SIZE];

            // Read response bits while toggling clock continuously
            for (int bitIndex = 0; bitIndex < RESPONSE_SIZE * 8; bitIndex++)
            {
                // Clock LOW phase MOSI LOW
                pinState = (byte)(pinState & ~SCLK & ~MOSI);
                WritePins(pinState);
                DelayMicroseconds(10);

                // Read MISO on clock LOW phase
                ftdi.GetPinStates(ref pins);
                int bit = (pins & MISO) != 0 ? 1 : 0;

                // Clock HIGH phase MOSI LOW
                pinState = (byte)(pinState | SCLK);
                WritePins(pinState);
                DelayMicroseconds(10);

                // Shift bit into response MSB first
                int byteIndex = bitIndex / 8;
                response[byteIndex] <<= 1;
                response[byteIndex] |= (byte)bit;
            }

            // Wait for READY to go LOW while toggling clock continuously
            waited = 0;
            while (waited < maxWaitMs)
            {
                // Clock LOW phase MOSI LOW
                pinState = (byte)(pinState & ~SCLK & ~MOSI);
                WritePins(pinState);
                DelayMicroseconds(10);

                // Clock HIGH phase MOSI LOW
                pinState = (byte)(pinState | SCLK);
                WritePins(pinState);
                DelayMicroseconds(10);

                ftdi.GetPinStates(ref pins);
                if ((pins & READY) == 0)
                {
                    break;
                }

                Thread.Sleep(1);
                waited += 1;
            }

            // Pull CS HIGH to end transaction
            pinState = (byte)(pinState | CS);
            WritePins(pinState);

            ftdi.Close();

            /*
            //for debugging
            Debug.WriteLine("SPI TX: " + BitConverter.ToString(dataToSend));
            Debug.WriteLine("SPI RX: " + BitConverter.ToString(response));
            */

            return response;
        }

        

        /// <summary>
        /// Prints the current state of the given Connect Four board to the debug output.
        /// </summary>
        /// <param name="board">The <see cref="Board"/> to be printed.</param>
        /// <remarks>
        /// Each cell is represented as follows:
        /// 'X' for AI player pieces, 'O' for Human player pieces, and '.' for empty cells.
        /// Output is written using <see cref="System.Diagnostics.Debug.WriteLine(string)"/>.
        /// </remarks>
        public void PrintBoard(Board board)
        {
            var sb = new StringBuilder();
            for (int r = 0; r < Board.Rows; r++)
            {
                for (int c = 0; c < Board.Cols; c++)
                {
                    var cell = board.GetCell(r, c);
                    sb.Append(cell switch
                    {
                        Board.Value.AI => 'X',
                        Board.Value.Human => 'O',
                        _ => '.'
                    });
                    sb.Append(' ');
                }
                sb.AppendLine();
            }
            sb.AppendLine();
            Debug.WriteLine(sb.ToString());
        }


    }

}