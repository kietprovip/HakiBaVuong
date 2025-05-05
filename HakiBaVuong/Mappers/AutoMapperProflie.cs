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
            CreateMap<Order, OrderDTO>().ReverseMap();
            CreateMap<OrderItem, OrderItemDTO>().ReverseMap();
            CreateMap<RegisterDTO, User>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => "staff"))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.Password, opt => opt.Ignore());

            CreateMap<User, LoginDTO>();
            CreateMap<RegisterCustomerDTO, Customer>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.Password, opt => opt.Ignore()).ReverseMap();
            CreateMap<User, LoginDTO>().ReverseMap();
            CreateMap<Cart, CartDTO>();
            CreateMap<CartItem, CartItemDTO>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.PriceSell, opt => opt.MapFrom(src => src.Product.PriceSell))
                .ForMember(dest => dest.Image, opt => opt.MapFrom(src => src.Product.Image));
            CreateMap<Payment, PaymentDTO>();
            CreateMap<PayOrderDTO, Payment>()
                .ForMember(dest => dest.Method, opt => opt.Condition(src => !string.IsNullOrEmpty(src.PaymentMethod)));
            CreateMap<UpdateOrderDTO, Order>()
                .ForMember(dest => dest.Status, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Status)))
                .ForMember(dest => dest.FullName, opt => opt.Condition(src => !string.IsNullOrEmpty(src.FullName)))
                .ForMember(dest => dest.Phone, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Phone)))
                .ForMember(dest => dest.Address, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Address)));
            CreateMap<FilterOrdersDTO, Order>()
                .ForMember(dest => dest.Status, opt => opt.Condition(src => !string.IsNullOrEmpty(src.Status)));
        }
    }
}
