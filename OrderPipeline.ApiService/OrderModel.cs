namespace OrderPipeline.ApiService;

public record OrderRequest(string CustomerId, decimal Amount, List<string> Items);
public record OrderEvent(string OrderId, string CustomerId, decimal Amount, DateTime CreatedAt);

