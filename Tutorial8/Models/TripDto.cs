namespace Tutorial8.Models;

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