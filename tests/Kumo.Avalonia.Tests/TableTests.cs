using System;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Input;
using KS = KumoThemeSupport.Controls;
using Xunit;

namespace Kumo.Avalonia.Tests;

public class TableTests
{
    private static KS.KumoTableColumn Col(string header, string width = "*") =>
        new KS.KumoTableColumn { Header = header, Width = GridLength.Parse(width) };

    private static KS.KumoTable Build(bool showSelection, out Window host)
    {
        var table = new KS.KumoTable
        {
            Columns =
            {
                Col("Region"),
                Col("Status"),
                Col("Requests", "110"),
            },
            ShowSelectionColumn = showSelection,
        };
        table.Rows = new AvaloniaList<KS.KumoTableRow>
        {
            new KS.KumoTableRow { Cells = new object?[] { "North America", "Healthy", "24.1M" } },
            new KS.KumoTableRow { Cells = new object?[] { "Europe", "Degraded", "12.9M" } },
            new KS.KumoTableRow { Cells = new object?[] { "Asia Pacific", "Healthy", "8.4M" }, IsSelected = true },
        };

        host = new Window { Width = 600, Height = 300 };
        host.Content = table;
        host.Show();
        Dispatcher.UIThread.RunJobs();
        return table;
    }

    private static Point ToPoint(Visual from, Point p, Visual relativeTo)
    {
        var m = from.TransformToVisual(relativeTo) ?? Matrix.Identity;
        return new Point(
            m.M11 * p.X + m.M21 * p.Y + m.M31,
            m.M12 * p.X + m.M22 * p.Y + m.M32);
    }

    private static Point CellOrigin(Visual cell, Visual table) => ToPoint(cell, new Point(0, 0), table);

    private static System.Collections.Generic.List<Border> OrderedCells(Grid grid, int row) =>
        grid.Children.Where(c => Grid.GetRow((Control)c) == row).OfType<Border>()
            .OrderBy(c => Grid.GetColumn((Control)c)).ToList();

