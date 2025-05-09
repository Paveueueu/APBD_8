using Tutorial8.Models.DTOs;

namespace Tutorial8.Services;

public interface ITripsService
{
    Task<List<TripDto>> GetTrips();
    Task<List<TripDto>?> GetTripsForClient(int clientId);
    Task<int> CreateClient(ClientDto clientDto);
    Task<(bool Success, int StatusCode, string Message)> RegisterClientForTrip(int clientId, int tripId);
    Task<(bool Success, int StatusCode, string Message)> UnregisterClientFromTrip(int clientId, int tripId);
}