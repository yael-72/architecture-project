using Microsoft.EntityFrameworkCore;
using ProductCatalogService.Data;
using ProductCatalogService.Interfaces;
using ProductCatalogService.Models;

namespace ProductCatalogService.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly CatalogDbContext _context;

    public ProductRepository(CatalogDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Product>> GetAllAsync() =>
        await _context.Products.Include(p => p.Category).ToListAsync();

    public async Task<(IEnumerable<Product> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize)
    {
        var totalCount = await _context.Products.CountAsync();
        var items = await _context.Products
            .Include(p => p.Category)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, totalCount);
    }

    public async Task<Product?> GetByIdAsync(int id) =>
        await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Product> CreateAsync(Product product)
    {
        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task<Product?> UpdateAsync(Product product)
    {
        var existing = await _context.Products.FindAsync(product.Id);
        if (existing == null) return null;
        _context.Entry(existing).CurrentValues.SetValues(product);
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return false;
        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id) =>
        await _context.Products.AnyAsync(p => p.Id == id);

    public async Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId) =>
        await _context.Products.Where(p => p.CategoryId == categoryId).Include(p => p.Category).ToListAsync();

    public async Task<IEnumerable<Product>> SearchByNameAsync(string searchTerm) =>
        await _context.Products.Where(p => p.Name.Contains(searchTerm)).Include(p => p.Category).ToListAsync();

    public async Task<(IEnumerable<Product> Items, int TotalCount)> SearchByNamePagedAsync(string searchTerm, int pageNumber, int pageSize)
    {
        var query = _context.Products.Where(p => p.Name.Contains(searchTerm)).Include(p => p.Category);
        var totalCount = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, totalCount);
    }
}
