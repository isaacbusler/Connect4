using Connect4.ViewModels;
using Connect4.Converters;
using Microsoft.Maui.Controls;
using System.Linq;
using Connect4.Models;

namespace Connect4.Views;

/// <summary>
/// The main page of the Connect4 game, responsible for rendering the UI grid and managing user interactions.
/// </summary>
public partial class MainPage : ContentPage
{
    private Connect4ViewModel vm; /// The ViewModel instance managing game state and logic.
    private readonly Button[,] buttons = new Button[Board.Rows, Board.Cols]; // 2D array to hold references to the grid buttons for easy access.

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class.
    /// Sets up the UI, binds the ViewModel, and builds the game board.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();

        vm = new Connect4ViewModel();
        BindingContext = vm;

        CreateBoardUI();
        BoardGrid.SizeChanged += OnBoardGridSizeChanged;
    }

    /// <summary>
    /// Dynamically creates the grid of buttons that visually represent the Connect4 board.
    /// Binds each button to a cell in the ViewModel.
    /// </summary>
    private void CreateBoardUI()
    {
        BoardGrid.RowDefinitions.Clear();
        BoardGrid.ColumnDefinitions.Clear();

        for (int i = 0; i < Board.Rows; i++)
            BoardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        for (int i = 0; i < Board.Cols; i++)
            BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        for (int row = 0; row < Board.Rows; row++)
        {
            for (int col = 0; col < Board.Cols; col++)
            {
                var btn = new Button
                {
                    BackgroundColor = Colors.LightGray,
                    TextColor = Colors.Transparent,
                    FontSize = 24,
                    Padding = 0,
                    Margin = 0,
                    IsEnabled = false,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };

                var cellVm = vm.Grid.FirstOrDefault(c => c.Row == row && c.Col == col);
                if (cellVm != null)
                {
                    btn.BindingContext = cellVm;
                    btn.SetBinding(Button.BackgroundColorProperty, new Binding("Value", BindingMode.OneWay, converter: new CellValueToSymbolConverter()));
                    btn.Text = "";
                    btn.BorderColor = Colors.Black;
                    btn.BorderWidth = 1;
                }

                Grid.SetRow(btn, row);
                Grid.SetColumn(btn, col);
                BoardGrid.Children.Add(btn);
                buttons[row, col] = btn;
            }
        }
    }

    /// <summary>
    /// Handles resizing of the board grid to ensure square buttons and responsive layout.
    /// Called whenever the size of the grid changes.
    /// </summary>
    private void OnBoardGridSizeChanged(object sender, EventArgs e)
    {
        if (BoardGrid.Width <= 0 || BoardGrid.Height <= 0)
            return;

        double spacingW = (Board.Cols - 1) * BoardGrid.ColumnSpacing;
        double spacingH = (Board.Rows - 1) * BoardGrid.RowSpacing;

        double cellWidth = (BoardGrid.Width - spacingW) / Board.Cols;
        double cellHeight = (BoardGrid.Height - spacingH) / Board.Rows;
        double cellSize = Math.Min(cellWidth, cellHeight);

        for (int row = 0; row < Board.Rows; row++)
        {
            for (int col = 0; col < Board.Cols; col++)
            {
                var btn = buttons[row, col];
                btn.WidthRequest = cellSize;
                btn.HeightRequest = cellSize;
                btn.CornerRadius = (int)(cellSize / 2);
            }
        }

        
        if (ArrowButtonsStack != null)
        {
            
            ArrowButtonsStack.ColumnSpacing = BoardGrid.ColumnSpacing;

            
            foreach (var child in ArrowButtonsStack.Children)
            {
                if (child is Button btn)
                {
                    btn.WidthRequest = cellSize;
                    btn.HeightRequest = 40;
                }
            }
        }
    }
}
