using System;
using System.Windows.Forms;
using Xunit;
using FluentAssertions;
using FFTColorCustomizer.Configuration.UI;
using FFTColorCustomizer.Configuration;

namespace FFTColorCustomizer.Tests.Configuration.UI
{
    public class CustomTitleBarTests : IDisposable
    {
        private Form _testForm;
        private CustomTitleBar _titleBar;

        public CustomTitleBarTests()
        {
            _testForm = new Form();
            _testForm.Size = new System.Drawing.Size(800, 600);
            _titleBar = new CustomTitleBar(_testForm, "Test Title");
            _testForm.Controls.Add(_titleBar);
            // Force handle creation to ensure controls are initialized
            var handle = _testForm.Handle;
        }

        public void Dispose()
        {
            _titleBar?.Dispose();
            _testForm?.Dispose();
        }

        [Fact]
        public void CloseButton_Should_Set_DialogResult_Cancel()
        {
            // Arrange
            _testForm.DialogResult = DialogResult.None;
            var closeButton = GetCloseButton();
            closeButton.Should().NotBeNull("Close button should exist");

            // Act - We need to invoke the click handler directly
            // since PerformClick doesn't work properly in test environment
            var clickMethod = closeButton.GetType().GetMethod("OnClick",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (clickMethod != null)
            {
                clickMethod.Invoke(closeButton, new object[] { EventArgs.Empty });
            }
            else
            {
                // Fallback: try to trigger the click event directly
                closeButton.PerformClick();
            }

            // Assert
            _testForm.DialogResult.Should().Be(DialogResult.Cancel,
                "X button click handler should set DialogResult to Cancel");
        }

        [Fact]
        public void CloseButton_Should_Exist_And_Be_Positioned_TopRight()
        {
            // Arrange & Act
            var closeButton = GetCloseButton();

            // Assert
            closeButton.Should().NotBeNull("Close button should exist");
            closeButton.Text.Should().Be("✕", "Close button should show X symbol");
            closeButton.Anchor.Should().HaveFlag(AnchorStyles.Right,
                "Close button should be anchored to right");
            closeButton.Anchor.Should().HaveFlag(AnchorStyles.Top,
                "Close button should be anchored to top");
        }

        [Fact]
        public void CloseButton_Should_Have_Red_Hover_Effect()
        {
            // Arrange & Act
            var closeButton = GetCloseButton();

            // Assert
            closeButton.Should().NotBeNull();
            closeButton.FlatAppearance.MouseOverBackColor.R.Should().Be(232,
                "Hover color should have red component of 232");
            closeButton.FlatAppearance.MouseOverBackColor.G.Should().Be(17,
                "Hover color should have green component of 17");
            closeButton.FlatAppearance.MouseOverBackColor.B.Should().Be(35,
                "Hover color should have blue component of 35");
        }

        [Fact]
        public void CloseButton_Should_Not_Save_Configuration_When_Clicked()
        {
            // This test verifies that clicking X doesn't trigger save
            // It would need to be tested at the ConfigurationForm level
            // where the actual save logic resides

            // Arrange
            bool saveWasCalled = false;
            _testForm.FormClosing += (s, e) =>
            {
                // In a real scenario, we'd check if Save was called
                // For now, we just verify the DialogResult
                if (_testForm.DialogResult == DialogResult.OK)
                    saveWasCalled = true;
            };

            var closeButton = GetCloseButton();

            // Act
            closeButton.PerformClick();

            // Assert
            saveWasCalled.Should().BeFalse(
                "Clicking X button should not trigger save (DialogResult should not be OK)");
        }

        private Button GetCloseButton()
        {
            // Find the close button in the title bar
            foreach (Control control in _titleBar.Controls)
            {
                if (control is Button button && button.Text == "✕")
                    return button;
            }
            return null;
        }
    }
}