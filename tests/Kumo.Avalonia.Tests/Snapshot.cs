using System.IO;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;

internal static class Snapshot
{
    private static readonly string Dir = Path.Combine(Path.GetTempPath(), "kumo-snapshots");

    public static void Capture(Window window, string path)
    {
        if (HeadlessWindowExtensions.CaptureRenderedFrame(window) is not { } frame)
            return;
        Directory.CreateDirectory(Dir);
        using var fs = File.Open(Path.Combine(Dir, Path.GetFileName(path)), FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete);
        frame.Save(fs, new PngBitmapEncoderOptions());
    }
}
