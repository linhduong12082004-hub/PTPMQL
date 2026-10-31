using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Models
{
    public class PaginatedList<T> : IPaginatedList
    {
        public const int DefaultPageSize = 10;

        private static readonly IReadOnlyList<int> AllowedPageSizes =
            Array.AsReadOnly(new[] { 10, 20, 50 });

        private PaginatedList(
            IReadOnlyList<T> items,
            int pageNumber,
            int pageSize,
            int totalCount,
            int totalPages)
        {
            Items = items;
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalCount = totalCount;
            TotalPages = totalPages;
            PageSizeOptions = AllowedPageSizes;
        }

        public IReadOnlyList<T> Items { get; }
        public int PageNumber { get; }
        public int PageSize { get; }
        public int TotalCount { get; }
        public int TotalPages { get; }
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
        public IReadOnlyList<int> PageSizeOptions { get; }

        public static async Task<PaginatedList<T>> CreateAsync(
            IQueryable<T> source,
            int pageNumber,
            int pageSize)
        {
            var totalCount = await source.CountAsync();
            var normalizedPageSize = AllowedPageSizes.Contains(pageSize)
                ? pageSize
                : DefaultPageSize;
            var totalPages = (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
            var normalizedPageNumber = totalPages == 0
                ? 1
                : Math.Clamp(pageNumber, 1, totalPages);

            var items = await source
                .Skip((normalizedPageNumber - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync();

            return new PaginatedList<T>(
                items,
                normalizedPageNumber,
                normalizedPageSize,
                totalCount,
                totalPages);
        }
    }
}
