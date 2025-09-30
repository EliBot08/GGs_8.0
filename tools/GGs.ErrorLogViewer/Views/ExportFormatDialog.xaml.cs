using System.Windows;
using GGs.ErrorLogViewer.Services;

namespace GGs.ErrorLogViewer.Views
{
    public partial class ExportFormatDialog : Window
    {
        public ExportFormat SelectedFormat { get; private set; }
        public bool IncludeDetails { get; private set; }
        public bool IncludeProperties { get; private set; }
        public bool UseCurrentTheme { get; private set; }

        public ExportFormatDialog()
        {
            InitializeComponent();
            UpdateFormatInfo();
            
            // Subscribe to radio button changes
            CsvRadio.Checked += (s, e) => UpdateFormatInfo();
            JsonRadio.Checked += (s, e) => UpdateFormatInfo();
            XmlRadio.Checked += (s, e) => UpdateFormatInfo();
            HtmlRadio.Checked += (s, e) => UpdateFormatInfo();
            TextRadio.Checked += (s, e) => UpdateFormatInfo();
        }

        private void UpdateFormatInfo()
        {
            if (FormatInfoText == null) return;

            string info = "";
            if (CsvRadio?.IsChecked == true)
            {
                info = "CSV format is ideal for importing into spreadsheet applications like Excel. Data is organized in rows and columns.";
            }
            else if (JsonRadio?.IsChecked == true)
            {
                info = "JSON format provides structured data that's easy to read and process programmatically. Great for developers and data analysis.";
            }
            else if (XmlRadio?.IsChecked == true)
            {
                info = "XML format offers structured markup with metadata support. Compatible with many enterprise systems and tools.";
            }
            else if (HtmlRadio?.IsChecked == true)
            {
                info = "HTML format creates a styled web page that can be viewed in any browser. Includes colors and formatting for easy reading.";
            }
            else if (TextRadio?.IsChecked == true)
            {
                info = "Plain text format provides simple, readable output that can be opened in any text editor. No special formatting.";
            }

            FormatInfoText.Text = info;
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            // Determine selected format
            if (CsvRadio.IsChecked == true)
                SelectedFormat = ExportFormat.Csv;
            else if (JsonRadio.IsChecked == true)
                SelectedFormat = ExportFormat.Json;
            else if (XmlRadio.IsChecked == true)
                SelectedFormat = ExportFormat.Xml;
            else if (HtmlRadio.IsChecked == true)
                SelectedFormat = ExportFormat.Html;
            else if (TextRadio.IsChecked == true)
                SelectedFormat = ExportFormat.Text;
            else
                SelectedFormat = ExportFormat.Csv; // Default

            // Get options
            IncludeDetails = IncludeDetailsCheck.IsChecked == true;
            IncludeProperties = IncludePropertiesCheck.IsChecked == true;
            UseCurrentTheme = UseCurrentThemeCheck.IsChecked == true;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}