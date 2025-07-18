using System.Collections.Generic;

namespace MCPVault.API.Models.Responses
{
    public class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => TotalCount > 0 ? (TotalCount + PageSize - 1) / PageSize : 0;
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}