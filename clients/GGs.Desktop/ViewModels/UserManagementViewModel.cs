using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using GGs.Shared.Api;
using GGs.Shared.Models;

namespace GGs.Desktop.ViewModels;

public class UserManagementViewModel : BaseViewModel
{
    private readonly GGs.Shared.Api.ApiClient _api;

    public ObservableCollection<UserDto> Users { get; } = new();

    private UserDto? _selectedUser;
    public UserDto? SelectedUser
    {
        get => _selectedUser;
        set => SetField(ref _selectedUser, value);
    }

    public ObservableCollection<string> AvailableRoles { get; } = new() { "Admin", "Manager", "Support", "User" };

    private string _newUserEmail = string.Empty;
    public string NewUserEmail
    {
        get => _newUserEmail;
        set => SetField(ref _newUserEmail, value);
    }

    private string _newUserRole = "User";
    public string NewUserRole
    {
        get => _newUserRole;
        set => SetField(ref _newUserRole, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand SuspendCommand { get; }
    public ICommand DeleteCommand { get; }

    public UserManagementViewModel(GGs.Shared.Api.ApiClient api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(async () => await LoadUsers());
        SuspendCommand = new RelayCommand<UserDto?>(async u => { if (u != null) await _api.SuspendUserAsync(u.Id); await LoadUsers(); });
        DeleteCommand = new RelayCommand<UserDto?>(async u => { if (u != null) await _api.DeleteUserAsync(u.Id); await LoadUsers(); });
    }

    public async Task LoadUsers()
    {
        Users.Clear();
        foreach (var u in await _api.GetUsersAsync()) Users.Add(u);
    }

    public async Task CreateUserAsync(string email, string password, string role)
    {
        await _api.CreateUserAsync(new CreateUserRequest { Email = email, Password = password, Roles = new[] { role } });
        await LoadUsers();
    }
}

