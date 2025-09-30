using System;
using System.Windows;
// using System.Windows.Controls; // Not needed since we use Window as base
using GGs.Desktop.Services;
using GGs.Desktop.ViewModels.Analytics;

namespace GGs.Desktop.Views.Analytics
{
    /// <summary>
    /// Enterprise Advanced Audit Search with comprehensive filtering and compliance reporting
    /// </summary>
    public partial class AuditSearchView : Window
    {
        public AuditSearchView()
        {
            InitializeComponent();
            // DataContext is set in XAML via <vm:AuditSearchViewModel />
        }
    }
}
