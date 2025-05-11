using System.ComponentModel.DataAnnotations;

namespace Tutorial8.Models;

public class ClientDto
{
    [Required]
    [StringLength(50)]
    public string FirstName { get; set; }
    
    [Required]
    [StringLength(50)]
    public string LastName { get; set; }
    
    [EmailAddress]
    public string Email { get; set; }
    
    [Phone]
    public string Telephone { get; set; }
    
    [Required]
    [RegularExpression(@"^\d{11}$")]
    public string Pesel { get; set; }
}