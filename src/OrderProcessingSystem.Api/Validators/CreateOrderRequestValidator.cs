using System;
using System.Linq;
using FluentValidation;
using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Validators;

/// <summary>
/// Validator for CreateOrderRequest using FluentValidation
/// </summary>
public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty()
            .WithMessage("Customer name is required")
            .MaximumLength(200)
            .WithMessage("Customer name cannot exceed 200 characters");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .WithMessage("Customer email is required")
            .EmailAddress()
            .WithMessage("Customer email must be a valid email address")
            .MaximumLength(255)
            .WithMessage("Customer email cannot exceed 255 characters");

        RuleFor(x => x.ShippingAddress)
            .NotEmpty()
            .WithMessage("Shipping address is required")
            .MaximumLength(500)
            .WithMessage("Shipping address cannot exceed 500 characters");

        RuleFor(x => x.Items)
            .NotNull()
            .WithMessage("Order items are required")
            .Must(items => items != null && items.Any())
            .WithMessage("Order must contain at least one item");

        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemRequestValidator());
    }
}

/// <summary>
/// Validator for OrderItemRequest
/// </summary>
public class OrderItemRequestValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("Product ID is required")
            .MaximumLength(100)
            .WithMessage("Product ID cannot exceed 100 characters");

        RuleFor(x => x.ProductName)
            .NotEmpty()
            .WithMessage("Product name is required")
            .MaximumLength(200)
            .WithMessage("Product name cannot exceed 200 characters");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0")
            .LessThanOrEqualTo(10000)
            .WithMessage("Quantity cannot exceed 10000");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0)
            .WithMessage("Unit price must be greater than 0")
            .LessThanOrEqualTo(1000000)
            .WithMessage("Unit price cannot exceed 1,000,000");
    }
}


