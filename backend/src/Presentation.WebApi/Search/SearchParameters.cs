namespace PointLurker.Presentation.WebApi.Search;

/// <summary>
/// The query-string names every searchable list endpoint answers to. They are the names the
/// frontend already writes to the browser URL, so its existing serializer becomes the API query
/// string with no translation layer and bookmarked filtered views keep resolving.
/// </summary>
internal static class SearchParameters
{
    // `q` and `cat` are free text that needs no parsing, so they are bound by parameter name
    // and have no constant here — one would imply a validation step that does not exist.
    public const string Available = "avail";
    public const string SubscriberOnly = "sub";
    public const string Featured = "feat";
    public const string MinCost = "min";
    public const string MaxCost = "max";
    public const string Sort = "sort";
    public const string Direction = "dir";

    public const string Offset = "offset";
    public const string Limit = "limit";

    public const string Status = "status";
    public const string From = "from";
    public const string To = "to";
    public const string WishlistEntry = "wishlistEntryId";

    public const string Active = "active";
    public const string Store = "store";
    public const string AutoRedeem = "auto";
    public const string Notify = "notify";
    public const string Blocked = "blocked";
}
