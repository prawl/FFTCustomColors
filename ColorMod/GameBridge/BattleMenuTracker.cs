namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Tracks the cursor position in battle submenus (e.g. Abilities → Attack/Mettle)
    /// where no memory address stores the cursor index.
    ///
    /// The game remembers submenu cursor position within a turn (Escape→re-Enter stays on same item),
    /// but resets to index 0 on a new turn.
    /// </summary>
    public class BattleMenuTracker
    {
        private const int VK_RETURN = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;

        private string[]? _items;

        public bool InSubmenu { get; private set; }
        public int CursorIndex { get; private set; }

        /// <summary>The item the cursor is currently on (null if not in submenu).</summary>
        public string? CurrentItem => InSubmenu && _items != null && CursorIndex >= 0 && CursorIndex < _items.Length
            ? _items[CursorIndex]
            : null;

        /// <summary>The item that was selected via Enter (null until Enter pressed, cleared on Escape/new turn).</summary>
        public string? SelectedItem { get; private set; }

        public void EnterAbilitiesSubmenu(string[] items)
        {
            _items = items;
            InSubmenu = true;
            // Don't reset CursorIndex — game remembers position within a turn
        }

        public void OnKeyPressed(int vkCode)
        {
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
                    SelectedItem = CurrentItem;
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
            CursorIndex = 0;
            SelectedItem = null;
        }

        public void Reset()
        {
            InSubmenu = false;
            CursorIndex = 0;
            SelectedItem = null;
            _items = null;
        }
    }
}
