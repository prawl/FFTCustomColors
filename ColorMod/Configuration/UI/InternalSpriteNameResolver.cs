using System.Linq;
using FFTColorCustomizer.Services;

namespace FFTColorCustomizer.Configuration.UI
{
    /// <summary>
    /// Turns a config-window character name ("Argath") into the internal FFT sprite name
    /// ("aru"), the one that fills in battle_&lt;name&gt;_spr.bin.
    ///
    /// The character registry (Data/StoryCharacters.json) is the source of truth, because it
    /// is also what the in-game apply path reads. Resolving both sides from the same place
    /// means a preview can never disagree with what the game is shown, and adding a character
    /// to the roster needs no code change here.
    ///
    /// The alias table is only for names that are NOT registry entries (bare "Ramza", which
    /// the registry splits into three chapter rows, and "Delita", who has no row yet).
    /// </summary>
    public static class InternalSpriteNameResolver
    {
        public static string Resolve(string characterName, CharacterDefinitionService characterService)
        {
            if (string.IsNullOrEmpty(characterName))
                return characterName;

            var registered = characterService?.GetCharacterByName(characterName);
            var spriteName = registered?.SpriteNames?.FirstOrDefault();
            if (!string.IsNullOrEmpty(spriteName))
                return spriteName;

            return characterName.ToLower() switch
            {
                "ramza" => "ramuza",
                "ramzachapter1" => "ramuza",
                "ramzachapter23" => "ramuza2",
                "ramzachapter4" => "ramuza3",
                "delita" => "dily",
                var other => other
            };
        }
    }
}
