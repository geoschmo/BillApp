namespace BillApp.Settings;

/// <summary>
/// Stores user preferences like window size and column widths.
/// </summary>
public class UserSettings
{
    // Window settings
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 700;
    public bool IsMaximized { get; set; } = false;

    // Column widths by view and column header
    public Dictionary<string, Dictionary<string, double>> ColumnWidths { get; set; } = new();

    /// <summary>
    /// Gets or sets column width for a specific view and column.
    /// </summary>
    public double GetColumnWidth(string viewName, string columnHeader, double defaultWidth)
    {
        if (ColumnWidths.TryGetValue(viewName, out var columns) &&
            columns.TryGetValue(columnHeader, out var width))
        {
            return width;
        }
        return defaultWidth;
    }

    /// <summary>
    /// Sets column width for a specific view and column.
    /// </summary>
    public void SetColumnWidth(string viewName, string columnHeader, double width)
    {
        if (!ColumnWidths.ContainsKey(viewName))
        {
            ColumnWidths[viewName] = new Dictionary<string, double>();
        }
        ColumnWidths[viewName][columnHeader] = width;
    }
}
