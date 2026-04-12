namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Tracks cursor position across three battle menu levels:
    ///   Level 1: Action menu (Move/Abilities/Wait) — tracked by memory at 0x1407FC620
    ///   Level 2: Abilities submenu (Attack/Mettle/Items) — tracked here (_items)
    ///   Level 3: Ability list (Focus/Rush/Shout/...) — tracked here (_abilities)
    ///
    /// The game remembers cursor positions within a turn (Escape→re-Enter stays on same item),
    /// but resets to index 0 on a new turn.
    /// </summary>
    public class BattleMenuTracker
    {
        private const int VK_RETURN = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;

        /// <summary>True after an ability was used this turn (SyncForScreen reset from submenu).
        /// Prevents stale memory flags from re-entering the submenu.</summary>
        public bool HasActedThisTurn { get; private set; }

        // Level 2: Abilities submenu (Attack/Mettle/Items)
        private string[]? _items;
        public bool InSubmenu { get; private set; }
        public int CursorIndex { get; set; }

        /// <summary>The submenu item the cursor is on (null if not in submenu).</summary>
        public string? CurrentItem => InSubmenu && !InAbilityList && _items != null
            && CursorIndex >= 0 && CursorIndex < _items.Length
            ? _items[CursorIndex]
            : null;

        /// <summary>The submenu item selected via Enter (null until pressed, cleared on Escape/new turn).</summary>
        public string? SelectedItem { get; private set; }

        // Level 3: Ability list (Focus/Rush/Shout/...)
        private string[]? _abilities;
        public bool InAbilityList { get; private set; }
        public int AbilityCursorIndex { get; private set; }

        /// <summary>The ability the cursor is on (null if not in ability list).</summary>
        public string? CurrentAbility => InAbilityList && _abilities != null
            && AbilityCursorIndex >= 0 && AbilityCursorIndex < _abilities.Length
            ? _abilities[AbilityCursorIndex]
            : null;

        /// <summary>The ability selected via Enter (null until pressed, cleared on Escape/new turn).</summary>
        public string? SelectedAbility { get; private set; }

        public void EnterAbilitiesSubmenu(string[] items)
        {
            if (HasActedThisTurn) return; // stale memory flags — already acted, can't re-enter
            _items = items;
            InSubmenu = true;
            // Don't reset CursorIndex — game remembers position within a turn
        }

        public void EnterAbilityList(string[] abilities)
        {
            _abilities = abilities;
            InAbilityList = true;
            // Don't reset AbilityCursorIndex — game remembers position within a turn
        }

        public void OnKeyPressed(int vkCode)
        {
            // Level 3: ability list takes priority
            if (InAbilityList && _abilities != null)
            {
                switch (vkCode)
                {
                    case VK_DOWN:
                        AbilityCursorIndex = (AbilityCursorIndex + 1) % _abilities.Length;
                        break;
                    case VK_UP:
                        AbilityCursorIndex = (AbilityCursorIndex - 1 + _abilities.Length) % _abilities.Length;
                        break;
                    case VK_RETURN:
                        SelectedAbility = CurrentAbility;
                        break;
                    case VK_ESCAPE:
                        InAbilityList = false;
                        SelectedAbility = null;
                        SelectedItem = null; // clear so SyncBattleMenuTracker doesn't re-enter
                        break;
                }
                return;
            }

            // Level 2: abilities submenu
            if (!InSubmenu || _items == null) return;

            switch (vkCode)
            {
                case VK_DOWN:
                    CursorIndex = (CursorIndex + 1) % _items.Length;
                    break;
                case VK_UP:
                    CursorIndex = (CursorIndex - 1 + _items.Length) % _items.Length;
                    break;
                case VK_RETURN:
                    SelectedItem = _items[CursorIndex];
                    break;
                case VK_ESCAPE:
                    InSubmenu = false;
                    SelectedItem = null;
                    break;
            }
        }

        /// <summary>
        /// Called when screen returns to Battle_MyTurn after an ability use.
        /// Resets all submenu/ability list state so ui= shows the correct main menu cursor.
        /// Unlike OnNewTurn, this doesn't imply a different unit — just that we're back at the action menu.
        /// </summary>
        /// <summary>
        /// Called by SyncBattleMenuTracker after each screen detection.
        /// Resets tracker state when screen transitions away from Abilities menus.
        /// </summary>
        public void SyncForScreen(string screenName)
        {
            if (screenName == "Battle_MyTurn" && (InSubmenu || InAbilityList))
            {
                HasActedThisTurn = true;
                ReturnToMyTurn();
            }
            else if (screenName != "Battle_Abilities" && (InSubmenu || InAbilityList))
            {
                HasActedThisTurn = true;
                ReturnToMyTurn();
            }
        }

        public void ReturnToMyTurn()
        {
            InSubmenu = false;
            InAbilityList = false;
            CursorIndex = 0;
            AbilityCursorIndex = 0;
            SelectedItem = null;
            SelectedAbility = null;
        }

        public void OnNewTurn()
        {
            InSubmenu = false;
            InAbilityList = false;
            CursorIndex = 0;
            AbilityCursorIndex = 0;
            SelectedItem = null;
            SelectedAbility = null;
            HasActedThisTurn = false;
        }

        public void Reset()
        {
            InSubmenu = false;
            InAbilityList = false;
            HasActedThisTurn = false;
            CursorIndex = 0;
            AbilityCursorIndex = 0;
            SelectedItem = null;
            SelectedAbility = null;
            _items = null;
            _abilities = null;
        }
    }
}
