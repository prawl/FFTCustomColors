# Known Bugs

## Moderate Issues

### 1. Configuration Changes Not Applying Until Reloaded-II Restart
**Status**: 🔴 Active
**Severity**: Moderate
**Reported**: December 2024

#### Description
User reports that after changing settings in the F1 configuration menu and saving, the sprite changes don't appear in-game even after triggering a sprite reload (by opening formation menu). The changes only take effect after closing and reopening Reloaded-II.

#### Steps to Reproduce
1. Launch game through Reloaded-II
2. Press F1 to open configuration menu
3. Change job colors/themes
4. Save configuration
5. Open formation menu to trigger sprite reload
6. Observe: No visual changes applied
7. Close and reopen Reloaded-II
8. Observe: Changes now visible

#### Expected Behavior
Configuration changes should apply immediately or after triggering a sprite reload without requiring Reloaded-II restart.

#### Workaround
Close and reopen Reloaded-II after making configuration changes.

#### Possible Causes
- Configuration might be cached at Reloaded-II level
- Sprite interception might not be refreshing with new configuration
- File handles might be locked until Reloaded-II restarts
- Configuration path might not be updating properly during runtime

#### Proposed Solutions
1. Force configuration reload when changes are saved
2. Clear any cached sprite paths when configuration updates
3. Implement a "hot reload" mechanism for configuration changes
4. Add a "Reload Configuration" button that properly refreshes everything

---

## Update History
- **2024-12-21**: Initial documentation of black enemy monks and enemy color change bugs
- **2024-12-21**: Added configuration not applying until Reloaded-II restart bug
- **2024-12-21**: FIXED enemy palette issues for all job-specific themes