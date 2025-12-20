using NextUnit;

namespace ClassLibrary.Sample.Tests;

/// <summary>
/// Tests for the OrderProcessor class, demonstrating business logic testing.
/// </summary>
public class OrderProcessorTests
{
    private readonly OrderProcessor _processor = new();

    [Test]
    public void ValidateOrder_ValidOrder_ReturnsValid()
    {
        // Arrange
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "P1", ProductName = "Product 1", Price = 10.00m, Quantity = 2 }
            }
        };

        // Act
        var result = _processor.ValidateOrder(order);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Test]
    public void ValidateOrder_EmptyOrderId_ReturnsInvalid()
    {
        var order = new Order
        {
            Id = "",
            Items = new List<OrderItem>
            {
                new() { ProductId = "P1", ProductName = "Product 1", Price = 10.00m, Quantity = 1 }
            }
        };

        var result = _processor.ValidateOrder(order);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Order ID is required"));
    }

    [Test]
    public void ValidateOrder_NoItems_ReturnsInvalid()
    {
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>()
        };

        var result = _processor.ValidateOrder(order);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("at least one item"));
    }

    [Test]
    public void ValidateOrder_NegativeQuantity_ReturnsInvalid()
    {
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "P1", ProductName = "Product 1", Price = 10.00m, Quantity = -1 }
            }
        };

        var result = _processor.ValidateOrder(order);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("positive quantity"));
    }

    [Test]
    public void ApplyCoupon_ValidCoupon_AppliesDiscount()
    {
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "P1", ProductName = "Product 1", Price = 100.00m, Quantity = 1 }
            }
        };

        bool result = _processor.ApplyCoupon(order, "SAVE10");

        Assert.True(result);
        Assert.Equal("SAVE10", order.CouponCode);
        Assert.Equal(10m, order.DiscountPercentage);
    }

    [Test]
    public void ApplyCoupon_InvalidCoupon_DoesNotApplyDiscount()
    {
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "P1", ProductName = "Product 1", Price = 100.00m, Quantity = 1 }
            }
        };

        bool result = _processor.ApplyCoupon(order, "INVALID");

        Assert.False(result);
        Assert.Null(order.CouponCode);
        Assert.Equal(0m, order.DiscountPercentage);
    }

    [Test]
    public void CalculateShipping_OrderOver50_ReturnsFreeShipping()
    {
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "P1", ProductName = "Product 1", Price = 60.00m, Quantity = 1 }
            }
        };

        decimal shipping = _processor.CalculateShipping(order);

        Assert.Equal(0m, shipping);
    }

    [Test]
    public void CalculateShipping_OrderUnder50_ReturnsFlatRate()
    {
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "P1", ProductName = "Product 1", Price = 30.00m, Quantity = 1 }
            }
        };

        decimal shipping = _processor.CalculateShipping(order);

        Assert.Equal(5.99m, shipping);
    }

    [Test]
    public void Order_CalculatesSubtotalCorrectly()
    {
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "P1", ProductName = "Product 1", Price = 10.00m, Quantity = 2 },
                new() { ProductId = "P2", ProductName = "Product 2", Price = 15.00m, Quantity = 1 }
            }
        };

        Assert.Equal(35.00m, order.Subtotal);
    }

    [Test]
    public void Order_CalculatesDiscountAmountCorrectly()
    {
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "P1", ProductName = "Product 1", Price = 100.00m, Quantity = 1 }
            },
            DiscountPercentage = 20m
        };

        Assert.Equal(20.00m, order.DiscountAmount);
        Assert.Equal(80.00m, order.Total);
    }

    /// <summary>
    /// Demonstrates parameterized tests for multiple coupon codes.
    /// </summary>
    [Test]
    [TestData(nameof(CouponTestCases))]
    public void ApplyCoupon_VariousCoupons_AppliesCorrectDiscount(string couponCode, decimal expectedDiscount)
    {
        var order = new Order
        {
            Id = "ORD-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "P1", ProductName = "Product 1", Price = 100.00m, Quantity = 1 }
            }
        };

        _processor.ApplyCoupon(order, couponCode);

        Assert.Equal(expectedDiscount, order.DiscountPercentage);
    }

    // Test data provider
    public static IEnumerable<object[]> CouponTestCases()
    {
        yield return new object[] { "SAVE10", 10m };
        yield return new object[] { "SAVE20", 20m };
        yield return new object[] { "WELCOME", 15m };
    }
}
