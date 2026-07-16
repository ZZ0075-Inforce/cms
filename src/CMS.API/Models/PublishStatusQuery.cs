namespace CMS.API.Models;

/// <summary>
/// Search DTO for POST /api/publish-statuses/query. A keyword LIKE on Description plus three
/// tri-state bit filters — null means "don't filter on this flag".
/// </summary>
public class PublishStatusQuery
{
    /// <summary>LIKE across Description.</summary>
    public string? Keyword { get; set; }

    /// <summary>null = 不篩選、true = 只顯示勾選、false = 只顯示未勾選。</summary>
    public bool? IsDraft { get; set; }

    public bool? IsPublished { get; set; }

    public bool? IsDiscontinued { get; set; }
}
