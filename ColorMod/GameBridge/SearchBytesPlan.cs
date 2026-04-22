using FFTColorCustomizer.Utilities;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Pure plan-builder for the <c>search_bytes</c> bridge action.
    /// Decodes the CommandRequest's three knobs (minAddr / maxAddr /
    /// broadSearch) into the concrete args for MemoryExplorer's
    /// SearchBytesInAllMemory. Keeps the decoding testable without
    /// pulling in Windows memory APIs.
    /// </summary>
    public readonly struct SearchBytesPlan
    {
        public long MinAddr { get; }
        public long MaxAddr { get; }
        public bool BroadSearch { get; }

        public SearchBytesPlan(long minAddr, long maxAddr, bool broadSearch)
        {
            MinAddr = minAddr;
            MaxAddr = maxAddr;
            BroadSearch = broadSearch;
        }

        public static SearchBytesPlan From(CommandRequest request)
        {
            long minA = CommandRequest.ParseAddrOrDefault(request.MinAddr, 0L);
            long maxA = CommandRequest.ParseAddrOrDefault(request.MaxAddr, long.MaxValue);
            return new SearchBytesPlan(minA, maxA, request.BroadSearch);
        }
    }
}
