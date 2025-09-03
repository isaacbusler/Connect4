using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Connect4.Models
{
    /// <summary>
    /// Represents the Connect4 game board, including logic for moves, turn management,
    /// win detection, and board cloning/resetting.
    /// </summary>
    public class Board
    {
        /// <summary>
        /// Represents the possible values of a cell on the board.
        /// </summary>
        public enum Value
        {
            Empty,
            AI,
            Human
        }

        public const int Rows = 6; // Standard Connect4 has 6 rows
        public const int Cols = 7; // Standard Connect4 has 7 columns

        private Value[,] board = new Value[Rows, Cols]; // 2D array to represent the board
        private Value currentTurn; // Tracks whose turn it is

        /// <summary>
        /// Initializes a new instance of the <see cref="Board"/> class with an empty board
        /// and sets the first turn to the human player.
        /// </summary>
        public Board()
        {
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Cols; j++)
                {
                    board[i, j] = Value.Empty;
                }
            }
            currentTurn = Value.Human;
        }

        /// <summary>
        /// Gets the value of the specified cell.
        /// </summary>
        /// <param name="row">The row index.</param>
        /// <param name="col">The column index.</param>
        /// <returns>The value of the cell.</returns>
        public Value GetCell(int row, int col)
        {
            ValidateCell(row, col);
            return board[row, col];
        }

        /// <summary>
        /// Sets the value of the specified cell.
        /// </summary>
        /// <param name="row">The row index.</param>
        /// <param name="col">The column index.</param>
        /// <param name="value">The value to set.</param>
        public void SetCell(int row, int col, Value value)
        {
            ValidateCell(row, col);
            board[row, col] = value;
        }

        /// <summary>
        /// Validates the specified cell coordinates to ensure they are within the valid range.
        /// </summary>
        /// <param name="row">The zero-based row index of the cell to validate.</param>
        /// <param name="col">The zero-based column index of the cell to validate.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="row"/> or <paramref name="col"/> is outside the valid range of cell coordinates.</exception>
        private void ValidateCell(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Cols)
            {
                throw new ArgumentException("Invalid cell coordinates");
            }
        }

        /// <summary>
        /// Checks whether a move to the specified cell is legal.
        /// </summary>
        public bool IsLegalMove(int row, int col)
        {
            return IsInBounds(row, col) && board[row, col] == Value.Empty;
        }

        /// <summary>
        /// Returns whether the given cell is within the bounds of the board.
        /// </summary>
        public bool IsInBounds(int row, int col)
        {
            return row >= 0 && row < Rows && col >= 0 && col < Cols;
        }

        /// <summary>
        /// Attempts to place a piece at the specified row and column for a player.
        /// Also switches turns after a successful move.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the move is not legal.</exception>
        public void MakeMove(int row, int col, Value player)
        {
            if (!IsLegalMove(row, col))
            {
                throw new ArgumentException($"Illegal move at ({row}, {col})");
            }

            board[row, col] = player;
            SwitchTurn();
        }

        /// <summary>
        /// Attempts to drop a piece in the specified column for a player.
        /// Places the piece in the lowest available row in that column.
        /// </summary>
        /// <param name="col">The column index.</param>
        /// <param name="player">The player making the move.</param>
        /// <returns>True if the move was successful; false if the column is full or invalid.</returns>
        public bool DropPiece(int col, Value player)
        {
            if (col < 0 || col >= Cols) return false;

            for (int row = Rows - 1; row >= 0; row--)
            {
                if (board[row, col] == Value.Empty)
                {
                    MakeMove(row, col, player);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the board is completely full (no empty cells in the top row).
        /// </summary>
        public bool IsFull()
        {
            for (int col = 0; col < Cols; col++)
            {
                if (board[0, col] == Value.Empty)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the player whose turn it currently is.
        /// </summary>
        public Value CurrentTurn => currentTurn;

        /// <summary>
        /// Switches the current turn from Human to AI or AI to Human.
        /// </summary>
        public void SwitchTurn()
        {
            currentTurn = currentTurn == Value.Human ? Value.AI : Value.Human;
        }

        /// <summary>
        /// Checks the board for a winner (4 in a row in any direction).
        /// </summary>
        /// <returns>
        /// The <see cref="Value"/> representing the winning player,
        /// or <see cref="Value.Empty"/> if there is no winner yet.
        /// </returns>
        public Value GetWinner()
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    Value player = board[row, col];
                    if (player == Value.Empty) continue;

                    // Horizontal
                    if (col <= Cols - 4 &&
                        player == board[row, col + 1] &&
                        player == board[row, col + 2] &&
                        player == board[row, col + 3])
                    {
                        return player;
                    }

                    // Vertical
                    if (row <= Rows - 4 &&
                        player == board[row + 1, col] &&
                        player == board[row + 2, col] &&
                        player == board[row + 3, col])
                    {
                        return player;
                    }

                    // Diagonal \
                    if (row <= Rows - 4 && col <= Cols - 4 &&
                        player == board[row + 1, col + 1] &&
                        player == board[row + 2, col + 2] &&
                        player == board[row + 3, col + 3])
                    {
                        return player;
                    }

                    // Diagonal /
                    if (row >= 3 && col <= Cols - 4 &&
                        player == board[row - 1, col + 1] &&
                        player == board[row - 2, col + 2] &&
                        player == board[row - 3, col + 3])
                    {
                        return player;
                    }
                }
            }

            return Value.Empty;
        }

        /// <summary>
        /// Returns a deep copy of the board, preserving the current state and turn.
        /// </summary>
        /// <returns>A new <see cref="Board"/> instance with copied data.</returns>
        public Board GetClone()
        {
            var clone = new Board();
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Cols; j++)
                {
                    clone.board[i, j] = this.board[i, j];
                }
            }
            clone.currentTurn = this.currentTurn;
            return clone;
        }

        /// <summary>
        /// Resets the board to its initial empty state and sets turn to human.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Cols; j++)
                {
                    board[i, j] = Value.Empty;
                }
            }
            currentTurn = Value.Human;
        }

        /// <summary>
        /// Returns a string representation of the board, useful for debugging.
        /// </summary>
        /// <returns>A string showing the board layout with symbols: '.' for empty, 'H' for Human, 'A' for AI.</returns>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(new string('#', Cols + 2));
            for (int i = 0; i < Rows; i++)
            {
                sb.Append("#");
                for (int j = 0; j < Cols; j++)
                {
                    switch (board[i, j])
                    {
                        case Value.Empty:
                            sb.Append(".");
                            break;
                        case Value.AI:
                            sb.Append("A");
                            break;
                        case Value.Human:
                            sb.Append("H");
                            break;
                    }
                }
                sb.AppendLine("#");
            }
            sb.AppendLine(new string('#', Cols + 2));
            return sb.ToString();
        }

    }
}
