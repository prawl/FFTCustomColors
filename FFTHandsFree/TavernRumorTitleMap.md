<!-- This file should not be longer than 200 lines, if so prune me. -->
# Tavern Rumor Title Map — Workflow

How to add a new entry to `RumorLookup.TitleToIndex` when a new Tavern rumor title is observed in-game.

## Why the map exists

Tavern UI shows rumor titles ("The Legend of the Zodiac Braves", "The Horror of Riovanes") in the left column. The body text in the right pane IS in `world_wldmes.bin` (decoded into `RumorCorpus.Bodies`), but titles ARE NOT anywhere in RAM or in the decoded file. We confirmed this session 32/33 via:

- Memory search for distinctive title phrases → zero UTF-8 / UTF-16LE hits
- Binary search for PSX-encoded titles across 318 .bin files in 0000-0004 pac dirs → zero hits

Titles appear to be composed at runtime from a UE4 string table or widget metadata we haven't reached. Until we decode that source, the title→body mapping is maintained by hand.

## The file

`ColorMod/GameBridge/RumorLookup.cs` holds a `Dictionary<string, int>` called `TitleToIndex`. Each key is the exact UI title; each value is the index into `RumorCorpus.Bodies`.

```csharp
private static readonly Dictionary<string, int> TitleToIndex =
    new(System.StringComparer.OrdinalIgnoreCase)
{
    { "The Legend of the Zodiac Braves", 10 },
    { "Zodiac Stones", 11 },
    { "The Horror of Riovanes", 19 },
};
```

## Adding a new entry

### 1. Observe the title in-game

Travel to a Tavern, open Rumors, screenshot each row. Note the exact title text (watch for "The " prefix, capitalization, punctuation).

### 2. Find the matching corpus body

Use the running mod:

```bash
source ./fft.sh
list_rumors          # shows all 123 decoded bodies with 80-char previews
read_rumor "<distinctive phrase from the on-screen body>"
```

When `read_rumor` returns a matching body, note the `[corpus #N]` index in the response header.

### 3. Add the entry

Edit [RumorLookup.cs](../ColorMod/GameBridge/RumorLookup.cs) — add the title + corpus index to `TitleToIndex`.

### 4. Add a regression test

In [WorldMesDecoderTests.cs](../Tests/GameBridge/WorldMesDecoderTests.cs), add a test asserting the title resolves and the body contains a distinctive phrase:

```csharp
[Fact]
public void RumorLookup_GetByTitle_YourNewTitle_ReturnsCorpusN()
{
    var lookup = new RumorLookup();
    var r = lookup.GetByTitle("Your New Title");
    Assert.NotNull(r);
    Assert.Equal(N, r!.Index);
    Assert.Contains("distinctive phrase", r.Body);
}
```

Run `./RunTests.sh` to confirm.

## Known unmappable titles

Some rumors cannot be mapped because their body text is NOT in `world_wldmes.bin` at all. Example: **"At Bael's End"** (Dorter chapter-4 row 3) — the word "Bael" returns zero hits across all 318 .bin files in 0000-0004 pac dirs. These live in a different resource file we haven't reached (likely a UE4 `.locres` file or `world_snplmes_bin.en.bin` with a different multi-byte encoding). Document such cases in this file so future explorers don't waste time searching `world_wldmes.bin` for them.

### Known unmappable titles

- `At Bael's End` — Dorter, chapter 4 row 3. Not in `world_wldmes.bin`.

## Why not just use body substring matching?

`read_rumor "<phrase>"` already works via body substring match — that's the fallback path when the title isn't in the map. The title map is the PREFERRED path because:

1. Faster — O(1) dict lookup vs O(N) substring scan
2. Cleaner UX — Claude reads the title from screen, passes it verbatim
3. Unambiguous — distinct titles can have overlapping body phrases

Keep both paths working. The map grows organically as new titles are observed.
