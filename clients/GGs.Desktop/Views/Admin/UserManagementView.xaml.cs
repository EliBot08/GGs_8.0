using System.Windows;
using System.Windows.Controls;
using GGs.Desktop.ViewModels;

namespace GGs.Desktop.Views.Admin;

public partial class UserManagementView : System.Windows.Controls.UserControl
{
    public UserManagementView()
    {
        InitializeComponent();
    }

    private async void CreateUser_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm)
        {
            var parent = this;
            var pwdBox = FindName("Pwd") as PasswordBox;
            var pwd = pwdBox?.Password ?? "";
            if (string.IsNullOrWhiteSpace(vm.NewUserEmail) || string.IsNullOrWhiteSpace(pwd))
            {
                MessageBox.Show("Email and password are required.");
                return;
            }
            await vm.CreateUserAsync(vm.NewUserEmail, pwd, vm.NewUserRole);
        }
    }
}

