using System.ComponentModel;

namespace FFTColorMod.Configuration
{
    public class Config : Configurable<Config>
    {
        // Squires (starting class)
        [DisplayName("Male Squire")]
        [Description("Color scheme for all male squires")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Squire_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Squire")]
        [Description("Color scheme for all female squires")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Squire_Female { get; set; } = ColorScheme.original;

        // Knights
        [DisplayName("Male Knight")]
        [Description("Color scheme for all male knights")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Knight_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Knight")]
        [Description("Color scheme for all female knights")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Knight_Female { get; set; } = ColorScheme.original;

        // Monks
        [DisplayName("Male Monk")]
        [Description("Color scheme for all male monks")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Monk_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Monk")]
        [Description("Color scheme for all female monks")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Monk_Female { get; set; } = ColorScheme.original;

        // Archers
        [DisplayName("Male Archer")]
        [Description("Color scheme for all male archers")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Archer_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Archer")]
        [Description("Color scheme for all female archers")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Archer_Female { get; set; } = ColorScheme.original;

        // White Mages
        [DisplayName("Male White Mage")]
        [Description("Color scheme for all male white mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme WhiteMage_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female White Mage")]
        [Description("Color scheme for all female white mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme WhiteMage_Female { get; set; } = ColorScheme.original;

        // Black Mages
        [DisplayName("Male Black Mage")]
        [Description("Color scheme for all male black mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme BlackMage_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Black Mage")]
        [Description("Color scheme for all female black mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme BlackMage_Female { get; set; } = ColorScheme.original;

        // Time Mages
        [DisplayName("Male Time Mage")]
        [Description("Color scheme for all male time mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme TimeMage_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Time Mage")]
        [Description("Color scheme for all female time mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme TimeMage_Female { get; set; } = ColorScheme.original;

        // Summoners
        [DisplayName("Male Summoner")]
        [Description("Color scheme for all male summoners")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Summoner_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Summoner")]
        [Description("Color scheme for all female summoners")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Summoner_Female { get; set; } = ColorScheme.original;

        // Thieves
        [DisplayName("Male Thief")]
        [Description("Color scheme for all male thieves")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Thief_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Thief")]
        [Description("Color scheme for all female thieves")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Thief_Female { get; set; } = ColorScheme.original;

        // Ninjas
        [DisplayName("Male Ninja")]
        [Description("Color scheme for all male ninjas")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Ninja_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Ninja")]
        [Description("Color scheme for all female ninjas")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Ninja_Female { get; set; } = ColorScheme.original;

        // Samurai
        [DisplayName("Male Samurai")]
        [Description("Color scheme for all male samurai")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Samurai_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Samurai")]
        [Description("Color scheme for all female samurai")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Samurai_Female { get; set; } = ColorScheme.original;

        // Dragoons
        [DisplayName("Male Dragoon")]
        [Description("Color scheme for all male dragoons")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Dragoon_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Dragoon")]
        [Description("Color scheme for all female dragoons")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Dragoon_Female { get; set; } = ColorScheme.original;

        // Chemists
        [DisplayName("Male Chemist")]
        [Description("Color scheme for all male chemists")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Chemist_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Chemist")]
        [Description("Color scheme for all female chemists")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Chemist_Female { get; set; } = ColorScheme.original;

        // Dancers (Female only)
        [DisplayName("Female Dancer")]
        [Description("Color scheme for all dancers")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Dancer_Female { get; set; } = ColorScheme.original;

        // Bards (Male only)
        [DisplayName("Male Bard")]
        [Description("Color scheme for all bards")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Bard_Male { get; set; } = ColorScheme.original;

        // Mimes
        [DisplayName("Male Mime")]
        [Description("Color scheme for all male mimes")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Mime_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Mime")]
        [Description("Color scheme for all female mimes")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Mime_Female { get; set; } = ColorScheme.original;

        // Calculators/Arithmeticians
        [DisplayName("Male Calculator")]
        [Description("Color scheme for all male calculators")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Calculator_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Calculator")]
        [Description("Color scheme for all female calculators")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Calculator_Female { get; set; } = ColorScheme.original;

        // Mediators/Orators
        [DisplayName("Male Mediator")]
        [Description("Color scheme for all male mediators")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Mediator_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Mediator")]
        [Description("Color scheme for all female mediators")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Mediator_Female { get; set; } = ColorScheme.original;

        // Mystics/Oracles
        [DisplayName("Male Mystic")]
        [Description("Color scheme for all male mystics")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Mystic_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Mystic")]
        [Description("Color scheme for all female mystics")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Mystic_Female { get; set; } = ColorScheme.original;

        // Geomancers
        [DisplayName("Male Geomancer")]
        [Description("Color scheme for all male geomancers")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Geomancer_Male { get; set; } = ColorScheme.original;

        [DisplayName("Female Geomancer")]
        [Description("Color scheme for all female geomancers")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme Geomancer_Female { get; set; } = ColorScheme.original;

        public string GetColorForSprite(string spriteName)
        {
            // Map sprite filename to the appropriate config property
            // Based on Better Palettes folder structure from mappings.txt

            // Knights
            if (spriteName.Contains("knight_m"))
                return Knight_Male.ToString();
            if (spriteName.Contains("knight_w"))
                return Knight_Female.ToString();

            // Archers (yumi = bow in Japanese)
            if (spriteName.Contains("yumi_m"))
                return Archer_Male.ToString();
            if (spriteName.Contains("yumi_w"))
                return Archer_Female.ToString();

            // Chemists (item)
            if (spriteName.Contains("item_m"))
                return Chemist_Male.ToString();
            if (spriteName.Contains("item_w"))
                return Chemist_Female.ToString();

            // Monks
            if (spriteName.Contains("monk_m"))
                return Monk_Male.ToString();
            if (spriteName.Contains("monk_w"))
                return Monk_Female.ToString();

            // White Mages (siro)
            if (spriteName.Contains("siro_m"))
                return WhiteMage_Male.ToString();
            if (spriteName.Contains("siro_w"))
                return WhiteMage_Female.ToString();

            // Black Mages (kuro)
            if (spriteName.Contains("kuro_m"))
                return BlackMage_Male.ToString();
            if (spriteName.Contains("kuro_w"))
                return BlackMage_Female.ToString();

            // Thieves
            if (spriteName.Contains("thief_m"))
                return Thief_Male.ToString();
            if (spriteName.Contains("thief_w"))
                return Thief_Female.ToString();

            // Ninjas
            if (spriteName.Contains("ninja_m"))
                return Ninja_Male.ToString();
            if (spriteName.Contains("ninja_w"))
                return Ninja_Female.ToString();

            // Squires (mina)
            if (spriteName.Contains("mina_m"))
                return Squire_Male.ToString();
            if (spriteName.Contains("mina_w"))
                return Squire_Female.ToString();

            // Time Mages (toki)
            if (spriteName.Contains("toki_m"))
                return TimeMage_Male.ToString();
            if (spriteName.Contains("toki_w"))
                return TimeMage_Female.ToString();

            // Summoners (syou)
            if (spriteName.Contains("syou_m"))
                return Summoner_Male.ToString();
            if (spriteName.Contains("syou_w"))
                return Summoner_Female.ToString();

            // Samurai (samu)
            if (spriteName.Contains("samu_m"))
                return Samurai_Male.ToString();
            if (spriteName.Contains("samu_w"))
                return Samurai_Female.ToString();

            // Dragoons (ryu)
            if (spriteName.Contains("ryu_m"))
                return Dragoon_Male.ToString();
            if (spriteName.Contains("ryu_w"))
                return Dragoon_Female.ToString();

            // Geomancers (fusui)
            if (spriteName.Contains("fusui_m"))
                return Geomancer_Male.ToString();
            if (spriteName.Contains("fusui_w"))
                return Geomancer_Female.ToString();

            // Oracles/Mystics (onmyo)
            if (spriteName.Contains("onmyo_m"))
                return Mystic_Male.ToString();
            if (spriteName.Contains("onmyo_w"))
                return Mystic_Female.ToString();

            // Mediators/Orators (waju)
            if (spriteName.Contains("waju_m"))
                return Mediator_Male.ToString();
            if (spriteName.Contains("waju_w"))
                return Mediator_Female.ToString();

            // Dancers (odori - female only)
            if (spriteName.Contains("odori_w"))
                return Dancer_Female.ToString();

            // Bards (gin - male only)
            if (spriteName.Contains("gin_m"))
                return Bard_Male.ToString();

            // Mimes (mono)
            if (spriteName.Contains("mono_m"))
                return Mime_Male.ToString();
            if (spriteName.Contains("mono_w"))
                return Mime_Female.ToString();

            // Calculators/Arithmeticians (san)
            if (spriteName.Contains("san_m"))
                return Calculator_Male.ToString();
            if (spriteName.Contains("san_w"))
                return Calculator_Female.ToString();

            // Default to original if no mapping found
            return "original";
        }
    }
}