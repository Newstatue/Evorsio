namespace Evorsio.AuthService.Models;

public class User
{ 
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Locale { get; set; } = "en-US";
}