using System.Windows;
using System.Windows.Controls;

namespace GGs.Desktop.Views.Controls
{
    public class ChatMessage : Control
    {
        static ChatMessage()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ChatMessage), new FrameworkPropertyMetadata(typeof(ChatMessage)));
        }

        public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
            nameof(Message), typeof(object), typeof(ChatMessage), new PropertyMetadata(null));

        public object? Message
        {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }
    }
}