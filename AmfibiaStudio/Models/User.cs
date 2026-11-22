using System;

namespace AmfibiaStudio.Models;

/// <summary>Пользователь магазина (как класс User в методичке, но с данными сайта).</summary>
public class User
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = "buyer";
    public string RoleName { get; set; } = "Покупатель";
    public string Phone { get; set; } = string.Empty;
    public string Country { get; set; } = "BY";
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string House { get; set; } = string.Empty;
    public string Apartment { get; set; } = string.Empty;
    public string Postal { get; set; } = string.Empty;
    public string? Shop { get; set; }
    public bool Blocked { get; set; }
    public string Password { get; set; } = string.Empty;
    public DateTime Registered { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public string Initials =>
        (FirstName.Length > 0 ? FirstName[..1] : "") + (LastName.Length > 0 ? LastName[..1] : "");

    public User Clone() => (User)MemberwiseClone();
}
