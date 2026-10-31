namespace DemoMVC.Models
{
    public interface IPaginatedList
    {
        int PageNumber { get; }
        int PageSize { get; }
        int TotalCount { get; }
        int TotalPages { get; }
        bool HasPreviousPage { get; }
        bool HasNextPage { get; }
        IReadOnlyList<int> PageSizeOptions { get; }
    }
}
