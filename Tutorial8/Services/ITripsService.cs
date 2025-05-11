using Tutorial8.Models;

namespace Tutorial8.Services;

public interface ITripsService
{
    Task<IEnumerable<TripDto>> GetTripsWithDetails();
    Task<IEnumerable<ClientTripDto>?> GetTripsForClient(int clientId);
    Task<int> CreateClient(ClientDto clientDto);
    Task<(bool Success, int StatusCode, string Message)> RegisterClientForTrip(int clientId, int tripId);
    Task<(bool Success, int StatusCode, string Message)> UnregisterClientFromTrip(int clientId, int tripId);
}