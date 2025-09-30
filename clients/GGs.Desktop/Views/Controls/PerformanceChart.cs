using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GGs.Desktop.Views.Controls
{
    [TemplatePart(Name = "PART_Canvas", Type = typeof(Canvas))]
    public class PerformanceChart : Control
    {
        private Canvas? _canvas;
        private readonly Random _rand = new Random();

        static PerformanceChart()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PerformanceChart), new FrameworkPropertyMetadata(typeof(PerformanceChart)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _canvas = GetTemplateChild("PART_Canvas") as Canvas;
            EnsurePlaceholder();
        }

        private void EnsurePlaceholder()
        {
            if (_canvas == null) return;
            _canvas.Children.Clear();

            var text = new TextBlock
            {
                Text = "Loading performance data...",
                Foreground = TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray,
                FontSize = 12
            };
            Canvas.SetLeft(text, 8);
            Canvas.SetTop(text, 8);
            _canvas.Children.Add(text);
        }

        public async Task InitializeWithRealtimeDataAsync()
        {
            // Simulate realtime data initialization and draw a simple animated polyline
            await Dispatcher.InvokeAsync(() => DrawSampleSeries(animate: true));
            await Task.Delay(100); // small yield for UX
        }

        public async Task InitializeWithStaticDataAsync()
        {
            // Draw a static series quickly
            await Dispatcher.InvokeAsync(() => DrawSampleSeries(animate: false));
            await Task.Delay(50);
        }

        private void DrawSampleSeries(bool animate)
        {
            if (_canvas == null) return;

            _canvas.Children.Clear();

            // Background grid lines
            var borderBrush = TryFindResource("BorderBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
            for (int i = 1; i <= 4; i++)
            {
                var y = i * (_canvas.ActualHeight / 5.0);
                var line = new Line
                {
                    X1 = 0,
                    X2 = Math.Max(0, _canvas.ActualWidth),
                    Y1 = y,
                    Y2 = y,
                    Stroke = borderBrush,
                    StrokeThickness = 1,
                    Opacity = 0.6
                };
                _canvas.Children.Add(line);
            }

            // Generate sample points
            int points = 40;
            double width = Math.Max(1, _canvas.ActualWidth);
            double height = Math.Max(1, _canvas.ActualHeight);
            var poly = new Polyline
            {
                Stroke = TryFindResource("PrimaryBrush") as Brush ?? Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };

            double xStep = width / (points - 1);
            double last = height * 0.5;
            for (int i = 0; i < points; i++)
            {
                // gently vary the value
                last += (_rand.NextDouble() - 0.5) * (height * 0.1);
                last = Math.Max(height * 0.1, Math.Min(height * 0.9, last));
                poly.Points.Add(new Point(i * xStep, last));
            }
            _canvas.Children.Add(poly);

            if (animate)
            {
                // Simple fade-in for effect
                poly.Opacity = 0;
                var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(350)));
                poly.BeginAnimation(UIElement.OpacityProperty, anim);
            }
        }
    }
}