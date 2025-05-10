namespace Tutorial9.Model;

public class OrderDTO
{
    public int IdOrder { get; set; }
    public int Amount {get; set;}
    public DateTime CreatedAt {get; set;}
    public DateTime? FulfilledAt {get; set;}
    
    public ProductDTO IdProductDto { get; set; }
}