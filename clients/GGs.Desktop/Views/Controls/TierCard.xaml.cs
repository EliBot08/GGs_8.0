using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GGs.Desktop.Views.Controls;

public partial class TierCard : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(TierCard), new PropertyMetadata(""));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(TierCard), new PropertyMetadata(""));

    public static readonly DependencyProperty PriceProperty = DependencyProperty.Register(
        nameof(Price), typeof(string), typeof(TierCard), new PropertyMetadata(""));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush),
        typeof(System.Windows.Media.Brush),
        typeof(TierCard),
        new PropertyMetadata(System.Windows.Media.Brushes.MediumPurple));

    public event RoutedEventHandler? Click;

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string Price { get => (string)GetValue(PriceProperty); set => SetValue(PriceProperty, value); }
public System.Windows.Media.Brush AccentBrush { get => (System.Windows.Media.Brush)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public TierCard()
    {
        try { InitializeComponent(); }
        catch { }
    }

    private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Click?.Invoke(this, new RoutedEventArgs());
    }
}
