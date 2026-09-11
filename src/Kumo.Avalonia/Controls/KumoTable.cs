using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace KumoThemeSupport.Controls;

/// <summary>
/// A named, sized table column. <see cref="Width"/> is a shared
/// <see cref="GridLength"/> applied to the header row and every data row,
/// so all columns line up exactly whether or not the selection column
/// is visible.
/// </summary>
public class KumoTableColumn
{
    public string Header { get; init; } = string.Empty;
    public GridLength Width { get; init; } = GridLength.Star;
    public bool AlignRight { get; init; }
    public bool Mono { get; init; }
}

/// <summary>
/// One data row. <see cref="Cells"/> supplies one value per declared
/// <see cref="KumoTableColumn"/>; <see cref="IsSelected"/> drives the row
/// tint and feeds the header select-all checkbox.
/// </summary>
public class KumoTableRow : AvaloniaObject
{
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<KumoTableRow, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    [TypeConverter(typeof(KumoCellsConverter))]
    public IReadOnlyList<object?> Cells { get; init; } = Array.Empty<object?>();
}

/// <summary>Splits comma-separated XAML text ("a,b,c") into row cells.</summary>
public class KumoCellsConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        ((string)value).Split(',');
}
/// <summary>
/// A Kumo table: one shared column plan for header + rows (columns always
/// line up), an optional checkbox selection column with a tri-state
/// select-all header, alternating row tints and tap-to-toggle rows. All
/// colors and fonts come from the generated Kumo token resources.
/// </summary>
public class KumoTable : TemplatedControl
{
    private AvaloniaList<KumoTableColumn> _columns = new();

    /// <summary>Column plan (header text, width, alignment). Appended to
    /// via XAML collection syntax; per-table instance.</summary>
    public AvaloniaList<KumoTableColumn> Columns
    {
        get => _columns;
        set
        {
            _columns = value ?? new AvaloniaList<KumoTableColumn>();
            Rebuild();
        }
    }

    private AvaloniaList<KumoTableRow> _rows = new();

    /// <summary>Data rows; per-table instance.</summary>
    public AvaloniaList<KumoTableRow> Rows
    {
        get => _rows;
        set
        {
            if (!ReferenceEquals(_rows, value))
            {
                foreach (var row in _rows)
                    row.PropertyChanged -= OnRowPropertyChanged;
                _rows = value ?? new AvaloniaList<KumoTableRow>();
                Rebuild();
            }
        }
    }

    public static readonly StyledProperty<bool> ShowSelectionColumnProperty =
        AvaloniaProperty.Register<KumoTable, bool>(nameof(ShowSelectionColumn), true);

    public bool ShowSelectionColumn
    {
        get => GetValue(ShowSelectionColumnProperty);
        set => SetValue(ShowSelectionColumnProperty, value);
    }

    private Grid? _grid;
    private CheckBox? _selectAll;

    private IBrush? _base;
    private IBrush? _alt;
    private IBrush? _tint;
    private IBrush? _fill;
    private IBrush? _textDefault;
    private IBrush? _textStrong;
    private IBrush? _line;
    private FontFamily? _mono;
    private readonly List<Border> _rowBorders = new();
    

    /// <summary>true while the component itself writes checkbox or row
    /// state, so its own handlers do not react to the echo.</summary>
    private bool _internalUpdate;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _grid = e.NameScope.Find<Grid>("PART_Grid");
        Rebuild();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ResourcesChanged += OnResourcesChanged;
        Rebuild();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        ResourcesChanged -= OnResourcesChanged;
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => Rebuild();

