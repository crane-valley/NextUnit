namespace ClassLibrary.Sample;

/// <summary>
/// Represents an order in an e-commerce system.
/// </summary>
public class Order
{
    public string Id { get; init; } = string.Empty;
    public List<OrderItem> Items { get; init; } = new();
    public decimal DiscountPercentage { get; set; }
    public string? CouponCode { get; set; }

    public decimal Subtotal => Items.Sum(item => item.Price * item.Quantity);
    public decimal DiscountAmount => Subtotal * (DiscountPercentage / 100m);
    public decimal Total => Subtotal - DiscountAmount;
}

/// <summary>
/// Represents an item in an order.
/// </summary>
public class OrderItem
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Quantity { get; init; }
}

/// <summary>
/// Processes orders and applies business rules.
/// </summary>
public class OrderProcessor
{
    private readonly Dictionary<string, decimal> _couponCodes = new()
    {
        ["SAVE10"] = 10m,
        ["SAVE20"] = 20m,
        ["WELCOME"] = 15m
    };

    /// <summary>
    /// Validates an order before processing.
    /// </summary>
    public OrderValidationResult ValidateOrder(Order order)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(order.Id))
        {
            errors.Add("Order ID is required");
        }

        if (order.Items.Count == 0)
        {
            errors.Add("Order must contain at least one item");
        }

        if (order.Items.Any(item => item.Quantity <= 0))
        {
            errors.Add("All items must have positive quantity");
        }

        if (order.Items.Any(item => item.Price < 0))
        {
            errors.Add("All items must have non-negative price");
        }

        return new OrderValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    /// <summary>
    /// Applies a coupon code to an order.
    /// </summary>
    public bool ApplyCoupon(Order order, string couponCode)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
        {
            return false;
        }

        if (_couponCodes.TryGetValue(couponCode.ToUpperInvariant(), out var discount))
        {
            order.CouponCode = couponCode;
            order.DiscountPercentage = discount;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates shipping cost based on order total.
    /// </summary>
    public decimal CalculateShipping(Order order)
    {
        // Free shipping for orders over $50
        if (order.Total >= 50m)
        {
            return 0m;
        }

        // Flat rate shipping for smaller orders
        return 5.99m;
    }
}

/// <summary>
/// Result of order validation.
/// </summary>
public class OrderValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
}
