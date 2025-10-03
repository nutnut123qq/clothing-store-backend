using Microsoft.EntityFrameworkCore;
using ClothingStore.API.Models;

namespace ClothingStore.API.Data
{
    public class ClothingStoreContext : DbContext
    {
        public ClothingStoreContext(DbContextOptions<ClothingStoreContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Seed data
            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "Áo thun nam cao cấp",
                    Description = "Áo thun cotton 100% thoáng mát, phù hợp cho mùa hè",
                    Price = 299000,
                    ImageUrl = "https://via.placeholder.com/300x300?text=Áo+Thun+Nam",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 2,
                    Name = "Quần jean nữ",
                    Description = "Quần jean slim fit chất liệu denim cao cấp",
                    Price = 599000,
                    ImageUrl = "https://via.placeholder.com/300x300?text=Quần+Jean+Nữ",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = 3,
                    Name = "Áo khoác hoodie",
                    Description = "Áo khoác hoodie unisex ấm áp cho mùa đông",
                    Price = 799000,
                    ImageUrl = "https://via.placeholder.com/300x300?text=Hoodie",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );
        }
    }
}
