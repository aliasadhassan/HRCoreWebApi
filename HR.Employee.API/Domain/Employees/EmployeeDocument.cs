namespace HR.Employee.API.Domain.Employees;

using HR.Employee.API.Domain.Common;

/// <summary>File Blob Storage mein; yahan sirf metadata + private StorageKey (public URL kabhi nahi).</summary>
public sealed class EmployeeDocument : AuditableEntity
{
    private const long MaxSizeBytes = 20 * 1024 * 1024;   // 20 MB

    public Guid EmployeeId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public string Title { get; private set; } = default!;
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = default!;
    public DateOnly? IssueDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }

    private EmployeeDocument() { }

    internal static EmployeeDocument Create(
        Guid tenantId, DocumentType type, string title, string fileName, string contentType,
        long sizeBytes, string storageKey, DateOnly? issueDate, DateOnly? expiryDate)
    {
        if (sizeBytes is <= 0 or > MaxSizeBytes)
            throw new DomainException("Document size must be between 1 byte and 20 MB.");
        if (issueDate is not null && expiryDate is not null && expiryDate < issueDate)
            throw new DomainException("Expiry date cannot be before the issue date.");

        return new EmployeeDocument
        {
            TenantId = tenantId,
            DocumentType = type,
            Title = Guard.Required(title, "Title", 200),
            FileName = Guard.Required(fileName, "File name", 260),
            ContentType = Guard.Required(contentType, "Content type", 100),
            SizeBytes = sizeBytes,
            StorageKey = Guard.Required(storageKey, "Storage key", 500),
            IssueDate = issueDate,
            ExpiryDate = expiryDate
        };
    }
}
