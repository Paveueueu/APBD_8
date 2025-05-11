using Microsoft.Data.SqlClient;
using Tutorial8.Models;

namespace Tutorial8.Services;

public class TripsService : ITripsService
{
    private readonly string _connectionString;

    public TripsService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default") ??
                            throw new InvalidOperationException("Connection string configuration missing.");
    }


    public async Task<IEnumerable<TripDto>> GetTripsWithDetails()
    {
        const string sql = """
                           SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, c.IdCountry, c.Name AS CountryName
                           FROM Trip t
                           LEFT JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
                           LEFT JOIN Country c ON ct.IdCountry = c.IdCountry
                           """;

        await using var conn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(sql, conn);
        await conn.OpenAsync();

        // get all trips
        var result = new Dictionary<int, TripDto>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tripId = reader.GetInt32(0);
            if (!result.TryGetValue(tripId, out var trip))
            {
                trip = new TripDto
                {
                    Id = tripId,
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    Countries = []
                };
                result[tripId] = trip;
            }

            // add matching country names
            trip.Countries.Add(new CountryDto { Name = reader.GetString(7) });
        }

        return result.Values;
    }

    public async Task<IEnumerable<ClientTripDto>?> GetTripsForClient(int clientId)
    {
        // check if exists
        
        const string sql = """
                           SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, ct.RegisteredAt, ct.PaymentDate
                           FROM Client_Trip ct
                           INNER JOIN Trip t ON ct.IdTrip = t.IdTrip
                           WHERE ct.IdClient = @clientId
                           """;
        
        await using var conn = new SqlConnection(_connectionString);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@clientId", clientId);
        await conn.OpenAsync();

        // get all trips for the client
        var result = new List<ClientTripDto>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ClientTripDto
            {
                IdTrip = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                DateFrom = reader.GetDateTime(3),
                DateTo = reader.GetDateTime(4),
                MaxPeople = reader.GetInt32(5),
                RegisteredAt = reader.GetInt32(6),
                PaymentDate = reader.IsDBNull(7) ? null : reader.GetInt32(7)
            });
        }

        return result;
    }

    public async Task<int> CreateClient(ClientDto clientDto)
    {
        const string insertClientQuery = """
                                         INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
                                         OUTPUT INSERTED.IdClient
                                         VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)
                                         """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if email already exists
        await using (var checkCmd = new SqlCommand("SELECT 1 FROM Client WHERE Email = @Email", conn))
        {
            checkCmd.Parameters.AddWithValue("@Email", clientDto.Email);
            var exists = await checkCmd.ExecuteScalarAsync();
            if (exists != null)
                throw new Exception("Client with this email already exists.");
        }
        
        // Check if pesel already exists
        await using (var checkCmd = new SqlCommand("SELECT 1 FROM Client WHERE Pesel = @Pesel", conn))
        {
            checkCmd.Parameters.AddWithValue("@Pesel", clientDto.Email);
            var exists = await checkCmd.ExecuteScalarAsync();
            if (exists != null)
                throw new Exception("Client with this pesel already exists.");
        }

        // Validate email
        if (!clientDto.Email.Contains('@'))
        {
            throw new Exception("Invalid email format.");
        }

        // Validate pesel
        if (string.IsNullOrEmpty(clientDto.Pesel) || clientDto.Pesel.Length != 11 || !clientDto.Pesel.All(char.IsDigit))
        {
            throw new Exception("Invalid PESEL format.");
        }

        // Add new client
        await using (var insertCmd = new SqlCommand(insertClientQuery, conn))
        {
            insertCmd.Parameters.AddWithValue("@FirstName", clientDto.FirstName);
            insertCmd.Parameters.AddWithValue("@LastName", clientDto.LastName);
            insertCmd.Parameters.AddWithValue("@Email", clientDto.Email);
            insertCmd.Parameters.AddWithValue("@Telephone", (object?)clientDto.Telephone ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@Pesel", (object?)clientDto.Pesel ?? DBNull.Value);

            var newId = (int)(await insertCmd.ExecuteScalarAsync() ?? throw new InvalidOperationException());
            return newId;
        }
    }


    public async Task<(bool Success, int StatusCode, string Message)> RegisterClientForTrip(int clientId, int tripId)
    {
        await using var conn = new SqlConnection(_connectionString);
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
        await using (var cmd = new SqlCommand(
                         "SELECT 1 FROM Client_Trip WHERE IdClient = @ClientId AND IdTrip = @TripId", conn))
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
            var count = (int)(await cmd.ExecuteScalarAsync() ?? maxPeople);

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
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Check if client already registered for this trip
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