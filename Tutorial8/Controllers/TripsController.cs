using Tutorial8.Models.DTOs;
using Tutorial8.Services;
using Microsoft.AspNetCore.Mvc;

namespace Tutorial8.Controllers;

[Route("api")]
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
    [HttpGet("trips")]
    public async Task<IActionResult> GetTrips()
    {
        var trips = await _tripsService.GetTrips();
        return Ok(trips);
    }
    
    /// <summary>
    /// Get trips associated with specified client.
    /// </summary>
    [HttpGet("clients/{clientId}/trips")]
    public async Task<IActionResult> GetTripsForClient(int clientId)
    {
        var result = await _tripsService.GetTripsForClient(clientId);
        if (result == null)
            return NotFound($"Client no. {clientId} not found");
        return Ok(result);
    }
    
    /// <summary>
    /// Create a new client record.
    /// </summary>
    [HttpPost("clients")]
    public async Task<IActionResult> CreateClient([FromBody] ClientDto clientDto)
    {
        var check1 = string.IsNullOrWhiteSpace(clientDto.FirstName);
        var check2 = string.IsNullOrWhiteSpace(clientDto.LastName);
        var check3 = string.IsNullOrWhiteSpace(clientDto.Email);
        
        if (check1 || check2 || check3)
        {
            return BadRequest("FirstName, LastName and Email are all required.");
        }
        
        try
        {
            var id = await _tripsService.CreateClient(clientDto);
            return Created($"/api/clients/{id}", new { ClientId = id });
        }
        catch (Exception ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Register specified client for specified trip.
    /// </summary>
    [HttpPut("clients/{clientId}/trips/{tripId}")]
    public async Task<IActionResult>  RegisterClientForTrip(int clientId, int tripId)
    {
        var result = await _tripsService.RegisterClientForTrip(clientId, tripId);
        if (!result.Success)
            return StatusCode(result.StatusCode, result.Message);

        return Created($"/api/clients/{clientId}/trips/{tripId}", new { ClientId = clientId, TripId = tripId });
    }

    /// <summary>
    /// Unregister specified client from specified trip.
    /// </summary>
    [HttpDelete("clients/{clientId}/trips/{tripId}")]
    public async Task<IActionResult>  UnregisterClientFromTrip(int clientId, int tripId)
    {
        var result = await _tripsService.UnregisterClientFromTrip(clientId, tripId);
        if (!result.Success)
            return StatusCode(result.StatusCode, result.Message);

        return NoContent();
    }
}
