using System.Windows.Forms;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests.Helpers
{
    /// <summary>
    /// A test-specific version of ConfigurationForm that prevents the form from showing.
    /// </summary>
    public class TestConfigurationForm : ConfigurationForm
    {
        private bool _allowShow;

        public TestConfigurationForm(Config config, string configPath = null, string modPath = null)
            : base(config, configPath, modPath)
        {
            _allowShow = false;
        }

        /// <summary>
        /// Override to prevent the form from actually showing during tests.
        /// </summary>
        protected override void SetVisibleCore(bool value)
        {
            // Only allow the form to be shown if explicitly allowed
            // This prevents the form from appearing during handle creation
            if (!IsHandleCreated && value)
            {
                CreateHandle();
                value = false;
            }
            base.SetVisibleCore(_allowShow && value);
        }

        /// <summary>
        /// Allow the form to be shown (for tests that really need it).
        /// </summary>
        public void AllowShow()
        {
            _allowShow = true;
        }
    }
}
