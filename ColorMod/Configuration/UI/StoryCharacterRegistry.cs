using System;
using System.Collections.Generic;

namespace FFTColorMod.Configuration.UI
{
    /// <summary>
    /// Registry for story character configurations to eliminate duplication
    /// </summary>
    public static class StoryCharacterRegistry
    {
        public delegate object GetValueDelegate();
        public delegate void SetValueDelegate(object value);

        public class StoryCharacterConfig
        {
            public string Name { get; set; }
            public Type EnumType { get; set; }
            public GetValueDelegate GetValue { get; set; }
            public SetValueDelegate SetValue { get; set; }
            public string PreviewName { get; set; }
        }

        public static Dictionary<string, StoryCharacterConfig> GetStoryCharacters(Config config)
        {
            return new Dictionary<string, StoryCharacterConfig>
            {
                ["Agrias"] = new StoryCharacterConfig
                {
                    Name = "Agrias",
                    EnumType = typeof(AgriasColorScheme),
                    GetValue = () => config.Agrias,
                    SetValue = (v) => config.Agrias = (AgriasColorScheme)v,
                    PreviewName = "Agrias"
                },
                ["Orlandeau"] = new StoryCharacterConfig
                {
                    Name = "Orlandeau",
                    EnumType = typeof(OrlandeauColorScheme),
                    GetValue = () => config.Orlandeau,
                    SetValue = (v) => config.Orlandeau = (OrlandeauColorScheme)v,
                    PreviewName = "Orlandeau"
                },
                ["Cloud"] = new StoryCharacterConfig
                {
                    Name = "Cloud",
                    EnumType = typeof(CloudColorScheme),
                    GetValue = () => config.Cloud,
                    SetValue = (v) => config.Cloud = (CloudColorScheme)v,
                    PreviewName = "Cloud"
                },
                ["Mustadio"] = new StoryCharacterConfig
                {
                    Name = "Mustadio",
                    EnumType = typeof(MustadioColorScheme),
                    GetValue = () => config.Mustadio,
                    SetValue = (v) => config.Mustadio = (MustadioColorScheme)v,
                    PreviewName = "Mustadio"
                },
                ["Reis"] = new StoryCharacterConfig
                {
                    Name = "Reis",
                    EnumType = typeof(ReisColorScheme),
                    GetValue = () => config.Reis,
                    SetValue = (v) => config.Reis = (ReisColorScheme)v,
                    PreviewName = "Reis"
                },
                ["Malak"] = new StoryCharacterConfig
                {
                    Name = "Malak",
                    EnumType = typeof(MalakColorScheme),
                    GetValue = () => config.Malak,
                    SetValue = (v) => config.Malak = (MalakColorScheme)v,
                    PreviewName = "Malak"
                },
                ["Rafa"] = new StoryCharacterConfig
                {
                    Name = "Rapha",
                    EnumType = typeof(RafaColorScheme),
                    GetValue = () => config.Rafa,
                    SetValue = (v) => config.Rafa = (RafaColorScheme)v,
                    PreviewName = "Rafa"
                },
                ["Delita"] = new StoryCharacterConfig
                {
                    Name = "Delita",
                    EnumType = typeof(DelitaColorScheme),
                    GetValue = () => config.Delita,
                    SetValue = (v) => config.Delita = (DelitaColorScheme)v,
                    PreviewName = "Delita"
                },
                ["Alma"] = new StoryCharacterConfig
                {
                    Name = "Alma",
                    EnumType = typeof(AlmaColorScheme),
                    GetValue = () => config.Alma,
                    SetValue = (v) => config.Alma = (AlmaColorScheme)v,
                    PreviewName = "Alma"
                },
                ["Wiegraf"] = new StoryCharacterConfig
                {
                    Name = "Wiegraf",
                    EnumType = typeof(WiegrafColorScheme),
                    GetValue = () => config.Wiegraf,
                    SetValue = (v) => config.Wiegraf = (WiegrafColorScheme)v,
                    PreviewName = "Wiegraf"
                },
                ["Celia"] = new StoryCharacterConfig
                {
                    Name = "Celia",
                    EnumType = typeof(CeliaColorScheme),
                    GetValue = () => config.Celia,
                    SetValue = (v) => config.Celia = (CeliaColorScheme)v,
                    PreviewName = "Celia"
                },
                ["Lettie"] = new StoryCharacterConfig
                {
                    Name = "Lettie",
                    EnumType = typeof(LettieColorScheme),
                    GetValue = () => config.Lettie,
                    SetValue = (v) => config.Lettie = (LettieColorScheme)v,
                    PreviewName = "Lettie"
                },
                ["Ovelia"] = new StoryCharacterConfig
                {
                    Name = "Ovelia",
                    EnumType = typeof(OveliaColorScheme),
                    GetValue = () => config.Ovelia,
                    SetValue = (v) => config.Ovelia = (OveliaColorScheme)v,
                    PreviewName = "ovelia"
                },
                ["Simon"] = new StoryCharacterConfig
                {
                    Name = "Simon",
                    EnumType = typeof(SimonColorScheme),
                    GetValue = () => config.Simon,
                    SetValue = (v) => config.Simon = (SimonColorScheme)v,
                    PreviewName = "simon"
                },
                ["Gaffgarion"] = new StoryCharacterConfig
                {
                    Name = "Gaffgarion",
                    EnumType = typeof(GaffgarionColorScheme),
                    GetValue = () => config.Gaffgarion,
                    SetValue = (v) => config.Gaffgarion = (GaffgarionColorScheme)v,
                    PreviewName = "gaffgarion"
                },
                ["Elmdore"] = new StoryCharacterConfig
                {
                    Name = "Elmdore",
                    EnumType = typeof(ElmdoreColorScheme),
                    GetValue = () => config.Elmdore,
                    SetValue = (v) => config.Elmdore = (ElmdoreColorScheme)v,
                    PreviewName = "elmdore"
                },
                ["Vormav"] = new StoryCharacterConfig
                {
                    Name = "Vormav",
                    EnumType = typeof(VormavColorScheme),
                    GetValue = () => config.Vormav,
                    SetValue = (v) => config.Vormav = (VormavColorScheme)v,
                    PreviewName = "vormav"
                },
                ["Zalbag"] = new StoryCharacterConfig
                {
                    Name = "Zalbag",
                    EnumType = typeof(ZalbagColorScheme),
                    GetValue = () => config.Zalbag,
                    SetValue = (v) => config.Zalbag = (ZalbagColorScheme)v,
                    PreviewName = "zalbag"
                },
                ["Zalmo"] = new StoryCharacterConfig
                {
                    Name = "Zalmo",
                    EnumType = typeof(ZalmoColorScheme),
                    GetValue = () => config.Zalmo,
                    SetValue = (v) => config.Zalmo = (ZalmoColorScheme)v,
                    PreviewName = "zalmo"
                }
            };
        }

        public static void ResetAllStoryCharacters(Config config)
        {
            config.Agrias = AgriasColorScheme.original;
            config.Alma = AlmaColorScheme.original;
            config.Celia = CeliaColorScheme.original;
            config.Cloud = CloudColorScheme.original;
            config.Delita = DelitaColorScheme.original;
            config.Elmdore = ElmdoreColorScheme.original;
            config.Gaffgarion = GaffgarionColorScheme.original;
            config.Lettie = LettieColorScheme.original;
            config.Malak = MalakColorScheme.original;
            config.Mustadio = MustadioColorScheme.original;
            config.Orlandeau = OrlandeauColorScheme.original;
            config.Ovelia = OveliaColorScheme.original;
            config.Rafa = RafaColorScheme.original;
            config.Reis = ReisColorScheme.original;
            config.Simon = SimonColorScheme.original;
            config.Vormav = VormavColorScheme.original;
            config.Wiegraf = WiegrafColorScheme.original;
            config.Zalbag = ZalbagColorScheme.original;
            config.Zalmo = ZalmoColorScheme.original;
        }
    }
}