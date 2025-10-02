#nullable enable
using System;
using System.Windows;
using System.Windows.Media;

namespace GGs.ErrorLogViewer.Helpers
{
    public static class ScreenResolutionHelper
    {
        public static Size GetPrimaryScreenSize()
        {
            return new Size(
                SystemParameters.PrimaryScreenWidth,
                SystemParameters.PrimaryScreenHeight
            );
        }

        public static Size GetWorkAreaSize()
        {
            return new Size(
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height
            );
        }

        public static double GetScaleFactor()
        {
            try
            {
                if (Application.Current?.MainWindow != null)
                {
                    var source = PresentationSource.FromVisual(Application.Current.MainWindow);
                    if (source?.CompositionTarget != null)
                    {
                        return source.CompositionTarget.TransformToDevice.M11;
                    }
                }
            }
            catch
            {
                // Fallback to default
            }
            return 1.0;
        }

        public static WindowSize GetOptimalWindowSize()
        {
            var workArea = GetWorkAreaSize();
            
            // Use 90% of work area for optimal viewing
            return new WindowSize
            {
                Width = workArea.Width * 0.9,
                Height = workArea.Height * 0.9,
                MinWidth = Math.Min(1280, workArea.Width * 0.7),
                MinHeight = Math.Min(720, workArea.Height * 0.7)
            };
        }

        public static bool IsSmallScreen()
        {
            var size = GetWorkAreaSize();
            return size.Width < 1366 || size.Height < 768;
        }

        public static bool IsHighDPI()
        {
            return GetScaleFactor() > 1.25;
        }
    }

    public class WindowSize
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double MinWidth { get; set; }
        public double MinHeight { get; set; }
    }
}
