namespace PhanNgocQuynhHuong_AntiTheftAPI.DTOs.Admin;

public class PagedResult<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
}
