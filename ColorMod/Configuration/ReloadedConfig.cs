using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace FFTColorMod.Configuration
{
    /// <summary>
    /// Configuration class for Reloaded-II mod loader
    /// This will be displayed in the Reloaded-II configuration UI
    /// </summary>
    public class ReloadedConfig
    {
        [DisplayName("Male Knight Color")]
        [Description("Color scheme for all male knights")]
        [DefaultValue("original")]
        public string KnightMale { get; set; } = "original";

        [DisplayName("Female Knight Color")]
        [Description("Color scheme for all female knights")]
        [DefaultValue("original")]
        public string KnightFemale { get; set; } = "original";

        [DisplayName("Male Archer Color")]
        [Description("Color scheme for all male archers")]
        [DefaultValue("original")]
        public string ArcherMale { get; set; } = "original";

        [DisplayName("Female Archer Color")]
        [Description("Color scheme for all female archers")]
        [DefaultValue("original")]
        public string ArcherFemale { get; set; } = "original";

        [DisplayName("Male Monk Color")]
        [Description("Color scheme for all male monks")]
        [DefaultValue("original")]
        public string MonkMale { get; set; } = "original";

        [DisplayName("Female Monk Color")]
        [Description("Color scheme for all female monks")]
        [DefaultValue("original")]
        public string MonkFemale { get; set; } = "original";

        [DisplayName("Male Thief Color")]
        [Description("Color scheme for all male thieves")]
        [DefaultValue("original")]
        public string ThiefMale { get; set; } = "original";

        [DisplayName("Female Thief Color")]
        [Description("Color scheme for all female thieves")]
        [DefaultValue("original")]
        public string ThiefFemale { get; set; } = "original";

        [DisplayName("Male Dragoon Color")]
        [Description("Color scheme for all male dragoons")]
        [DefaultValue("original")]
        public string DragoonMale { get; set; } = "original";

        [DisplayName("Female Dragoon Color")]
        [Description("Color scheme for all female dragoons")]
        [DefaultValue("original")]
        public string DragoonFemale { get; set; } = "original";

        // Add more job properties as needed...

        public List<string> GetAvailableColorSchemes()
        {
            return new List<string>
            {
                "original",
                "corpse_brigade",
                "lucavi",
                "northern_sky",
                "southern_sky"
            };
        }
    }
}