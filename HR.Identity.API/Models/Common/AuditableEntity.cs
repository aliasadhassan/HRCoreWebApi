namespace HR.Identity.API.Models.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    /// <summary>Concurrency = Postgres xmin (bytea RowVersion nahi).</summary>
    public uint RowVersion { get; set; }
}
