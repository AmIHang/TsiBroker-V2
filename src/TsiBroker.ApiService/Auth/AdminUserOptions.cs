namespace TsiBroker.ApiService.Auth;

public class AdminUserOptions
{
    public const string SectionName = "AdminUser";

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
