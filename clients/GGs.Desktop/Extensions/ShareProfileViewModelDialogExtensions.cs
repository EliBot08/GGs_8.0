using System;

namespace GGs.Desktop.Extensions
{
    public static partial class ShareProfileViewModelDialogExtensions
    {
        public static object GetShareSettings(this ShareProfileViewModelDialog dialog)
        {
            return new { AllowEdit = true, AllowCopy = true, ExpirationDays = 30 };
        }
    }

    public class ShareProfileViewModelDialog
    {
        public string ProfileName { get; set; } = string.Empty;
        public bool AllowEdit { get; set; }
        public bool AllowCopy { get; set; }
        public int ExpirationDays { get; set; } = 30;
        public object ShareSettings => new { AllowEdit, AllowCopy, ExpirationDays };
    }
}
