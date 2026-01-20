using System;
using System.IO;
using FFTColorCustomizer.Configuration;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;


namespace FFTColorCustomizer.Tests
{
    public class DirectPropertyTest
    {
        private readonly ITestOutputHelper _output;

        public DirectPropertyTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Test_Direct_Property_Setting()
        {
            var config = new Config();

            // Directly set the property
            config["Archer_Female"] = "lucavi"; // lucavi

            _output.WriteLine($"After direct set - Archer_Female: {config["Archer_Female"]}");
            _output.WriteLine($"After direct set - Squire_Male: {config["Squire_Male"]}");

            // Now serialize it
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                Converters = new[] { new Newtonsoft.Json.Converters.StringEnumConverter() }
            };

            var json = JsonConvert.SerializeObject(config, settings);
            _output.WriteLine($"Serialized JSON (first 500 chars):");
            _output.WriteLine(json.Substring(0, Math.Min(500, json.Length)));

            // Deserialize it back
            var config2 = JsonConvert.DeserializeObject<Config>(json, settings);
            _output.WriteLine($"After deserialize - Archer_Female: {config2["Archer_Female"]}");
            _output.WriteLine($"After deserialize - Squire_Male: {config2["Squire_Male"]}");

            Assert.Equal("lucavi", config2["Archer_Female"]); // lucavi
            Assert.Equal("original", config2["Squire_Male"]); // original
        }

        [Fact]
        public void Test_Indexer_Property_Setting()
        {
            var config = new Config();

            // Set via indexer (dictionary-based)
            _output.WriteLine($"Setting Archer_Female via indexer...");

            config["Archer_Female"] = "lucavi"; // lucavi

            _output.WriteLine($"After indexer set - Archer_Female: {config["Archer_Female"]}");
            _output.WriteLine($"After indexer set - Squire_Male: {config["Squire_Male"]}");

            // Verify the right property was set
            Assert.Equal("lucavi", config["Archer_Female"]); // lucavi
            Assert.Equal("original", config["Squire_Male"]); // original
        }
    }
}
