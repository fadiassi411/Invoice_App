namespace InvoiceSoftware.Core.Models;

public sealed class ApplicationSettings : BaseEntity
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}

public sealed class DocumentSequence : BaseEntity
{
    public SequenceKind Kind { get; set; }
    public string Prefix { get; set; } = "";
    public int Year { get; set; }
    public long LastNumber { get; set; }
}

public sealed class AuditLog : BaseEntity
{
    public string Action { get; set; } = "";
    public string EntityName { get; set; } = "";
    public int? EntityId { get; set; }
    public string? Details { get; set; }
    public string UserName { get; set; } = "System";
}

public sealed class BackupRecord : BaseEntity
{
    public string FilePath { get; set; } = "";
    public DateTime BackupDate { get; set; } = DateTime.UtcNow;
    public string ApplicationVersion { get; set; } = "1.0.0";
    public bool Verified { get; set; }
}

public sealed class User : BaseEntity
{
    public string UserName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Administrator";
    public bool IsActive { get; set; } = true;
}
