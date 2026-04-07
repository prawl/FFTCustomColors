using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class EventScriptLookupTests
    {
        [Fact]
        public void GetScript_ValidEventId_ReturnsDialogue()
        {
            var mesDir = "c:/Users/ptyRa/OneDrive/Desktop/Pac Files/0002.en/fftpack/text/";
            if (!Directory.Exists(mesDir)) return; // Skip if files not available

            var lookup = new EventScriptLookup(mesDir);
            var script = lookup.GetScript(2);

            Assert.NotNull(script);
            Assert.Contains(script!.Lines, l => l.Text.Contains("Lady Ovelia"));
        }

        [Fact]
        public void GetScript_InvalidEventId_ReturnsNull()
        {
            var mesDir = "c:/Users/ptyRa/OneDrive/Desktop/Pac Files/0002.en/fftpack/text/";
            if (!Directory.Exists(mesDir)) return;

            var lookup = new EventScriptLookup(mesDir);
            var script = lookup.GetScript(9999);

            Assert.Null(script);
        }

        [Fact]
        public void GetScript_Event10_HasRogueDialogue()
        {
            var mesDir = "c:/Users/ptyRa/OneDrive/Desktop/Pac Files/0002.en/fftpack/text/";
            if (!Directory.Exists(mesDir)) return;

            var lookup = new EventScriptLookup(mesDir);
            var script = lookup.GetScript(10);

            Assert.NotNull(script);
            Assert.Contains(script!.Lines, l => l.Speaker == "Rogue");
        }

        [Fact]
        public void GetScript_Event4_HasAgriasDialogue()
        {
            var mesDir = "c:/Users/ptyRa/OneDrive/Desktop/Pac Files/0002.en/fftpack/text/";
            if (!Directory.Exists(mesDir)) return;

            var lookup = new EventScriptLookup(mesDir);
            var script = lookup.GetScript(4);

            Assert.NotNull(script);
            Assert.Contains(script!.Lines, l => l.Speaker == "Agrias");
        }

        [Fact]
        public void GetFormattedScript_ReturnsReadableText()
        {
            var mesDir = "c:/Users/ptyRa/OneDrive/Desktop/Pac Files/0002.en/fftpack/text/";
            if (!Directory.Exists(mesDir)) return;

            var lookup = new EventScriptLookup(mesDir);
            var text = lookup.GetFormattedScript(2);

            Assert.NotNull(text);
            Assert.Contains("Knight:", text!);
            Assert.Contains("Lady Ovelia, it is time.", text!);
        }
    }
}
