using System;
using System.Windows.Forms;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Configuration.UI;

namespace FFTColorCustomizer
{
    public class TestCarousel
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TestForm());
        }
    }

    public class TestForm : Form
    {
        public TestForm()
        {
            Text = "FFT Color Customizer - Debug Launcher";
            Width = 400;
            Height = 200;
            StartPosition = FormStartPosition.CenterScreen;

            var openConfigButton = new Button
            {
                Text = "Open Configuration",
                Width = 200,
                Height = 50,
                Left = 100,
                Top = 50
            };

            openConfigButton.Click += (s, e) =>
            {
                // Create a test config
                var config = new Config();

                // Get the mod installation path (current directory for testing)
                string modPath = System.IO.Path.GetDirectoryName(Application.ExecutablePath);

                // Create and show the configuration form
                using (var configForm = new ConfigurationForm(config, null, modPath))
                {
                    configForm.ShowDialog();
                }
            };

            Controls.Add(openConfigButton);

            var debugInfo = new Label
            {
                Text = "Click button to test carousel UI",
                Width = 300,
                Height = 20,
                Left = 50,
                Top = 120,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            Controls.Add(debugInfo);
        }
    }
}