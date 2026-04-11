using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Metadata lookup for monster action abilities (range, AoE, target, element, effect).
    ///
    /// Works alongside MonsterAbilities.cs: that dict maps class → ability name list,
    /// this dict maps ability name → full ActionAbilityInfo. Together they let scan_move
    /// render complete ability data for every monster on the field.
    ///
    /// Data source: FFT Fandom wiki "Final Fantasy Tactics enemy abilities" A-Z table.
    /// Values are expressed the same way as ActionAbilityLookup (HRange as string,
    /// VRange/AoE/HoE as int, Target as string like "enemy"/"ally"/"self"/"AoE").
    ///
    /// NOTE: "Range" values like "Auto" mean the ability targets self or fires around
    /// the user without a cursor pick. VRange 99 = Unlimited / Infinite in the wiki.
    /// </summary>
    public static class MonsterAbilityLookup
    {
        private static readonly Dictionary<string, ActionAbilityInfo> ByName = new()
        {
            // === Skeleton family ===
            ["Chop"] = new(0, "Chop", 0, "1", 2, 1, 0, "enemy",
                "Strike the target with a bony-handed chop."),
            ["Thunder Anima"] = new(0, "Thunder Anima", 0, "3", 99, 1, 0, "enemy",
                "Attack by unleashing a lightning spirit.", Element: "Lightning"),
            ["Water Anima"] = new(0, "Water Anima", 0, "3", 99, 1, 0, "enemy",
                "Attack by unleashing a water spirit.", Element: "Water"),
            ["Ice Anima"] = new(0, "Ice Anima", 0, "3", 99, 1, 0, "enemy",
                "Attack by unleashing an ice spirit.", Element: "Ice"),
            ["Wind Anima"] = new(0, "Wind Anima", 0, "3", 99, 1, 0, "enemy",
                "Attack by unleashing a wind spirit.", Element: "Wind"),

            // === Ghost family ===
            ["Ectoplasm"] = new(0, "Ectoplasm", 0, "3", 99, 1, 0, "enemy",
                "Attack by striking with a mass of arcane power."),
            ["Sleep Touch"] = new(0, "Sleep Touch", 0, "1", 2, 1, 0, "enemy",
                "Put the target in a deep sleep with a touch.", AddedEffect: "May inflict Sleep"),
            ["Oily Touch"] = new(0, "Oily Touch", 0, "1", 2, 1, 0, "enemy",
                "Coat the target in oil with a greasy touch.", AddedEffect: "May inflict Oil"),
            ["Drain Touch"] = new(0, "Drain Touch", 0, "1", 2, 1, 0, "enemy",
                "Drain HP from the target with a foul touch."),
            ["Zombie Touch"] = new(0, "Zombie Touch", 0, "1", 2, 1, 0, "enemy",
                "Corrupt the target's flesh, turning it undead.", AddedEffect: "May inflict Undead"),

            // === Panther family ===
            ["Claw"] = new(0, "Claw", 0, "1", 3, 1, 0, "enemy",
                "Attack with keen-edged claws."),
            ["Cat Scratch"] = new(0, "Cat Scratch", 0, "1", 2, 1, 0, "enemy",
                "Attack with a powerful kick (random physical damage)."),
            ["Venom Fang"] = new(0, "Venom Fang", 0, "1", 2, 1, 0, "enemy",
                "Poison the target by injecting venom.", AddedEffect: "May inflict Poison"),
            ["Blaster"] = new(0, "Blaster", 0, "3", 99, 1, 0, "enemy",
                "Attack by releasing luminous energy.", AddedEffect: "May inflict Stone or Stop"),
            ["Blood Drain"] = new(0, "Blood Drain", 0, "1", 0, 1, 0, "enemy",
                "Feast on the target's blood to restore HP.", AddedEffect: "May inflict Vampire"),

            // === Eye/Ahriman family ===
            ["Wing Buffet"] = new(0, "Wing Buffet", 0, "1", 2, 1, 0, "enemy",
                "Attack with a mighty wing-flap."),
            ["Dread Gaze"] = new(0, "Dread Gaze", 0, "3", 99, 1, 0, "enemy",
                "Powerful gaze discourages you. Lowers Bravery stat by 10."),
            ["Bewitching Gaze"] = new(0, "Bewitching Gaze", 0, "3", 99, 1, 0, "enemy",
                "Inflict status effects with the overwhelming gaze of a massive eye.",
                AddedEffect: "May inflict: Stone, Blindness, Silence, Immobilize, Disable"),
            ["Doom"] = new(0, "Doom", 0, "3", 99, 1, 0, "enemy",
                "Gaze into the aether to find and cut short the target's silvery thread of life.",
                AddedEffect: "May inflict Doom"),
            ["Beam"] = new(0, "Beam", 0, "4", 3, 1, 0, "enemy",
                "Dissolve magick skills, reducing magick attack power."),

            // === Aevis/Bird family ===
            ["Talon Dive"] = new(0, "Talon Dive", 0, "1", 2, 1, 0, "enemy",
                "Attack by tearing with talons while in midair."),
            ["Beak"] = new(0, "Beak", 0, "1", 1, 1, 0, "enemy",
                "Attack vital spots with a long beak, petrifying the target.",
                AddedEffect: "May inflict Stone"),
            ["Peck"] = new(0, "Peck", 0, "1", 1, 1, 0, "enemy",
                "Employ a special skill that reduces physical attack power."),
            ["Glitterlust"] = new(0, "Glitterlust", 0, "1", 1, 1, 0, "enemy",
                "Receive glittering gil (steals gil from target)."),
            ["Featherbomb"] = new(0, "Featherbomb", 0, "3", 99, 1, 0, "enemy",
                "Attack by dropping an explosive feather on the target."),

            // === Chocobo family ===
            ["Choco Beak"] = new(0, "Choco Beak", 0, "1", 2, 1, 0, "enemy",
                "Attack by pecking with enormous beak."),
            ["Choco Cure"] = new(0, "Choco Cure", 0, "Auto", 2, 2, 0, "ally/AoE",
                "Invigorate the life force and restore HP with a flap of the wings."),
            ["Choco Esuna"] = new(0, "Choco Esuna", 0, "Auto", 2, 2, 0, "ally/AoE",
                "Purge status effects with a cleansing flap of the wings.",
                AddedEffect: "Removes: Stone, Blindness, Silence, Poison, Stop, Immobilize, Disable"),
            ["Choco Pellets"] = new(0, "Choco Pellets", 0, "4", 99, 1, 0, "enemy",
                "Attack by shooting concealed spheres."),
            ["Choco Meteor"] = new(0, "Choco Meteor", 0, "5", 99, 1, 0, "enemy",
                "Attack by raining down a small meteor."),

            // === Goblin family ===
            ["Tackle"] = new(0, "Tackle", 0, "1", 2, 1, 0, "enemy",
                "Attack with a rushing body blow."),
            ["Eye Gouge"] = new(0, "Eye Gouge", 0, "1", 2, 1, 0, "enemy",
                "Attack the eyes, robbing the target of sight.", AddedEffect: "May inflict Blindness"),
            ["Spin Punch"] = new(0, "Spin Punch", 0, "Auto", 1, 2, 0, "AoE",
                "Soundly thrash foes in four directions."),
            ["Goblin Punch"] = new(0, "Goblin Punch", 0, "1", 1, 1, 0, "enemy",
                "Attack with a thorough beating. Damage = max HP - current HP."),
            ["Bloodfeast"] = new(0, "Bloodfeast", 0, "1", 0, 1, 0, "enemy",
                "Bite the target to suck their blood and absorb their HP (3/4 max HP drain)."),

            // === Bomb family ===
            ["Bite"] = new(0, "Bite", 0, "1", 2, 1, 0, "enemy",
                "Attack by biting with an enormous mouth."),
            ["Self-Destruct"] = new(0, "Self-Destruct", 0, "Auto", 3, 3, 0, "AoE",
                "Self-destruct, inflicting damage in the surrounding area.",
                AddedEffect: "May inflict Oil"),
            ["Bomblet"] = new(0, "Bomblet", 0, "1", 0, 1, 0, "enemy",
                "Attack by tossing a bomblet as a grenade."),
            ["Spark"] = new(0, "Spark", 0, "3", 0, 1, 0, "self",
                "Attack the area with spreading flames (heals self with fire).",
                Element: "Fire"),
            ["Flame Attack"] = new(0, "Flame Attack", 0, "3", 99, 1, 0, "enemy",
                "Attack by shooting out flames.", Element: "Fire"),

            // === Bull/Minotaur family ===
            ["Pickaxe"] = new(0, "Pickaxe", 0, "1", 2, 1, 0, "enemy",
                "Attack with a mighty pickaxe blow."),
            ["Feral Spin"] = new(0, "Feral Spin", 0, "Auto", 1, 2, 0, "AoE",
                "Attack with a fearsome spin of the pickaxe."),
            ["Beef Up"] = new(0, "Beef Up", 0, "Auto", 0, 1, 0, "self",
                "Increase physical attack power by stamping the earth and absorbing its life force."),
            ["Earthsplitter"] = new(0, "Earthsplitter", 0, "Auto", 1, 3, 0, "AoE",
                "Attack by stamping the earth, dispatching a shockwave throughout the vicinity.",
                Element: "Earth"),
            ["Breathe Fire"] = new(0, "Breathe Fire", 0, "2", 99, 1, 0, "enemy",
                "Expel a combustible liquid produced in the body and set it alight, attacking with flaming breath.",
                Element: "Fire"),

            // === Mindflayer/Piscodaemon family ===
            ["Tentacles"] = new(0, "Tentacles", 0, "1", 2, 1, 0, "enemy",
                "Attack by flinging filthy tentacles about."),
            ["Ink"] = new(0, "Ink", 0, "2", 99, 1, 0, "enemy",
                "Rob the target of sight by belching black ink.", AddedEffect: "May inflict Blindness"),
            ["Dischord"] = new(0, "Dischord", 0, "Auto", 1, 3, 0, "AoE",
                "Nullify status effects by producing uncanny sound waves.",
                AddedEffect: "Removes: Float, Reraise, Invisibility, Regen, Protect, Shell, Haste, Faith, Reflect"),
            ["Mind Blast"] = new(0, "Mind Blast", 0, "3", 1, 2, 0, "enemy",
                "Disrupt the psyche, destroying the rational mind.",
                AddedEffect: "May inflict Confusion or Berserk"),
            ["Level Drain"] = new(0, "Level Drain", 0, "3", 1, 2, 0, "enemy",
                "Lowers target's level by 1."),

            // === Malboro family ===
            ["Lick"] = new(0, "Lick", 0, "1", 0, 1, 0, "ally",
                "Smear on viscous saliva to form an invisible, magick-reflecting wall.",
                AddedEffect: "Grants Reflect"),
            ["Goo"] = new(0, "Goo", 0, "1", 0, 1, 0, "enemy",
                "Belch forth sticky secretions from deep within to gum up the target.",
                AddedEffect: "Inflicts Immobilize"),
            ["Bad Breath"] = new(0, "Bad Breath", 0, "Auto", 0, 3, 0, "AoE",
                "Belch forth putrid stench to inflict status ailments.",
                AddedEffect: "May inflict: Stone, Blindness, Confusion, Silence, Oil, Toad, Poison, Sleep"),
            ["Malboro Spores"] = new(0, "Malboro Spores", 0, "1", 0, 1, 0, "enemy",
                "Coat the target in malboro spores, permanently turning them into a malboro."),

            // === Dragon family ===
            ["Charge"] = new(0, "Charge", 0, "1", 2, 1, 0, "enemy",
                "Inflict damage with a body blow."),
            ["Tail Sweep"] = new(0, "Tail Sweep", 0, "1", 2, 1, 0, "enemy",
                "Attack by swinging a thick tail (randomized damage)."),
            ["Fire Breath"] = new(0, "Fire Breath", 0, "2", 1, 1, 0, "enemy",
                "Attack with breath of flame in four directions.", Element: "Fire"),
            ["Ice Breath"] = new(0, "Ice Breath", 0, "2", 1, 1, 0, "enemy",
                "Attack with breath of ice in four directions.", Element: "Ice"),
            ["Thunder Breath"] = new(0, "Thunder Breath", 0, "2", 1, 1, 0, "enemy",
                "Attack with breath of lightning in four directions.", Element: "Lightning"),

            // === Behemoth family ===
            ["Gore"] = new(0, "Gore", 0, "1", 2, 1, 0, "enemy",
                "Attack with piercing horns."),
            ["Heave"] = new(0, "Heave", 0, "1", 0, 1, 0, "enemy",
                "Attack by goring and tossing the target.", AddedEffect: "May inflict KO"),
            ["Gigaflare"] = new(0, "Gigaflare", 0, "4", 0, 3, 0, "AoE",
                "Attack by bringing down a burst of white-hot energy."),
            ["Twister"] = new(0, "Twister", 0, "4", 2, 3, 0, "AoE",
                "Call forth a tornado to damage the surrounding area.", Element: "Wind"),
            ["Almagest"] = new(0, "Almagest", 0, "4", 1, 3, 0, "AoE",
                "Space-time warp damage equal to user's max HP - current HP."),

            // === Pig family ===
            ["Reckless Charge"] = new(0, "Reckless Charge", 0, "1", 0, 1, 0, "enemy",
                "Run at the target at full tilt, crashing into them."),
            ["Squeal"] = new(0, "Squeal", 0, "1", 1, 1, 0, "ally",
                "Use a fierce cry to summon KO'd units from the brink of death and restore their HP.",
                AddedEffect: "Revives from KO"),
            ["Toot"] = new(0, "Toot", 0, "1", 1, 1, 0, "enemy",
                "Inflict status effects with an eye-watering miasma.",
                AddedEffect: "May inflict: Confusion, Sleep"),
            ["Snort"] = new(0, "Snort", 0, "1", 1, 1, 0, "enemy",
                "Mesmerize the target with a ferocious snort.",
                AddedEffect: "May inflict Charmed (human only)"),
            ["Bequeath Bacon"] = new(0, "Bequeath Bacon", 0, "1", 1, 1, 0, "ally",
                "Target's level goes up by 1, and the user becomes a crystal."),

            // === Treant family ===
            ["Leaf Rain"] = new(0, "Leaf Rain", 0, "Auto", 0, 2, 0, "AoE",
                "Attack by sprinkling leaves about."),
            ["Life Nymph"] = new(0, "Life Nymph", 0, "Auto", 0, 2, 0, "ally/AoE",
                "Employ the life force of leaves to restore HP."),
            ["Guardian Nymph"] = new(0, "Guardian Nymph", 0, "Auto", 0, 2, 0, "ally/AoE",
                "Employ the life force of leaves to increase resistance to physical attacks.",
                AddedEffect: "Grants Protect"),
            ["Shell Nymph"] = new(0, "Shell Nymph", 0, "Auto", 0, 2, 0, "ally/AoE",
                "Employ the life force of leaves to increase resistance to magickal attacks.",
                AddedEffect: "Grants Shell"),
            ["Magick Nymph"] = new(0, "Magick Nymph", 0, "Auto", 0, 2, 0, "ally/AoE",
                "Employ the life force of leaves to restore MP."),

            // === Hydra family ===
            ["Tri-Attack"] = new(0, "Tri-Attack", 0, "1", 0, 1, 0, "enemy",
                "Each of three heads attacks in turn (3 adjacent panels)."),
            ["Tri-Flame"] = new(0, "Tri-Flame", 0, "4", 0, 2, 0, "AoE",
                "Each of three heads attacks with flame (3 random hits in area).",
                Element: "Fire"),
            ["Tri-Thunder"] = new(0, "Tri-Thunder", 0, "4", 0, 2, 0, "AoE",
                "Each of three heads attacks with lightning (3 random hits in area).",
                Element: "Lightning"),
            ["Tri-Breath"] = new(0, "Tri-Breath", 0, "2", 0, 2, 0, "AoE",
                "Each of three heads attacks with insidious breath (1/2 max HP damage)."),
            ["Dark Whisper"] = new(0, "Dark Whisper", 0, "4", 0, 2, 0, "AoE",
                "Each of three heads attacks with darkness (6 hits in area).",
                Element: "Dark", AddedEffect: "May inflict KO or Sleep"),
        };

        /// <summary>
        /// Look up metadata for a monster ability by name. Returns null if unknown.
        /// </summary>
        public static ActionAbilityInfo? GetByName(string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return ByName.TryGetValue(name, out var info) ? info : null;
        }
    }
}
