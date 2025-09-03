using Connect4.Helpers;
using Connect4.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Connect4.ViewModels
{

    /// <summary>
    /// ViewModel class that manages the state and logic of a Connect 4 game.
    /// Implements commands for player moves, AI moves, and game reset.
    /// Notifies the UI of changes through INotifyPropertyChanged.
    ///
    /// The FPGA usage flag controlling hardware acceleration can be toggled
    /// in the <see cref="DropPiece(int)"/> method when calling the AI's GetNextMove function.
    /// </summary>
    public class Connect4ViewModel : INotifyPropertyChanged
    {
        private Board board;
        private AI ai = new AI();

        private RelayCommand<int> dropPieceCommand;
        private RelayCommand resetGameCommand;

        public ObservableCollection<CellViewModel> Grid { get; }
        public ICommand DropPieceCommand => dropPieceCommand;
        public ICommand ResetGameCommand => resetGameCommand;

        private string statusMessage;
        private bool isGameOver;
        private bool AIIsThinking;

        /// <summary>
        /// Gets or sets the current status message displayed in the UI.
        /// </summary>
        /// <remarks>
        /// Notifies the UI when the value changes using <see cref="OnPropertyChanged"/>.
        /// </remarks>
        public string StatusMessage
        {
            get => statusMessage;
            set { statusMessage = value; OnPropertyChanged(); }
        }


        /// <summary>
        /// Gets or sets a value indicating whether the game is over.
        /// </summary>
        /// <remarks>
        /// When the value changes, it notifies the UI via <see cref="OnPropertyChanged"/> and 
        /// updates the availability of related commands like <c>dropPieceCommand</c> and <c>resetGameCommand</c>.
        /// </remarks>
        public bool IsGameOver
        {
            get => isGameOver;
            set
            {
                if (isGameOver != value)
                {
                    isGameOver = value;
                    OnPropertyChanged();
                    dropPieceCommand.RaiseCanExecuteChanged();
                    resetGameCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Connect4ViewModel"/> class.
        /// Sets up the game board, command bindings, and initial status message.
        /// </summary>
        /// <remarks>
        /// Initializes the <see cref="Grid"/> with empty cells and binds commands to game actions.
        /// </remarks>
        public Connect4ViewModel()
        {
            board = new Board();
            Grid = new ObservableCollection<CellViewModel>();
            for (int r = 0; r < Board.Rows; r++)
                for (int c = 0; c < Board.Cols; c++)
                    Grid.Add(new CellViewModel { Row = r, Col = c, Value = Board.Value.Empty });

            dropPieceCommand = new RelayCommand<int>(DropPiece, _ => !IsGameOver && !AIIsThinking);
            resetGameCommand = new RelayCommand(() => ResetGame(), () => IsGameOver);
            StatusMessage = "Your turn! You are red. AI is yellow.";
        }

        /// <summary>
        /// Handles the logic for when the human player attempts to drop a piece in the specified column.
        /// Also triggers the AI to make its move asynchronously after the human move.
        /// </summary>
        /// <param name="col">The column index where the human player wants to drop a piece.</param>
        /// <remarks>
        /// This method checks if the column is full, updates the game board and UI,
        /// evaluates end-of-game conditions, and asynchronously runs the AI move.
        /// After the AI move, it checks again for end-of-game conditions and updates the UI.
        /// </remarks>
        private async void DropPiece(int col)
        {
            if (!board.DropPiece(col, Board.Value.Human))
            {
                StatusMessage = "Column full! Try again.";
                return;
            }
            else
            {
                Debug.WriteLine($"Human piece dropped in col {col}");
            }
            AIIsThinking = true;
            dropPieceCommand.RaiseCanExecuteChanged();
            RefreshGrid();
            if (CheckGameEnd()) return;

            StatusMessage = "AI thinking...";
            await Task.Yield();
            int[] bestMove = new int[1];

            // The last parameter in GetNextMove controls whether the AI evaluation
            // uses FPGA acceleration or not:
            // - true: Use FPGA-accelerated evaluation.
            // - false: Use software-based evaluation running on the CPU.
            //
            // Switch this value to enable or disable FPGA usage.
            await Task.Run(() => ai.GetNextMove(board, bestMove, false));

            if (!board.DropPiece(bestMove[0], Board.Value.AI))
            {
                StatusMessage = "AI couldn't move — tie!";
                IsGameOver = true;
                dropPieceCommand.RaiseCanExecuteChanged();
                return;
            }
            AIIsThinking = false;
            dropPieceCommand.RaiseCanExecuteChanged();
            RefreshGrid();
            if (CheckGameEnd()) return;

            StatusMessage = "Your turn!";
        }

        /// <summary>
        /// Checks whether the game has ended due to a win or tie, and updates the game state and UI accordingly.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the game has ended (win or tie); otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// If the human or AI has won, or if the board is full (tie), this method sets <see cref="IsGameOver"/> to true,
        /// updates the <see cref="StatusMessage"/>, and disables the drop piece command.
        /// </remarks>
        private bool CheckGameEnd()
        {
            var winner = board.GetWinner();

            if (winner == Board.Value.Human)
            {
                StatusMessage = "🎉 You win!";
                IsGameOver = true;
                dropPieceCommand.RaiseCanExecuteChanged();
                return true;
            }
            if (winner == Board.Value.AI)
            {
                StatusMessage = "💀 AI wins!";
                IsGameOver = true;
                dropPieceCommand.RaiseCanExecuteChanged();
                return true;
            }
            if (board.IsFull())
            {
                StatusMessage = "🤝 It's a tie!";
                IsGameOver = true;
                dropPieceCommand.RaiseCanExecuteChanged();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Updates the visual grid to reflect the current state of the board.
        /// </summary>
        /// <remarks>
        /// Iterates through each cell in the grid and updates its value based on the board's current state.
        /// Triggers <c>OnPropertyChanged</c> in <c>CellViewModel</c> when a value has changed to notify the UI.
        /// </remarks>
        private void RefreshGrid()
        {
            foreach (var cell in Grid)
            {
                var newValue = board.GetCell(cell.Row, cell.Col);
                if (cell.Value != newValue)
                    cell.Value = newValue;
            }
        }

        /// <summary>
        /// Resets the game to its initial state.
        /// </summary>
        /// <remarks>
        /// Clears the board, resets game status flags, updates the UI grid,
        /// and sets the status message to indicate a new game has started.
        /// Also ensures that UI commands are updated accordingly.
        /// </remarks>
        private void ResetGame()
        {
            board.Reset();
            IsGameOver = false;
            AIIsThinking = false;
            RefreshGrid();
            StatusMessage = "Your turn! You are red. AI is yellow.";
            dropPieceCommand.RaiseCanExecuteChanged();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string p = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
