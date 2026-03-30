namespace RadialSek.Models
{
    public sealed class CategorySymbolOption
    {
        public CategorySymbolOption(string key, string displayName, string description)
        {
            Key = key;
            DisplayName = displayName;
            Description = description;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public string Description { get; }
    }
}