    /// <summary>Resolves a Kumo token resource for the current theme variant.</summary>
    private T? Tok<T>(string key)
    {
        if (Application.Current is { } app &&
            app.TryFindResource(key, ActualThemeVariant, out var value))
            return (T?)value;
        return default;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ShowSelectionColumnProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var grid = _grid;
        if (grid is null)
        {
            return;
        }

        _base = Tok<IBrush>("KumoBrushBase");
        _alt = Tok<IBrush>("KumoBrushElevated");
        _tint = Tok<IBrush>("KumoBrushTint");
        _line = Tok<IBrush>("KumoBrushLine");
        _fill = Tok<IBrush>("KumoBrushFill");
        _textDefault = Tok<IBrush>("KumoBrushTextDefault");
        _textStrong = Tok<IBrush>("KumoBrushTextStrong");
        _mono = Tok<FontFamily>("KumoFontMono");

        foreach (var row in _rows)
            row.PropertyChanged -= OnRowPropertyChanged;

        _rowBorders.Clear();
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        _selectAll = null;

        var columns = Columns;
        var rows = Rows;
        var firstColumn = ShowSelectionColumn ? 1 : 0;
        var totalColumns = firstColumn + columns.Count;

        if (ShowSelectionColumn)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }
        foreach (var column in columns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(column.Width));
        }

        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        foreach (var _ in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        // Header row.
        if (ShowSelectionColumn)
        {
            var selectAll = new CheckBox
            {
                Name = "PART_SelectAll",
                IsThreeState = false,
                IsChecked = null, // indeterminate until the data rows decide
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0),
            };
            selectAll.IsCheckedChanged += OnSelectAllChanged;
            grid.Children.Add(WrapHeader(selectAll, 0, _base, _fill));
            _selectAll = selectAll;
        }
        for (var c = 0; c < columns.Count; c++)
        {
            var column = columns[c];
            var text = new TextBlock
            {
                Text = column.Header,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeight.SemiBold,
            };
            text.Foreground = _textDefault;
            text.FontSize = Tok<double>("KumoFontSizeBase");
            if (column.AlignRight)
                text.HorizontalAlignment = HorizontalAlignment.Right;
            grid.Children.Add(WrapHeader(text, firstColumn + c, _base, _fill));
        }

        // Data rows. Each row is one Border spanning the full column plan
        // hosting a nested Grid with the SAME column definitions, so cell
        // alignment always matches the header.
        foreach (var row in rows)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
            row.PropertyChanged += OnRowPropertyChanged;
        }

        for (var r = 0; r < rows.Count; r++)
        {
            BuildDataRow(grid, rows[r], r + 1, firstColumn, totalColumns);
        }

        RefreshRowVisuals();
        RefreshSelectAll();
    }

    private static Border WrapHeader(Control content, int column, IBrush? bg, IBrush? line)
    {
        var cell = new Border
        {
            Padding = new Thickness(12),
            Background = bg,
            BorderBrush = line,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        Grid.SetColumn(cell, column);
        cell.Child = content;
        return cell;
    }

    private void BuildDataRow(Grid grid, KumoTableRow row, int rowIndex, int firstColumn, int totalColumns)
    {
        var rowBorder = new Border();
        Grid.SetRow(rowBorder, rowIndex);
        Grid.SetColumnSpan(rowBorder, totalColumns);
        rowBorder.Background = _base;

        var inner = new Grid();
        if (ShowSelectionColumn)
        {
            inner.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }
        foreach (var column in Columns)
        {
            inner.ColumnDefinitions.Add(new ColumnDefinition(column.Width));
        }

        if (ShowSelectionColumn)
        {
            var check = new CheckBox
            {
                Margin = new Thickness(12, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            check[!CheckBox.IsCheckedProperty] = row[!KumoTableRow.IsSelectedProperty];
            inner.Children.Add(WrapBody(check, 0));
        }

        var columns = Columns;
        var cells = row.Cells;
        for (var c = 0; c < columns.Count && c < cells.Count; c++)
        {
            var column = columns[c];
            var text = new TextBlock
            {
                Text = cells[c]?.ToString() ?? string.Empty,
                VerticalAlignment = VerticalAlignment.Center,
            };
            text.Foreground = _textDefault;
            text.FontSize = Tok<double>("KumoFontSizeBase");
            if (column.Mono)
                text.FontFamily = _mono;
            if (column.AlignRight)
                text.HorizontalAlignment = HorizontalAlignment.Right;
            inner.Children.Add(WrapBody(text, firstColumn + c));
        }

        rowBorder.Child = inner;
        rowBorder.Tapped += OnRowTapped;
        grid.Children.Add(rowBorder);
        _rowBorders.Add(rowBorder);
    }

    private static Border WrapBody(Control content, int column)
    {
        var cell = new Border { Padding = new Thickness(12) };
        Grid.SetColumn(cell, column);
        cell.Child = content;
        return cell;
    }

    private void OnRowTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is not Border row || _internalUpdate)
        {
            return;
        }

        // A tap on the row's own checkbox flips it via the checkbox itself;
        // taps elsewhere on the row proxy-toggle it.
        if (e.Source is Visual v && IsInCheckBox(v))
        {
            return;
        }

        var box = row.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault();
        if (box is null || ReferenceEquals(box, _selectAll))
        {
            return;
        }

        box.IsChecked = box.IsChecked != true;
    }

    private static bool IsInCheckBox(Visual v)
    {
        while (v is not null)
        {
            if (v is CheckBox)
                return true;
            v = v.GetVisualParent();
        }
        return false;
    }

    private void OnRowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == KumoTableRow.IsSelectedProperty)
        {
            RefreshRowVisuals();
            RefreshSelectAll();
        }
    }

    private void OnSelectAllChanged(object? sender, RoutedEventArgs e)
    {
        if (_internalUpdate || _selectAll is null)
        {
            return;
        }

        // Clicking an indeterminate select-all checks everything.
        var value = _selectAll.IsChecked is null || _selectAll.IsChecked == true;
        _internalUpdate = true;
        try
        {
            foreach (var row in Rows)
                row.IsSelected = value;
        }
        finally
        {
            _internalUpdate = false;
        }
        RefreshRowVisuals();
        RefreshSelectAll();
    }

    private void RefreshRowVisuals()
    {
        var rows = Rows;
        for (var i = 0; i < rows.Count && i < _rowBorders.Count; i++)
        {
            _rowBorders[i].Background = rows[i].IsSelected ? _tint :
                i % 2 == 0 ? _alt : _base;
        }
    }

    private void RefreshSelectAll()
    {
        if (_selectAll is null)
        {
            return;
        }

        var rows = Rows;
        if (rows.Count == 0)
        {
            _selectAll.IsChecked = false;
            return;
        }

        var selected = rows.Count(row => row.IsSelected);

        _internalUpdate = true;
        try
        {
            _selectAll.IsChecked = selected == rows.Count ? true :
                selected > 0 ? null : false;
        }
        finally
        {
            _internalUpdate = false;
        }
    }
}
