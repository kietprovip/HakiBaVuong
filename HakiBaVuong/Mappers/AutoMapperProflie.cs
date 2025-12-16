using AutoMapper;
using HakiBaVuong.DTOs;
using HakiBaVuong.Models;

namespace sexthu.Mappers
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<User, UserDTO>().ReverseMap();
            CreateMap<Brand, BrandDTO>().ReverseMap();

            CreateMap<Customer, CustomerDTO>()
                .ForMember(dest => dest.Addresses, opt => opt.MapFrom(src => src.Addresses))
                .ReverseMap();

            CreateMap<CustomerAddress, CustomerAddressDTO>().ReverseMap();
            CreateMap<Product, ProductDTO>().ReverseMap();
            CreateMap<Inventory, InventoryDTO>().ReverseMap();

            CreateMap<Order, OrderDTO>()
                .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems))
                .ForMember(dest => dest.Payment, opt => opt.MapFrom(src => src.Payment))
                .ForMember(dest => dest.ShipperName, opt => opt.MapFrom(src => src.Shipper != null ? src.Shipper.Name : null))
                .ForMember(dest => dest.ShipperPhone, opt => opt.MapFrom(src => src.Shipper != null ? src.Shipper.Phone : null))
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
                .ForMember(dest => dest.DeliveryStatus, opt => opt.Condition(src => !string.IsNullOrEmpty(src.DeliveryStatus)));

            CreateMap<RegisterDTO, User>()
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => "Staff"))
                .ForMember(dest => dest.Password, opt => opt.Ignore());

            CreateMap<User, LoginDTO>().ReverseMap();

            CreateMap<RegisterCustomerDTO, Customer>()
                .ForMember(dest => dest.Password, opt => opt.Ignore())
                .ReverseMap();

            CreateMap<Cart, CartDTO>().ReverseMap();

            CreateMap<CartItem, CartItemDTO>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.PriceSell, opt => opt.MapFrom(src => src.Product.PriceSell))
                .ReverseMap();

            CreateMap<Comment, CommentDTO>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer.Name))
                .ReverseMap();

            CreateMap<PayOrderDTO, Payment>()
                .ForMember(dest => dest.Method, opt => opt.Condition(src => !string.IsNullOrEmpty(src.PaymentMethod)));

            CreateMap<Category, CategoryDTO>().ReverseMap();
            CreateMap<Revenue, RevenueDTO>().ReverseMap();
            CreateMap<RevenueHistory, RevenueHistoryDTO>().ReverseMap();

            CreateMap<Shipper, ShipperDTO>()
                .ForMember(dest => dest.Rating, opt => opt.MapFrom(src => src.Rating ?? 0))
                .ReverseMap();

            CreateMap<ShipperRegisterDTO, Shipper>()
                .ForMember(dest => dest.Password, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow));

            CreateMap<ShipperUpdateDTO, Shipper>()
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<ShipperRating, ShipperRatingDTO>().ReverseMap();

            CreateMap<Order, OrderDetailForShipperDTO>()
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.FullName))
                .ForMember(dest => dest.CustomerPhone, opt => opt.MapFrom(src => src.Phone))
                .ForMember(dest => dest.CustomerAddress, opt => opt.MapFrom(src => src.Address))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.OrderItems));

            CreateMap<OrderItem, OrderItemForShipperDTO>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.ProductName ?? (src.Product != null ? src.Product.Name : "Sản phẩm")))
                .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Price));
        }
    
    }
}