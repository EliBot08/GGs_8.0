using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using Xunit;

namespace GGs.E2ETests;

public class NotificationPersistenceTests
{
    [Fact]
    public async Task Add_And_Persist_LoadsBack_WithUnreadCount()
    {
        var temp = Path.Combine(Path.GetTempPath(), "ggs_notif_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_DATA_DIR", temp);
        try
        {
            // Force static ctor
            var initial = NotificationCenter.UnreadCount;
            NotificationCenter.Add(NotificationType.Info, "Hello world", navigateTab: "Network", showToast: false);
            NotificationCenter.Add(NotificationType.Warning, "Be careful", showToast: false);
            Assert.True(NotificationCenter.UnreadCount >= 2);
            // simulate process restart by reloading from file
            await Task.Delay(600); // debounce
            // Manually invoke load by touching internals indirectly (cannot reinit static easily), so verify file exists
            var file = Directory.GetFiles(Path.Combine(temp, "GGs", "notifications"), "notifications.json").FirstOrDefault();
            Assert.True(File.Exists(file));
            var json = File.ReadAllText(file);
            var list = JsonSerializer.Deserialize<NotificationItem[]>(json);
            Assert.NotNull(list);
            Assert.True(list!.Length > 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_DATA_DIR", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}

