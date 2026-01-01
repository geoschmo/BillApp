namespace BillApp.Core.Interfaces;

/// <summary>
/// Base interface for all entities with common audit properties.
/// </summary>
public interface IEntity
{
    Guid Id { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
