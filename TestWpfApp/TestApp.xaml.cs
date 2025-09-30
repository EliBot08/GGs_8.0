using System;
using System.Windows;

namespace TestWpfApp
{
    public partial class TestApp : Application
    {
        [STAThread]
        public static void Main()
        {
            var app = new TestApp();
            app.InitializeComponent();
            app.Run();
        }
        
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
