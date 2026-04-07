using System.Text.Json;
using FFTColorCustomizer.Utilities;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class ReadDialogueTests
    {
        [Fact]
        public void CommandResponse_Dialogue_SerializesWhenSet()
        {
            var response = new CommandResponse
            {
                Id = "test1",
                Status = "completed",
                Dialogue = "Ovelia: Please, don't fight.\nAgrias: Princess, stay behind me."
            };

            var json = JsonSerializer.Serialize(response);
            Assert.Contains("\"dialogue\":", json);
            Assert.Contains("Ovelia", json);
        }

        [Fact]
        public void CommandResponse_Dialogue_OmittedWhenNull()
        {
            var response = new CommandResponse
            {
                Id = "test2",
                Status = "completed"
            };

            var json = JsonSerializer.Serialize(response);
            Assert.DoesNotContain("dialogue", json);
        }
    }
}
