using Avalonia;
using Avalonia.Headless;
using Kumo.Demo;

namespace Kumo.Avalonia.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { ShouldRenderOnUIThread = true });
}
