using System.Windows;
using System.Windows.Controls;

namespace GGs.Desktop.Views.Controls
{
    public partial class FeatureHighlight : UserControl
    {
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(FeatureHighlight), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(FeatureHighlight), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(FeatureHighlight), new PropertyMetadata(string.Empty));

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public FeatureHighlight()
        {
            InitializeComponent();
        }
    }
}