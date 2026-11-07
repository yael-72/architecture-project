using BffService.DTOs;
using SharedKernel.DTOs;

namespace BffService.Clients;

public interface ICatalogClient
{
    Task<PagedResult<DownstreamProductDto>?> GetProductsAsync(int pageNumber, int pageSize);
    Task<DownstreamProductDto?> GetProductByIdAsync(int id);
    Task<IEnumerable<DownstreamCategoryDto>?> GetCategoriesAsync();
}
