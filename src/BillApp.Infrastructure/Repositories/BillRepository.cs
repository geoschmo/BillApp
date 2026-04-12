using BillApp.Core.Enums;
using BillApp.Core.Interfaces.Repositories;
using BillApp.Core.Models;
using BillApp.Infrastructure.Data;

namespace BillApp.Infrastructure.Repositories;

/// <summary>
/// LiteDB implementation of the Bill repository.
/// </summary>
public class BillRepository : RepositoryBase<Bill>, IBillRepository
{
    public BillRepository(LiteDbContext context) : base(context)
    {
    }

    public Task<IEnumerable<Bill>> GetByStatusAsync(PaymentStatus status)
    {
        var bills = Collection.Find(b => b.Status == status);
        return Task.FromResult(bills);
    }

    public Task<IEnumerable<Bill>> GetByPayeeAsync(Guid payeeId)
    {
        var bills = Collection.Find(b => b.PayeeId == payeeId);
        return Task.FromResult(bills);
    }

    public Task<IEnumerable<Bill>> GetByPaymentAccountAsync(Guid paymentAccountId)
    {
        var bills = Collection.Find(b => b.PaymentAccountId == paymentAccountId);
        return Task.FromResult(bills);
    }

    public Task<IEnumerable<Bill>> GetUpcomingAsync(int days)
    {
        var endDate = DateTime.Today.AddDays(days);
        var bills = Collection.Find(b =>
            b.Status == PaymentStatus.Pending &&
            b.DueDate >= DateTime.Today &&
            b.DueDate <= endDate);
        return Task.FromResult(bills);
    }

    public Task<IEnumerable<Bill>> GetOverdueAsync()
    {
        var bills = Collection.Find(b =>
            b.Status == PaymentStatus.Pending &&
            b.DueDate < DateTime.Today);
        return Task.FromResult(bills);
    }

    public Task<IEnumerable<Bill>> GetByDueDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var bills = Collection.Find(b =>
            b.DueDate >= startDate &&
            b.DueDate <= endDate);
        return Task.FromResult(bills);
    }

    public Task<IEnumerable<Bill>> GetByPaidDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var bills = Collection.Find(b =>
            b.PaidDate.HasValue &&
            b.PaidDate.Value >= startDate &&
            b.PaidDate.Value <= endDate);
        return Task.FromResult(bills);
	}
}
