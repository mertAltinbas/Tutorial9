using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Tutorial9.Model;

namespace Tutorial9.Services;

public class WarehouseService : IWarehouseService
{
    private readonly string _connectionString;

    public WarehouseService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<List<WarehouseDTO>> GetWarehousesAsync()
    {
        var warehouses = new List<WarehouseDTO>();

        var query = @"SELECT * FROM Warehouse";

        using var conn = new SqlConnection(_connectionString);
        using var cmd = new SqlCommand(query, conn);
        await conn.OpenAsync();
        var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            int idWarehouse = reader.GetInt32(reader.GetOrdinal("IdWarehouse"));
            string nameWarehouse = reader.GetString(reader.GetOrdinal("Name"));
            string adressWarehouse = reader.GetString(reader.GetOrdinal("Address"));

            var warhouseDTO = new WarehouseDTO
            {
                IdWarehouse = idWarehouse,
                Name = nameWarehouse,
                Address = adressWarehouse
            };
            warehouses.Add(warhouseDTO);
        }

        return warehouses;
    }

    public async Task<int> AddProductToWarehouseAsync(ProductWarehouseDTO request)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        DbTransaction transaction = await connection.BeginTransactionAsync();

        try
        {
            await using SqlCommand command = new SqlCommand();
            command.Connection = connection;
            command.Transaction = (SqlTransaction)transaction;

            // 1. Validate product exists
            command.CommandText = "SELECT IdProduct FROM Product WHERE IdProduct = @IdProduct";
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            if (await command.ExecuteScalarAsync() == null)
                throw new ArgumentException("Product does not exist");

            // 2. Validate warehouse exists
            command.Parameters.Clear();
            command.CommandText = "SELECT IdWarehouse FROM Warehouse WHERE IdWarehouse = @IdWarehouse";
            command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
            if (await command.ExecuteScalarAsync() == null)
                throw new ArgumentException("Warehouse does not exist");

            // 3. Validate amount
            if (request.Amount <= 0)
                throw new ArgumentException("Amount must be greater than 0");

            // 4. Find matching unfulfilled order
            command.Parameters.Clear();
            command.CommandText = @"
                SELECT TOP 1 o.IdOrder 
                FROM [Order] o
                LEFT JOIN Product_Warehouse pw ON o.IdOrder = pw.IdOrder
                WHERE o.IdProduct = @IdProduct 
                AND o.Amount = @Amount
                AND o.CreatedAt < @CreatedAt
                AND pw.IdProductWarehouse IS NULL";

            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

            var orderId = await command.ExecuteScalarAsync() as int?;
            if (!orderId.HasValue)
                throw new ArgumentException("No valid unfulfilled order found for this product");

            // 5. Get product price
            command.Parameters.Clear();
            command.CommandText = "SELECT Price FROM Product WHERE IdProduct = @IdProduct";
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            var price = (decimal)await command.ExecuteScalarAsync();

            // 6. Update order fulfillment
            command.Parameters.Clear();
            command.CommandText = "UPDATE [Order] SET FulfilledAt = GETUTCDATE() WHERE IdOrder = @OrderId";
            command.Parameters.AddWithValue("@OrderId", orderId);
            await command.ExecuteNonQueryAsync();

            // 7. Insert into Product_Warehouse
            command.Parameters.Clear();
            command.CommandText = @"
                INSERT INTO Product_Warehouse 
                (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
                VALUES (@IdWarehouse, @IdProduct, @OrderId, @Amount, @Price, @CreatedAt);
                SELECT SCOPE_IDENTITY();";

            command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            command.Parameters.AddWithValue("@OrderId", orderId);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@Price", price * request.Amount);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            var insertedId = Convert.ToInt32(await command.ExecuteScalarAsync());
            await transaction.CommitAsync();
            return insertedId;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<int> AddProductViaStoredProcedureAsync(ProductWarehouseDTO request)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await using SqlCommand command = new SqlCommand("AddProductToWarehouse", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
        command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
        command.Parameters.AddWithValue("@Amount", request.Amount);
        command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

        await connection.OpenAsync();
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}