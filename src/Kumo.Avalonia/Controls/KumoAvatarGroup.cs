using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace KumoThemeSupport.Controls;

/// <summary>
/// Kumo stacked avatars: comma separated <see cref="Names"/> render as
/// overlapping <see cref="KumoAvatar"/> discs separated by opaque
/// surface-colored rings, capped at <see cref="Max"/> items plus a "+N"
/// overflow pill. <see cref="Direction"/> = "right" (default) or "left"
/// chooses which side the stack hangs from and which name sits in front.
/// </summary>
public class KumoAvatarGroup : TemplatedControl
{
    public static readonly StyledProperty<string> NamesProperty =
        AvaloniaProperty.Register<KumoAvatarGroup, string>(nameof(Names), defaultValue: "");

    public static readonly StyledProperty<string> SizeProperty =
        AvaloniaProperty.Register<KumoAvatarGroup, string>(nameof(Size), defaultValue: "base");

    public static readonly StyledProperty<int> MaxProperty =
        AvaloniaProperty.Register<KumoAvatarGroup, int>(nameof(Max), defaultValue: 4);

    public static readonly StyledProperty<string> DirectionProperty =
        AvaloniaProperty.Register<KumoAvatarGroup, string>(nameof(Direction), defaultValue: "front");

    public string Names
    {
        get => GetValue(NamesProperty);
        set => SetValue(NamesProperty, value);
    }

    public string Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public int Max
    {
        get => GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    /// <summary>"front" (default): the first name renders over the pile;
    /// "back": the first name sits underneath the rest.</summary>
    public string Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Render();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NamesProperty || change.Property == SizeProperty
            || change.Property == MaxProperty || change.Property == DirectionProperty)
        {
            Render();
        }
    }

    private void Render()
    {
        var host = this.GetVisualDescendants()
            .OfType<StackPanel>()
            .FirstOrDefault(p => p.Name == "PART_Host");
        if (host is null)
        {
            return;
        }
        host.Children.Clear();

        var names = (Names ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .ToArray();
        if (names.Length == 0)
        {
            return;
        }

        var front = !string.Equals(Direction, "back", StringComparison.OrdinalIgnoreCase);

        var visible = Math.Clamp(Math.Max(Max, 1), 1, names.Length);
        var (disc, overlap) = Size switch
        {
            "xs" => (20.0, 8.0),
            "sm" => (24.0, 11.0),
            "lg" => (40.0, 18.0),
            _ => (32.0, 15.0),
        };
        var ringBrush = this.TryFindResource("KumoBrushControl", out var surface)
            ? surface as IBrush : null;

        // Positions and paint order are decoupled via ZIndex: children sit
        // in the caller's name order left-to-right; Direction flips who is
        // in front without shifting the first child's slot off the edge.
        for (var index = 0; index < visible; index++)
        {
            var avatar = new KumoAvatar
            {
                DisplayName = names[index],
                Size = Size,
            };
            if (index > 0)
            {
                avatar.Margin = new Thickness(-overlap, 0, 0, 0);
                avatar.BorderBrush = ringBrush;
                avatar.StrokeWidth = 2;
            }
            if (front)
            {
                avatar.ZIndex = visible - index;
            }
            host.Children.Add(avatar);
        }

        if (names.Length > visible)
        {
            var overflow = new Border
            {
                Width = disc,
                Height = disc,
                CornerRadius = new CornerRadius(disc / 2),
                Margin = new Thickness(-overlap, 0, 0, 0),
                Background = ringBrush,
                BorderBrush = this.TryFindResource("KumoBrushLine", out var line)
                    ? line as IBrush : null,
                BorderThickness = new Thickness(1),
                ZIndex = visible + 1,
                Child = new TextBlock
                {
                    Text = $"+{names.Length - visible}",
                    FontSize = 11,
                    FontWeight = FontWeight.Medium,
                    Foreground = this.TryFindResource("KumoBrushTextDefault", out var text)
                        ? text as IBrush : null,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            host.Children.Add(overflow);
        }
    }
}
