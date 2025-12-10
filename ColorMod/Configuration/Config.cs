using System.ComponentModel;

namespace FFTColorMod.Configuration
{
    public class Config : Configurable<Config>
    {
        // Squires (starting class)
        [DisplayName("Male Squire")]
        [Description("Color scheme for all male squires")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme SquireMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Squire")]
        [Description("Color scheme for all female squires")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme SquireFemale { get; set; } = ColorScheme.original;

        // Knights
        [DisplayName("Male Knight")]
        [Description("Color scheme for all male knights")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme KnightMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Knight")]
        [Description("Color scheme for all female knights")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme KnightFemale { get; set; } = ColorScheme.original;

        // Monks
        [DisplayName("Male Monk")]
        [Description("Color scheme for all male monks")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme MonkMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Monk")]
        [Description("Color scheme for all female monks")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme MonkFemale { get; set; } = ColorScheme.original;

        // Archers
        [DisplayName("Male Archer")]
        [Description("Color scheme for all male archers")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme ArcherMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Archer")]
        [Description("Color scheme for all female archers")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme ArcherFemale { get; set; } = ColorScheme.original;

        // White Mages
        [DisplayName("Male White Mage")]
        [Description("Color scheme for all male white mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme WhiteMageMale { get; set; } = ColorScheme.original;

        [DisplayName("Female White Mage")]
        [Description("Color scheme for all female white mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme WhiteMageFemale { get; set; } = ColorScheme.original;

        // Black Mages
        [DisplayName("Male Black Mage")]
        [Description("Color scheme for all male black mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme BlackMageMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Black Mage")]
        [Description("Color scheme for all female black mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme BlackMageFemale { get; set; } = ColorScheme.original;

        // Time Mages
        [DisplayName("Male Time Mage")]
        [Description("Color scheme for all male time mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme TimeMageMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Time Mage")]
        [Description("Color scheme for all female time mages")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme TimeMageFemale { get; set; } = ColorScheme.original;

        // Summoners
        [DisplayName("Male Summoner")]
        [Description("Color scheme for all male summoners")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme SummonerMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Summoner")]
        [Description("Color scheme for all female summoners")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme SummonerFemale { get; set; } = ColorScheme.original;

        // Thieves
        [DisplayName("Male Thief")]
        [Description("Color scheme for all male thieves")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme ThiefMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Thief")]
        [Description("Color scheme for all female thieves")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme ThiefFemale { get; set; } = ColorScheme.original;

        // Ninjas
        [DisplayName("Male Ninja")]
        [Description("Color scheme for all male ninjas")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme NinjaMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Ninja")]
        [Description("Color scheme for all female ninjas")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme NinjaFemale { get; set; } = ColorScheme.original;

        // Samurai
        [DisplayName("Male Samurai")]
        [Description("Color scheme for all male samurai")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme SamuraiMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Samurai")]
        [Description("Color scheme for all female samurai")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme SamuraiFemale { get; set; } = ColorScheme.original;

        // Dragoons
        [DisplayName("Male Dragoon")]
        [Description("Color scheme for all male dragoons")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme DragoonMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Dragoon")]
        [Description("Color scheme for all female dragoons")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme DragoonFemale { get; set; } = ColorScheme.original;

        // Chemists
        [DisplayName("Male Chemist")]
        [Description("Color scheme for all male chemists")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme ChemistMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Chemist")]
        [Description("Color scheme for all female chemists")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme ChemistFemale { get; set; } = ColorScheme.original;

        // Dancers (Female only)
        [DisplayName("Female Dancer")]
        [Description("Color scheme for all dancers")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme DancerFemale { get; set; } = ColorScheme.original;

        // Bards (Male only)
        [DisplayName("Male Bard")]
        [Description("Color scheme for all bards")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme BardMale { get; set; } = ColorScheme.original;

        // Mimes
        [DisplayName("Male Mime")]
        [Description("Color scheme for all male mimes")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme MimeMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Mime")]
        [Description("Color scheme for all female mimes")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme MimeFemale { get; set; } = ColorScheme.original;

        // Calculators/Arithmeticians
        [DisplayName("Male Calculator")]
        [Description("Color scheme for all male calculators")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme CalculatorMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Calculator")]
        [Description("Color scheme for all female calculators")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme CalculatorFemale { get; set; } = ColorScheme.original;

        // Mediators/Orators
        [DisplayName("Male Mediator")]
        [Description("Color scheme for all male mediators")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme MediatorMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Mediator")]
        [Description("Color scheme for all female mediators")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme MediatorFemale { get; set; } = ColorScheme.original;

        // Mystics/Oracles
        [DisplayName("Male Mystic")]
        [Description("Color scheme for all male mystics")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme MysticMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Mystic")]
        [Description("Color scheme for all female mystics")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme MysticFemale { get; set; } = ColorScheme.original;

        // Geomancers
        [DisplayName("Male Geomancer")]
        [Description("Color scheme for all male geomancers")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme GeomancerMale { get; set; } = ColorScheme.original;

        [DisplayName("Female Geomancer")]
        [Description("Color scheme for all female geomancers")]
        [DefaultValue(ColorScheme.original)]
        public ColorScheme GeomancerFemale { get; set; } = ColorScheme.original;

        public string GetColorForSprite(string spriteName)
        {
            // Map sprite filename to the appropriate config property
            // Based on Better Palettes folder structure from mappings.txt

            // Knights
            if (spriteName.Contains("knight_m"))
                return KnightMale.ToString();
            if (spriteName.Contains("knight_w"))
                return KnightFemale.ToString();

            // Archers (yumi = bow in Japanese)
            if (spriteName.Contains("yumi_m"))
                return ArcherMale.ToString();
            if (spriteName.Contains("yumi_w"))
                return ArcherFemale.ToString();

            // Chemists (item)
            if (spriteName.Contains("item_m"))
                return ChemistMale.ToString();
            if (spriteName.Contains("item_w"))
                return ChemistFemale.ToString();

            // Monks
            if (spriteName.Contains("monk_m"))
                return MonkMale.ToString();
            if (spriteName.Contains("monk_w"))
                return MonkFemale.ToString();

            // White Mages (siro)
            if (spriteName.Contains("siro_m"))
                return WhiteMageMale.ToString();
            if (spriteName.Contains("siro_w"))
                return WhiteMageFemale.ToString();

            // Black Mages (kuro)
            if (spriteName.Contains("kuro_m"))
                return BlackMageMale.ToString();
            if (spriteName.Contains("kuro_w"))
                return BlackMageFemale.ToString();

            // Thieves
            if (spriteName.Contains("thief_m"))
                return ThiefMale.ToString();
            if (spriteName.Contains("thief_w"))
                return ThiefFemale.ToString();

            // Ninjas
            if (spriteName.Contains("ninja_m"))
                return NinjaMale.ToString();
            if (spriteName.Contains("ninja_w"))
                return NinjaFemale.ToString();

            // Squires (mina)
            if (spriteName.Contains("mina_m"))
                return SquireMale.ToString();
            if (spriteName.Contains("mina_w"))
                return SquireFemale.ToString();

            // Time Mages (toki)
            if (spriteName.Contains("toki_m"))
                return TimeMageMale.ToString();
            if (spriteName.Contains("toki_w"))
                return TimeMageFemale.ToString();

            // Summoners (syou)
            if (spriteName.Contains("syou_m"))
                return SummonerMale.ToString();
            if (spriteName.Contains("syou_w"))
                return SummonerFemale.ToString();

            // Samurai (samu)
            if (spriteName.Contains("samu_m"))
                return SamuraiMale.ToString();
            if (spriteName.Contains("samu_w"))
                return SamuraiFemale.ToString();

            // Dragoons (ryu)
            if (spriteName.Contains("ryu_m"))
                return DragoonMale.ToString();
            if (spriteName.Contains("ryu_w"))
                return DragoonFemale.ToString();

            // Geomancers (fusui)
            if (spriteName.Contains("fusui_m"))
                return GeomancerMale.ToString();
            if (spriteName.Contains("fusui_w"))
                return GeomancerFemale.ToString();

            // Oracles/Mystics (onmyo)
            if (spriteName.Contains("onmyo_m"))
                return MysticMale.ToString();
            if (spriteName.Contains("onmyo_w"))
                return MysticFemale.ToString();

            // Mediators/Orators (waju)
            if (spriteName.Contains("waju_m"))
                return MediatorMale.ToString();
            if (spriteName.Contains("waju_w"))
                return MediatorFemale.ToString();

            // Dancers (odori - female only)
            if (spriteName.Contains("odori_w"))
                return DancerFemale.ToString();

            // Bards (gin - male only)
            if (spriteName.Contains("gin_m"))
                return BardMale.ToString();

            // Mimes (mono)
            if (spriteName.Contains("mono_m"))
                return MimeMale.ToString();
            if (spriteName.Contains("mono_w"))
                return MimeFemale.ToString();

            // Calculators/Arithmeticians (san)
            if (spriteName.Contains("san_m"))
                return CalculatorMale.ToString();
            if (spriteName.Contains("san_w"))
                return CalculatorFemale.ToString();

            // Default to original if no mapping found
            return "original";
        }
    }
}