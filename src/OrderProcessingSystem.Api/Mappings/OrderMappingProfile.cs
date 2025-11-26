using System;
using System.Linq;
using AutoMapper;
using OrderProcessingSystem.Models;

namespace OrderProcessingSystem.Api.Mappings;

/// <summary>
/// AutoMapper profile for order-related mappings
/// </summary>
public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<CreateOrderRequest, Order>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid().ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => OrderStatus.Created))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => 
                src.Items != null ? src.Items.Sum(item => item.Quantity * item.UnitPrice) : 0))
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => 
                src.Items != null ? src.Items.Select(item => new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList() : new System.Collections.Generic.List<OrderItem>()));

        CreateMap<Order, OrderCreatedEvent>()
            .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.EventType, opt => opt.MapFrom(src => Constants.EventTypes.OrderCreated));
    }
}

