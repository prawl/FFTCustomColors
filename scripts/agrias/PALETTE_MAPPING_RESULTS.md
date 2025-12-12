# Agrias Palette Mapping Results

## Test Results (2024-12-12)

### Color Test Observations:
- **RED (indices 3-6)**: Upper chest armor, pants, boots
  - ⚠️ Issue: 1 pixel on her eye (index overlap)
- **GREEN (indices 7-10)**: Hair outline, waist-to-arms armor sections
- **BLUE (indices 11-15)**: Face, hair, under armor, gloves

### Critical Findings:
1. **Indices 11-15 control face/hair** - MUST NOT be modified for armor themes
2. **Index 3-6 has minor eye pixel overlap** - May need careful color selection
3. **Indices 7-10 are safe for secondary armor colors**

### Recommended Palette Strategy for Agrias:
```
Indices 0-2:   Keep BLACK (shadows/outlines)
Indices 3-6:   Primary armor color (careful with brightness to minimize eye pixel issue)
Indices 7-10:  Secondary armor/accent color (safe to modify)
Indices 11-15: DO NOT MODIFY (face/hair/skin tones)
```

### Comparison with Other Characters:
- **Beowulf**: Had issues with indices 7-10 affecting face (black eye problem)
- **Agrias**: Has issues with indices 11-15 being face/hair
- **Conclusion**: Each character has unique palette mapping!