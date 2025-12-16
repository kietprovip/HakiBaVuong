using Microsoft.EntityFrameworkCore;
using HakiBaVuong.Models;

namespace HakiBaVuong.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerAddress> CustomerAddresses { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<ViewedProduct> ViewedProducts { get; set; }
        public DbSet<Revenue> Revenues { get; set; }
        public DbSet<RevenueHistory> RevenueHistories { get; set; }
        public DbSet<Wishlist> Wishlists { get; set; }
        public DbSet<Shipper> Shippers { get; set; }
        public DbSet<ShipperRating> ShipperRatings { get; set; }
        public DbSet<ShipperLocation> ShipperLocations { get; set; } = null!;
        public DbSet<ShortVideo> ShortVideos { get; set; } = null!;
        public DbSet<VideoProduct> VideoProducts { get; set; } = null!;
        public DbSet<VideoLike> VideoLikes { get; set; } = null!;
        public DbSet<VideoComment> VideoComments { get; set; } = null!;
        public DbSet<BrandFollow> BrandFollows { get; set; } = null!;
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>()
                .Property(p => p.PriceSell)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Product>()
                .Property(p => p.PriceCost)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.Price)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Revenue>()
                .Property(r => r.DailyRevenue)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Revenue>()
                .Property(r => r.MonthlyRevenue)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Revenue>()
                .Property(r => r.YearlyRevenue)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<RevenueHistory>()
                .Property(rh => rh.DailyRevenue)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<RevenueHistory>()
                .Property(rh => rh.MonthlyRevenue)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<RevenueHistory>()
                .Property(rh => rh.YearlyRevenue)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Brand>()
                .HasOne(b => b.Owner)
                .WithMany()
                .HasForeignKey(b => b.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerAddress>()
                .HasOne(ca => ca.Customer)
                .WithMany()
                .HasForeignKey(ca => ca.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Brand)
                .WithMany()
                .HasForeignKey(p => p.BrandId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Inventory>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany()
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithOne(o => o.Payment)
                .HasForeignKey<Payment>(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Product)
                .WithMany()
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Customer)
                .WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductCategory>()
                .HasKey(pc => new { pc.ProductId, pc.CategoryId });

            modelBuilder.Entity<ProductCategory>()
                .HasOne(pc => pc.Product)
                .WithMany(p => p.ProductCategories)
                .HasForeignKey(pc => pc.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductCategory>()
                .HasOne(pc => pc.Category)
                .WithMany(c => c.ProductCategories)
                .HasForeignKey(pc => pc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ViewedProduct>()
                .HasKey(vp => new { vp.CustomerId, vp.ProductId });

            modelBuilder.Entity<ViewedProduct>()
                .HasOne(vp => vp.Customer)
                .WithMany(c => c.ViewedProducts)
                .HasForeignKey(vp => vp.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ViewedProduct>()
                .HasOne(vp => vp.Product)
                .WithMany()
                .HasForeignKey(vp => vp.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Wishlist>()
                .HasKey(w => new { w.CustomerId, w.ProductId });

            modelBuilder.Entity<Wishlist>()
                .HasOne(w => w.Customer)
                .WithMany(c => c.Wishlist)
                .HasForeignKey(w => w.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Wishlist>()
                .HasOne(w => w.Product)
                .WithMany()
                .HasForeignKey(w => w.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Revenue>()
                .HasOne(r => r.Order)
                .WithMany()
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RevenueHistory>()
                .HasOne(rh => rh.Brand)
                .WithMany()
                .HasForeignKey(rh => rh.BrandId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<VideoProduct>()
                .HasOne(vp => vp.Video)
                .WithMany(v => v.VideoProducts)
                .HasForeignKey(vp => vp.VideoId)
                .OnDelete(DeleteBehavior.Cascade); 

            modelBuilder.Entity<VideoProduct>()
                .HasOne(vp => vp.Product)
                .WithMany() 
                .HasForeignKey(vp => vp.ProductId)
                .OnDelete(DeleteBehavior.NoAction); 


            modelBuilder.Entity<VideoLike>()
                .HasKey(vl => new { vl.VideoId, vl.CustomerId });

            modelBuilder.Entity<BrandFollow>()
                .HasKey(bf => new { bf.BrandId, bf.CustomerId });

            base.OnModelCreating(modelBuilder);
        }
    }
}