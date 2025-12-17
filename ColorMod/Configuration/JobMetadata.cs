namespace FFTColorCustomizer.Configuration
{
    /// <summary>
    /// Metadata for a job/character configuration
    /// </summary>
    public class JobMetadata
    {
        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string JsonPropertyName { get; }

        public JobMetadata(string category, string displayName, string description, string jsonPropertyName)
        {
            Category = category;
            DisplayName = displayName;
            Description = description;
            JsonPropertyName = jsonPropertyName;
        }
    }
}
