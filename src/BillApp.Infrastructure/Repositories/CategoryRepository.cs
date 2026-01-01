using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Infrastructure.Data;

namespace BillApp.Infrastructure.Repositories;

/// <summary>
/// LiteDB implementation of the Category repository.
/// </summary>
public class CategoryRepository : RepositoryBase<Category>, ICategoryRepository
{
    public CategoryRepository(LiteDbContext context) : base(context)
    {
    }

    public Task<Category?> GetByNameAsync(string name)
    {
        var category = Collection.FindOne(c =>
            c.Name.ToLower() == name.ToLower());
        return Task.FromResult<Category?>(category);
    }

    public Task<IEnumerable<Category>> GetDefaultCategoriesAsync()
    {
        var categories = Collection.Find(c => c.IsDefault);
        return Task.FromResult(categories);
    }
}
