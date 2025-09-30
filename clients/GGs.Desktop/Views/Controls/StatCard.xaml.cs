using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GGs.Desktop.Views.Controls
{
    public partial class StatCard : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatCard));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatCard));

        public static readonly DependencyProperty ChangeProperty =
            DependencyProperty.Register(nameof(Change), typeof(string), typeof(StatCard));

        public static readonly DependencyProperty IsPositiveProperty =
            DependencyProperty.Register(nameof(IsPositive), typeof(bool), typeof(StatCard),
                new PropertyMetadata(true, OnIsPositiveChanged));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(StatCard),
                new PropertyMetadata(string.Empty, OnIconChanged));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string Change
        {
            get => (string)GetValue(ChangeProperty);
            set => SetValue(ChangeProperty, value);
        }

        public bool IsPositive
        {
            get => (bool)GetValue(IsPositiveProperty);
            set => SetValue(IsPositiveProperty, value);
        }

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public StatCard()
        {
            InitializeComponent();
            UpdateChangeIndicator();
        }

        private static void OnIsPositiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatCard statCard)
            {
                statCard.UpdateChangeIndicator();
            }
        }

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatCard statCard)
            {
                statCard.UpdateIcon();
            }
        }

        private void UpdateChangeIndicator()
        {
            var primaryBrush = FindResource("PrimaryBrush") as SolidColorBrush;
            var successBrush = FindResource("SuccessBrush") as SolidColorBrush;
            var dangerBrush = FindResource("DangerBrush") as SolidColorBrush;

            if (IsPositive)
            {
                ChangeIcon.Data = Geometry.Parse("M5 15l7-7 7 7"); // Up arrow
                ChangeIcon.Fill = successBrush;
                ChangeText.Foreground = successBrush;
            }
            else
            {
                ChangeIcon.Data = Geometry.Parse("M19 9l-7 7-7-7"); // Down arrow
                ChangeIcon.Fill = dangerBrush;
                ChangeText.Foreground = dangerBrush;
            }
        }

        private void UpdateIcon()
        {
            if (!string.IsNullOrEmpty(Icon))
            {
                try
                {
                    IconPath.Data = Geometry.Parse(Icon);
                }
                catch
                {
                    // Fallback to default icon if parsing fails
                    IconPath.Data = Geometry.Parse("M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z");
                }
            }
        }
    }
}
