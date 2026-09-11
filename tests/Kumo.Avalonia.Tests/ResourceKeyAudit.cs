using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Kumo.Demo;
using Xunit;

namespace Kumo.Avalonia.Tests;

/// <summary>
/// Token-key audit: every `DynamicResource Kumo*` reference in the library
/// or demo XAML must resolve to a real resource. A typo'd brush key renders
/// transparent with zero complaints (the KumoBrushTextSecondary spinner
/// bug), so this catches it in CI instead of an eyeballed demo.
/// </summary>
public class ResourceKeyAudit
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "Kumo.Avalonia", "Themes")))
            {
                return dir.FullName;
            }
            dir = dir.Parent!;
        }
        throw new InvalidOperationException("repo root not found");
    }

    private static readonly Regex RefPattern = new(
        @"\{DynamicResource (?'key'Kumo\w+)\}",
        RegexOptions.Compiled);

    private static Dictionary<string, List<string>> Collect()
    {
        var root = FindRepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.axaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();

        var result = new Dictionary<string, List<string>>();
        foreach (var file in files.Select(Path.GetFullPath))
        {
            foreach (Match m in RefPattern.Matches(File.ReadAllText(file)))
            {
                var list = result.TryGetValue(m.Groups["key"].Value, out var existing)
                    ? existing
                    : result[m.Groups["key"].Value] = new List<string>();
                if (!list.Contains(file))
                {
                    list.Add(file);
                }
            }
        }
        return result;
    }

    private static bool KeyExists(string key)
    {
        var app = Application.Current!;
        foreach (var variant in new[] { ThemeVariant.Default, ThemeVariant.Light, ThemeVariant.Dark })
        {
            app.TryGetResource(key, variant, out var value);
            if (value is not null)
            {
                return true;
            }
        }
        return false;
    }

    [AvaloniaFact]
    public void Every_Kumo_resource_reference_resolves()
    {
        var used = Collect();
        var missing = used.Keys.Where(k => !KeyExists(k)).OrderBy(k => k).ToList();
        var message = missing.Count == 0 ? "" :
            "Unresolved DynamicResource keys (tokens-only rule):\n  " + string.Join("\n  ", missing);
        Assert.True(message == "", message);
    }

    [AvaloniaFact]
    public void Audit_would_catch_known_bad_key()
    {
        Assert.False(KeyExists("KumoBrushTextSecondary"));
        Assert.True(KeyExists("KumoBrushBrush") == false); // sanity: not over-match
        Assert.True(KeyExists("KumoBrushBrand"));
    }
}
