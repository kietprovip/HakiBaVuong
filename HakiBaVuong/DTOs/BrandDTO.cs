using HakiBaVuong.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace HakiBaVuong.DTOs
{
    public class BrandDTO
    {
        public string Name { get; set; }
        public int OwnerId { get; set; }
    }
}
