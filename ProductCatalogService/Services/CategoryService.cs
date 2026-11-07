using ProductCatalogService.DTOs;
using ProductCatalogService.Interfaces;
using ProductCatalogService.Models;

namespace ProductCatalogService.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(ICategoryRepository categoryRepository, ILogger<CategoryService> logger)
    {
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<CategoryResponseDto>> GetAllCategoriesAsync()
    {
        var categories = await _categoryRepository.GetAllAsync();
        return categories.Select(MapToResponseDto);
    }

    public async Task<CategoryResponseDto?> GetCategoryByIdAsync(int id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        return category != null ? MapToResponseDto(category) : null;
    }

    public async Task<CategoryResponseDto> CreateCategoryAsync(CategoryCreateDto createDto)
    {
        var category = new Category { Name = createDto.Name, Description = createDto.Description };
        var created = await _categoryRepository.CreateAsync(category);
        _logger.LogInformation("Category created with ID: {CategoryId}", created.Id);
        return MapToResponseDto(created);
    }

    public async Task<CategoryResponseDto?> UpdateCategoryAsync(int id, CategoryUpdateDto updateDto)
    {
        var existing = await _categoryRepository.GetByIdAsync(id);
        if (existing == null) return null;
        if (updateDto.Name != null) existing.Name = updateDto.Name;
        if (updateDto.Description != null) existing.Description = updateDto.Description;
        var updated = await _categoryRepository.UpdateAsync(existing);
        return updated != null ? MapToResponseDto(updated) : null;
    }

    public async Task<bool> DeleteCategoryAsync(int id) =>
        await _categoryRepository.DeleteAsync(id);

    private static CategoryResponseDto MapToResponseDto(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        CreatedAt = category.CreatedAt,
        ProductCount = category.Products?.Count ?? 0
    };
}
