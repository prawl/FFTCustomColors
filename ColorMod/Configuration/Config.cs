using System.ComponentModel;
using System.Text.Json.Serialization;

namespace FFTColorMod.Configuration
{
    public class Config : Configurable<Config>
    {
        // Squires (starting class)
        [DisplayName("Male Squire")]
        [Description("Color scheme for all male squires")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("SquireMale")]
        public ColorScheme Squire_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Squire")]
        [Description("Color scheme for all female squires")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("SquireFemale")]
        public ColorScheme Squire_Female { get; set; } = ColorScheme.original;

        // Knights
        [DisplayName("Male Knight")]
        [Description("Color scheme for all male knights")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("KnightMale")]
        public ColorScheme Knight_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Knight")]
        [Description("Color scheme for all female knights")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("KnightFemale")]
        public ColorScheme Knight_Female { get; set; } = ColorScheme.original;

        // Monks
        [DisplayName("Male Monk")]
        [Description("Color scheme for all male monks")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("MonkMale")]
        public ColorScheme Monk_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Monk")]
        [Description("Color scheme for all female monks")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("MonkFemale")]
        public ColorScheme Monk_Female { get; set; } = ColorScheme.original;

        // Archers
        [DisplayName("Male Archer")]
        [Description("Color scheme for all male archers")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("ArcherMale")]
        public ColorScheme Archer_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Archer")]
        [Description("Color scheme for all female archers")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("ArcherFemale")]
        public ColorScheme Archer_Female { get; set; } = ColorScheme.original;

        // White Mages
        [DisplayName("Male White Mage")]
        [Description("Color scheme for all male white mages")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("WhiteMageMale")]
        public ColorScheme WhiteMage_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female White Mage")]
        [Description("Color scheme for all female white mages")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("WhiteMageFemale")]
        public ColorScheme WhiteMage_Female { get; set; } = ColorScheme.original;

        // Black Mages
        [DisplayName("Male Black Mage")]
        [Description("Color scheme for all male black mages")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("BlackMageMale")]
        public ColorScheme BlackMage_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Black Mage")]
        [Description("Color scheme for all female black mages")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("BlackMageFemale")]
        public ColorScheme BlackMage_Female { get; set; } = ColorScheme.original;

        // Time Mages
        [DisplayName("Male Time Mage")]
        [Description("Color scheme for all male time mages")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("TimeMageMale")]
        public ColorScheme TimeMage_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Time Mage")]
        [Description("Color scheme for all female time mages")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("TimeMageFemale")]
        public ColorScheme TimeMage_Female { get; set; } = ColorScheme.original;

        // Summoners
        [DisplayName("Male Summoner")]
        [Description("Color scheme for all male summoners")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("SummonerMale")]
        public ColorScheme Summoner_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Summoner")]
        [Description("Color scheme for all female summoners")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("SummonerFemale")]
        public ColorScheme Summoner_Female { get; set; } = ColorScheme.original;

        // Thieves
        [DisplayName("Male Thief")]
        [Description("Color scheme for all male thieves")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("ThiefMale")]
        public ColorScheme Thief_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Thief")]
        [Description("Color scheme for all female thieves")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("ThiefFemale")]
        public ColorScheme Thief_Female { get; set; } = ColorScheme.original;

        // Ninjas
        [DisplayName("Male Ninja")]
        [Description("Color scheme for all male ninjas")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("NinjaMale")]
        public ColorScheme Ninja_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Ninja")]
        [Description("Color scheme for all female ninjas")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("NinjaFemale")]
        public ColorScheme Ninja_Female { get; set; } = ColorScheme.original;

        // Samurai
        [DisplayName("Male Samurai")]
        [Description("Color scheme for all male samurai")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("SamuraiMale")]
        public ColorScheme Samurai_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Samurai")]
        [Description("Color scheme for all female samurai")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("SamuraiFemale")]
        public ColorScheme Samurai_Female { get; set; } = ColorScheme.original;

        // Dragoons
        [DisplayName("Male Dragoon")]
        [Description("Color scheme for all male dragoons")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("DragoonMale")]
        public ColorScheme Dragoon_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Dragoon")]
        [Description("Color scheme for all female dragoons")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("DragoonFemale")]
        public ColorScheme Dragoon_Female { get; set; } = ColorScheme.original;

        // Chemists
        [DisplayName("Male Chemist")]
        [Description("Color scheme for all male chemists")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("ChemistMale")]
        public ColorScheme Chemist_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Chemist")]
        [Description("Color scheme for all female chemists")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("ChemistFemale")]
        public ColorScheme Chemist_Female { get; set; } = ColorScheme.original;

        // Dancers (Female only)
        [DisplayName("Female Dancer")]
        [Description("Color scheme for all dancers")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("DancerFemale")]
        public ColorScheme Dancer_Female { get; set; } = ColorScheme.original;

        // Bards (Male only)
        [DisplayName("Male Bard")]
        [Description("Color scheme for all bards")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("BardMale")]
        public ColorScheme Bard_Male { get; set; } = ColorScheme.original;

        // Mimes
        [DisplayName("Male Mime")]
        [Description("Color scheme for all male mimes")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("MimeMale")]
        public ColorScheme Mime_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Mime")]
        [Description("Color scheme for all female mimes")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("MimeFemale")]
        public ColorScheme Mime_Female { get; set; } = ColorScheme.original;

        // Calculators/Arithmeticians
        [DisplayName("Male Calculator")]
        [Description("Color scheme for all male calculators")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("CalculatorMale")]
        public ColorScheme Calculator_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Calculator")]
        [Description("Color scheme for all female calculators")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("CalculatorFemale")]
        public ColorScheme Calculator_Female { get; set; } = ColorScheme.original;

