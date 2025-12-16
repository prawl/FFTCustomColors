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
                ["Ovelia"] = new StoryCharacterConfig
                {
                    Name = "Ovelia",
                    EnumType = typeof(OveliaColorScheme),
                    GetValue = () => config.Ovelia,
                    SetValue = (v) => config.Ovelia = (OveliaColorScheme)v,
                    PreviewName = "ovelia"
                },
                ["Zalbag"] = new StoryCharacterConfig
                {
                    Name = "Zalbag",
                    EnumType = typeof(ZalbagColorScheme),
                    GetValue = () => config.Zalbag,
                    SetValue = (v) => config.Zalbag = (ZalbagColorScheme)v,
                    PreviewName = "zalbag"
                },
            };
        }

        public static void ResetAllStoryCharacters(Config config)
        {
            config.Agrias = AgriasColorScheme.original;
            config.Alma = AlmaColorScheme.original;
            config.Cloud = CloudColorScheme.original;
            config.Delita = DelitaColorScheme.original;
            config.Mustadio = MustadioColorScheme.original;
            config.Orlandeau = OrlandeauColorScheme.original;
            config.Ovelia = OveliaColorScheme.original;
            config.Reis = ReisColorScheme.original;
            config.Wiegraf = WiegrafColorScheme.original;
            config.Zalbag = ZalbagColorScheme.original;
        }
    }
}