using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Infrastructure.Data;

namespace BillApp.Infrastructure.Repositories;

/// <summary>
/// LiteDB implementation of the Payee repository.
/// </summary>
public class PayeeRepository : RepositoryBase<Payee>, IPayeeRepository
{
    private readonly ICategoryRepository _categoryRepository;

    public PayeeRepository(LiteDbContext context, ICategoryRepository categoryRepository) : base(context)
    {
        _categoryRepository = categoryRepository;
    }

    public Task<Payee?> GetByNameAsync(string name)
    {
        var payee = Collection.FindOne(p =>
            p.Name.ToLower() == name.ToLower());
        return Task.FromResult<Payee?>(payee);
    }

    public async Task<IEnumerable<Payee>> GetAllWithCategoriesAsync()
    {
        var payees = Collection.FindAll().ToList();
        var categories = (await _categoryRepository.GetAllAsync()).ToDictionary(c => c.Id);

        foreach (var payee in payees)
        {
            if (payee.CategoryId.HasValue && categories.TryGetValue(payee.CategoryId.Value, out var category))
            {
                payee.Category = category;
            }
        }

        return payees;
    }
}
