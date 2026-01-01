using BillApp.Core.Interfaces;

namespace BillApp.Core.Models;

/// <summary>
/// Base class for all entities providing common properties.
/// </summary>
public abstract class EntityBase : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
