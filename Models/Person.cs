using System;
using System.ComponentModel.DataAnnotations;

namespace delivery_system_dotnet.Models;

public abstract class Person
{
    protected string dni = null!;
    protected string name = null!;
    protected DateTime dateBirth;
    protected string address = null!;
    protected string mail = null!;
    protected bool gender;
    protected string phone = null!;
    protected string password = null!;

    [Key]
    public string Dni 
    { 
        get => dni; 
        set => dni = value; 
    }

    public string Name 
    { 
        get => name; 
        set => name = value; 
    }

    public DateTime DateBirth 
    { 
        get => dateBirth; 
        set => dateBirth = value; 
    }

    public string Address 
    { 
        get => address; 
        set => address = value; 
    }

    public string Mail 
    { 
        get => mail; 
        set => mail = value; 
    }

    public bool Gender 
    { 
        get => gender; 
        set => gender = value; 
    }

    public string Phone 
    { 
        get => phone; 
        set => phone = value; 
    }

    public string Password
    {
        get => password;
        set => password = value;
    }

    // Foto de perfil (ruta relativa dentro de wwwroot). Nullable/aditivo.
    public string? ProfilePhotoUrl { get; set; }

    public string CalculateAge(DateTime dateBirth)
    {
        int age = DateTime.Today.Year - dateBirth.Year;
        if (dateBirth.Date > DateTime.Today.AddYears(-age)) age--;
        return age.ToString();
    }

    public string GetPassword() => password;
    public void SetPassword(string newPassword) => password = newPassword;
}
