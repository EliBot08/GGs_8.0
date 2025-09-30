using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GGs.Desktop.Views.Controls
{
    public class QuickActionButton : Button
    {
        static QuickActionButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(QuickActionButton), new FrameworkPropertyMetadata(typeof(QuickActionButton)));
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(QuickActionButton), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(QuickActionButton), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(string), typeof(QuickActionButton), new PropertyMetadata(string.Empty));

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

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
    }
}