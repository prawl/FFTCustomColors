#!/bin/bash
# Fast bridge: write command, poll response, output result
# Usage: ./bridge.sh '{"id":"x","keys":[...]}'
B="/c/program files (x86)/steam/steamapps/common/FINAL FANTASY TACTICS - The Ivalice Chronicles/Reloaded/Mods/FFTColorCustomizer/claude_bridge"
CMD="$1"
ID=$(echo "$CMD" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "$CMD" > "$B/command.json"
for i in $(seq 1 120); do
  if [ -f "$B/response.json" ] && grep -q "\"id\":\"$ID\"" "$B/response.json" 2>/dev/null; then
    cat "$B/response.json"
    exit 0
  fi
  sleep 0.25
done
echo "TIMEOUT"