        // Mediators/Orators
        [DisplayName("Male Mediator")]
        [Description("Color scheme for all male mediators")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("MediatorMale")]
        public ColorScheme Mediator_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Mediator")]
        [Description("Color scheme for all female mediators")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("MediatorFemale")]
        public ColorScheme Mediator_Female { get; set; } = ColorScheme.original;

        // Mystics/Oracles
        [DisplayName("Male Mystic")]
        [Description("Color scheme for all male mystics")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("MysticMale")]
        public ColorScheme Mystic_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Mystic")]
        [Description("Color scheme for all female mystics")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("MysticFemale")]
        public ColorScheme Mystic_Female { get; set; } = ColorScheme.original;

        // Geomancers
        [DisplayName("Male Geomancer")]
        [Description("Color scheme for all male geomancers")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("GeomancerMale")]
        public ColorScheme Geomancer_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Geomancer")]
        [Description("Color scheme for all female geomancers")]
        [DefaultValue(ColorScheme.original)]
        [JsonPropertyName("GeomancerFemale")]
        public ColorScheme Geomancer_Female { get; set; } = ColorScheme.original;

        public string GetColorForSprite(string spriteName)
        {
            // Map sprite filename to the appropriate config property
            // Based on Better Palettes folder structure from mappings.txt

            // Knights
            if (spriteName.Contains("knight_m"))
                return Knight_Male.GetDescription();
            if (spriteName.Contains("knight_w"))
                return Knight_Female.GetDescription();

            // Archers (yumi = bow in Japanese)
            if (spriteName.Contains("yumi_m"))
                return Archer_Male.GetDescription();
            if (spriteName.Contains("yumi_w"))
                return Archer_Female.GetDescription();

            // Chemists (item)
            if (spriteName.Contains("item_m"))
                return Chemist_Male.GetDescription();
            if (spriteName.Contains("item_w"))
                return Chemist_Female.GetDescription();

            // Monks
            if (spriteName.Contains("monk_m"))
                return Monk_Male.GetDescription();
            if (spriteName.Contains("monk_w"))
                return Monk_Female.GetDescription();

            // White Mages (siro)
            if (spriteName.Contains("siro_m"))
                return WhiteMage_Male.GetDescription();
            if (spriteName.Contains("siro_w"))
                return WhiteMage_Female.GetDescription();

            // Black Mages (kuro)
            if (spriteName.Contains("kuro_m"))
                return BlackMage_Male.GetDescription();
            if (spriteName.Contains("kuro_w"))
                return BlackMage_Female.GetDescription();

            // Thieves
            if (spriteName.Contains("thief_m"))
                return Thief_Male.GetDescription();
            if (spriteName.Contains("thief_w"))
                return Thief_Female.GetDescription();

            // Ninjas
            if (spriteName.Contains("ninja_m"))
                return Ninja_Male.GetDescription();
            if (spriteName.Contains("ninja_w"))
                return Ninja_Female.GetDescription();

            // Squires (mina)
            if (spriteName.Contains("mina_m"))
                return Squire_Male.GetDescription();
            if (spriteName.Contains("mina_w"))
                return Squire_Female.GetDescription();

            // Time Mages (toki)
            if (spriteName.Contains("toki_m"))
                return TimeMage_Male.GetDescription();
            if (spriteName.Contains("toki_w"))
                return TimeMage_Female.GetDescription();

            // Summoners (syou)
            if (spriteName.Contains("syou_m"))
                return Summoner_Male.GetDescription();
            if (spriteName.Contains("syou_w"))
                return Summoner_Female.GetDescription();

            // Samurai (samu)
            if (spriteName.Contains("samu_m"))
                return Samurai_Male.GetDescription();
            if (spriteName.Contains("samu_w"))
                return Samurai_Female.GetDescription();

            // Dragoons (ryu)
            if (spriteName.Contains("ryu_m"))
                return Dragoon_Male.GetDescription();
            if (spriteName.Contains("ryu_w"))
                return Dragoon_Female.GetDescription();

            // Geomancers (fusui)
            if (spriteName.Contains("fusui_m"))
                return Geomancer_Male.GetDescription();
            if (spriteName.Contains("fusui_w"))
                return Geomancer_Female.GetDescription();

            // Oracles/Mystics (onmyo)
            if (spriteName.Contains("onmyo_m"))
                return Mystic_Male.GetDescription();
            if (spriteName.Contains("onmyo_w"))
                return Mystic_Female.GetDescription();

            // Mediators/Orators (waju)
            if (spriteName.Contains("waju_m"))
                return Mediator_Male.GetDescription();
            if (spriteName.Contains("waju_w"))
                return Mediator_Female.GetDescription();

            // Dancers (odori - female only)
            if (spriteName.Contains("odori_w"))
                return Dancer_Female.GetDescription();

            // Bards (gin - male only)
            if (spriteName.Contains("gin_m"))
                return Bard_Male.GetDescription();

            // Mimes (mono)
            if (spriteName.Contains("mono_m"))
                return Mime_Male.GetDescription();
            if (spriteName.Contains("mono_w"))
                return Mime_Female.GetDescription();

            // Calculators/Arithmeticians (san)
            if (spriteName.Contains("san_m"))
                return Calculator_Male.GetDescription();
            if (spriteName.Contains("san_w"))
                return Calculator_Female.GetDescription();

            // Default to original if no mapping found
            return ColorScheme.original.GetDescription();
        }
    }
}