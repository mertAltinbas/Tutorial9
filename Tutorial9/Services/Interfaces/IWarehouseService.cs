using Tutorial9.Model;

namespace Tutorial9.Services;

public interface IWarehouseService
{
    Task<List<WarehouseDTO>> GetWarehousesAsync();
    Task<int> AddProductToWarehouseAsync(ProductWarehouseDTO warehouse);
    Task<int> AddProductViaStoredProcedureAsync(ProductWarehouseDTO request);
}