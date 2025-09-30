using GGs.Shared.Tweaks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GGs.Desktop.Services
{
    public class DataExportService
    {
        public async Task<bool> ExportToCsvAsync(List<TweakApplicationLog> logs, string filePath)
        {
            await Task.Delay(100);
            return true;
        }

        public async Task<bool> ExportToExcelAsync(List<TweakApplicationLog> logs, string filePath)
        {
            await Task.Delay(100);
            return true;
        }

        public async Task<bool> ExportToJsonAsync(List<TweakApplicationLog> logs, string filePath)
        {
            await Task.Delay(100);
            return true;
        }

        public async Task<bool> ExportToFileAsync(List<TweakApplicationLog> logs, string filePath, string format)
        {
            await Task.Delay(100);
            return true;
        }
    }
}