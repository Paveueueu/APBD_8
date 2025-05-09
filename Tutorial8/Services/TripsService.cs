using Microsoft.Data.SqlClient;
using Tutorial8.Models.DTOs;

namespace Tutorial8.Services;

public class TripsService : ITripsService
{
    private const string ConnectionString = "Data Source=localhost, 1433; User=sa; Password=yourStrong(!)Password; Integrated Security=False;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False";

    public async Task<List<TripDto>> GetTrips()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        
        // Get all trips
        await using var tripCmd = new SqlCommand("SELECT IdTrip, Name, Description, DateFrom, DateTo, MaxPeople FROM Trip", conn);
        await using var tReader = await tripCmd.ExecuteReaderAsync();
        var result = new Dictionary<int, TripDto>();
        while (await tReader.ReadAsync())
        {
            var trip = new TripDto
            {
                Id = tReader.GetInt32(0),
                Name = tReader.GetString(1),
                Description = tReader.IsDBNull(2) ? null : tReader.GetString(2),
                DateFrom = tReader.GetDateTime(3),
                DateTo = tReader.GetDateTime(4),
                MaxPeople = tReader.GetInt32(5),
                Countries = []
            };
            result[trip.Id] = trip;
        }
        tReader.Close();

        // Get matching countries
        await using var countryCmd = new SqlCommand("SELECT ct.IdTrip, c.Name FROM Country_Trip ct JOIN Country c ON ct.IdCountry = c.IdCountry", conn);
        await using var cReader = await countryCmd.ExecuteReaderAsync();
        while (await cReader.ReadAsync())
        {
            var tripId = cReader.GetInt32(0);
            var countryName = cReader.GetString(1);
            if (result.TryGetValue(tripId, out var trip))
            {
                trip.Countries.Add(new CountryDto { Name = countryName });
            }
        }

