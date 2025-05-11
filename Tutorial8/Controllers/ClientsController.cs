using Tutorial8.Models;
using Tutorial8.Services;
using Microsoft.AspNetCore.Mvc;

namespace Tutorial8.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ClientsController : ControllerBase
{
    private readonly ITripsService _tripsService;

    public ClientsController(ITripsService tripsService)
    {
        _tripsService = tripsService;
    }
    
    /// <summary>
    /// Get trips associated with specified client.
    /// </summary>
    [HttpGet("{clientId}/trips")]
    public async Task<IActionResult> GetTripsForClient(int clientId)
    {
        try
        {
            var result = await _tripsService.GetTripsForClient(clientId);
            if (result == null)
                return NotFound($"Client with id {clientId} not found");
            return Ok(result);
        }
        catch
        {
            return StatusCode(500);
        }
    }
    
    /// <summary>
    /// Create a new client record.
    /// </summary>
    [HttpPost]
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
            var result = await _tripsService.CreateClient(clientDto);
            if (!result.Success)
                return StatusCode(result.StatusCode, result.Message);
                
            return Created($"/api/clients/{result.Id}", new { ClientId = result.Id });
        }
        catch
        {
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Register specified client for specified trip.
    /// </summary>
    [HttpPut("{clientId}/trips/{tripId}")]
    public async Task<IActionResult>  RegisterClientForTrip(int clientId, int tripId)
    {
        try
        {
            var result = await _tripsService.RegisterClientForTrip(clientId, tripId);
            if (!result.Success)
                return StatusCode(result.StatusCode, result.Message);

            return Created($"/api/clients/{clientId}/trips/{tripId}", new { ClientId = clientId, TripId = tripId });
        }
        catch
        {
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Unregister specified client from specified trip.
    /// </summary>
    [HttpDelete("{clientId}/trips/{tripId}")]
    public async Task<IActionResult>  UnregisterClientFromTrip(int clientId, int tripId)
    {
        try
        {
            var result = await _tripsService.UnregisterClientFromTrip(clientId, tripId);
            if (!result.Success)
                return StatusCode(result.StatusCode, result.Message);

            return NoContent();
        }
        catch
        {
            return StatusCode(500);
        }
    }
}