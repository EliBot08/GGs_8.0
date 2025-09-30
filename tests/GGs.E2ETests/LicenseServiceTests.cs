using System;
using System.IO;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using GGs.Shared.Licensing;
using Xunit;

namespace GGs.E2ETests
{
    public class LicenseServiceTests
    {
        [Fact]
        public async Task License_ReadWrite_IsConcurrencySafe_And_UpdatesMetadata()
        {
            var temp = Path.Combine(Path.GetTempPath(), "ggs_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            Environment.SetEnvironmentVariable("GGS_DATA_DIR", temp);
            try
            {
                var svc = new LicenseService();
                // Persist a demo license via 16-char key path (bypasses online and RSA verification)
                var saveRes = await svc.ValidateAndSaveFromTextAsync("1234567890ABCDEF");
                Assert.True(saveRes.ok);

                // Concurrent reads should succeed without exceptions
                var tasks = new Task[5];
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        Assert.True(svc.TryLoadRaw(out var loaded));
                        Assert.NotNull(loaded);
                        Assert.Equal("DEMO", loaded!.KeyFingerprint);
                    });
                }
                await Task.WhenAll(tasks);

                // Metadata should be present
                var meta = svc.GetMetadata();
                Assert.NotNull(meta);
                Assert.Equal("DEMO", meta.KeyFingerprint);
            }
            finally
            {
                // cleanup
                try { Directory.Delete(temp, true); } catch { }
                Environment.SetEnvironmentVariable("GGS_DATA_DIR", null);
            }
        }
    }
}

