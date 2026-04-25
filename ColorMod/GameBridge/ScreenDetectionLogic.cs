using System.Collections.Generic;
using System.Linq;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure logic for screen detection — no memory reads, no side effects.
    /// Extracted from CommandWatcher.DetectScreen for testability.
    ///
    /// Rewritten 2026-04-14 based on 46-sample memory audit (detection_audit.md).
    /// Key findings that shaped this rewrite:
    ///   - battleMode is overloaded (cursor-tile-class + submode), NOT a stable screen discriminator
    ///   - encA/encB are noise counters that drift, cannot be used as discriminators
    ///   - gameOverFlag is sticky across process lifetime once set
    ///   - rawLocation=0-42 means AT a named location (shop/village/campaign ground), 255 means "unspecified"
    ///   - Two distinct TitleScreen states exist (fresh process + post-GameOver + post-battle-stale)
    ///   - BattleAutoBattle rule cannot fire correctly — UI label handles that case
    ///   - BattleCasting is memory-indistinguishable from BattleAttacking
    ///   - Mid-battle dialogue clears slot0 to 0xFFFFFFFF (unit slots torn down)
    /// See detection_audit.md for full data.
    /// </summary>
    public static class ScreenDetectionLogic
    {
        /// <summary>
        /// Minimum "real event" eventId (inclusive). Values below this are
        /// unset sentinels (0) or invalid.
        /// </summary>
        public const int EventIdRealMin = 1;

        /// <summary>
        /// Exclusive upper bound for "real event" eventId. Values at or above
        /// this are name IDs from the aliased nameId address (used during
        /// combat animations) or invalid.
        /// </summary>
        public const int EventIdRealMaxExclusive = 400;

        /// <summary>
        /// Secondary "unset" sentinel. Some paths leave eventId at 0xFFFF
        /// instead of 0 when no event is playing.
        /// </summary>
        public const int EventIdUnsetAlt = 0xFFFF;

        /// <summary>
        /// Exclusive upper bound for mid-battle event IDs (stricter than
        /// EventIdRealMaxExclusive). During combat animations the eventId
        /// address (0x14077CA94) aliases as the active-unit nameId, and
        /// nameIds start at 200 — so any eventId &gt;= 200 seen during an
        /// active battle is almost certainly a nameId alias, not a real
        /// story-event ID. This tighter bound protects the mid-battle
        /// dialogue detection rule from firing on animation frames.
        /// </summary>
        public const int EventIdMidBattleMaxExclusive = 200;

        /// <summary>
        /// Returns true when the eventId corresponds to a real story/battle
        /// event (inclusive of 1, exclusive of 400, excluding the 0xFFFF alt
        /// sentinel). Use this as the single source of truth for the real-event
        /// range check across all detection rules.
        /// </summary>
        public static bool IsRealEvent(int eventId)
        {
            return eventId >= EventIdRealMin
                && eventId < EventIdRealMaxExclusive
                && eventId != EventIdUnsetAlt;
        }

        /// <summary>
        /// Returns true when eventId is one of the canonical unset sentinels
        /// (0 or 0xFFFF). Disjoint with <see cref="IsRealEvent"/> — a value
        /// cannot be both.
        /// </summary>
        public static bool IsEventIdUnset(int eventId)
        {
            return eventId == 0 || eventId == EventIdUnsetAlt;
        }

        /// <summary>
        /// Returns true when eventId is a real story-event ID AND below the
        /// mid-battle nameId-alias threshold. Used for mid-battle dialogue
        /// detection where the eventId address aliases as nameId during
        /// animations. Stricter than <see cref="IsRealEvent"/>.
        /// </summary>
        public static bool IsMidBattleEvent(int eventId)
        {
            return eventId >= EventIdRealMin
                && eventId < EventIdMidBattleMaxExclusive
                && eventId != EventIdUnsetAlt;
        }

        /// <summary>
        /// Locations with multi-stage battle sequences (campaign sub-selector minimap).
        /// These locations show a minimap of sub-battles instead of a LocationMenu.
        /// When rawLocation matches one of these AND locationMenuFlag=0 (not in a shop
        /// menu), the player is on the BattleSequence screen.
        /// </summary>
        internal static readonly HashSet<int> BattleSequenceLocations = new()
        {
            1,  // Riovanes Castle
            3,  // Lionel Castle
            4,  // Limberry Castle
            5,  // Zeltennia Castle
            15, // Ziekden Fortress
            16, // Mullonde
            18, // Orbonne Monastery
            21, // Fort Besselat
        };

        /// <summary>
        /// Override detection output when the SM has stronger signal than
        /// memory reads. Currently handles one known-collision pair:
        /// SaveSlotPicker (from PartyMenuOptions → Save) is byte-identical
        /// to TravelList across all 28 detection inputs. The SM tracks the
        /// Enter-on-Save transition explicitly, so when detection sees
        /// TravelList but SM says SaveSlotPicker, trust the SM. All other
        /// detection results pass through unchanged — they indicate real
        /// screen transitions the SM may have missed.
        /// </summary>
        public static string ResolveAmbiguousScreen(GameScreen smScreen, string detectedName)
        {
            if (smScreen == GameScreen.SaveSlotPicker && detectedName == "TravelList")
                return "SaveSlotPicker";
            // TavernRumors/TavernErrands are byte-identical to each other and to
            // LocationMenu in all 24 detection inputs (live-verified at Sal
            // Ghidos 2026-04-17). The SM tracks cursor-at-Enter; when it says
            // we're on a Tavern sub-screen but detection falls through to
            // LocationMenu, trust the SM.
            if (smScreen == GameScreen.TavernRumors && detectedName == "LocationMenu")
                return "TavernRumors";
            if (smScreen == GameScreen.TavernErrands && detectedName == "LocationMenu")
                return "TavernErrands";
            return detectedName;
        }

        /// <summary>
        /// Session-46 extension: ambiguity-resolver overload that also
        /// considers whether the SM is "freshly-seeded" (no keys pressed
        /// since it was last set — i.e. just-booted or just drift-recovered).
        ///
        /// Post-load WorldMap vs freshly-opened TravelList are byte-identical
        /// (live capture 2026-04-19 at Grogh Heights: hover=254, moveMode=255,
        /// party=0, ui=1 for BOTH). Detect() defaults to TravelList via the
        /// party=0+ui=1 fallback rule. We can only trust SM's WorldMap
        /// answer when SM is PROVABLY fresh — if the user opened the list
        /// via a key that went through the SM path, keysSinceLastSetScreen
        /// would be > 0. Preserves the existing "trust detection when SM is
        /// stale" contract for the normal-play path.
        /// </summary>
        public static string ResolveAmbiguousScreen(
            GameScreen smScreen, string detectedName,
            int keysSinceLastSetScreen, bool lastSetScreenFromKey)
        {
            // First pass: the 3-arg overload's rules.
            var primary = ResolveAmbiguousScreen(smScreen, detectedName);
            if (primary != detectedName) return primary;

            // Post-load WorldMap override: SM is WorldMap AND it hasn't
            // received any key events since being set (i.e. this is a
            // clean post-boot / post-recovery state, not a stale SM that
            // missed a user keypress).
            if (smScreen == GameScreen.WorldMap && detectedName == "TravelList"
                && keysSinceLastSetScreen == 0 && !lastSetScreenFromKey)
                return "WorldMap";

            return detectedName;
        }

        public static string Detect(
            int party, int ui, int rawLocation,
            long slot0, long slot9,
            int battleMode, int moveMode, int paused, int gameOverFlag,
            int battleTeam, int battleActed, int battleMoved,
            int encA, int encB, bool isPartySubScreen, int eventId = 0,
            int submenuFlag = 0, int menuCursor = -1, int hover = 255,
            int locationMenuFlag = 0, int insideShopFlag = 0,
            int shopSubMenuIndex = 0, int shopTypeIndex = 0,
            int unitsTabFlag = 0, int inventoryTabFlag = 0,
            int encounterFlag = 0, int menuDepth = -1,
            int battleSequenceFlag = 0,
            bool eventHasChoice = false,
            int choiceModalFlag = 0)
        {
            // rawLocation is the last-visited named place (village/shop/campaign ground).
            // It's STICKY — retains the last-visited location even when the player leaves.
            // To determine whether the player is CURRENTLY at that location, rely on
            // locationMenuFlag (0x140D43481) — the dedicated signal verified via memory
            // diff 2026-04-14. hover alone is unreliable (254 vs 255 depends on stale
            // cursor state, not current screen).
            bool atNamedLocation = rawLocation >= 0 && rawLocation <= 42
                                   && locationMenuFlag == 1;

            // Unit slots: slot0=0xFF means units are placed on the battlefield. During combat
            // animations and some dialogue sequences, slot0 can flicker. slot9=0xFFFFFFFF is
            // the broader "battle system active" sentinel (stays set longer than slot0).
            bool unitSlotsPopulated = slot0 == 255 && slot9 == 0xFFFFFFFF;
            bool battleModeActive = slot9 == 0xFFFFFFFF
                && (battleMode == 0 || battleMode == 1 || battleMode == 2 || battleMode == 3 || battleMode == 4 || battleMode == 5);

            // moveMode world-map signals: values 13, 20, and other non-0/non-255 means the
            // world-map cursor system is active — we are NOT in battle even if slot0/slot9
            // are stale from a prior session. In-battle moveMode is always 0 or 255.
            bool onWorldMapByMoveMode = moveMode != 0 && moveMode != 255;

            bool actedOrMoved = battleActed == 1 || battleMoved == 1;

            // Formation: battle sentinels PLUS slot0=0xFFFFFFFF (units not placed yet).
            // Checked BEFORE atNamedLocation override because Formation happens at a named
            // battle location (rawLocation 0-42) but is distinctly a battle-setup state.
            if (slot0 == 0xFFFFFFFFL && slot9 == 0xFFFFFFFF && battleMode == 1)
                return "BattleFormation";

            // BattleSequence: multi-stage campaign sub-selector minimap (e.g. Orbonne
            // Monastery Vaults). Enabled session 44; session 48 2026-04-19 swapped
            // the discriminator byte from the save-baked 0x14077D1F8 to the runtime
            // 0x1407774B4 (reads 2 when panel open, 1 when closed). Combined with
            // the location whitelist (1,3,4,5,15,16,18,21 — 8 multi-stage story
            // locations) this disambiguates the minimap from WorldMap-at-same-loc.
            // See memory/project_battle_sequence_flag_sticky.md for why the old
            // flag was replaced.
            //
            // Session 48 refinement: the flag ALSO reads 2 during enemy turns in
            // the actual battle launched from the minimap (live-observed at
            // Orbonne Vaults). The minimap-only variant has no units placed —
            // slot9 ≠ 0xFFFFFFFF. Mid-battle has slot9 == 0xFFFFFFFF. Gate on
            // that so the rule doesn't eat BattleEnemiesTurn during combat.
            if (battleSequenceFlag != 0
                && rawLocation >= 0 && rawLocation <= 42
                && BattleSequenceLocations.Contains(rawLocation)
                && slot9 != 0xFFFFFFFF)
                return "BattleSequence";

            // PartyMenu tab flags: 0x140D3A41E (Units) and 0x140D3A38E (Inventory)
            // are cross-session-stable binary flags that read 1 ONLY when the
            // player is on that specific PartyMenu tab (or a sub-screen within it:
            // CharacterStatus, EqA, pickers, etc.).
            //
            // Only override when party==0 (stale byte). When party==1, the
            // existing `party==1 → PartyMenu` rule at line ~109 handles it, and
            // the downstream SM-based resolution distinguishes sub-screens
            // (CharacterStatus, EqA, pickers). If we return "PartyMenuUnits" here
            // unconditionally, the stale-SM recovery block misinterprets a
            // legitimate CharacterStatus as stale and stomps it back to PartyMenu.
            // menuDepth==0 means outer screen (WorldMap/PartyMenu grid/CS).
            // The tab flags can be stale after leaving PartyMenu (e.g. after
            // battle_flee the unitsTabFlag stays 1 on WorldMap). Only trust
            // the flags when menuDepth is unknown (-1) or > 0 (inside a panel).
            // When menuDepth==0, prefer the later WorldMap/TravelList rules.
            //
            // Session 46 stress-fix: ALSO skip when we have affirmative world-map
            // signals (hover is a real location ID, moveMode is active). A
            // post-flee state on WorldMap keeps unitsTabFlag=1 but clearly
            // shows world-map bytes; don't latch onto the stale tab flag.
            bool hasWorldMapSignal = (hover >= 0 && hover <= 42)
                                     || (moveMode != 0 && moveMode != 255);
            if (party == 0 && (unitsTabFlag == 1 || inventoryTabFlag == 1)
                && menuDepth != 0 && !hasWorldMapSignal)
                return "PartyMenuUnits";

            // atNamedLocation and onWorldMapByMoveMode both override stale battle sentinels.
            // If we're at a shop/village (hover=255 + rawLocation in range) OR the world-map
            // cursor is active (moveMode=13/20/etc), we're NOT in battle, even if slot0/slot9
            // still carry battle residue from a prior session.
            //
            // Session 46 flicker guard: if battleMode is in the active player-turn range
            // (1..5) AND slot9=0xFFFFFFFF, that's strong evidence a live battle frame is
            // rendering — stronger than moveMode alone, which can transiently flicker to
            // a world-map value mid-animation. Only in this tight case do we DISTRUST the
            // moveMode override. Post-battle (battleMode=0) with real world-map bytes
            // still correctly exits the battle branch (e.g. Orbonne encounter-at-worldmap).
            //
            // Exceptions:
            //   - Formation (checked above) legitimately fires at a battle location
            //   - paused=1 means a pause/GameOver/Desertion screen is overlaid on top;
            //     stay in in-battle branch so those rules run.
            bool battleActiveTurnFrame = slot9 == 0xFFFFFFFF
                && battleMode >= 1 && battleMode <= 5;
            bool worldMapSignalTrusted = onWorldMapByMoveMode && !battleActiveTurnFrame;

            // S59 LIVE REPRO: after battle ends and player returns to a
            // WorldMap battleground node (rawLocation 0-42), the battle
            // sentinels (slot0=0xFF, slot9=0xFFFFFFFF) stay stale until the
            // game reallocates them. locationMenuFlag=0 on a node without
            // a menu open → atNamedLocation=false → inBattle=true and
            // detection wrongly reports BattleActing. When battleMode=0
            // AND moveMode=0 AND we're at a real world-map location,
            // we're NOT in battle even if sentinels are stale.
            bool postBattleWorldMapAtNode = rawLocation >= 0 && rawLocation <= 42
                && battleMode == 0 && moveMode == 0 && paused == 0;
            bool inBattle = (unitSlotsPopulated || battleModeActive)
                && !postBattleWorldMapAtNode
                && (paused == 1 || (!atNamedLocation && !worldMapSignalTrusted));

            // === Out-of-battle screens ===

            if (!inBattle)
            {
                // Rule order matters — specific-signal rules (party/encounter/
                // eventId) run FIRST, then location/cursor heuristics. Rules
                // that depend on weaker signals (hover, rawLocation alone) can
                // over-fire if they run before authoritative flags.

                // PartyMenu: party flag set. Authoritative — runs before any
                // hover/location heuristics to prevent "opened PartyMenu while
                // cursor was on a map location" cases from misdetecting as
                // WorldMap.
                if (party == 1)
                    return "PartyMenuUnits";

                // EncounterDialog: dedicated flag at 0x140D87830 reads non-zero
                // (observed value: 10) during the encounter prompt, 0 otherwise.
                // Runs early so hover-based world-map rules can't preempt.
                if (encounterFlag != 0)
                    return "EncounterDialog";

                // Cutscene: real story event, not at a named location.
                // eventId 1-399 = real event ID (nameIds start at 400, 0xFFFF/0 = unset).
                if (IsRealEvent(eventId) && rawLocation == 255)
                    return "Cutscene";

                // WorldMap with cursor on a named location: hover holds the location ID
                // (0-42) when the player's cursor is actively hovering a named map location.
                // This is a STRONG discriminator — titles/menus don't have hover in this range.
                // Checked before TitleScreen to preempt the strict uninit-fingerprint rule.
                if (hover >= 0 && hover <= 42 && rawLocation == 255)
                    return "WorldMap";

                // Note (session 46, 2026-04-19): post-load WorldMap and
                // a freshly-opened TravelList are BYTE-IDENTICAL in current
                // inputs — both show `hover=254, moveMode=255, party=0,
                // ui=1, slot0=0xFFFFFFFF` at Grogh Heights live capture.
                // No memory signal distinguishes them. The split happens
                // via SM-override in ResolveAmbiguousScreen (below):
                // whichever screen the SM thinks we're on wins, because
                // SM tracks the key-press log that moved us between the
                // two. Leaving the old party=0+ui=1 → TravelList fallback
                // in place as the detection default; SM overrides it when
                // appropriate.

                // BattleSequence (multi-stage campaign sub-selector, e.g. Orbonne Vaults)
                // is handled below via location whitelist after LocationMenu rule.

                // TitleScreen (strict): fresh process launch before save load. Two valid
                // fingerprints:
                //   (a) slot0=0xFFFFFFFF — uninit memory (truly fresh process)
                //   (b) slot9=0 — battle system never activated (no save loaded)
                // Either combined with rawLocation=255, ui=0, and event sentinel indicates
                // we're at the title screen before any save load. Other TitleScreen variants
                // (post-GameOver, post-battle stale) are handled by fallback rules further down.
                if (rawLocation == 255 && ui == 0 && paused == 0
                    && (slot0 == 0xFFFFFFFFL || slot9 == 0)
                    && IsEventIdUnset(eventId))
                    return "TitleScreen";

                // BattleChoice: story dialogue with a 2-option objective prompt
                // (e.g. "1. Defeat the Brigade" / "2. Rescue the captive" at
                // Mandalia Plain event 016). Two-part discriminator:
                //  1. `eventHasChoice`: the event's .mes script contains 0xFB.
                //     Set at EventScriptLookup load (per-session, one-time scan).
                //     Proves THIS event CAN produce a choice prompt somewhere
                //     in its timeline.
                //  2. `choiceModalFlag`: runtime byte at 0x140C70055 that reads
                //     non-zero only while the 2-option modal is actually drawn.
                //     Found session 44 via heap diff (narration → choice
                //     transition within the same event). Without this, the
                //     rule would over-fire during the narration prefix of a
                //     choice event, labeling regular dialogue as BattleChoice.
                if (atNamedLocation && IsRealEvent(eventId)
                    && slot0 == 0xFFFFFFFFL
                    && eventHasChoice
                    && choiceModalFlag != 0)
                    return "BattleChoice";

                // Pre-battle dialogue at a named location: eventId in real range + slot0=0xFFFFFFFF
                // indicates a pre-battle cutscene has been triggered (e.g. Orbonne Loffrey scene).
                if (atNamedLocation && IsRealEvent(eventId)
                    && slot0 == 0xFFFFFFFFL)
                    return "BattleDialogue";

                // Session 48: formation-phase dialogue at a battle location
                // (e.g. Mandalia Plain Brigade scene event 16, mid-formation).
                // battleMode==1 + real event + rawLocation in range catches
                // dialogue that fires AFTER auto_place_units but BEFORE the
                // battle sentinels (slot0=255 / slot9=0xFFFFFFFF) flip. The
                // existing atNamedLocation rule misses this because
                // locationMenuFlag isn't set post-formation, and the mid-battle
                // rule misses because slot9 isn't 0xFFFFFFFF yet. Use eventHasChoice +
                // choiceModalFlag to distinguish BattleChoice from regular dialogue.
                if (battleMode == 1 && IsRealEvent(eventId)
                    && rawLocation >= 0 && rawLocation <= 42
                    && slot9 != 0xFFFFFFFF)
                    return (eventHasChoice && choiceModalFlag != 0) ? "BattleChoice" : "BattleDialogue";

                // 2026-04-24 save-load variant: save resumed at a battle
                // location (rawLocation 0..42) with an active pre-battle
                // BattleDialogue, but battleMode had settled to 0 and
                // locationMenuFlag wasn't set — leaving the TravelList rule
                // at line 471-472 (party==0 && ui==1) to steal the frame
                // before reaching this path. A real eventId is the tell:
                // world-map idle has eventId==0 / unset; BattleDialogue
                // states have a live eventId. Gate this before the
                // world-side party/ui rules.
                if (IsRealEvent(eventId)
                    && rawLocation >= 0 && rawLocation <= 42
                    && battleMode == 0)
                    return (eventHasChoice && choiceModalFlag != 0) ? "BattleChoice" : "BattleDialogue";

                // Post-battle Desertion: at named battle location, game PAUSED with warning
                // dialog (Brave/Faith threshold triggered desertion risk). Both audit samples
                // of Desertion (#44, #45) had paused=1 and submenuFlag=1.
                if (atNamedLocation && slot0 == 255 && paused == 1 && submenuFlag == 1
                    && actedOrMoved && battleMode == 0)
                    return "BattleDesertion";

                // Post-battle Victory: at named battle location, NOT paused (auto-advancing
                // result screen). submenuFlag may still be 1 from prior submenu state.
                if (atNamedLocation && slot0 == 255 && paused == 0 && actedOrMoved
                    && battleMode == 0)
                    return "BattleVictory";

                // (EncounterDialog handled at the top of this branch — runs
                // before hover/location heuristics so the encounter prompt
                // can't be mislabeled as WorldMap or LocationMenu.)

                // Sub-action entered inside a shop/service. shopSubMenuIndex at
                // 0x14184276C reads 0 on the shop menu, 255 on world map, and
                // shop-specific values once a sub-action is Entered. These take
                // priority over insideShopFlag because live testing showed
                // insideShopFlag doesn't reliably fire on a fresh process.
                //
                // shopTypeIndex at 0x140D435F0 disambiguates which shop the player
                // is currently inside (0=Outfitter, 1=Tavern, 2=WarriorsGuild,
                // 3=PoachersDen). We key the sub-action switch on both.
                //
                // Mapped 2026-04-14 for Outfitter at Dorter: Buy=1, Sell=4, Fitting=6.
                // Tavern / Warriors' Guild / Poachers' Den sub-action values NOT
                // YET MAPPED — those shops will fall through to SettlementMenu
                // until a scan session records their shopSubMenuIndex values.
                if (rawLocation >= 0 && rawLocation <= 42)
                {
                    string? subState = ResolveShopSubAction(shopTypeIndex, shopSubMenuIndex);
                    if (subState != null) return subState;
                }

                // Shop interior: player has pressed Enter on a shop/service in
                // LocationMenu and is now at the sub-action selector (Outfitter
                // Buy/Sell/Fitting, Tavern Rumors/Errands, Warriors' Guild
                // Recruit/Rename, Poachers' Den Process/Sell Carcasses).
                // Signal at 0x141844DD0 — verified via module diff 2026-04-14
                // cycling through all 4 shop types at Dorter. shopTypeIndex
                // identifies WHICH shop; we return the shop-specific screen
                // name so Claude sees `[Outfitter] ui=Buy` instead of the
                // generic `[SettlementMenu] ui=Outfitter`.
                //
                // Historical names: this state was "ShopInterior" → "SettlementMenu"
                // → (now) shop-specific names per shopTypeIndex. The earlier
                // renames conflated the settlement-level shop selector (which
                // is LocationMenu) with the shop interior (which is this).
                if (insideShopFlag == 1 && rawLocation >= 0 && rawLocation <= 42)
                    return ShopTypeLabels.ForIndex(shopTypeIndex);

                // LocationMenu: player is inside a named location's menu. Which specific
                // shop/service is highlighted lives in screen.UI (populated by the caller
                // from shopTypeIndex at 0x140D435F0), NOT as a separate screen name —
                // consistent with how we label action-menu cursor positions (e.g.
                // BattleMyTurn with ui=AutoBattle).
                if (atNamedLocation)
                    return "LocationMenu";

                // BattleSequence handled above (before inBattle calculation) because
                // post-sub-battle residue makes battleModeActive=true.

                // Mid-battle dialogue with slot0 torn down: rawLocation=255 + real event +
                // slot0=0xFFFFFFFF + acted/moved=1 (happened after an action).
                if (rawLocation == 255 && slot0 == 0xFFFFFFFFL
                    && IsRealEvent(eventId)
                    && actedOrMoved)
                    return (eventHasChoice && choiceModalFlag != 0) ? "BattleChoice" : "BattleDialogue";

                // (EncounterDialog handled at top of this branch.)

                // World-map side states (rawLocation=255 OR stale rawLocation with hover=254).
                // WorldMap and TravelList are byte-identical in current inputs after a
                // fresh load. Best-effort split on ui:
                //   party=0, ui=1 → TravelList
                //   party=0, ui=0 → WorldMap
                if (party == 0 && ui == 1)
                    return "TravelList";
                if (party == 0 && ui == 0)
                    return "WorldMap";

                if (isPartySubScreen)
                    return "PartySubScreen";

                // No loose TitleScreen fallback here. The strict TitleScreen rule
                // above (line ~166) requires proper uninit sentinels. A residual
                // rawLocation=255 state that matches NO world-side rule (WorldMap/
                // TravelList/Cutscene/EncounterDialog) is genuinely unknown, not
                // a title screen. Returning "Unknown" surfaces the ambiguity so
                // callers can investigate instead of silently mislabeling post-
                // GameOver or post-battle stale states as TitleScreen.
                return "Unknown";
            }

            // === In-battle screens ===

            // Victory sentinel at named battle location during the banner window.
            // Session 44 post-Gariland Victory had the Desertion fingerprint
            // (paused=1, submenuFlag=1, slot0=255, acted+moved, battleMode=0,
            // atNamedLocation=true via locationMenuFlag). Without this rule,
            // postBattlePausedState below routes it to Desertion. encA==encB==255
            // fires only during the ~1-second banner window (session-45 Zeklaus
            // + session-49 Siedge Weald live-captured). See memory
            // `project_battle_victory_encA255.md`, TODO §0 Session 44.
            if (atNamedLocation && slot0 == 255 && actedOrMoved && battleMode == 0
                && battleTeam == 0 && encA == 255 && encB == 255)
                return "BattleVictory";

            // Post-battle at a named location: acted/moved sticky, battleMode=0.
            // This path only reached when atNamedLocation=true AND battle sentinels haven't
            // cleared. Since atNamedLocation makes inBattle=false, these post-battle screens
            // are structurally unreachable here — they're handled in the !inBattle branch.
            // We keep the rules here for defensive coverage if atNamedLocation handling changes.
            bool postBattle = unitSlotsPopulated && battleMode == 0 && actedOrMoved && atNamedLocation;
            bool postBattlePausedState = unitSlotsPopulated && battleMode == 0 && paused == 1 && actedOrMoved;
            // Orbonne variant (session 21): slot0=0x67 + active eventId + party=1/ui=1. Narrower
            // than postBattlePausedState to avoid swallowing stale-flag post-worldmap states.
            bool orbonneDesertion = battleModeActive && battleMode == 0 && paused == 1
                && actedOrMoved && slot0 != 0xFFFFFFFFL && slot0 != 255
                && party == 1 && ui == 1 && IsRealEvent(eventId);

            // Desertion: post-battle pause + submenu (warning dialog overlay).
            // Does NOT require encA==encB (noise counter).
            //
            // battleTeam==0 requirement: real Desertion fires when a PLAYER
            // unit walked off the field, so their team byte reads 0. Live-
            // repro 2026-04-25 Zeklaus Desert showed a post-Victory misdetect
            // where stale slot-0 pollution made battleTeam read 1 while
            // the post-battle sentinel pattern matched; without this guard
            // the Desertion rule fired on what was actually a clean Victory.
            if (postBattlePausedState && submenuFlag == 1 && battleTeam == 0)
                return "BattleDesertion";

            // Desertion variant from Orbonne (session 21): slot0=0x67 at rawLocation=255
            // with active battle eventId. submenuFlag=1 indicates the warning dialog.
            if (orbonneDesertion && submenuFlag == 1 && battleTeam == 0)
                return "BattleDesertion";

            // Victory: post-battle at named location, no pause (auto-advancing result screen).
            if (postBattle && paused == 0)
                return "BattleVictory";

            // Victory variant from Orbonne (session 21): slot0=0x67 at rawLocation=255.
            // Discriminator from post-battle stale flags (which have party=0, ui=0, eventId=0):
            // Victory has an active battle eventId in range 1..399 + party=1 + ui=1.
            if (battleModeActive && battleMode == 0 && actedOrMoved && paused == 0
                && slot0 != 0xFFFFFFFFL && slot0 != 255
                && party == 1 && ui == 1
                && IsRealEvent(eventId))
                return "BattleVictory";

            // BattleVictory sentinel: encA=255 AND encB=255 is a unique transient
            // signature captured at Zeklaus win 2026-04-19 (session 45) and
            // reproduced Siedge Weald 2026-04-20 (session 49).
            //
            // Session 49 follow-up: also require `actedOrMoved` to guard against
            // battle-start misfires. During the first frame of a new battle, the
            // encA byte briefly reads 0xFF before being set to a real value
            // (2026-04-20 live repro: 4-player battle first frame showed
            // encA=0xFF and `screen` returned BattleVictory when we were
            // actually on BattleMyTurn frame 1). actedOrMoved is false
            // pre-first-turn-action; during a real Victory banner it's true
            // because the final combat action happened before the banner.
            //
            // Ordering: MUST fire BEFORE LoadGame / post-GameOver TitleScreen /
            // GameOver — during the banner, gameOverFlag and submenuFlag are
            // both sticky from pre-victory.
            //
            // See memory/project_battle_victory_encA255.md.
            //
            // S58 tightening: additionally require submenuFlag==1. During mid-
            // cast animations (e.g. Shout), encA/encB can transiently hit 255
            // while battleMode and battleTeam stay at 0 and actedOrMoved=true
            // from the just-registered action — matching the sentinel spuriously.
            // The Victory banner overlay sets submenuFlag=1 (live-verified in
            // session-49 Siedge Weald capture); a mid-cast animation has
            // submenuFlag=0.
            //
            // 2026-04-24 further tightening: battle_ability response flashes
            // land with submenuFlag==1 too (ability picker / targeting overlay
            // is still up), bypassing the S58 guard. gameOverFlag==1 is set
            // by the Victory banner overlay itself (live-captured at both
            // Siedge Weald and Zeklaus banners); a mid-cast flicker has
            // gameOverFlag==0 because the battle hasn't ended. Adding this
            // discriminator closes the flash without regressing legitimate
            // sentinel captures.
            if (encA == 255 && encB == 255 && battleMode == 0 && battleTeam == 0
                && actedOrMoved && submenuFlag == 1 && gameOverFlag == 1)
                return "BattleVictory";

            // LoadGame: reached from GameOver menu. Shares stale battle state with GameOver
            // but paused=0 (GameOver has paused=1). Runs before EnemiesTurn to preempt stale
            // battleTeam=1.
            //
            // gameOverFlag is sticky — it can persist across save-reload into a later real
            // cutscene. Guard the LoadGame rule with an eventId check so a live cutscene
            // (eventId 1..399) doesn't get mis-labeled. 0 and 0xFFFF both mean "no event"
            // and are valid LoadGame states. See TODO "Cutscene misdetects as LoadGame".
            if (paused == 0 && gameOverFlag == 1 && battleMode == 0 && !actedOrMoved
                && !atNamedLocation && IsEventIdUnset(eventId))
                return "LoadGame";

            // Post-GameOver TitleScreen: title reached by returning from game-over menu.
            // Stale battle residue (slot0=0xFF) but at rawLocation=255, paused=0,
            // gameOverFlag=1, submenuFlag=1, menuCursor=2.
            if (rawLocation == 255 && paused == 0 && gameOverFlag == 1 && submenuFlag == 1
                && battleMode == 0 && !actedOrMoved && menuCursor == 2)
                return "TitleScreen";

            // Session 48: ally-turn dialogue. When battleTeam==2 (neutral/ally
            // phase), combat-animation nameId aliasing doesn't apply because
            // active combat is team 0 or 1. That lets us use the looser
            // IsRealEvent range (< 400) instead of the 200 cap, so eventIds
            // like 302 (Orbonne Vaults Loffrey pre-stage) no longer fall
            // through to BattleAlliesTurn. Checked BEFORE the turn-owner
            // fallthrough at battleTeam==2 → BattleAlliesTurn.
            if (battleTeam == 2 && IsRealEvent(eventId) && battleMode == 0 && paused == 0)
                return (eventHasChoice && choiceModalFlag != 0) ? "BattleChoice" : "BattleDialogue";

            // Mid-battle dialogue: story event playing during active battle. In-battle uses
            // the stricter < EventIdMidBattleMaxExclusive (200) filter because eventId
            // address (0x14077CA94) aliases as active-unit nameId during combat animations.
            // Checked BEFORE the post-battle-stale TitleScreen fallback, because some
            // dialogue states share acted/moved/submenuFlag sticky values with post-battle.
            if (IsMidBattleEvent(eventId) && battleMode == 0 && paused == 0)
                return (eventHasChoice && choiceModalFlag != 0) ? "BattleChoice" : "BattleDialogue";

            // Post-battle stale at rawLocation=255: After a battle ends (or a battle
            // dialogue is dismissed), the game transitions back to the world map but stale
            // battle sentinels persist briefly (slot0=0xFF, slot9=0xFFFFFFFF, acted=1,
            // moved=1). During this transition moveMode may still read 0 before flipping
            // to a world-map value. Treat as WorldMap — that's what the player is returning
            // to. (TitleScreen requires full uninit sentinels handled earlier in !inBattle.)
            if (rawLocation == 255 && paused == 0 && battleMode == 0 && actedOrMoved
                && submenuFlag == 1)
                return "WorldMap";

            // Post-banner Victory: Ramza-died-but-countered-final-kill edge case.
            // Shares fingerprint with GameOver (paused=1, gameOverFlag=1,
            // battleMode=0) but the player-team triggered the final action so
            // battleTeam=0. Real GameOver is battleTeam=1 (enemy just killed
            // the last player unit). Session 49 captured this scenario
            // 2026-04-20: Kenrick countered an attack for the final enemy kill
            // while dying himself — detection returned GameOver for ~3 seconds
            // before the proper Victory banner took over.
            //
            // See memory/feedback_victory_gameover_both_encA255_risk.md.
            if (paused == 1 && battleMode == 0 && gameOverFlag == 1
                && battleTeam == 0)
                return "BattleVictory";

            // GameOver: paused + game-over flag + enemy-triggered. Session 44
            // 2026-04-19 live-capture at Siedge Weald showed actedOrMoved=true
            // because a unit's action (the one that killed Ramza) completed
            // right before the GameOver banner. The old rule's `!actedOrMoved`
            // requirement blocked correct detection — gameOverFlag is a
            // dedicated signal and doesn't need action-state disambiguation.
            // Session 49 added `battleTeam != 0` guard — team=0 Victory-with-
            // Ramza-dying edge case is handled above.
            // Captured fingerprint: paused=1, gameOverFlag=1, battleMode=0,
            // battleTeam=1, battleActed=1, battleMoved=1, menuCursor=4.
            if (paused == 1 && battleMode == 0 && gameOverFlag == 1
                && battleTeam != 0)
                return "GameOver";

            // Chest reward banner ("Obtained X!"): can fire mid-Ramza-turn
            // (battleTeam=0, original Zeklaus 2026-04-19 capture) OR after a
            // chest-move auto-ends Ramza's turn (battleTeam=1, gameOverFlag=1
            // transient — Siedge Weald 2026-04-25 capture). Both cases share
            // the chest-banner fingerprint exactly:
            //   battleMode=1, moveMode=0, submenuFlag=1, menuCursor=0,
            //   encA=1, encB=1, paused=0
            // The fingerprint is distinctive enough to fire ahead of the
            // team-guarded crystal-pickup block below — that block's
            // `battleTeam==0` requirement was missing the chest-at-turn-end
            // case, leaving the banner classified as BattleEnemiesTurn.
            if (battleMode == 1 && moveMode == 0 && submenuFlag == 1
                && menuCursor == 0 && encA == 1 && encB == 1 && paused == 0)
                return "BattleRewardObtainedBanner";

            // Crystal/chest move-confirm Yes/No popup ("Use the crystal..."
            // / "Move to this tile and open the chest?"): same team-cycling
            // issue as the reward banner — when the move ends Ramza's turn,
            // battleTeam is no longer 0 so the original team-guarded block
            // misses it. Live-captured 2026-04-25 Siedge Weald: pre-save
            // had submenuFlag=1, gameOverFlag=1; post-save+restore both
            // cleared but dialog persisted, so they're not load-bearing.
            // Robust fingerprint:
            //   battleMode=1, moveMode=0, menuCursor=0, encA>=2, paused=0
            // (encA==0/1 are the banner cases handled above; battleMode==1
            // and moveMode==0 distinguish from normal BattleMoving which
            // uses moveMode==255.)
            if (battleMode == 1 && moveMode == 0 && menuCursor == 0
                && encA >= 2 && paused == 0)
                return "BattleCrystalMoveConfirm";

            // Crystal-pickup sequence: 4 modal states fired when a unit steps
            // onto a crystallized unit's tile. Live-captured at Zeklaus event 40
            // (2026-04-19). All share battleMode=1, submenuFlag=1, menuCursor=0,
            // battleTeam=0 with normal BattleMoving but differ on moveMode,
            // paused, and encA (widget-stack-depth byte). Must fire BEFORE the
            // generic `paused==1 → BattlePaused` rule and BEFORE the
            // `battleMode==1 → BattleMoving` rule because two crystal states
            // have paused=1 and all four have battleMode=1. See
            // memory/project_crystal_states_undetected.md for the fingerprint
            // table and encA interpretation.
            if (battleMode == 1 && submenuFlag == 1 && menuCursor == 0 && battleTeam == 0)
            {
                if (paused == 1)
                {
                    // S2 (Acquire/Restore chooser) vs S3 (Yes/No acquire confirm).
                    // encA is a widget-stack depth byte: S2 ≈ 4, S3 ≈ 7.
                    return encA >= 5 ? "BattleAbilityAcquireConfirm" : "BattleCrystalReward";
                }
                if (moveMode == 0)
                {
                    // Three moveMode=0 + paused=0 states, disambiguated by encA:
                    //   encA==0 → S4 "Ability learned!" banner (crystal reward)
                    //   encA==1 → "Obtained X!" banner (chest loot dismissal)
                    //   encA>=2 → S1 MoveConfirm Yes/No popup (crystal OR chest)
                    // Live-captured Zeklaus 2026-04-19 across 4 pickups (crystal,
                    // longsword chest, leather clothing chest, crystal-ability).
                    // Normal BattleMoving uses moveMode==255 so it falls through.
                    if (encA == 0) return "BattleAbilityLearnedBanner";
                    if (encA == 1) return "BattleRewardObtainedBanner";
                    return "BattleCrystalMoveConfirm";
                }
                // moveMode==255 falls through to the normal BattleMoving rule below.
            }

            // Status screen: clicked INTO Status from pause menu. Needs submenuFlag=1
            // (subscreen open) in addition to paused=1 + menuCursor=3. Without submenuFlag,
            // cursor=3 on the pause menu just means hovering the Status item, which is still
            // BattlePaused.
            if (paused == 1 && menuCursor == 3 && submenuFlag == 1)
                return "BattleStatus";
            if (paused == 1)
                return "BattlePaused";

            // Turn-owner rules MUST run before the battleMode-based targeting/move rules:
            // enemies and NPC allies can have battleMode 1/2/4/5 during their own actions
            // (pathing, targeting), which previously false-triggered BattleMoving /
            // BattleAttacking. Live-verified 2026-04-19 at Zeklaus event 40: during the
            // enemy turn the bridge reported `BattleMoving` / `BattleAttacking` based on
            // the enemy's own movement mode. Gate the player-only submodes on battleTeam==0.
            //
            // Session 46 fix: removed the `!actedOrMoved` guard — once an enemy moves
            // (moved=1) mid-turn they still OWN the turn and the submode bytes
            // (battleMode=4 targeting, battleMode=2 facing) are theirs, not the player's.
            // Stress probe showed a moved=1 enemy turn fell through to BattleAttacking /
            // BattleWaiting as if the player was acting.
            //
            // 2026-04-25 fix: added `battleMode != 0` guard. Live-captured post-Siedge-
            // Weald: battle ended, game returned to world map, but battleTeam=1 stale-
            // persisted from the prior enemy turn. With no battleMode guard, the rule
            // fired BattleEnemiesTurn while the screenshot showed the world map.
            // battleMode==0 is the out-of-battle sentinel; turn-owner state only exists
            // when actually in battle (modes 1/2/3/4/5).
            if (battleMode != 0 && battleTeam == 1)
                return "BattleEnemiesTurn";
            if (battleMode != 0 && battleTeam == 2)
                return "BattleAlliesTurn";

            // Targeting submodes — cast-time and instant collapse into BattleAttacking.
            // battleMode values 1, 4, 5 all indicate "cursor in a targeting submode":
            //   4 = cursor on a valid instant-attack target
            //   5 = cursor on caster's self-target tile (cast-time)
            //   1 = cursor on a tile that isn't a valid target (off-highlight) —
            //       AMBIGUOUS with Move-mode's "cursor on non-movable tile". Session 30
            //       confirmed: during BattleMoving, battleMode reads 1 when the cursor
            //       sits on a tile outside the highlighted move grid. Discriminator:
            //       menuCursor reflects the action menu item selected to reach this
            //       state. Move = index 0, Abilities/targeting = index 1. When
            //       battleMode==1 AND menuCursor==0, we're in Move mode on an
            //       off-grid tile — route to BattleMoving, not BattleAttacking.
            // Cast-time and instant are indistinguishable from memory; callers track
            // cast-time via the ability that was selected (client-side state).
            if (battleMode == 4 || battleMode == 5)
                return "BattleAttacking";
            if (battleMode == 1)
            {
                // Move-mode-on-off-grid-tile signature: menuCursor=0 + submenuFlag=1
                // (Move was selected from the action menu; submenu is active for the
                // move grid UI). Real targeting has menuCursor>=1 OR submenuFlag=0.
                if (menuCursor == 0 && submenuFlag == 1)
                    return "BattleMoving";
                return "BattleAttacking";
            }

            // Waiting: facing selection post-Wait. battleMode=2 + menuCursor=2 distinguishes
            // from BattleMoving (same battleMode=2 but different cursor).
            if (battleMode == 2 && menuCursor == 2)
                return "BattleWaiting";

            if (battleMode == 2)
                return "BattleMoving";

            // Abilities submenu: submenuFlag=1 + battleMode=3 + player's turn + menuCursor==1.
            // The menuCursor==1 guard is essential — after using an ability and returning to
            // the action menu, submenuFlag stays 1 but cursor resets to 0 (Move). Without
            // the cursor check, that post-action state would misfire as Abilities.
            if (submenuFlag == 1 && battleMode == 3 && battleTeam == 0 && menuCursor == 1
                && actedOrMoved)
                return "BattleAbilities";

            // Action menu — player's turn (no action yet).
            // Enemy/ally turn-owner rules run earlier so they can preempt the
            // battleMode-based targeting/move rules; the old copies that sat
            // here were redundant tombstones.
            if (battleTeam == 0 && !actedOrMoved)
                return "BattleMyTurn";

            // Acting: player's turn with flags set.
            if (battleTeam == 0 && actedOrMoved)
                return "BattleActing";

            return "Battle";
        }

        /// <summary>
        /// Converts a selected ability/skillset name into its screen state name.
        /// E.g. "Attack" → "BattleAttack", "White Magicks" → "BattleWhiteMagicks"
        /// </summary>
        public static string GetAbilityScreenName(string abilityName)
        {
            var words = abilityName.Split(' ');
            var pascal = string.Concat(words.Select(w =>
                char.ToUpper(w[0]) + w.Substring(1)));
            return $"Battle{pascal}";
        }

        /// <summary>
        /// Resolves the shop sub-action state name from (shopTypeIndex,
        /// shopSubMenuIndex). Returns null if the combination isn't mapped yet
        /// — the caller falls through to SettlementMenu in that case.
        ///
        /// Mapped 2026-04-14:
        ///   Outfitter (0): Buy=1, Sell=4, Fitting=6
        /// Unmapped (TODO — scan each shop live):
        ///   Tavern (1): Rumors, Errands, ...?
        ///   WarriorsGuild (2): Recruit, Rename, Dismiss, ...?
        ///   PoachersDen (3): Process Carcasses, Sell Carcasses, ...?
        /// </summary>
        public static string? ResolveShopSubAction(int shopTypeIndex, int shopSubMenuIndex)
        {
            switch (shopTypeIndex)
            {
                case 0: // Outfitter — mapped
                    return shopSubMenuIndex switch
                    {
                        1 => "OutfitterBuy",
                        4 => "OutfitterSell",
                        6 => "OutfitterFitting",
                        _ => null,
                    };
                case 1: // Tavern — TODO: scan values and add here
                    return null;
                case 2: // Warriors' Guild — mapped 2026-04-19 at Bervenia
                    return shopSubMenuIndex switch
                    {
                        0x2B => "WarriorsGuildRecruit",
                        0x1A => "WarriorsGuildRename",
                        _ => null,
                    };
                case 3: // Poachers' Den — mapped 2026-04-19 at Dorter
                    return shopSubMenuIndex switch
                    {
                        0x0F => "PoachersDenProcessCarcasses",
                        0x12 => "PoachersDenSellCarcasses",
                        _ => null,
                    };
                default:
                    return null;
            }
        }
    }
}
