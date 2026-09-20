namespace AuthService.Domain.Entities;

public sealed class EmailChangeRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string NewEmail { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public bool Used { get; set; }
    public DateTime CreatedAt { get; set; }
    public User User { get; set; } = null!;
}
