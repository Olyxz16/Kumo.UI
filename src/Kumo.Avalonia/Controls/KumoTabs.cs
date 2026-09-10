using Avalonia.Controls;
using KumoThemeSupport;

namespace KumoThemeSupport.Controls;

/// <summary>
/// Kumo segmented tabs: recessed list with a base active tab and the native
/// 200 ms sliding indicator (translate + scale pop-in on first render),
/// equivalent of the upstream Tabs component.
/// </summary>
public class KumoTabs : TabControl
{
    public KumoTabs()
    {
        TabSlide.SetIsEnabled(this, true);
        Classes.Add("kumo-tabs");
    }
}
