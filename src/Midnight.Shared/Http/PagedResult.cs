namespace Midnight.Shared.Http;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class PagedQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }

    public const int MaxPageSize = 100;

    // Upper bound keeps (page - 1) * pageSize from overflowing
    public int SafePage => Math.Clamp(Page, 1, int.MaxValue / MaxPageSize);
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}
