using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Connect4.Models;

namespace Connect4.Converters;

/// <summary>
/// Converts a Connect4 cell value (Human, AI, or Empty) to a corresponding color for UI rendering.
/// </summary>
public class CellValueToSymbolConverter : IValueConverter
{
    /// <summary>
    /// Converts a <see cref="Board.Value"/> (cell value) to a <see cref="Color"/> for display in the UI.
    /// </summary>
    /// <param name="value">The cell value, expected to be of type <see cref="Board.Value"/>.</param>
    /// <param name="targetType">The type of the binding target property (unused).</param>
    /// <param name="parameter">Optional parameter (unused).</param>
    /// <param name="culture">The culture to use in the converter (unused).</param>
    /// <returns>
    /// A <see cref="Color"/>: 
    /// <list type="bullet">
    ///   <item><see cref="Colors.Red"/> for Human player</item>
    ///   <item><see cref="Colors.Yellow"/> for AI player</item>
    ///   <item><see cref="Colors.LightGray"/> for empty cells or unrecognized input</item>
    /// </list>
    /// </returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Board.Value cellValue)
        {
            return cellValue switch
            {
                Board.Value.Human => Colors.Red,
                Board.Value.AI => Colors.Yellow,
                _ => Colors.LightGray
            };
        }

        return Colors.LightGray;  // fallback
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
