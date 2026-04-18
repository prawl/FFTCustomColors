namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure resolution logic for the get_rumor bridge action. Extracted from
    /// CommandWatcher so the priority / fallback chain can be unit-tested
    /// without spinning up the full bridge pipeline.
    ///
    /// Resolution order:
    ///   1. Non-empty searchLabel → title-map or body-substring (title wins)
    ///   2. locationId &gt;= 0 → CityRumors (cityId, unitIndex → corpus index)
    ///   3. fallback → unitIndex as raw corpus index
    /// </summary>
    public static class RumorResolver
    {
        public readonly struct Result
        {
            public bool Ok { get; }
            public WorldMesDecoder.Rumor? Rumor { get; }
            public string Error { get; }

            private Result(bool ok, WorldMesDecoder.Rumor? rumor, string error)
            {
                Ok = ok; Rumor = rumor; Error = error;
            }

            public static Result Success(WorldMesDecoder.Rumor r) => new(true, r, "");
            public static Result Failure(string error) => new(false, null, error);
        }

        public static Result Resolve(RumorLookup lookup, string? searchLabel, int locationId, int unitIndex)
        {
            if (!string.IsNullOrWhiteSpace(searchLabel))
            {
                var r = lookup.GetByTitle(searchLabel!) ?? lookup.GetByBodySubstring(searchLabel!);
                if (r == null)
                    return Result.Failure($"No rumor matches title or body for '{searchLabel}'");
                return Result.Success(r);
            }

            if (locationId >= 0)
            {
                int? corpusIdx = CityRumors.Lookup(locationId, unitIndex);
                if (corpusIdx == null)
                    return Result.Failure($"No rumor mapped for city {locationId} row {unitIndex} (see FFTHandsFree/TavernRumorTitleMap.md to add it)");
                var r = lookup.GetByIndex(corpusIdx.Value);
                if (r == null)
                    return Result.Failure($"CityRumors mapped to corpus index {corpusIdx.Value} but lookup returned null (corpus has {lookup.Count} entries)");
                return Result.Success(r);
            }

            var byIdx = lookup.GetByIndex(unitIndex);
            if (byIdx == null)
                return Result.Failure($"Rumor index {unitIndex} out of range (corpus has {lookup.Count} entries)");
            return Result.Success(byIdx);
        }
    }
}
