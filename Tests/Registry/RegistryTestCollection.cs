using Xunit;

namespace Tests.Registry
{
    // This collection ensures that all registry tests run sequentially
    // to avoid race conditions with the shared static StoryCharacterRegistry
    [CollectionDefinition("RegistryTests", DisableParallelization = true)]
    public class RegistryTestCollection
    {
        // This class is only used as a marker for the collection
    }
}