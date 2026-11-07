using Microsoft.EntityFrameworkCore;
using ProductCatalogService.Data;
using ProductCatalogService.Interfaces;
using ProductCatalogService.Models;

namespace ProductCatalogService.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly CatalogDbContext _context;

    public CategoryRepository(CatalogDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Category>> GetAllAsync() =>
        await _context.Categories.Include(c => c.Products).ToListAsync();

    public async Task<Category?> GetByIdAsync(int id) =>
        await _context.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Category> CreateAsync(Category category)
    {
        category.CreatedAt = DateTime.UtcNow;
        category.UpdatedAt = DateTime.UtcNow;
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<Category?> UpdateAsync(Category category)
    {
        var existing = await _context.Categories.FindAsync(category.Id);
        if (existing == null) return null;
        _context.Entry(existing).CurrentValues.SetValues(category);
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return false;
        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id) =>
        await _context.Categories.AnyAsync(c => c.Id == id);
}
