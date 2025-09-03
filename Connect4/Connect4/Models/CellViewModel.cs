using System.ComponentModel;
using System.Runtime.CompilerServices;
using Connect4.Models;

/// <summary>
/// ViewModel representing a single cell in the Connect4 board.
/// Supports data binding and change notification.
/// </summary>
public class CellViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Gets or sets the row index of the cell.
    /// </summary>
    public int Row { get; set; }

    /// <summary>
    /// Gets or sets the column index of the cell.
    /// </summary>
    public int Col { get; set; }

    private Board.Value value; /// Backing field for the Value property.

    /// <summary>
    /// Gets or sets the value of the cell (Empty, Human, or AI).
    /// Notifies UI when the value changes.
    /// </summary>
    public Board.Value Value
    {
        get => value;
        set
        {
            if (this.value != value)
            {
                this.value = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event for the given property name.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed. Auto-filled by compiler.</param>
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
