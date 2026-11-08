namespace OnlineShopping_MVC.Models.Domain;

/// <summary>
/// Base type for every domain entity.
/// <see cref="CreatedUtc"/>, <see cref="ModifiedUtc"/> and <see cref="IsDeleted"/> are maintained by
/// <c>AuditableEntityInterceptor</c>; removing an entity through EF marks it as deleted instead of deleting the row,
/// and a global query filter hides deleted rows (use <c>IgnoreQueryFilters()</c> to see them).
/// </summary>
public abstract class BaseEntity
{
    // Version 7 GUIDs are time-ordered, so new rows append to the end of the primary-key index.
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTime CreatedUtc { get; set; }
    public DateTime? ModifiedUtc { get; set; }
    public bool IsDeleted { get; set; }
}
