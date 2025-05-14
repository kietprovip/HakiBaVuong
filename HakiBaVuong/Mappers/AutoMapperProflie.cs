using AutoMapper;
using HakiBaVuong.DTOs;
using HakiBaVuong.Models;

namespace sexthu.Mappers
{
    public class AutoMapperProflie : Profile
    {
        public AutoMapperProflie()
        {
            CreateMap<User, UserDTO>().ReverseMap();
            CreateMap<Brand, BrandDTO>().ReverseMap();
            CreateMap<Permission, PermissionDTO>().ReverseMap();
            CreateMap<StaffPermission, StaffPermissionDTO>().ReverseMap();
            CreateMap<Customer, CustomerDTO>().ReverseMap()
                .ForMember(dest => dest.Addresses, opt => opt.MapFrom(src => src.Addresses));
            CreateMap<CustomerAddress, CustomerAddressDTO>().ReverseMap();
            CreateMap<Product, ProductDTO>().ReverseMap();
            CreateMap<Inventory, InventoryDTO>().ReverseMap();

            CreateMap<Order, OrderDTO>()
                .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems))
                .ForMember(dest => dest.Payment, opt => opt.MapFrom(src => src.Payment))
                .ReverseMap();

            CreateMap<OrderItem, OrderItemDTO>().ReverseMap();

            CreateMap<Payment, PaymentDTO>().ReverseMap();

            CreateMap<CreateOrderDTO, Order>()
                .ForMember(dest => dest.FullName, opt => opt.Condition(src => !string.IsNullOrEmpty(src.FullName)))
                .ForMember(dest => dest.Phone, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Phone)))
                .ForMember(dest => dest.Address, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Address)));

            CreateMap<UpdateOrderDTO, Order>()
                .ForMember(dest => dest.Status, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Status)))
                .ForMember(dest => dest.DeliveryStatus, opt => opt.Condition(src => !string.IsNullOrEmpty(src.DeliveryStatus)))
                .ForMember(dest => dest.EstimatedDeliveryDate, opt => opt.MapFrom(src => src.EstimatedDeliveryDate))
                .ForMember(dest => dest.FullName, opt => opt.Condition(src => !string.IsNullOrEmpty(src.FullName)))
                .ForMember(dest => dest.Phone, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Phone)))
                .ForMember(dest => dest.Address, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Address)));

            CreateMap<FilterOrdersDTO, Order>()
                .ForMember(dest => dest.Status, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Status)))
                .ForMember(dest => dest.DeliveryStatus, opt => opt.Condition(src => !string.IsNullOrEmpty(src.DeliveryStatus)))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.StartDate ?? DateTime.MinValue))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.EndDate ?? DateTime.MaxValue));

            CreateMap<RegisterDTO, User>()
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => "staff"))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.Password, opt => opt.Ignore());

            CreateMap<User, LoginDTO>().ReverseMap();

            CreateMap<RegisterCustomerDTO, Customer>()
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.Password, opt => opt.Ignore())
                .ReverseMap();

            CreateMap<Cart, CartDTO>().ReverseMap();
            CreateMap<CartItem, CartItemDTO>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.PriceSell, opt => opt.MapFrom(src => src.Product.PriceSell))
                .ForMember(dest => dest.Image, opt => opt.MapFrom(src => src.Product.Image))
                .ReverseMap();

            CreateMap<PayOrderDTO, Payment>()
                .ForMember(dest => dest.Method, opt => opt.Condition(src => !string.IsNullOrEmpty(src.PaymentMethod)));
        }
    }
}