using System;
using System.Windows;

namespace TestWindow
{
    public partial class TestApp : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            try
            {
                var window = new TestWindow();
                window.Show();
                window.Activate();
                window.Focus();
                Console.WriteLine("Test window created and shown");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating test window: {ex.Message}");
            }
        }
    }
}
