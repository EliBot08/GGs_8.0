using System;
using System.Threading.Tasks;

namespace GGs.Desktop.Services
{
    public class AdminHubClient
    {
        private static AdminHubClient? _instance;
        
        public static AdminHubClient Instance => _instance ??= new AdminHubClient("https://localhost:5001");

        public AdminHubClient(string baseUrl)
        {
            BaseUrl = baseUrl;
        }

        public string BaseUrl { get; }

        public async Task<bool> ConnectAsync()
        {
            await Task.Delay(100);
            return true;
        }

        public async Task DisconnectAsync()
        {
            await Task.Delay(100);
        }

        public async Task SendMessageAsync(string message)
        {
            await Task.Delay(100);
        }

        public async Task<bool> EnsureConnectedAsync()
        {
            await Task.Delay(100);
            return true;
        }

        #pragma warning disable CS0067
        public event EventHandler<string>? AuditAdded;
        #pragma warning restore CS0067
    }
}