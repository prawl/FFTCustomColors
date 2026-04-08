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

        // Level 2: Abilities submenu (Attack/Mettle/Items)
        private string[]? _items;
        public bool InSubmenu { get; private set; }
        public int CursorIndex { get; private set; }

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

        public void OnNewTurn()
        {
            InSubmenu = false;
            InAbilityList = false;
            CursorIndex = 0;
            AbilityCursorIndex = 0;
            SelectedItem = null;
            SelectedAbility = null;
        }

        public void Reset()
        {
            InSubmenu = false;
            InAbilityList = false;
            CursorIndex = 0;
            AbilityCursorIndex = 0;
            SelectedItem = null;
            SelectedAbility = null;
            _items = null;
            _abilities = null;
        }
    }
}
