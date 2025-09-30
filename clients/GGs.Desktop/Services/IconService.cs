using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GGs.Desktop.Services;

public static class IconService
{
    private static readonly string[] Candidates = new[]
    {
        "assets/app-icon.ico",
        "assets/logo.ico",
        "assets/ggs.ico",
        "assets/icon.ico",
        "assets/logo.png",
        "assets/ggs.png",
        "assets/icon.png"
    };

    public static bool ApplyWindowIcon(Window w)
    {
        try
        {
            var path = FindIconPath();
            if (path == null) return false;
            var uri = new Uri(path);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            w.Icon = bmp;
            return true;
        }
        catch { return false; }
    }

    public static string? FindIconPath()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            foreach (var c in Candidates)
            {
                var full = Path.Combine(baseDir, c.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                {
                    return new Uri(full).AbsoluteUri;
                }
            }
        }
        catch { }
        return null;
    }
}
