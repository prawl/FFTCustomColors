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
list_rumors          # shows all 26 decoded bodies with first-sentence previews
read_rumor "<distinctive phrase from the on-screen body>"
```

When `read_rumor` returns a matching body, note the `[corpus #N]` index in the response header.

The `list_rumors` preview is the **first sentence** of each body (via `RumorLookup.FirstSentence`), so most entries are title-matchable at a glance. If the first sentence doesn't disambiguate, fall through to `read_rumor "<phrase>"` with a distinctive mid-body phrase.

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

Some rumors cannot be mapped because their body text is NOT in `world_wldmes.bin` at all.

- **`At Bael's End`** — Dorter, chapter 4 row 3. The word "Bael" returns zero hits across all 318 .bin files in pac dirs 0000-0004 (PSX encoded). Next-investigation targets (in priority order): (a) `0002.en.pac/fftpack/world_snplmes_bin.en.bin` (~245KB, mixed encoding with `D1/D2/D3` multibyte sequences — candidate for a different decoder pass); (b) UE4 `Paks/*.pak` locres files; (c) pac dirs 0005-0011 not yet scanned.

Document such cases here so future explorers don't waste time searching `world_wldmes.bin` for them.

## City + row resolution (newer path)

As of session 34, `get_rumor` also accepts `{locationId, unitIndex}` to resolve a rumor body directly from the Tavern cursor position — no title or distinctive phrase required. The table lives in [CityRumors.cs](../ColorMod/GameBridge/CityRumors.cs) keyed by settlement id → row → corpus index.

When you visit a new city's Tavern, add its rows to `CityRumors.Table` alongside any new title-map entries. This lets future sessions resolve rumors from cursor position alone, which is useful once the TavernRumors cursorRow pointer-chain walk ships (memory note: `project_tavern_rumor_cursor.md`).

## Why not just use body substring matching?

`read_rumor "<phrase>"` already works via body substring match — that's the fallback path when neither the title nor the city+row is known. The title map + city-row path are preferred because:

1. **Faster** — O(1) dict lookup vs O(N) substring scan
2. **Cleaner UX** — Claude reads the title from screen, passes it verbatim
3. **Unambiguous** — distinct titles can have overlapping body phrases
4. **No typos** — substring matches silently fail when the human mistypes; map lookup surfaces "no rumor matches" cleanly

Keep all three paths working. The maps grow organically as new titles / city rows are observed.
