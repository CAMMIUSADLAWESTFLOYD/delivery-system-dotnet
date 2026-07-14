using System;
using System.Text.RegularExpressions;

namespace delivery_system_dotnet.Models;

public class Admin : Person
{
    public string Succursal { get; set; } = null!;
    public string Rol { get; set; } = null!;
    public double Salary { get; set; }

    public Admin()
    {
    }

    // Constructor de copia
    public Admin(Admin oldAdmin)
    {
        this.dni = oldAdmin.Dni;
        this.name = oldAdmin.Name;
        this.dateBirth = oldAdmin.DateBirth;
        this.address = oldAdmin.Address;
        this.mail = oldAdmin.Mail;
        this.gender = oldAdmin.Gender;
        this.phone = oldAdmin.Phone;
        this.password = oldAdmin.Password;
        this.Succursal = oldAdmin.Succursal;
        this.Rol = oldAdmin.Rol;
        this.Salary = oldAdmin.Salary;
    }

    public string ViewListClient() => "Client list viewed by admin.";
    public string ViewListDelivery() => "Delivery list viewed by admin.";
    public string AddPersons() => "Person added successfully.";
    public void AprobeVehicle() { }
    public bool VerifyData() => true;
    public bool VerifyUsers() => true;
    public bool VerifyMail() => VerificateMail(this.Mail);

    public string GenerateCode(string mail)
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    public bool VerificateMail(string mail)
    {
        if (string.IsNullOrEmpty(mail)) return false;
        return Regex.IsMatch(mail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }

    public bool ComparePassword(string password)
    {
        if (string.IsNullOrEmpty(this.Password)) return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, this.Password);
        }
        catch
        {
            return this.Password == password;
        }
    }
}
