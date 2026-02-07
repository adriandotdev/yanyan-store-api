using System.ComponentModel.DataAnnotations.Schema;

public enum UserRole
{
    Admin,
    User
}
public class User
{
    public int Id {get; set;}

    public string Username {get; set;} = string.Empty;

    [Column(TypeName = "varchar(20)")]
    public UserRole Role {get; set;}

    public string Password {get; set;} = string.Empty;
}