    [AvaloniaFact]
    public void Columns_align_with_and_without_selection_column()
    {
        foreach (var showSelection in new[] { true, false })
        {
            var table = Build(showSelection, out var host);
            try
            {
                var grid = table.GetVisualDescendants().OfType<Grid>().First();
                var headerCells = OrderedCells(grid, 0);
                // The first data row lives in its own inner grid; its cells
                // (checkbox column included) must mirror the header plan.
                var firstRowHost = grid.Children.Where(c => Grid.GetRow((Control)c) == 1)
                    .OfType<Border>().First();
                var firstRowGrid = (Grid)firstRowHost.Child!;
                var bodyCells = firstRowGrid.Children.OfType<Border>()
                    .OrderBy(c => Grid.GetColumn((Control)c)).ToList();

                Assert.True(headerCells.Count == bodyCells.Count,
                    $"dump: {Dump(table)}");

                for (var i = 0; i < headerCells.Count; i++)
                {
                    var h = CellOrigin(headerCells[i], table);
                    var b = CellOrigin(bodyCells[i], table);
                    Assert.True(Math.Abs(h.X - b.X) < 0.51,
                        $"column {i} left misaligned (header {h.X:F2}, body {b.X:F2}, selection={showSelection})");
                    Assert.True(Math.Abs(headerCells[i].Bounds.Width - bodyCells[i].Bounds.Width) < 1.01,
                        $"column {i} width mismatch (header vs body, selection={showSelection})");
                }
            }
            finally
            {
                host.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Select_all_and_row_checkbox_semantics()
    {
        var table = Build(true, out var host);
        try
        {
            var all = table.GetVisualDescendants().OfType<CheckBox>().ToList();
            var selectAll = all.First(c => c.Name == "PART_SelectAll");
            var rowBoxes = all.Where(c => c.Name != "PART_SelectAll").ToList();
            Assert.Equal(3, rowBoxes.Count);

            // Row 3 starts selected -> header shows indeterminate.
            Assert.Null(selectAll.IsChecked);

            // Select all -> every row true, header true.
            selectAll.IsChecked = true;
            Assert.True(table.Rows.All(r => r.IsSelected));

            // Deselect one row -> header back to indeterminate.
            table.Rows[1].IsSelected = false;
            Assert.Null(selectAll.IsChecked);

            // Nothing selected -> header false.
            table.Rows[0].IsSelected = false;
            table.Rows[2].IsSelected = false;
            Assert.False(selectAll.IsChecked);
        }
        finally
        {
            host.Close();
        }
    }

    private static string Dump(Visual v, int depth = 0)
    {
        var pad = new string(' ', depth * 2);
        var info = $"{pad}{v.GetType().Name}";
        if (Grid.GetRow((Control)v) + Grid.GetColumn((Control)v) + Grid.GetColumnSpan((Control)v) > 0)
            info += $" r{Grid.GetRow((Control)v)} c{Grid.GetColumn((Control)v)} span{Grid.GetColumnSpan((Control)v)}";
        if (v is Border b)
            info += $" w={b.Bounds.Width:F1}";
        var children = v.GetVisualChildren().OfType<Visual>().ToList();
        return info + (children.Count == 0 ? "" :
            "\n" + string.Join("\n", children.Select(c => Dump(c, depth + 1))));
    }

    [AvaloniaFact]
    public void Zebra_and_selected_row_tints()
    {
        var table = Build(true, out var host);
        try
        {
            var grid = table.GetVisualDescendants().OfType<Grid>().First();
            var rows = grid.Children.Where(c => Grid.GetRow((Control)c) > 0).OfType<Border>().ToList();
            Assert.Equal(3, rows.Count);

            var bg = new Func<int, string>(i =>
                ((SolidColorBrush)rows[i].Background!).Color.ToString());
            // Row 1 dark, row 2 light(--base), row 3 selected tint differs from base.
            Assert.NotEqual(bg(0), bg(1));
            Assert.NotEqual(bg(1), bg(2));
        }
        finally
        {
            host.Close();
        }
    }

    [AvaloniaFact]
    public void Header_select_all_follows_row_checkbox_clicks()
    {
        // Regression: the row checkbox binding was one-way, so clicking a
        // row box never wrote back to KumoTableRow.IsSelected and the
        // header checkbox never noticed.
        var table = Build(true, out var host);
        try
        {
            var all = table.GetVisualDescendants().OfType<CheckBox>().ToList();
            var selectAll = all.First(c => c.Name == "PART_SelectAll");
            var rowBoxes = all.Where(c => c.Name != "PART_SelectAll").ToList();

            // Fixture state: row 3 starts selected -> header indeterminate.
            Assert.Null(selectAll.IsChecked);

            // Click row 3's box again -> nothing selected -> header unchecked.
            rowBoxes[2].IsChecked = false;
            Assert.False(selectAll.IsChecked ?? true);

            // Click row 1's box -> header back to indeterminate.
            rowBoxes[0].IsChecked = true;
            Assert.True(table.Rows[0].IsSelected);
            Assert.Null(selectAll.IsChecked);

            // Click row 2's box -> 2 of 3 still indeterminate.
            rowBoxes[1].IsChecked = true;
            Assert.True(table.Rows[1].IsSelected);
            Assert.Null(selectAll.IsChecked);

            // Click row 3's box back -> everything selected -> header checked.
            rowBoxes[2].IsChecked = true;
            Assert.True(selectAll.IsChecked == true);
        }
        finally
        {
            host.Close();
        }
    }

    [AvaloniaFact]
    public void Row_checkbox_click_toggles_without_focusing()
    {
        var table = Build(true, out var host);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var box = table.GetVisualDescendants().OfType<CheckBox>()
                .First(c => c.Name != "PART_SelectAll");

            // Real pointer click on the checkbox itself.
            var point = box.TranslatePoint(new Point(box.Bounds.Width / 2, box.Bounds.Height / 2), host)!.Value;
            host.MouseDown(point, MouseButton.Left);
            host.MouseUp(point, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            Assert.False(box.IsFocused);
            Assert.True(table.Rows[0].IsSelected);
        }
        finally
        {
            host.Close();
        }
    }
}
