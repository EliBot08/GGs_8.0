using System.Windows;
using System.Windows.Controls;

namespace GGs.Desktop.Views.Controls
{
    public partial class ProgressDot : UserControl
    {
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ProgressDot), new PropertyMetadata(false));

        public static readonly DependencyProperty IsCompletedProperty =
            DependencyProperty.Register(nameof(IsCompleted), typeof(bool), typeof(ProgressDot), new PropertyMetadata(false));

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public bool IsCompleted
        {
            get => (bool)GetValue(IsCompletedProperty);
            set => SetValue(IsCompletedProperty, value);
        }

        public ProgressDot()
        {
            InitializeComponent();
        }
    }
}