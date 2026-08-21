using Xunit;

namespace Tests.Registry
{
    // This collection ensures that all registry tests run sequentially to avoid race
    // conditions with the shared static StoryCharacterRegistry, and (CC-26) with the process
    // wide singletons that point at a test's own temp mod folder: UserThemeServiceSingleton
    // and CharacterServiceSingleton. Any test class that Initializes/SetsModPath on either of
    // those belongs in this collection, so it never runs at the same time as a class that
    // deletes the folder the other one is still pointing at.
    [CollectionDefinition("RegistryTests", DisableParallelization = true)]
    public class RegistryTestCollection
    {
        // This class is only used as a marker for the collection
    }
}