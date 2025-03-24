using AutoMapper;
using HakiBaVuong.DTOs;
using HakiBaVuong.Models;

namespace sexthu.Mappers
{
    public class AutoMapperProflie : Profile
    {
        public AutoMapperProflie() {
            CreateMap<User, UserDTO>().ReverseMap();
            CreateMap<Brand, BrandDTO>().ReverseMap();
            CreateMap<Permission, PermissionDTO>().ReverseMap();
            CreateMap<StaffPermission, StaffPermissionDTO>().ReverseMap();
            CreateMap<Customer, CustomerDTO>().ReverseMap();
            CreateMap<CustomerAddress, CustomerAddressDTO>().ReverseMap();
            CreateMap<Product, ProductDTO>().ReverseMap();
            CreateMap<Inventory, InventoryDTO>().ReverseMap();
            CreateMap<Order, OrderDTO>().ReverseMap();
            CreateMap<OrderItem, OrderItemDTO>().ReverseMap();
            CreateMap<RegisterDTO, User>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => "staff"))
            .ForMember(dest => dest.Password, opt => opt.Ignore());

            CreateMap<User, LoginDTO>();
            CreateMap<RegisterCustomerDTO, Customer>()
            .ForMember(dest => dest.Password, opt => opt.Ignore()).ReverseMap();
            CreateMap<User, LoginDTO>().ReverseMap();

        }
    }
}
