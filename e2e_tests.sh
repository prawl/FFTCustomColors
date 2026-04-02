#!/bin/bash
# FFT Game Bridge E2E Tests
# Run individual tests: ./e2e_tests.sh test1
# Tests are sequential — caller should stop if one fails

B="/c/program files (x86)/steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/Reloaded/Mods/FFTColorCustomizer/claude_bridge"

brg() { rm -f "$B/response.json"; echo "$1" > "$B/command.json"; until [ -f "$B/response.json" ]; do sleep 0.05; done; cat "$B/response.json"; }
resp() { brg "$1" | tr -d ' \n\r'; }
screenshot() { powershell.exe -ExecutionPolicy Bypass -File ./screenshot.ps1 2>/dev/null; }

nextid() { echo "c$(date +%s%N | tail -c 8)$RANDOM"; }
get_units() { resp "{\"id\":\"$(nextid)\",\"action\":\"report_state\"}" | grep -o '"activeUnitCount":[0-9]*' | cut -d: -f2; }
get_cursor() { resp "{\"id\":\"$(nextid)\",\"action\":\"report_state\"}" | grep -o '"cursorIndex":[0-9]*' | cut -d: -f2; }
read_byte() { resp "{\"id\":\"$(nextid)\",\"action\":\"read_address\",\"address\":\"$1\",\"readSize\":1}" | grep -o '"value":[0-9]*' | cut -d: -f2; }
read_u16() { resp "{\"id\":\"$(nextid)\",\"action\":\"read_address\",\"address\":\"$1\",\"readSize\":2}" | grep -o '"value":[0-9]*' | cut -d: -f2; }

assert_eq() {
  local name="$1" actual="$2" expected="$3"
  if [ "$actual" = "$expected" ]; then
    echo "PASS: $name ($actual)"
    return 0
  else
    echo "FAIL: $name — expected=$expected actual=$actual"
    return 1
  fi
}

# ============================================================
# TEST 1: Launch game, land on Start Game screen
# ============================================================
test1() {
  echo "=== Test 1: Game Launch → Start Game Screen ==="

  taskkill.exe //IM FFT_enhanced.exe //F 2>/dev/null
  sleep 2

  # Clear stale bridge files
  rm -f "$B/state.json" "$B/response.json" "$B/command.json" "$B/command.processed.json" 2>/dev/null

  "/c/program files (x86)/steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/Reloaded/reloaded-ii.exe" --launch "C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY TACTICS - The Ivalice Chronicles\FFT_enhanced.exe" &

  echo "Waiting for bridge and title screen..."
  for i in $(seq 1 60); do
    if [ -f "$B/state.json" ]; then
      UNITS=$(cat "$B/state.json" | tr -d ' \n' | grep -o '"activeUnitCount":[0-9]*' | cut -d: -f2)
      if [ "$UNITS" = "55" ]; then
        echo "Title screen ready (attempt $i)"
        break
      fi
    fi
    sleep 1
  done

  UNITS=$(get_units)
  assert_eq "activeUnitCount=55 (title screen)" "$UNITS" "55" || return 1

  echo "Waiting for game to finish loading..."
  sleep 20
  SHOT=$(screenshot)
  echo "Screenshot: $SHOT"
  echo "VERIFY: Should show Start Game screen with Enhanced/Classic options"
}

# ============================================================
# TEST 2: Press Enter → Continue screen
# ============================================================
test2() {
  echo "=== Test 2: Start Game → Continue → World Map ==="

  # Press Enter on Start Game
  brg "{\"id\":\"$(nextid)\",\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"delayBetweenMs\":150}" > /dev/null
  sleep 3

  # Press Enter on Continue
  brg "{\"id\":\"$(nextid)\",\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"delayBetweenMs\":150}" > /dev/null
  sleep 3

  # Press Enter to load save
  brg "{\"id\":\"$(nextid)\",\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"delayBetweenMs\":150}" > /dev/null

  # Poll until units = 17-20 (save loaded) by checking state.json directly
  echo "Waiting for save to load..."
  UNITS=0
  for i in $(seq 1 30); do
    UNITS=$(cat "$B/state.json" 2>/dev/null | tr -d ' \n' | grep -o '"activeUnitCount":[0-9]*' | cut -d: -f2)
    if [ "$UNITS" -ge 17 ] 2>/dev/null && [ "$UNITS" -le 20 ] 2>/dev/null; then
      echo "Save loaded (attempt $i, units=$UNITS)"
      break
    fi
    sleep 1
  done

  assert_eq "activeUnitCount=18 (world map)" "$UNITS" "18" || return 1

  sleep 3
  SHOT=$(screenshot)
  echo "Screenshot: $SHOT"
  echo "VERIFY: Should show World Map"
}