        return result.Values.ToList();
    }
    
    public async Task<List<TripDto>?> GetTripsForClient(int clientId)
    {
        const string selectTripsQuery = """
                                            SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, ct.RegisteredAt, ct.PaymentDate
                                            FROM Client_Trip ct
                                            JOIN Trip t ON ct.IdTrip = t.IdTrip
                                            WHERE ct.IdClient = @IdClient
                                            """;

        const string selectCountriesQuery = """
                                      SELECT ct.IdTrip, c.Name
                                      FROM Country_Trip ct
                                      JOIN Country c ON ct.IdCountry = c.IdCountry
                                      WHERE ct.IdTrip IN (
                                            SELECT IdTrip FROM Client_Trip WHERE IdClient = @IdClient
                                      )
                                      """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        // Check if client exists
        await using (var checkCmd = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @IdClient", conn))
        {
            checkCmd.Parameters.AddWithValue("@IdClient", clientId);
            var exists = await checkCmd.ExecuteScalarAsync();
            if (exists == null)
                return null;
        }

        // Get trips for thee client
        var result = new Dictionary<int, TripDto>();
        await using (var tripCmd = new SqlCommand(selectTripsQuery, conn))
        {
            tripCmd.Parameters.AddWithValue("@IdClient", clientId);
            await using var reader = await tripCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var trip = new TripDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    Countries = []
                };
                result[trip.Id] = trip;
            }
        }

        // Get countries for the trips
        await using (var countryCmd = new SqlCommand(selectCountriesQuery, conn))
        {
            countryCmd.Parameters.AddWithValue("@IdClient", clientId);
            await using var countryReader = await countryCmd.ExecuteReaderAsync();
            while (await countryReader.ReadAsync())
            {
                var tripId = countryReader.GetInt32(0);
                var countryName = countryReader.GetString(1);
                if (result.TryGetValue(tripId, out var trip))
                    trip.Countries.Add(new CountryDto { Name = countryName });
            }
        }
        return result.Values.ToList();
    }

    public async Task<int> CreateClient(ClientDto clientDto)
    {
        const string insertClientQuery = """
                                    INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
                                    OUTPUT INSERTED.IdClient
                                    VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)
                                    """;

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        // Check if email already exists
        await using (var checkCmd = new SqlCommand("SELECT 1 FROM Client WHERE Email = @Email", conn))
        {
            checkCmd.Parameters.AddWithValue("@Email", clientDto.Email);
            var exists = await checkCmd.ExecuteScalarAsync();
            if (exists != null)
                throw new Exception("Client with this email already exists.");
        }

        // Add new client
        await using (var insertCmd = new SqlCommand(insertClientQuery, conn))
        {
            insertCmd.Parameters.AddWithValue("@FirstName", clientDto.FirstName);
            insertCmd.Parameters.AddWithValue("@LastName", clientDto.LastName);
            insertCmd.Parameters.AddWithValue("@Email", clientDto.Email);
            insertCmd.Parameters.AddWithValue("@Telephone", (object?)clientDto.Telephone ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@Pesel", (object?)clientDto.Pesel ?? DBNull.Value);

            var newId = (int) (await insertCmd.ExecuteScalarAsync() ?? throw new InvalidOperationException());
            return newId;
        }
    }


    public async Task<(bool Success, int StatusCode, string Message)> RegisterClientForTrip(int clientId, int tripId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        // Check if client exists
        await using (var checkCmd = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @IdClient", conn))
        {
            checkCmd.Parameters.AddWithValue("@IdClient", clientId);
            var exists = await checkCmd.ExecuteScalarAsync();
            if (exists != null)
                return (false, 404, "Client not found");
        }

        // Check if trip exists
        var maxPeople = 0;
        await using (var cmd = new SqlCommand("SELECT MaxPeople FROM Trip WHERE IdTrip = @Id", conn))
        {
            cmd.Parameters.AddWithValue("@Id", tripId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                maxPeople = reader.GetInt32(0);
            }
            else
            {
                return (false, 404, "Trip not found");
            }
        }
            

        // Check if client already registered for this trip
        await using (var cmd = new SqlCommand("SELECT 1 FROM Client_Trip WHERE IdClient = @ClientId AND IdTrip = @TripId", conn))
        {
            cmd.Parameters.AddWithValue("@ClientId", clientId);
            cmd.Parameters.AddWithValue("@TripId", tripId);
            if (await cmd.ExecuteScalarAsync() != null)
                return (false, 409, "Client already registered for this trip");
        }

        // Check if client doesn't exceed max number of participants
        await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @TripId", conn))
        {
            cmd.Parameters.AddWithValue("@TripId", tripId);
            var count = (int) (await cmd.ExecuteScalarAsync() ?? maxPeople);
            
            if (count >= maxPeople) 
            {
                return (false, 409, "No more reservations allowed for this trip");
            }
        }
        
        // Register client for the trip
        var nowInt = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
        await using (var cmd = new SqlCommand("INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt) VALUES (@ClientId, @TripId, @RegisteredAt)", conn))
        {
            cmd.Parameters.AddWithValue("@ClientId", clientId);
            cmd.Parameters.AddWithValue("@TripId", tripId);
            cmd.Parameters.AddWithValue("@RegisteredAt", nowInt);
            await cmd.ExecuteNonQueryAsync();
        }

        return (true, 201, $"Client {clientId} registered for trip {tripId}");
    }


    public async Task<(bool Success, int StatusCode, string Message)> UnregisterClientFromTrip(int clientId, int tripId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        // Check if client registered for this trip
        await using (var checkCmd = new SqlCommand("SELECT 1 FROM Client_Trip WHERE IdClient = @ClientId AND IdTrip = @TripId", conn))
        {
            checkCmd.Parameters.AddWithValue("@ClientId", clientId);
            checkCmd.Parameters.AddWithValue("@TripId", tripId);
            if (await checkCmd.ExecuteScalarAsync() == null)
                return (false, 404, "Registration not found");
        }

        // Unregister client trom the trip
        await using (var deleteCmd = new SqlCommand("DELETE FROM Client_Trip WHERE IdClient = @ClientId AND IdTrip = @TripId", conn))
        {
            deleteCmd.Parameters.AddWithValue("@ClientId", clientId);
            deleteCmd.Parameters.AddWithValue("@TripId", tripId);
            await deleteCmd.ExecuteNonQueryAsync();
        }

        return (true, 204, $"Unregistered client {clientId} from trip {tripId}");
    }
}