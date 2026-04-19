using FFTColorCustomizer.Utilities;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    /// <summary>
    /// Characterization pin on the CommandRequest JSON schema. The shell
    /// helpers (fft.sh) write JSON directly with hardcoded field names —
    /// a silent rename on the C# side would break EVERY helper without a
    /// compile error. These tests lock in the canonical field names +
    /// default values so renames must pass through an explicit test flip.
    ///
    /// If one of these fails because you renamed a field:
    ///   1. Update the test here
    ///   2. Grep fft.sh for the old name and update every emitter
    ///   3. Update any C# callers using the field
    ///
    /// Adding a new field? Add an assertion here so the schema pin stays
    /// complete.
    /// </summary>
    public class CommandRequestSchemaTests
    {
        [Fact]
        public void Id_IsCalled_id()
        {
            var json = JsonSerializer.Serialize(new CommandRequest { Id = "abc" });
            Assert.Contains("\"id\":\"abc\"", json);
        }

        [Fact]
        public void Action_IsCalled_action()
        {
            var json = JsonSerializer.Serialize(new CommandRequest { Action = "scan_units" });
            Assert.Contains("\"action\":\"scan_units\"", json);
        }

        [Fact]
        public void To_IsCalled_to_NotToScreen()
        {
            // This is the one most likely to bite: To → ToScreen would be
            // a tempting rename, and execute_action flows all pass `to`.
            var json = JsonSerializer.Serialize(new CommandRequest { To = "WorldMap" });
            Assert.Contains("\"to\":\"WorldMap\"", json);
            Assert.DoesNotContain("\"toScreen\"", json);
        }

        [Fact]
        public void Pattern_IsCalled_pattern()
        {
            var json = JsonSerializer.Serialize(new CommandRequest { Pattern = "AABB" });
            Assert.Contains("\"pattern\":\"AABB\"", json);
        }

        [Fact]
        public void MinMaxAddr_AreCalled_minAddr_maxAddr_OmittedWhenNull()
        {
            // Session 47 addition. When both are null, they must be
            // omitted from the JSON so existing callers don't get
            // surprised by new required fields.
            var jsonEmpty = JsonSerializer.Serialize(new CommandRequest());
            Assert.DoesNotContain("\"minAddr\"", jsonEmpty);
            Assert.DoesNotContain("\"maxAddr\"", jsonEmpty);

            var jsonSet = JsonSerializer.Serialize(new CommandRequest
            {
                MinAddr = "0x4000000000",
                MaxAddr = "0x4200000000"
            });
            Assert.Contains("\"minAddr\":\"0x4000000000\"", jsonSet);
            Assert.Contains("\"maxAddr\":\"0x4200000000\"", jsonSet);
        }

        [Theory]
        [InlineData("\"searchValue\"", "SearchValue")]
        [InlineData("\"searchLabel\"", "SearchLabel")]
        [InlineData("\"fromLabel\"", "FromLabel")]
        [InlineData("\"toLabel\"", "ToLabel")]
        [InlineData("\"delayBetweenMs\"", "DelayBetweenMs")]
        [InlineData("\"description\"", "Description")]
        [InlineData("\"locationId\"", "LocationId")]
        [InlineData("\"unitIndex\"", "UnitIndex")]
        [InlineData("\"direction\"", "Direction")]
        [InlineData("\"waitForScreen\"", "WaitForScreen")]
        [InlineData("\"waitUntilScreenNot\"", "WaitUntilScreenNot")]
        [InlineData("\"waitTimeoutMs\"", "WaitTimeoutMs")]
        [InlineData("\"pattern\"", "Pattern")]
        [InlineData("\"address\"", "Address")]
        [InlineData("\"readSize\"", "ReadSize")]
        [InlineData("\"blockSize\"", "BlockSize")]
        [InlineData("\"slot\"", "Slot")]
        [InlineData("\"verbose\"", "Verbose")]
        public void KnownField_SerializesWith_LowerCaseName(string expectedJsonFragment, string propName)
        {
            // Force every property to emit (not just non-defaults) by
            // setting sentinel values. Confirms all [JsonPropertyName]
            // attributes stay in lowerCamelCase.
            var req = new CommandRequest
            {
                Action = "test",
                SearchValue = 99,
                SearchLabel = "x",
                FromLabel = "x",
                ToLabel = "x",
                DelayBetweenMs = 777,
                Description = "x",
                LocationId = 99,
                UnitIndex = 99,
                Direction = "x",
                WaitForScreen = "x",
                WaitUntilScreenNot = "x",
                WaitTimeoutMs = 12345,
                Pattern = "AABB",
                Address = "0x1000",
                ReadSize = 8,
                BlockSize = 32,
                Slot = 3,
                Verbose = true,
            };
            var json = JsonSerializer.Serialize(req);
            Assert.Contains(expectedJsonFragment, json);
        }

        [Fact]
        public void Defaults_MatchHistoricalContract()
        {
            // Pin default values that callers rely on without explicitly
            // setting them. If a default changes, every caller that assumed
            // it needs to be audited.
            var req = new CommandRequest();
            Assert.Equal(-1, req.Slot);            // sentinel for "not set"
            Assert.Equal(-1, req.LocationId);      // ditto
            Assert.Equal(0, req.UnitIndex);
            Assert.Equal(150, req.DelayBetweenMs);
            Assert.Equal(1, req.ReadSize);
            Assert.Equal(0, req.BlockSize);
            Assert.Equal(2000, req.WaitTimeoutMs);
            Assert.False(req.Verbose);
            Assert.NotNull(req.Keys); // empty list, not null
            Assert.Empty(req.Keys);
        }

        [Fact]
        public void RoundTrip_PreservesKeys()
        {
            // Common shape: keys array with VK + name. Used by execute_key.
            var original = new CommandRequest
            {
                Id = "1",
                Action = "execute_key",
                Keys = new List<KeyCommand>
                {
                    new() { Vk = 0x1B, Name = "Escape" },
                    new() { Vk = 0x0D, Name = "Enter", HoldMs = 100 },
                }
            };
            var json = JsonSerializer.Serialize(original);
            var roundtrip = JsonSerializer.Deserialize<CommandRequest>(json);

            Assert.NotNull(roundtrip);
            Assert.Equal(2, roundtrip!.Keys.Count);
            Assert.Equal(0x1B, roundtrip.Keys[0].Vk);
            Assert.Equal("Escape", roundtrip.Keys[0].Name);
            Assert.Equal(100, roundtrip.Keys[1].HoldMs);
        }

        [Fact]
        public void UnknownField_InIncomingJson_IsIgnored()
        {
            // Backwards compat: if shell helpers ever add new fields ahead
            // of a C# deploy, the C# side must not throw. System.Text.Json
            // default is to ignore unknown properties; pin that behavior.
            var json = """{"id":"1","action":"scan_move","someFutureField":"x","anotherNew":99}""";
            var req = JsonSerializer.Deserialize<CommandRequest>(json);
            Assert.NotNull(req);
            Assert.Equal("1", req!.Id);
            Assert.Equal("scan_move", req.Action);
        }
    }
}
