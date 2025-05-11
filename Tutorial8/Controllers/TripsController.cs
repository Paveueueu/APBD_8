using Tutorial8.Models;
using Tutorial8.Services;
using Microsoft.AspNetCore.Mvc;

namespace Tutorial8.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TripsController : ControllerBase
{
    private readonly ITripsService _tripsService;

    public TripsController(ITripsService tripsService)
    {
        _tripsService = tripsService;
    }
    
    /// <summary>
    /// Get all the trips with their information.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTripsWithDetails()
    {
        var trips = await _tripsService.GetTripsWithDetails();
        return Ok(trips);
    }
}