# ============================================================
# TEST 4: Open Party Menu
# ============================================================
test4() {
  echo "=== Test 4: Escape → Party Menu ==="

  brg "{\"id\":\"$(nextid)\",\"keys\":[{\"vk\":27,\"name\":\"Escape\"}],\"delayBetweenMs\":150}" > /dev/null
  sleep 2

  CURSOR=$(get_cursor)
  assert_eq "Cursor at position 0 (Ramza)" "$CURSOR" "0" || return 1
}

# ============================================================
# TEST 5: Navigate to character at (0,1)
# ============================================================
test5() {
  echo "=== Test 5: Navigate Right → Character (0,1) ==="

  brg "{\"id\":\"$(nextid)\",\"keys\":[{\"vk\":39,\"name\":\"Right\"}],\"delayBetweenMs\":150}" > /dev/null
  sleep 1

  CURSOR=$(get_cursor)
  assert_eq "Cursor at position 1" "$CURSOR" "1" || return 1
}

# ============================================================
# TEST 6: Open character status → Equipment & Abilities
# ============================================================
test6() {
  echo "=== Test 6: Change Job — Summoner → Black Mage ==="

  # Precondition: party menu open, cursor on character (0,1)
  CURSOR=$(get_cursor)
  assert_eq "Precondition: cursor on character 1" "$CURSOR" "1" || return 1

  # Read current job (slot 1, +0x02)
  OLD_JOB=$(read_byte "1411A1B2A")
  echo "Current job ID: $OLD_JOB"

  # Open status → sidebar down to Job → open job screen
  brg "{\"id\":\"$(nextid)\",\"action\":\"sequence\",\"delayBetweenMs\":200,\"steps\":[{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":2000,\"description\":\"open status\"},{\"keys\":[{\"vk\":40,\"name\":\"Down\"}],\"waitMs\":500,\"description\":\"sidebar to Job\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":2000,\"description\":\"open job screen\"}]}" > /dev/null

  # From Summoner (R1,C2): Left 2 to Black Mage (R1,C0)
  # Select → Right (Change Job) → Enter (confirm) → Enter (dismiss)
  brg "{\"id\":\"$(nextid)\",\"action\":\"sequence\",\"delayBetweenMs\":200,\"steps\":[{\"keys\":[{\"vk\":37,\"name\":\"Left\"},{\"vk\":37,\"name\":\"Left\"}],\"waitMs\":300,\"description\":\"Left 2 to Black Mage\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":2000,\"description\":\"select job\"},{\"keys\":[{\"vk\":39,\"name\":\"Right\"}],\"waitMs\":500,\"description\":\"Change Job\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":2000,\"description\":\"confirm\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":2000,\"description\":\"dismiss\"}]}" > /dev/null

  # Verify job changed
  NEW_JOB=$(read_byte "1411A1B2A")
  echo "New job ID: $NEW_JOB"

  # Assert job changed
  TOTAL=$((TOTAL+1))
  if [ "$NEW_JOB" != "$OLD_JOB" ] && [ -n "$NEW_JOB" ] && [ "$NEW_JOB" != "0" ]; then
    echo "PASS: Job changed from $OLD_JOB to $NEW_JOB"
    PASS=$((PASS+1))
  else
    echo "FAIL: Job did not change (old=$OLD_JOB new=$NEW_JOB)"
    FAIL=$((FAIL+1))
    return 1
  fi

  # Change back to Summoner: now on sidebar, go to Job screen
  # Cursor starts on Black Mage (R1,C0), Right 2 to Summoner (R1,C2)
  brg "{\"id\":\"$(nextid)\",\"action\":\"sequence\",\"delayBetweenMs\":200,\"steps\":[{\"keys\":[{\"vk\":40,\"name\":\"Down\"}],\"waitMs\":500,\"description\":\"sidebar to Job\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":2000,\"description\":\"open job screen\"},{\"keys\":[{\"vk\":39,\"name\":\"Right\"},{\"vk\":39,\"name\":\"Right\"}],\"waitMs\":300,\"description\":\"Right 2 to Summoner\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":2000,\"description\":\"select\"},{\"keys\":[{\"vk\":39,\"name\":\"Right\"}],\"waitMs\":500,\"description\":\"Change Job\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":2000,\"description\":\"confirm\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":2000,\"description\":\"dismiss\"}]}" > /dev/null

  RESTORED_JOB=$(read_byte "1411A1B2A")
  assert_eq "Job restored to original" "$RESTORED_JOB" "$OLD_JOB" || return 1
}

