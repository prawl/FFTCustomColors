using FFTColorCustomizer.GameBridge;
using Xunit;

namespace FFTColorCustomizer.Tests.GameBridge
{
    public class JobBaseStatsTableTests
    {
        [Theory]
        [InlineData("Squire", 4, 3)]
        [InlineData("Chemist", 3, 3)]
        [InlineData("Knight", 3, 3)]
        [InlineData("Archer", 3, 3)]
        [InlineData("Monk", 3, 4)]
        [InlineData("White Mage", 3, 3)]
        [InlineData("Black Mage", 3, 3)]
        [InlineData("Time Mage", 3, 3)]
        [InlineData("Summoner", 3, 3)]
        [InlineData("Thief", 4, 4)]
        [InlineData("Orator", 3, 3)]
        [InlineData("Mystic", 3, 3)]
        [InlineData("Geomancer", 4, 3)]
        [InlineData("Dragoon", 3, 4)]
        [InlineData("Samurai", 3, 3)]
        [InlineData("Ninja", 4, 3)]
        [InlineData("Arithmetician", 3, 3)]
        [InlineData("Bard", 3, 3)]
        [InlineData("Dancer", 3, 3)]
        [InlineData("Mime", 4, 4)]
        [InlineData("Onion Knight", 3, 3)]
        [InlineData("Dark Knight", 3, 3)]
        public void GenericJobs_MatchWikiBaseMoveJump(string jobName, int expMove, int expJump)
        {
            var stats = JobBaseStatsTable.TryGet(jobName);
            Assert.NotNull(stats);
            Assert.Equal((expMove, expJump), stats!.Value);
        }

        [Theory]
        [InlineData("Goblin", 4, 3)]
        [InlineData("Black Goblin", 4, 3)]
        [InlineData("Gobbledygook", 4, 3)]
        [InlineData("Skeleton", 3, 3)]
        [InlineData("Bonesnatch", 3, 3)]
        [InlineData("Skeletal Fiend", 3, 3)]
        [InlineData("Ghoul", 3, 3)]
        [InlineData("Ghast", 3, 3)]
        [InlineData("Revenant", 3, 3)]
        [InlineData("Chocobo", 4, 4)]
        [InlineData("Black Chocobo", 4, 6)]
        [InlineData("Red Chocobo", 4, 4)]
        [InlineData("Bomb", 4, 3)]
        [InlineData("Grenade", 4, 3)]
        [InlineData("Exploder", 4, 3)]
        [InlineData("Floating Eye", 3, 6)]
        [InlineData("Ahriman", 3, 6)]
        [InlineData("Plague Horror", 3, 6)]
        [InlineData("Red Panther", 4, 4)]
        [InlineData("Coeurl", 4, 4)]
        [InlineData("Vampire Cat", 4, 4)]
        [InlineData("Dragon", 3, 3)]
        [InlineData("Blue Dragon", 3, 3)]
        [InlineData("Red Dragon", 3, 3)]
        [InlineData("Minotaur", 3, 3)]
        [InlineData("Wisenkin", 3, 3)]
        [InlineData("Sacred", 3, 3)]
        [InlineData("Sekhret", 3, 3)]
        [InlineData("Malboro", 3, 3)]
        [InlineData("Ochu", 3, 3)]
        [InlineData("Great Malboro", 3, 3)]
        [InlineData("Dryad", 3, 3)]
        [InlineData("Treant", 3, 3)]
        [InlineData("Elder Treant", 3, 3)]
        [InlineData("Juravis", 3, 5)]
        [InlineData("Jura Aevis", 3, 5)]
        [InlineData("Steelhawk", 3, 5)]
        [InlineData("Cockatrice", 3, 5)]
        [InlineData("Behemoth", 4, 3)]
        [InlineData("Behemoth King", 4, 3)]
        [InlineData("Dark Behemoth", 4, 3)]
        [InlineData("Pig", 3, 3)]
        [InlineData("Swine", 3, 3)]
        [InlineData("Wild Boar", 3, 3)]
        [InlineData("Piscodaemon", 3, 3)]
        [InlineData("Squidraken", 3, 3)]
        [InlineData("Mindflayer", 3, 3)]
        [InlineData("Hydra", 3, 3)]
        [InlineData("Greater Hydra", 3, 3)]
        [InlineData("Tiamat", 3, 3)]
        public void Monsters_MatchCanonicalBaseMoveJump(string jobName, int expMove, int expJump)
        {
            var stats = JobBaseStatsTable.TryGet(jobName);
            Assert.NotNull(stats);
            Assert.Equal((expMove, expJump), stats!.Value);
        }

        [Theory]
        [InlineData("Gallant Knight", 4, 3)]
        [InlineData("Heretic", 4, 3)]
        [InlineData("Holy Knight", 3, 3)]
        [InlineData("Machinist", 3, 3)]
        [InlineData("Engineer", 3, 3)]
        [InlineData("Templar", 3, 3)]
        [InlineData("Divine Knight", 3, 3)]
        [InlineData("Sword Saint", 3, 4)]
        [InlineData("Thunder God", 3, 4)]
        [InlineData("Skyseer", 3, 3)]
        [InlineData("Netherseer", 3, 3)]
        [InlineData("Heaven Knight", 3, 3)]
        [InlineData("Hell Knight", 3, 3)]
        [InlineData("Fell Knight", 3, 3)]
        [InlineData("Dragonkin", 3, 4)]
        [InlineData("Holy Dragon", 3, 3)]
        [InlineData("Soldier", 3, 3)]
        [InlineData("Automaton", 3, 3)]
        [InlineData("Steel Giant", 3, 3)]
        [InlineData("Sky Pirate", 4, 4)]
        [InlineData("Game Hunter", 3, 3)]
        [InlineData("Princess", 3, 3)]
        [InlineData("Cleric", 3, 3)]
        [InlineData("Astrologer", 3, 3)]
        [InlineData("Arc Knight", 3, 3)]
        [InlineData("Rune Knight", 3, 3)]
        [InlineData("Assassin", 4, 4)]
        [InlineData("Nightblade", 4, 4)]
        [InlineData("White Knight", 3, 3)]
        public void StoryCharacterJobs_MatchCanonicalBaseMoveJump(string jobName, int expMove, int expJump)
        {
            var stats = JobBaseStatsTable.TryGet(jobName);
            Assert.NotNull(stats);
            Assert.Equal((expMove, expJump), stats!.Value);
        }

        [Fact]
        public void UnknownJob_ReturnsNull()
        {
            Assert.Null(JobBaseStatsTable.TryGet("Nonexistent Job"));
        }

        [Fact]
        public void NullOrEmpty_ReturnsNull()
        {
            Assert.Null(JobBaseStatsTable.TryGet(null));
            Assert.Null(JobBaseStatsTable.TryGet(""));
        }
    }
}
