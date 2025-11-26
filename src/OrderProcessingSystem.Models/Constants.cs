namespace OrderProcessingSystem.Models;

/// <summary>
/// Constants used throughout the application
/// </summary>
public static class Constants
{
    /// <summary>
    /// Event type constants
    /// </summary>
    public static class EventTypes
    {
        public const string OrderCreated = "Order.Created";
        public const string OrderProcessed = "Order.Processed";
        public const string OrderShipped = "Order.Shipped";
        public const string OrderDelivered = "Order.Delivered";
        public const string OrderCancelled = "Order.Cancelled";
    }

    /// <summary>
    /// Default values
    /// </summary>
    public static class Defaults
    {
        public const string DatabaseName = "OrderProcessingDB";
        public const string ContainerName = "Orders";
    }

    /// <summary>
    /// Validation messages
    /// </summary>
    public static class ValidationMessages
    {
        public const string OrderIdRequired = "Order ID cannot be null or empty";
        public const string OrderIdInvalidFormat = "Order ID must be a valid GUID";
        public const string OrderRequestRequired = "Order request cannot be null";
        public const string OrderRequired = "Order cannot be null";
        public const string ItemsRequired = "Order must contain at least one item";
        public const string QuantityInvalid = "Item {0} must have a quantity greater than 0";
        public const string UnitPriceInvalid = "Item {0} must have a unit price greater than 0";
    }
}


