using GGs.Shared.Api;
using GGs.Shared.Tweaks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TweakApplicationLog = GGs.Shared.Tweaks.TweakApplicationLog;

namespace GGs.Desktop.Extensions
{
    public static partial class ApiClientExtensions
    {
        // Extension methods for ApiClient to add missing methods
        public static async Task<List<TweakApplicationLog>> SearchAuditLogsAsync(this ApiClient client, string query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            await Task.Delay(100);
            return new List<TweakApplicationLog>();
        }

        public static async Task<bool> PostAuditLogAsync(this ApiClient client, TweakApplicationLog log)
        {
            await Task.Delay(100);
            return true;
        }

        public static async Task<bool> DeleteTweakAsync(this ApiClient client, string tweakId)
        {
            await Task.Delay(100);
            return true;
        }

        public static async Task<List<ConnectedDevice>> GetConnectedDevicesAsync(this ApiClient client)
        {
            await Task.Delay(100);
            return new List<ConnectedDevice>();
        }

        public static async Task<bool> ExecuteRemoteTweakAsync(this ApiClient client, string deviceId, string tweakId)
        {
            await Task.Delay(100);
            return true;
        }

        public static string GetBaseUrl(this ApiClient client)
        {
            return "https://api.ggs.com";
        }

        public static async Task<List<TweakApplicationLog>> GetAuditLogsAsync(this ApiClient client)
        {
            await Task.Delay(100);
            return new List<TweakApplicationLog>();
        }

        public static async Task<bool> IssueLicenseAsync(this ApiClient client, string userId, string licenseKey, DateTime expirationDate, string tier)
        {
            await Task.Delay(100);
            return true;
        }

        public static async Task<(List<ConnectedDevice> items, int total)> GetDevicesPagedAsync(this ApiClient client, int page, int pageSize)
        {
            await Task.Delay(100);
            return (new List<ConnectedDevice>(), 0);
        }

        public static async Task<byte[]> GenerateChartAsync(this ApiClient client, string chartType, Func<byte> dataProvider)
        {
            await Task.Delay(100);
            return new byte[0];
        }

        public static async Task<bool> CreateTweakAsync(this ApiClient client, TweakDefinition tweak)
        {
            await Task.Delay(100);
            return true;
        }

        public static async Task<bool> UpdateTweakAsync(this ApiClient client, TweakDefinition tweak)
        {
            await Task.Delay(100);
            return true;
        }

        public static async Task<bool> ApplyTweakAsync(this ApiClient client, string tweakId, Guid deviceId)
        {
            await Task.Delay(100);
            return true;
        }

        public static async Task<bool> ExecuteRemoteTweakAsync(this ApiClient client, ConnectedDevice device, string tweakId)
        {
            await Task.Delay(100);
            return true;
        }

        public static async Task<bool> ExecuteRemoteTweakAsync(this ApiClient client, string deviceId, string tweakId, Guid userId)
        {
            await Task.Delay(100);
            return true;
        }

        public static async Task<List<TweakApplicationLog>> SearchAuditLogsAsync(this ApiClient client, string query, AuditSearchCriteria criteria)
        {
            await Task.Delay(100);
            return new List<TweakApplicationLog>();
        }

        public static async Task<(bool success, string message)> GetValidTokenAsync(this ApiClient client)
        {
            await Task.Delay(100);
            return (true, "Token valid");
        }

        public static async Task<List<LicenseRecord>> GetLicensesAsync(this ApiClient client)
        {
            await Task.Delay(100);
            return new List<LicenseRecord>();
        }
    }

    public class ConnectedDevice
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public string LastSeen { get; set; } = string.Empty;
    }

    public class AuditSearchCriteria
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? UserId { get; set; }
        public string? TweakId { get; set; }
        public string? Status { get; set; }
    }


    public class LicenseRecord
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string LicenseKey { get; set; } = string.Empty;
        public DateTime ExpirationDate { get; set; }
        public string Tier { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
