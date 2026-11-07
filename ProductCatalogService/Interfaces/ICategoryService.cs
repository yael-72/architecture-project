using ProductCatalogService.DTOs;

namespace ProductCatalogService.Interfaces;

public interface ICategoryService
{
    Task<IEnumerable<CategoryResponseDto>> GetAllCategoriesAsync();
    Task<CategoryResponseDto?> GetCategoryByIdAsync(int id);
    Task<CategoryResponseDto> CreateCategoryAsync(CategoryCreateDto createDto);
    Task<CategoryResponseDto?> UpdateCategoryAsync(int id, CategoryUpdateDto updateDto);
    Task<bool> DeleteCategoryAsync(int id);
}
