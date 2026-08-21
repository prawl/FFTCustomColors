using Xunit;
using FFTColorCustomizer.Configuration;
using FFTColorCustomizer.Tests.Helpers;
using FFTColorCustomizer.ThemeEditor;

namespace Tests.Configuration
{
    // --- CC-27 follow-up: the "Theme Saved" message named the wrong thing for monsters ---
    //
    // Saving a theme for Bonesnatch (Skeleton family, rank II) showed "successfully saved the
    // Skeleton" instead of "Bonesnatch (rank 2)", and told the user to look in "Generic
    // Characters" instead of "Monsters" -- the family-vs-display-name split (JobName is the
    // family key, not what the user picked) leaked into user-facing text. These tests drive
    // ConfigurationForm.BuildThemeSavedMessage directly, since MessageBox itself can't be
    // asserted on.
    public class ThemeSavedMessageTests
    {
        private static TestConfigurationForm NewForm() => new TestConfigurationForm(new Config());

        [Fact]
        public void BuildThemeSavedMessage_ForAMonsterSave_NamesTheSelectedRank_NotTheFamilyKey()
        {
            using var form = NewForm();
            var args = new ThemeSavedEventArgs("Skeleton", "Bonesnatch Bones", new byte[512], "Bonesnatch (rank 2)");

            var message = form.BuildThemeSavedMessage(args);

            Assert.Contains("\"Bonesnatch (rank 2)\"", message);
            Assert.DoesNotContain("\"Skeleton\"", message);
        }

        [Fact]
        public void BuildThemeSavedMessage_ForAMonsterSave_NamesTheMonstersSection_NotGenericCharacters()
        {
            using var form = NewForm();
            var args = new ThemeSavedEventArgs("Skeleton", "Bonesnatch Bones", new byte[512], "Bonesnatch (rank 2)");

            var message = form.BuildThemeSavedMessage(args);

            Assert.Contains("\"Monsters\"", message);
            Assert.DoesNotContain("\"Generic Characters\"", message);
        }

        [Fact]
        public void BuildThemeSavedMessage_ForAMonsterSave_MentionsFamilyScope()
        {
            using var form = NewForm();
            var args = new ThemeSavedEventArgs("Skeleton", "Bonesnatch Bones", new byte[512], "Bonesnatch (rank 2)");

            var message = form.BuildThemeSavedMessage(args);

            Assert.Contains("every rank of the Skeleton family", message);
        }

        [Fact]
        public void BuildThemeSavedMessage_ForAGenericJobSave_ProducesTheExistingMessageUnchanged()
        {
            using var form = NewForm();
            var args = new ThemeSavedEventArgs("Squire_Male", "Ocean Blue", new byte[512]);

            var message = form.BuildThemeSavedMessage(args);

            Assert.Equal(
                "Theme 'Ocean Blue' saved successfully!\n\nYou can select it under \"Squire (Male)\" in the \"Generic Characters\" section above.",
                message);
        }

        [Fact]
        public void BuildThemeSavedMessage_ForAWotLJobSave_ProducesTheExistingMessageUnchanged()
        {
            using var form = NewForm();
            var args = new ThemeSavedEventArgs("DarkKnight_Male", "Midnight", new byte[512]);

            var message = form.BuildThemeSavedMessage(args);

            Assert.Equal(
                "Theme 'Midnight' saved successfully!\n\nYou can select it under \"DarkKnight (Male)\" in the \"WotL Jobs\" section above.",
                message);
        }

        [Fact]
        public void BuildThemeSavedMessage_ForAStoryCharacterSave_ProducesTheExistingMessageUnchanged()
        {
            using var form = NewForm();
            var args = new ThemeSavedEventArgs("Agrias", "Royal Guard", new byte[512]);

            var message = form.BuildThemeSavedMessage(args);

            Assert.Equal(
                "Theme 'Royal Guard' saved successfully!\n\nYou can select it under \"Agrias\" in the \"Story Characters\" section above.",
                message);
        }

        [Fact]
        public void BuildThemeSavedMessage_ForAnNpcSave_ProducesTheExistingMessageUnchanged()
        {
            using var form = NewForm();
            var args = new ThemeSavedEventArgs("Alma", "Pilot Blue", new byte[512]);

            var message = form.BuildThemeSavedMessage(args);

            Assert.Equal(
                "Theme 'Pilot Blue' saved successfully!\n\nYou can select it under \"Alma\" in the \"NPCs\" section above.",
                message);
        }
    }
}
