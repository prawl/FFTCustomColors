namespace FFTColorCustomizer.GameBridge
{
    public static class AttackVerification
    {
        public static AttackResult Evaluate(int hpBefore, int hpAfter)
        {
            int damage = hpBefore - hpAfter;
            bool hit = damage > 0;

            int healAmount = hpAfter > hpBefore ? hpAfter - hpBefore : 0;

            return new AttackResult
            {
                Hit = hit,
                Killed = hit && hpAfter <= 0,
                Damage = hit ? damage : 0,
                Healed = healAmount > 0,
                HealAmount = healAmount,
                HpBefore = hpBefore,
                HpAfter = hpAfter
            };
        }
    }

    public class AttackResult
    {
        public bool Hit { get; set; }
        public bool Killed { get; set; }
        public int Damage { get; set; }
        public bool Healed { get; set; }
        public int HealAmount { get; set; }
        public int HpBefore { get; set; }
        public int HpAfter { get; set; }
    }
}
