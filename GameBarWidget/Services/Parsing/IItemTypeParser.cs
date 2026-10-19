namespace GameBarWidget.Services.Parsing
{
    /// <summary>
    /// Contract for domain-specific item type parsers.
    /// Inspects whether an item matches the type and parses specialized attributes.
    /// </summary>
    public interface IItemTypeParser
    {
        bool CanParse(PoeItem item, string[] headerLines, string[] blocks);
        void Parse(PoeItem item, string[] headerLines, string[] blocks);
    }
}
