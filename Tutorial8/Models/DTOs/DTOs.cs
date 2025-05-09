namespace Tutorial8.Models.DTOs;

public class TripDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<CountryDto> Countries { get; set; }
    public string? Description { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int MaxPeople { get; set; }
}

public class CountryDto
{
    public string Name { get; set; }
}

public class ClientDto
{
    public int IdClient { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Telephone { get; set; }
    public string Pesel { get; set; }
}