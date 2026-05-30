using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FFTColorCustomizer.Services
{
    /// <summary>
    /// A named preset colorway for one tier. Either a single whole-creature tint (<see cref="All"/>,
    /// applied to every themable section — used for single-section families and families whose
    /// per-part grouping isn't confirmed yet) or per-section colors (<see cref="BySection"/>, keyed
    /// by JobSection.Name — a designed multi-part look). <see cref="ColorFor"/> resolves a section.
    /// </summary>
    public sealed class MonsterPreset
    {
        private static readonly Dictionary<string, Color> Empty = new Dictionary<string, Color>();

        public Color? All { get; }
        public IReadOnlyDictionary<string, Color> BySection { get; }

        public MonsterPreset(Color all) { All = all; BySection = Empty; }
        public MonsterPreset(Dictionary<string, Color> bySection) { All = null; BySection = bySection; }

        /// <summary>Color for a section name: its explicit per-section color, else the whole-creature
        /// tint, else null (leave that section at its original palette).</summary>
        public Color? ColorFor(string sectionName)
            => BySection.TryGetValue(sectionName, out var c) ? c : All;
    }

    /// <summary>
    /// One themable monster family. The three ranks share a single sprite bin
    /// (battle_&lt;name&gt;_spr.bin in FFTPack unit/) as palettes 0/1/2; recolor = edit that bin's
    /// palette section indices (uniformHue). See docs/ADDING_A_MONSTER.md.
    ///
    /// <see cref="Family"/> is the one canonical key: it names the editor entry, the
    /// Images/&lt;Family&gt;/original/ HD-BMP folder, the Data/SectionMappings/Monster/&lt;Family&gt;.json
    /// mapping, and the SpriteSheetExtractor.FrameLayout.For("&lt;Family&gt;") crop. User themes save
    /// under it (tier-agnostic — one recolor applies to any tier's palette).
    /// </summary>
    public sealed class MonsterFamily
    {
        private static readonly string[] Romans = { "I", "II", "III" };

        public string Family { get; }
        public string DisplayName { get; }            // config-window subsection label, e.g. "Goblin Family"
        public string Bin { get; }                    // battle_gob_spr.bin
        public string[] TierDisplayNames { get; }     // ["Goblin","Black Goblin","Gobbledygook"]
        public int[] PaletteIndices { get; }          // bin palette per tier (default 0/1/2)
        // tierKey -> (preset name -> MonsterPreset)
        public Dictionary<string, Dictionary<string, MonsterPreset>> Presets { get; }

        public MonsterFamily(string family, string displayName, string bin, string[] tierDisplayNames,
            Dictionary<string, Dictionary<string, MonsterPreset>> presets, int[] paletteIndices = null)
        {
            Family = family;
            DisplayName = displayName;
            Bin = bin;
            TierDisplayNames = tierDisplayNames;
            PaletteIndices = paletteIndices ?? new[] { 0, 1, 2 };
            Presets = presets;
        }

        /// <summary>Config / serialization keys per tier, e.g. "Goblin_RankI".</summary>
        public string[] TierKeys => new[] { $"{Family}_RankI", $"{Family}_RankII", $"{Family}_RankIII" };

        /// <summary>JSON property name persisted in Config.json per tier, e.g. "GoblinRankI".</summary>
        public string JsonKey(int tierIndex) => $"{Family}Rank{Romans[tierIndex]}";

        /// <summary>Editor / user-theme key (tier-agnostic) == the family name.</summary>
        public string EditorKey => Family;

        /// <summary>Roman label for a tier index (0 -> "I").</summary>
        public string Roman(int tierIndex) => Romans[tierIndex];
    }

    /// <summary>
    /// The catalog of themable monster families. Adding a family here (plus its section mapping,
    /// HD BMP, original bin, and FrameLayout) wires it into Config, the config window, the theme
    /// editor, the apply pipeline, and serialization — no per-family code elsewhere.
    ///
    /// Presets: single-color families use <see cref="P"/> (one tint applied to every section). Once a
    /// family's per-part section grouping is confirmed in-editor, its presets become per-section
    /// colorways via <see cref="Sec"/> (see Aevis — distinct Wing/Chest/Eye/Feet colors per preset).
    ///
    /// Note on a couple of source bins: FFT internal names diverge from the bestiary. The Malboro
    /// uses battle_mol_spr.bin ("mol" = Morbol; battle_mara is a humanoid). The Dragon uses
    /// battle_dora1_spr.bin (its palettes 0/1/2 carry the green/blue/red tiers; battle_dora_spr is
    /// a humanoid with empty palettes 1/2). Hydra/Tiamat is deferred — battle_hebi_spr.bin has no
    /// 3-tier palette swap (only palette 0 populated), so it needs a separate source-bin study.
    /// </summary>
    public static class MonsterThemeRegistry
    {
        public const string Original = "original";

        private static Color Hex(string h)
        {
            h = h.TrimStart('#');
            return Color.FromArgb(
                Convert.ToInt32(h.Substring(0, 2), 16),
                Convert.ToInt32(h.Substring(2, 2), 16),
                Convert.ToInt32(h.Substring(4, 2), 16));
        }

        // Single-color presets: three tiers, each a list of (name, "#RRGGBB") tints applied to every
        // themable section (whole-creature recolor).
        private static Dictionary<string, Dictionary<string, MonsterPreset>> P(string fam,
            (string name, string hex)[] t1, (string name, string hex)[] t2, (string name, string hex)[] t3)
        {
            Dictionary<string, MonsterPreset> M((string name, string hex)[] a)
                => a.ToDictionary(x => x.name, x => new MonsterPreset(Hex(x.hex)));
            return new Dictionary<string, Dictionary<string, MonsterPreset>>
            {
                [$"{fam}_RankI"] = M(t1),
                [$"{fam}_RankII"] = M(t2),
                [$"{fam}_RankIII"] = M(t3),
            };
        }

        // Per-section preset: a color per JobSection.Name. Sections not listed stay at the tier's
        // original palette.
        private static MonsterPreset Sec(params (string section, string hex)[] parts)
            => new MonsterPreset(parts.ToDictionary(p => p.section, p => Hex(p.hex)));

        public static readonly IReadOnlyList<MonsterFamily> Families = new List<MonsterFamily>
        {
            new MonsterFamily("Chocobo", "Chocobo Family", "battle_cyoko_spr.bin",
                new[] { "Yellow Chocobo", "Black Chocobo", "Red Chocobo" },
                P("Chocobo",
                    new[] { ("White", "#EBEBEB"), ("Blue", "#2D6EE1"), ("Orange", "#F08C1E") },
                    new[] { ("Crimson", "#C81E37"), ("Emerald", "#1EAF5F"), ("Violet", "#964BD2") },
                    new[] { ("Cyan", "#28C3CD"), ("Lime", "#8CCD2D"), ("Magenta", "#D737C3") })),

            // Goblin — per-section presets (sections confirmed in-editor: Hat / Limbs / Stripe / Skin).
            new MonsterFamily("Goblin", "Goblin Family", "battle_gob_spr.bin",
                new[] { "Goblin", "Black Goblin", "Gobbledygook" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Goblin_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Inferno Red"]   = Sec(("Hat", "#C82828"), ("Limbs", "#6B3A2A"), ("Stripe", "#E8C040"), ("Skin", "#7AA84A")),
                        ["Ruby Scrapper"] = Sec(("Hat", "#A01838"), ("Limbs", "#4A2A38"), ("Stripe", "#D0A0B0"), ("Skin", "#8AB85A")),
                        ["Crimson Brute"] = Sec(("Hat", "#E04830"), ("Limbs", "#7A4028"), ("Stripe", "#2A2A30"), ("Skin", "#A0C060")),
                    },
                    ["Goblin_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Obsidian Shade"] = Sec(("Hat", "#503060"), ("Limbs", "#2A2838"), ("Stripe", "#8868B0"), ("Skin", "#6A7A5A")),
                        ["Voidsteel"]      = Sec(("Hat", "#383048"), ("Limbs", "#50545E"), ("Stripe", "#A0A8B8"), ("Skin", "#707860")),
                        ["Nightshade Hex"] = Sec(("Hat", "#7838A8"), ("Limbs", "#3A2848"), ("Stripe", "#C040A0"), ("Skin", "#5A6A4A")),
                    },
                    ["Goblin_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Venom Bog"]    = Sec(("Hat", "#4A8830"), ("Limbs", "#2A4A20"), ("Stripe", "#C8E040"), ("Skin", "#A89060")),
                        ["Goblin Slime"] = Sec(("Hat", "#88C828"), ("Limbs", "#4A6A18"), ("Stripe", "#E0F060"), ("Skin", "#90A870")),
                        ["Toxic Bloom"]  = Sec(("Hat", "#208058"), ("Limbs", "#104838"), ("Stripe", "#C040A0"), ("Skin", "#B0A070")),
                    },
                }),

            new MonsterFamily("Bomb", "Bomb Family", "battle_bom_spr.bin",
                new[] { "Bomb", "Grenade", "Exploder" },
                P("Bomb",
                    new[] { ("Inferno", "#F84818"), ("Ember", "#C83008"), ("Magma", "#F87038") },
                    new[] { ("Cryo", "#3858E0"), ("Frost", "#6080F8"), ("Voltage", "#7048D8") },
                    new[] { ("Cinder", "#484048"), ("Obsidian", "#202028"), ("Brimstone", "#706060") })),

            // Panther — per-section presets (sections confirmed in-editor: Primary fur / Ears / Eyes).
            new MonsterFamily("Panther", "Panther Family", "battle_hyou_spr.bin",
                new[] { "Red Panther", "Coeurl", "Vampire Cat" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Panther_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Red Panther"] = Sec(("Primary", "#C2451F"), ("Ears", "#7A2810"), ("Eyes", "#E8D040")),
                        ["Savanna Tan"] = Sec(("Primary", "#C98A3A"), ("Ears", "#8A5A20"), ("Eyes", "#50C840")),
                        ["Blood Lynx"]  = Sec(("Primary", "#8E1B1B"), ("Ears", "#4A1010"), ("Eyes", "#E0C020")),
                    },
                    ["Panther_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Coeurl Silver"] = Sec(("Primary", "#8A8F99"), ("Ears", "#4A4E58"), ("Eyes", "#50D0E0")),
                        ["Frost Lynx"]    = Sec(("Primary", "#6FA8C7"), ("Ears", "#3A5A70"), ("Eyes", "#C0F0FF")),
                        ["Onyx Coeurl"]   = Sec(("Primary", "#33363D"), ("Ears", "#1A1C20"), ("Eyes", "#D040D0")),
                    },
                    ["Panther_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Vampire Violet"] = Sec(("Primary", "#6B3C8C"), ("Ears", "#3A1E50"), ("Eyes", "#E0C040")),
                        ["Nightshade Cat"] = Sec(("Primary", "#3A2A5C"), ("Ears", "#1E1430"), ("Eyes", "#50E060")),
                        ["Crimson Fang"]   = Sec(("Primary", "#9C1F4A"), ("Ears", "#500A20"), ("Eyes", "#E0E040")),
                    },
                }),

            // Mindflayer — per-section presets (sections confirmed in-editor: Robe / Hair&Skin / Accent).
            new MonsterFamily("Mindflayer", "Mindflayer Family", "battle_ika_spr.bin",
                new[] { "Piscodaemon", "Squidrakin", "Mindflayer" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Mindflayer_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Brackish Bile"] = Sec(("Robe", "#4F7A3A"), ("HairSkin", "#C0B080"), ("Accent", "#C9923E")),
                        ["Tidepool Murk"] = Sec(("Robe", "#2E6E73"), ("HairSkin", "#A8C0A0"), ("Accent", "#1F4A50")),
                        ["Spawning Roe"]  = Sec(("Robe", "#C9923E"), ("HairSkin", "#E0C0A0"), ("Accent", "#7A5A28")),
                    },
                    ["Mindflayer_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Inkdraake Violet"] = Sec(("Robe", "#6E4AA8"), ("HairSkin", "#B0A0C0"), ("Accent", "#C0A040")),
                        ["Abyssal Indigo"]   = Sec(("Robe", "#2B3A8C"), ("HairSkin", "#9098B0"), ("Accent", "#5060C0")),
                        ["Krakenscale Teal"] = Sec(("Robe", "#1F7C8C"), ("HairSkin", "#A0C0B8"), ("Accent", "#E0A030")),
                    },
                    ["Mindflayer_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Cerebral Crimson"]  = Sec(("Robe", "#A8202A"), ("HairSkin", "#D0A8A0"), ("Accent", "#E0C040")),
                        ["Pallid Cortex"]     = Sec(("Robe", "#9FA88C"), ("HairSkin", "#C8C0A0"), ("Accent", "#6A7050")),
                        ["Psionic Amethyst"]  = Sec(("Robe", "#7A3B9C"), ("HairSkin", "#B0A0C0"), ("Accent", "#40E0C0")),
                    },
                }),

            // Skeleton — per-section presets (sections confirmed in-editor: Cape / Primary bone / Leather).
            new MonsterFamily("Skeleton", "Skeleton Family", "battle_sukeru_spr.bin",
                new[] { "Skeleton", "Bonesnatch", "Skeletal Fiend" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Skeleton_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Crypt Violet"] = Sec(("Cape", "#5A3A9C"), ("Primary", "#D8D0C0"), ("Leather", "#5A4028")),
                        ["Grave Frost"]  = Sec(("Cape", "#3A6FB0"), ("Primary", "#D0D8DC"), ("Leather", "#4A5560")),
                        ["Plague Moss"]  = Sec(("Cape", "#4E7A38"), ("Primary", "#C8CCA8"), ("Leather", "#5A4A28")),
                    },
                    ["Skeleton_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Forest Wraith"] = Sec(("Cape", "#3F7A3A"), ("Primary", "#C8CCB0"), ("Leather", "#4A3A20")),
                        ["Venom Sap"]     = Sec(("Cape", "#7FB02A"), ("Primary", "#D0D0A8"), ("Leather", "#5A5020")),
                        ["Tomb Ash"]      = Sec(("Cape", "#566070"), ("Primary", "#C0C0C0"), ("Leather", "#3A3A40")),
                    },
                    ["Skeleton_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Infernal Crimson"] = Sec(("Cape", "#B0241C"), ("Primary", "#E0D0C0"), ("Leather", "#5A2818")),
                        ["Cinder Orange"]    = Sec(("Cape", "#D85A1E"), ("Primary", "#E0D4B8"), ("Leather", "#6A3A18")),
                        ["Void Amethyst"]    = Sec(("Cape", "#6A1FB0"), ("Primary", "#C8C0D0"), ("Leather", "#3A2848")),
                    },
                }),

            // Ghost — per-section presets (sections confirmed in-editor: Primary body / Robe / Hair Band).
            new MonsterFamily("Ghost", "Ghost Family", "battle_yurei_spr.bin",
                new[] { "Ghoul", "Ghast", "Revenant" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Ghost_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Pallor"]      = Sec(("Primary", "#C9B0C0"), ("Robe", "#6A5A7A"), ("HairBand", "#D0C040")),
                        ["Sallow Rot"]  = Sec(("Primary", "#9FB76A"), ("Robe", "#5A6A38"), ("HairBand", "#C8A030")),
                        ["Grave Lilac"] = Sec(("Primary", "#9B6FC8"), ("Robe", "#4A3A6A"), ("HairBand", "#D0D0E0")),
                    },
                    ["Ghost_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Ashen Pall"]  = Sec(("Primary", "#8C8788"), ("Robe", "#4A484E"), ("HairBand", "#B0A040")),
                        ["Cinder Veil"] = Sec(("Primary", "#C0503C"), ("Robe", "#5A2820"), ("HairBand", "#E0C040")),
                        ["Bruise Mire"] = Sec(("Primary", "#5C4A7A"), ("Robe", "#2E2440"), ("HairBand", "#8A80A0")),
                    },
                    ["Ghost_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Bonemeal"]        = Sec(("Primary", "#D8C28A"), ("Robe", "#7A6A48"), ("HairBand", "#C04040")),
                        ["Crypt Verdigris"] = Sec(("Primary", "#4FA86B"), ("Robe", "#2A5A40"), ("HairBand", "#C0A040")),
                        ["Wraith Indigo"]   = Sec(("Primary", "#5050B0"), ("Robe", "#2A2A60"), ("HairBand", "#C0C0E0")),
                    },
                }),

            // Ahriman — per-section presets (sections confirmed in-editor: Primary eye-body / Wings).
            new MonsterFamily("Ahriman", "Ahriman Family", "battle_arli_spr.bin",
                new[] { "Floating Eye", "Ahriman", "Plague Horror" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Ahriman_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Verdant Watcher"] = Sec(("Primary", "#62A483"), ("Wings", "#8A6A3A")),
                        ["Swamp Gaze"]      = Sec(("Primary", "#3E7A4A"), ("Wings", "#5A4A2A")),
                        ["Bile Pupil"]      = Sec(("Primary", "#8FB300"), ("Wings", "#6A5A30")),
                    },
                    ["Ahriman_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Amber Iris"]      = Sec(("Primary", "#E0922A"), ("Wings", "#6A3A8C")),
                        ["Scorched Sclera"] = Sec(("Primary", "#B35A1F"), ("Wings", "#4A2A6A")),
                        ["Goldeye Doom"]    = Sec(("Primary", "#D4B400"), ("Wings", "#5A3A28")),
                    },
                    ["Ahriman_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Plague Crimson"]  = Sec(("Primary", "#C8392E"), ("Wings", "#3A4A6A")),
                        ["Pestilent Rose"]  = Sec(("Primary", "#A83A6F"), ("Wings", "#2A3A5A")),
                        ["Necrotic Violet"] = Sec(("Primary", "#7B3AA8"), ("Wings", "#4A5A3A")),
                    },
                }),

            // Aevis — per-section presets (sections confirmed in-editor: Eye / Wings / Chest / Feet).
            new MonsterFamily("Aevis", "Aevis Family", "battle_tori_spr.bin",
                new[] { "Jura Aevis", "Steelhawk", "Cockatrice" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Aevis_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Tempest Steel"] = Sec(("Wings", "#3B6EA5"), ("Chest", "#C6D2DE"), ("Eye", "#E8C030"), ("Feet", "#8A6A3A")),
                        ["Glacier Plume"] = Sec(("Wings", "#5FB9C4"), ("Chest", "#E0ECF0"), ("Eye", "#5AA0E0"), ("Feet", "#5A6E78")),
                        ["Stormcrow"]     = Sec(("Wings", "#454A60"), ("Chest", "#9AA0AE"), ("Eye", "#E0C84A"), ("Feet", "#2E323E")),
                    },
                    ["Aevis_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Gilded Talon"] = Sec(("Wings", "#C8A032"), ("Chest", "#ECE0A4"), ("Eye", "#C83020"), ("Feet", "#8A6012")),
                        ["Bronze Gale"]  = Sec(("Wings", "#9C6B2E"), ("Chest", "#D2AC68"), ("Eye", "#E0A020"), ("Feet", "#5A3C18")),
                        ["Verdant Hawk"] = Sec(("Wings", "#54802E"), ("Chest", "#A6C264"), ("Eye", "#E0C040"), ("Feet", "#5A4420")),
                    },
                    ["Aevis_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Basilisk Crimson"] = Sec(("Wings", "#B5283C"), ("Chest", "#E4A4AC"), ("Eye", "#E8C84A"), ("Feet", "#6A1820")),
                        ["Venom Royal"]      = Sec(("Wings", "#7A2E8C"), ("Chest", "#C4A6D6"), ("Eye", "#46C846"), ("Feet", "#3E1852")),
                        ["Cinder Petrify"]   = Sec(("Wings", "#D6552A"), ("Chest", "#C6B6A4"), ("Eye", "#E03020"), ("Feet", "#3A3028")),
                    },
                }),

            // Pig — per-section presets (sections confirmed in-editor: Primary hide / Nose & Ears).
            new MonsterFamily("Pig", "Pig Family", "battle_uri_spr.bin",
                new[] { "Pig", "Swine", "Wild Boar" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Pig_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Farmstead Tan"] = Sec(("Primary", "#B8A070"), ("NoseEars", "#E0A0A8")),
                        ["Truffle Brown"] = Sec(("Primary", "#8C6030"), ("NoseEars", "#D08890")),
                        ["Sty Mud"]       = Sec(("Primary", "#6B5226"), ("NoseEars", "#C07880")),
                    },
                    ["Pig_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Glazed Ham"]   = Sec(("Primary", "#D0907C"), ("NoseEars", "#E8B0A0")),
                        ["Rosy Swine"]   = Sec(("Primary", "#C86E78"), ("NoseEars", "#F0C0C8")),
                        ["Smoked Bacon"] = Sec(("Primary", "#A84A40"), ("NoseEars", "#D88078")),
                    },
                    ["Pig_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Steel Boar"]    = Sec(("Primary", "#70809C"), ("NoseEars", "#C09098")),
                        ["Tusker Slate"]  = Sec(("Primary", "#4A5878"), ("NoseEars", "#A07880")),
                        ["Wild Bristle"]  = Sec(("Primary", "#3C4860"), ("NoseEars", "#907078")),
                    },
                }),

            // Treant — per-section presets (auto-derived: Foliage crown / Trunk bark).
            new MonsterFamily("Treant", "Treant Family", "battle_ki_spr.bin",
                new[] { "Dryad", "Treant", "Elder Treant" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Treant_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Verdant Sprout"] = Sec(("Foliage", "#5B9E2D"), ("Trunk", "#6A4A28")),
                        ["Spring Glade"]   = Sec(("Foliage", "#8FCB3A"), ("Trunk", "#7A5A30")),
                        ["Mossbloom"]      = Sec(("Foliage", "#3E7A35"), ("Trunk", "#5A4020")),
                    },
                    ["Treant_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Autumn Bramble"]  = Sec(("Foliage", "#B5462E"), ("Trunk", "#5A3A20")),
                        ["Ironbark Canopy"] = Sec(("Foliage", "#6E4B8B"), ("Trunk", "#3A2A40")),
                        ["Blightleaf"]      = Sec(("Foliage", "#4C6B3A"), ("Trunk", "#4A3A22")),
                    },
                    ["Treant_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Goldleaf Elder"] = Sec(("Foliage", "#D9A21B"), ("Trunk", "#7A5A28")),
                        ["Amberheart"]     = Sec(("Foliage", "#C2701A"), ("Trunk", "#6A4420")),
                        ["Ancient Ash"]    = Sec(("Foliage", "#A8902E"), ("Trunk", "#5A5040")),
                    },
                }),

            // Minotaur — per-section presets (auto-derived: Body hide / Limbs grey fur / Horns keratin).
            new MonsterFamily("Minotaur", "Minotaur Family", "battle_minota_spr.bin",
                new[] { "Wisenkin", "Minotaur", "Sekhret" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Minotaur_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Verdant Sprout"] = Sec(("Body", "#4C7A2E"), ("Limbs", "#6A7A5A"), ("Horns", "#D8C090")),
                        ["Mossback"]       = Sec(("Body", "#6B8E23"), ("Limbs", "#7A8A5A"), ("Horns", "#D8C090")),
                        ["Bogwretch"]      = Sec(("Body", "#3B5323"), ("Limbs", "#5A6A4A"), ("Horns", "#C8B080")),
                    },
                    ["Minotaur_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Bloodrust"]      = Sec(("Body", "#A23B1E"), ("Limbs", "#7A5A50"), ("Horns", "#E0C8A0")),
                        ["Emberhide"]      = Sec(("Body", "#C75B28"), ("Limbs", "#8A6A58"), ("Horns", "#E0C8A0")),
                        ["Crimson Charge"] = Sec(("Body", "#8B1A1A"), ("Limbs", "#6A4A48"), ("Horns", "#D8B890")),
                    },
                    ["Minotaur_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Royal Amethyst"]  = Sec(("Body", "#6A2C91"), ("Limbs", "#6A5A78"), ("Horns", "#D8C8A0")),
                        ["Twilight Tyrant"] = Sec(("Body", "#4B0082"), ("Limbs", "#50486A"), ("Horns", "#C8B8A0")),
                        ["Wraithviolet"]    = Sec(("Body", "#8A4FBF"), ("Limbs", "#7A6A88"), ("Horns", "#E0D0B0")),
                    },
                }),

            // Malboro — per-section presets (auto-derived: Body vine / Maw mouth).
            new MonsterFamily("Malboro", "Malboro Family", "battle_mol_spr.bin",
                new[] { "Malboro", "Ochu", "Great Malboro" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Malboro_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Toxic Bloom"] = Sec(("Body", "#5C8A2E"), ("Maw", "#C84028")),
                        ["Swamp Maw"]   = Sec(("Body", "#3E5B22"), ("Maw", "#A83828")),
                        ["Spore Olive"] = Sec(("Body", "#7A9A3A"), ("Maw", "#D86030")),
                    },
                    ["Malboro_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Ochu Violet"]     = Sec(("Body", "#7A3D9E"), ("Maw", "#D04060")),
                        ["Venom Indigo"]    = Sec(("Body", "#4B2C8C"), ("Maw", "#C03850")),
                        ["Nightshade Plum"] = Sec(("Body", "#9B4FB0"), ("Maw", "#E05070")),
                    },
                    ["Malboro_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Great Verdigris"] = Sec(("Body", "#1F8F6B"), ("Maw", "#E0A828")),
                        ["Elder Bile"]      = Sec(("Body", "#C0A93A"), ("Maw", "#C84830")),
                        ["Abyssal Teal"]    = Sec(("Body", "#16707A"), ("Maw", "#D87038")),
                    },
                }),

            // Behemoth — per-section presets (auto-derived: Body hide / Mane crest / Horns & Spikes).
            new MonsterFamily("Behemoth", "Behemoth Family", "battle_behi_spr.bin",
                new[] { "Behemoth", "Behemoth King", "Dark Behemoth" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Behemoth_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Royal Violet"]  = Sec(("Body", "#7A2E8C"), ("Mane", "#B08040"), ("Spikes", "#C03828")),
                        ["Crimson Maw"]   = Sec(("Body", "#B0263A"), ("Mane", "#C09050"), ("Spikes", "#3A2A40")),
                        ["Verdant Beast"] = Sec(("Body", "#2E8C3A"), ("Mane", "#A07840"), ("Spikes", "#C04020")),
                    },
                    ["Behemoth_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Imperial Gold"]   = Sec(("Body", "#C8932E"), ("Mane", "#6A4A28"), ("Spikes", "#A03020")),
                        ["Sovereign Azure"] = Sec(("Body", "#2A5AC8"), ("Mane", "#C0A050"), ("Spikes", "#D0C040")),
                        ["Emerald Crown"]   = Sec(("Body", "#1F8C5A"), ("Mane", "#C0A050"), ("Spikes", "#C84030")),
                    },
                    ["Behemoth_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Abyssal Slate"] = Sec(("Body", "#2E4250"), ("Mane", "#5A6A78"), ("Spikes", "#A02818")),
                        ["Voidfire Red"]  = Sec(("Body", "#A02818"), ("Mane", "#4A3A30"), ("Spikes", "#E0A030")),
                        ["Necro Violet"]  = Sec(("Body", "#5A2A78"), ("Mane", "#6A5A40"), ("Spikes", "#40C060")),
                    },
                }),

            // Dragon — per-section presets (auto-derived: Body scales / Belly underscales).
            new MonsterFamily("Dragon", "Dragon Family", "battle_dora1_spr.bin",
                new[] { "Dragon", "Blue Dragon", "Red Dragon" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Dragon_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Forest"]  = Sec(("Body", "#2D6B2A"), ("Belly", "#C8B070")),
                        ["Emerald"] = Sec(("Body", "#2EA860"), ("Belly", "#C8C890")),
                        ["Jade"]    = Sec(("Body", "#3D9468"), ("Belly", "#C0C0A0")),
                    },
                    ["Dragon_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Sapphire"] = Sec(("Body", "#2050C0"), ("Belly", "#C0C8D0")),
                        ["Cobalt"]   = Sec(("Body", "#1855B0"), ("Belly", "#B0B8C8")),
                        ["Azure"]    = Sec(("Body", "#3080D0"), ("Belly", "#C8D8E0")),
                    },
                    ["Dragon_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Crimson"] = Sec(("Body", "#C82828"), ("Belly", "#E0C0A0")),
                        ["Scarlet"] = Sec(("Body", "#D83820"), ("Belly", "#E0C8A0")),
                        ["Ember"]   = Sec(("Body", "#C85020"), ("Belly", "#E0B890")),
                    },
                }),

            // Hydra — per-section presets (auto-derived: Body scales / Belly underscales; mirrors Dragon).
            // Source bin battle_dora2_spr.bin (pal0/1/2 all populated 14/14/15; pal0 == Tiamat HD BMP
            // 1098/1099/1136 1:1, quantized L1=0). Structural twin of the Dragon (dora1) family. The old
            // guess battle_hebi_spr.bin had only pal0 live. idx14 is the cross-tier eye sentinel (blue/
            // magenta/green per tier) — left unthemed, the mirror of Dragon's idx15 magenta sentinel.
            new MonsterFamily("Hydra", "Hydra Family", "battle_dora2_spr.bin",
                new[] { "Hydra", "Greater Hydra", "Tiamat" },
                new Dictionary<string, Dictionary<string, MonsterPreset>>
                {
                    ["Hydra_RankI"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Ember"] = Sec(("Body", "#C85820"), ("Belly", "#D8C088")),
                        ["Magma"] = Sec(("Body", "#D86828"), ("Belly", "#E0C890")),
                        ["Rust"]  = Sec(("Body", "#A8481C"), ("Belly", "#C8B078")),
                    },
                    ["Hydra_RankII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Wraith Violet"] = Sec(("Body", "#7A3D9E"), ("Belly", "#B0A8C0")),
                        ["Ashen Slate"]   = Sec(("Body", "#5A5560"), ("Belly", "#C0C0B0")),
                        ["Bruise Plum"]   = Sec(("Body", "#9B4FB0"), ("Belly", "#C8B8D0")),
                    },
                    ["Hydra_RankIII"] = new Dictionary<string, MonsterPreset>
                    {
                        ["Goldscale"]    = Sec(("Body", "#D8A828"), ("Belly", "#E0D090")),
                        ["Sunfire"]      = Sec(("Body", "#E0B83A"), ("Belly", "#E8D8A0")),
                        ["Brass Tyrant"] = Sec(("Body", "#B89030"), ("Belly", "#D0C080")),
                    },
                }),
        };

        /// <summary>The family that owns a tier key (e.g. "Goblin_RankII" -> Goblin), or null.</summary>
        public static MonsterFamily ForTierKey(string tierKey)
            => Families.FirstOrDefault(f => f.TierKeys.Contains(tierKey));

        /// <summary>The family by name (== editor key), or null.</summary>
        public static MonsterFamily ForFamily(string family)
            => Families.FirstOrDefault(f => f.Family == family);

        /// <summary>Tier index 0/1/2 for a tier key, or -1.</summary>
        public static int TierIndexForKey(string tierKey)
        {
            var fam = ForTierKey(tierKey);
            return fam == null ? -1 : Array.IndexOf(fam.TierKeys, tierKey);
        }

        /// <summary>The bin palette index a tier recolors (default 0/1/2; family may override).</summary>
        public static int PaletteIndexForTier(string tierKey)
        {
            var fam = ForTierKey(tierKey);
            int ti = TierIndexForKey(tierKey);
            return (fam != null && ti >= 0) ? fam.PaletteIndices[ti] : 0;
        }

        /// <summary>Dropdown theme names for a tier: "original" plus the family's presets for that tier.</summary>
        public static List<string> GetThemeNames(string tierKey)
        {
            var names = new List<string> { Original };
            var fam = ForTierKey(tierKey);
            if (fam != null && fam.Presets.TryGetValue(tierKey, out var m))
                names.AddRange(m.Keys);
            return names;
        }

        /// <summary>Resolves a tier preset name to its colorway. False for "original"/unknown.</summary>
        public static bool TryGetPreset(string tierKey, string themeName, out MonsterPreset preset)
        {
            preset = null;
            var fam = ForTierKey(tierKey);
            return fam != null && fam.Presets.TryGetValue(tierKey, out var m) && m.TryGetValue(themeName, out preset);
        }
    }
}
