namespace AuthService.Application.DTOs.Account;

public sealed class ChangeNameRequest { public string FirstName { get; set; } = string.Empty; public string LastName { get; set; } = string.Empty; }
public sealed class EmailChangeStartRequest { public string NewEmail { get; set; } = string.Empty; public string CurrentPassword { get; set; } = string.Empty; }
public sealed class EmailChangeVerifyRequest { public string Code { get; set; } = string.Empty; }
public sealed class ChangePasswordRequest { public string CurrentPassword { get; set; } = string.Empty; public string NewPassword { get; set; } = string.Empty; public string ConfirmPassword { get; set; } = string.Empty; }
public sealed class DeleteAccountRequest { public string CurrentPassword { get; set; } = string.Empty; public string Confirmation { get; set; } = string.Empty; }
public sealed class EmailChangeResponse { public int CodeExpiresInSeconds { get; set; } public int ResendAvailableInSeconds { get; set; } }
