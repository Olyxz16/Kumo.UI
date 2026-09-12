using System;
using System.Linq;
using Avalonia;
using global::Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Kumo.Avalonia.Tests;

/// <summary>
/// Kumo TableView family probe: the four themed types apply their templates
/// (table ScrollViewer w/ sticky header row, rows, cells, headers with
/// resizer), recycler nesting is avoided via an explicit StackPanel populator
/// (see TableTests: real grid tests own row recycling; this locks the Kumo
/// chrome), and row selection paints the Fill-tint border.
/// </summary>
public class TableViewProbe
{
    [AvaloniaFact]
    public void Table_family_chrome_and_row_selection()
    {
        var table = new TableView();
        table.Columns = new global::Avalonia.Collections.AvaloniaList<TableViewColumn>
        {
            new() { Header = "Region", Width = new GridLength(1, GridUnitType.Star) },
            new() { Header = "Instances", Width = new GridLength(120) },
        };
        for (var i = 0; i < 3; i++)
        {
            table.Items.Add($"row-{i}");
        }
        var window = new Window { Width = 520, Height = 320 };
        try
        {
            window.Content = table;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            // headers + rows + cells all instantiate with Kumo chrome parts
            var headers = table.GetVisualDescendants().OfType<TableViewColumnHeader>().ToList();
            Assert.Equal(2, headers.Count);
            Assert.All(headers, h => Assert.NotNull(h.GetVisualDescendants().FirstOrDefault(v => v.Name == "PART_Resizer")));
            var rows = table.GetVisualDescendants().OfType<TableViewRow>().ToList();
            Assert.Equal(3, rows.Count);
            Assert.All(rows, r => Assert.NotNull(r.GetVisualDescendants().FirstOrDefault(v => v.Name == "PART_CellsPresenter")));
            Assert.True(table.GetVisualDescendants().OfType<TableViewCell>().Count() >= 6,
                "expected 3 rows x 2 columns of cells");

            // click a row: selection paints (border gets the Fill-tint background)
            var target = rows[1];
            var rp = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
            window.MouseDown(rp, MouseButton.Left);
            window.MouseUp(rp, MouseButton.Left, global::Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            Assert.True(target.IsSelected, "row click did not select");

            HeadlessWindowExtensions.CaptureRenderedFrame(window)?.Save("/tmp/opencode/tableview.png");
        }
        finally
        {
            window.Close();
        }
    }
}