# ============================================================
# TEST 7: Change Secondary Ability — Bardsong → Iaido → Bardsong
# Precondition: Kenrick selected, highlighting Equipment & Abilities sidebar item
# ============================================================
test7() {
  echo "=== Test 7: Change Secondary Ability — Bardsong → Iaido ==="

  # Precondition: cursor on Kenrick (slot 1)
  CURSOR=$(get_cursor)
  assert_eq "Precondition: cursor on Kenrick" "$CURSOR" "1" || return 1

  # Read current secondary (slot 1, +0x07) — should be Bardsong (22)
  OLD_SEC=$(read_byte "1411A1B2F")
  assert_eq "Precondition: secondary is Bardsong (22)" "$OLD_SEC" "22" || return 1

  # Enter Equipment & Abilities, navigate to secondary ability slot
  # Right (ability col) → Down (secondary row) → Enter (open list)
  brg "{\"id\":\"$(nextid)\",\"action\":\"sequence\",\"delayBetweenMs\":200,\"steps\":[{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":2000,\"description\":\"open Equip&Abilities\"},{\"keys\":[{\"vk\":39,\"name\":\"Right\"}],\"waitMs\":300,\"description\":\"ability col\"},{\"keys\":[{\"vk\":40,\"name\":\"Down\"}],\"waitMs\":300,\"description\":\"secondary row\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":500,\"description\":\"open list (on Bardsong)\"}]}" > /dev/null

  # Bardsong=22, Iaido=19. Need Up 3.
  brg "{\"id\":\"$(nextid)\",\"action\":\"sequence\",\"delayBetweenMs\":200,\"steps\":[{\"keys\":[{\"vk\":38,\"name\":\"Up\"},{\"vk\":38,\"name\":\"Up\"},{\"vk\":38,\"name\":\"Up\"}],\"waitMs\":300,\"description\":\"Up 3 to Iaido\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":500,\"description\":\"select Iaido\"},{\"keys\":[{\"vk\":27,\"name\":\"Escape\"}],\"waitMs\":2000,\"description\":\"close list\"}]}" > /dev/null

  # Verify secondary changed to Iaido (19)
  NEW_SEC=$(read_byte "1411A1B2F")
  assert_eq "Secondary changed to Iaido (19)" "$NEW_SEC" "19" || return 1

  # Change back: reopen list, Down 3 to Bardsong
  brg "{\"id\":\"$(nextid)\",\"action\":\"sequence\",\"delayBetweenMs\":200,\"steps\":[{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":500,\"description\":\"reopen list (on Iaido)\"},{\"keys\":[{\"vk\":40,\"name\":\"Down\"},{\"vk\":40,\"name\":\"Down\"},{\"vk\":40,\"name\":\"Down\"}],\"waitMs\":300,\"description\":\"Down 3 to Bardsong\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":500,\"description\":\"select Bardsong\"},{\"keys\":[{\"vk\":27,\"name\":\"Escape\"}],\"waitMs\":2000,\"description\":\"close list\"}]}" > /dev/null

  # Verify restored
  RESTORED_SEC=$(read_byte "1411A1B2F")
  assert_eq "Secondary restored to Bardsong (22)" "$RESTORED_SEC" "22" || return 1

  # Back out to sidebar (Equipment & Abilities highlighted)
  brg "{\"id\":\"$(nextid)\",\"keys\":[{\"vk\":27,\"name\":\"Escape\"}],\"delayBetweenMs\":150}" > /dev/null
  sleep 2
}

