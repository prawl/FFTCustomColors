using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure helper: surface which entries of the in-battle action menu
    /// are effectively usable given the active unit's turn flags
    /// (BattleMoved / BattleActed).
    ///
    /// The menu is a stable 5-slot layout:
    ///   0 Move  (→ "Reset Move" after moving)
    ///   1 Abilities  (grayed after acting)
    ///   2 Wait  (always available)
    ///   3 Status  (always available)
    ///   4 Auto-battle  (always available)
    ///
    /// Callers use this before blindly navigating into a slot — wasted
    /// key presses on a grayed Abilities result in Enter dismissing the
    /// menu rather than opening the submenu.
    /// </summary>
    public record BattleMenuItem(string Name, int Slot, bool Available);

    public static class BattleMenuAvailability
    {
        public static IEnumerable<BattleMenuItem> For(int moved, int acted)
        {
            // Slot 0: Move or Reset Move
            string slot0 = moved == 1 ? "Reset Move" : "Move";
            yield return new BattleMenuItem(slot0, 0, true);

            // Slot 1: Abilities (greys out after acting)
            yield return new BattleMenuItem("Abilities", 1, acted != 1);

            // Slots 2-4: always available
            yield return new BattleMenuItem("Wait", 2, true);
            yield return new BattleMenuItem("Status", 3, true);
            yield return new BattleMenuItem("Auto-battle", 4, true);
        }

        public static bool CanMove(int moved, int acted) => moved == 0;
        public static bool CanAct(int moved, int acted) => acted == 0;
    }
}
