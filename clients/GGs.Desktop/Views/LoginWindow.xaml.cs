using System.Windows;

namespace GGs.Desktop.Views;

public partial class LoginWindow : Window
{
    public string? UserOrEmail { get; private set; }
    public string? Password { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        UserOrEmail = UserBox.Text;
        Password = PassBox.Password;
        DialogResult = true;
    }
}