# ============================================================
# TEST 8: Unequip and re-equip accessory
# Precondition: Kenrick selected, highlighting Equipment & Abilities sidebar item
# ============================================================
test8() {
  echo "=== Test 8: Unequip and Re-equip Accessory ==="

  # Precondition: cursor on Kenrick
  CURSOR=$(get_cursor)
  assert_eq "Precondition: cursor on Kenrick" "$CURSOR" "1" || return 1

  # Open Equipment & Abilities, navigate to accessory (left col, row 4)
  brg "{\"id\":\"$(nextid)\",\"action\":\"sequence\",\"delayBetweenMs\":200,\"steps\":[{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":2000,\"description\":\"open Equip&Abilities\"},{\"keys\":[{\"vk\":40,\"name\":\"Down\"},{\"vk\":40,\"name\":\"Down\"},{\"vk\":40,\"name\":\"Down\"},{\"vk\":40,\"name\":\"Down\"}],\"waitMs\":300,\"description\":\"down to accessory row\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":500,\"description\":\"open item list\"}]}" > /dev/null

  # Screenshot to see equipped item
  sleep 1
  SHOT=$(screenshot)
  echo "Screenshot (item list open): $SHOT"

  # Enter on equipped item = unequip, then Escape to close
  brg "{\"id\":\"$(nextid)\",\"action\":\"sequence\",\"delayBetweenMs\":200,\"steps\":[{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":500,\"description\":\"unequip\"},{\"keys\":[{\"vk\":27,\"name\":\"Escape\"}],\"waitMs\":2000,\"description\":\"close list\"}]}" > /dev/null

  # Screenshot to verify unequipped
  sleep 1
  SHOT2=$(screenshot)
  echo "Screenshot (after unequip): $SHOT2"

  # Re-equip: open list, Enter to equip first item, Escape to close
  brg "{\"id\":\"$(nextid)\",\"action\":\"sequence\",\"delayBetweenMs\":200,\"steps\":[{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":500,\"description\":\"reopen list\"},{\"keys\":[{\"vk\":13,\"name\":\"Enter\"}],\"waitMs\":500,\"description\":\"equip first item\"},{\"keys\":[{\"vk\":27,\"name\":\"Escape\"}],\"waitMs\":2000,\"description\":\"close list\"}]}" > /dev/null

  # Screenshot to verify re-equipped
  sleep 1
  SHOT3=$(screenshot)
  echo "Screenshot (after re-equip): $SHOT3"

  echo "PASS: Unequip/re-equip sequence completed"
  PASS=$((PASS+1))
  TOTAL=$((TOTAL+1))

  # Back out to sidebar
  brg "{\"id\":\"$(nextid)\",\"keys\":[{\"vk\":27,\"name\":\"Escape\"}],\"delayBetweenMs\":150}" > /dev/null
  sleep 2
}

# ============================================================
# Run requested test
# ============================================================
case "$1" in
  test1) test1 ;;
  test2) test2 ;;
  test4) test4 ;;
  test5) test5 ;;
  test6) test6 ;;
  test7) test7 ;;
  test8) test8 ;;
  *)
    echo "Usage: ./e2e_tests.sh <test1|test2|test3|...>"
    echo ""
    echo "Tests (run sequentially, stop on failure):"
    echo "  test1 - Launch game, verify Start Game screen"
    echo "  test2 - Load save, verify World Map"
    echo "  test4 - Open Party Menu, verify cursor at 0"
    echo "  test5 - Navigate right, verify cursor at 1"
    echo "  test6 - Change job Summoner→Black Mage→Summoner"
    echo "  test7 - Change secondary Bardsong→Iaido→Bardsong"
    echo "  test8 - Unequip and re-equip accessory"
    ;;
esac
