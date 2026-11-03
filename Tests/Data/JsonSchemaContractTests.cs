using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;

namespace Tests.Data
{
    /// <summary>
    /// Contract for every JSON file the mod ships. Until now a typo in a recipe or a
    /// registry failed silently at runtime; this walks the repo, checks each family against a
    /// schema under Tests/Data/Schemas/, and fails the build instead. The straggler test is the
    /// load-bearing half: a new data file that belongs to no known family goes red here, so
    /// nothing can be added without also being validated.
    /// </summary>
    public class JsonSchemaContractTests
    {
        private static string RepoRoot()
        {
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."),
                Directory.GetCurrentDirectory(),
            };
            foreach (var c in candidates)
            {
                if (File.Exists(Path.Combine(c, "ColorMod", "Data", "StoryCharacters.json")))
                    return Path.GetFullPath(c);
            }
            throw new InvalidOperationException("Repo root not found");
        }

        private static string SchemaDir => Path.Combine(RepoRoot(), "Tests", "Data", "Schemas");

        // JsonSchema.Net registers each loaded schema by its URI and refuses to load the same
        // file twice, so every load in this class goes through this cache.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, JsonSchema> SchemaCache = new();
        private static JsonSchema LoadSchema(string path) =>
            SchemaCache.GetOrAdd(Path.GetFullPath(path), p => JsonSchema.FromFile(p));

        // Publish staging areas are gitignored build caches, never the hand-edited source.
        private static bool IsIgnored(string path) =>
            path.Replace('\\', '/').Contains("/Publish/");

        /// <summary>Family name -> (schema file, files the family claims).</summary>
        private static Dictionary<string, (string Schema, string[] Files)> Families()
        {
            var root = RepoRoot();
            string[] Glob(string dir, string pattern, SearchOption opt = SearchOption.TopDirectoryOnly)
            {
                var d = Path.Combine(root, dir);
                return Directory.Exists(d)
                    ? Directory.GetFiles(d, pattern, opt).Where(f => !IsIgnored(f)).ToArray()
                    : Array.Empty<string>();
            }

            return new()
            {
                ["story-characters"] = ("story-characters.schema.json",
                    new[] { Path.Combine(root, "ColorMod", "Data", "StoryCharacters.json") }),
                ["job-classes"] = ("job-classes.schema.json", new[]
                {
                    Path.Combine(root, "ColorMod", "Data", "JobClasses.json"),
                    Path.Combine(root, "ColorMod", "Data", "WotLClasses.json"),
                }),
                ["section-mapping"] = ("section-mapping.schema.json",
                    Glob(Path.Combine("ColorMod", "Data", "SectionMappings"), "*.json", SearchOption.AllDirectories)),
                ["builtin-theme-recipe"] = ("builtin-theme-recipe.schema.json",
                    Glob(Path.Combine("ColorMod", "Data", "BuiltInThemes"), "*.json", SearchOption.AllDirectories)),
                ["reloaded-mod-config"] = ("reloaded-mod-config.schema.json",
                    new[] { Path.Combine(root, "ColorMod", "ModConfig.json") }),
                // Parse-only families: shape is owned by another system, a schema would just
                // restate it, but a syntax error must still fail the build.
                ["parse-only"] = ("", new[]
                {
                    Path.Combine(root, "ColorMod", "Config.json"),
                    Path.Combine(root, "ColorMod", "ModUserConfig.json"),
                }),
            };
        }

        public static TheoryData<string> AllDataFiles()
        {
            var data = new TheoryData<string>();
            foreach (var f in Families().Values.SelectMany(v => v.Files))
                data.Add(f);
            return data;
        }

        [Theory]
        [MemberData(nameof(AllDataFiles))]
        public void Every_Data_File_Parses(string path)
        {
            Assert.True(File.Exists(path), $"Registered data file missing: {path}");
            var ex = Record.Exception(() => JsonNode.Parse(File.ReadAllText(path)));
            Assert.True(ex == null, $"{path} is not valid JSON: {ex?.Message}");
        }

        [Fact]
        public void Every_Family_Has_Its_Schema_File()
        {
            foreach (var (family, (schema, _)) in Families())
            {
                if (schema.Length == 0) continue;
                var p = Path.Combine(SchemaDir, schema);
                Assert.True(File.Exists(p), $"Missing schema for family '{family}': {p}");
            }
        }

        public static TheoryData<string, string> FileSchemaPairs()
        {
            var data = new TheoryData<string, string>();
            foreach (var (_, (schema, files)) in Families())
            {
                if (schema.Length == 0) continue;
                foreach (var f in files)
                    data.Add(f, schema);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(FileSchemaPairs))]
        public void Every_Data_File_Matches_Its_Familys_Schema(string path, string schemaFile)
        {
            var schema = LoadSchema(Path.Combine(SchemaDir, schemaFile));
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var result = schema.Evaluate(doc.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
            });

            var errors = result.Details
                .Where(d => d.Errors != null && d.Errors.Count > 0)
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
                .Distinct()
                .ToList();
            Assert.True(result.IsValid,
                $"{Path.GetFileName(path)} violates {schemaFile}:\n  " + string.Join("\n  ", errors.Take(12)));
        }

        /// <summary>
        /// The straggler guard. Every tracked .json under ColorMod must belong to a family above,
        /// so a future data file cannot ship unvalidated by simply never being registered.
        /// </summary>
        [Fact]
        public void No_Shipped_Json_Escapes_Validation()
        {
            var root = RepoRoot();
            var known = Families().Values
                .SelectMany(v => v.Files)
                .Select(Path.GetFullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var strays = Directory.GetFiles(Path.Combine(root, "ColorMod"), "*.json", SearchOption.AllDirectories)
                .Where(f => !IsIgnored(f))
                .Where(f => !f.Replace('\\', '/').Contains("/obj/") && !f.Replace('\\', '/').Contains("/bin/"))
                .Select(Path.GetFullPath)
                .Where(f => !known.Contains(f))
                .ToList();

            Assert.True(strays.Count == 0,
                "Unvalidated JSON shipped under ColorMod. Register each in Families() with a schema:\n  "
                + string.Join("\n  ", strays));
        }

        /// <summary>Schemas themselves must parse as JSON, or a bad edit validates nothing.</summary>
        [Fact]
        public void Every_Schema_File_Parses()
        {
            Assert.True(Directory.Exists(SchemaDir), $"Missing schema directory: {SchemaDir}");
            foreach (var f in Directory.GetFiles(SchemaDir, "*.schema.json"))
            {
                var ex = Record.Exception(() => LoadSchema(f));
                Assert.True(ex == null, $"{f} is not a loadable schema: {ex?.Message}");
            }
        }
    }
}
