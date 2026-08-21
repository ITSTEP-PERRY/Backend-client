namespace AuthService.Domain.Entities;

public class EmailVerificationCode
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int Attempts { get; set; }

    public bool Used { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}