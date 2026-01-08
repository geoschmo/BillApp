using BillApp.Core.Enums;
using BillApp.Core.Models;

namespace BillApp.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for Bill entities with specialized queries.
/// </summary>
public interface IBillRepository : IRepository<Bill>
{
    Task<IEnumerable<Bill>> GetByStatusAsync(PaymentStatus status);
    Task<IEnumerable<Bill>> GetByPayeeAsync(Guid payeeId);
    Task<IEnumerable<Bill>> GetByPaymentAccountAsync(Guid paymentAccountId);
    Task<IEnumerable<Bill>> GetUpcomingAsync(int days);
    Task<IEnumerable<Bill>> GetOverdueAsync();
    Task<IEnumerable<Bill>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
}
