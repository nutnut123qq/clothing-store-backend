using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClothingStore.API.Data;
using ClothingStore.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace ClothingStore.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ClothingStoreContext _context;

        public ProductsController(ClothingStoreContext context)
        {
            _context = context;
        }

        // GET: api/products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts([FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                Console.WriteLine($"[GetProducts] Starting request - page: {page}, pageSize: {pageSize}, search: {search}");
                
                var query = _context.Products.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(p => p.Name.Contains(search) || p.Description.Contains(search));
                }

                Console.WriteLine("[GetProducts] Executing CountAsync...");
                var totalCount = await query.CountAsync();
                Console.WriteLine($"[GetProducts] Total count: {totalCount}");

                Console.WriteLine("[GetProducts] Fetching products...");
                
                // Simplest possible query - just fetch all without ordering
                Console.WriteLine("[GetProducts] Attempting raw query without ordering...");
                var productEntities = await query
                    .AsNoTracking()
                    .Take(pageSize)
                    .ToListAsync();

                Console.WriteLine($"[GetProducts] Found {productEntities.Count} product entities");

                // Map to DTOs in memory
                var products = productEntities.Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    ImageUrl = p.ImageUrl,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                }).ToList();

                Console.WriteLine($"[GetProducts] Mapped to {products.Count} DTOs");

                Response.Headers["X-Total-Count"] = totalCount.ToString();
                Response.Headers["X-Page"] = page.ToString();
                Response.Headers["X-Page-Size"] = pageSize.ToString();

                Console.WriteLine("[GetProducts] Returning response");
                return Ok(products);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetProducts ERROR] {ex.Message}");
                Console.WriteLine($"[GetProducts ERROR] Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: api/products/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDto>> GetProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            var productDto = new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };

            return Ok(productDto);
        }

    // POST: api/products
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ProductDto>> PostProduct(CreateProductDto createProductDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var product = new Product
            {
                Name = createProductDto.Name,
                Description = createProductDto.Description,
                Price = createProductDto.Price,
                ImageUrl = createProductDto.ImageUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            var productDto = new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, productDto);
        }

    // PUT: api/products/5
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> PutProduct(int id, UpdateProductDto updateProductDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            product.Name = updateProductDto.Name;
            product.Description = updateProductDto.Description;
            product.Price = updateProductDto.Price;
            product.ImageUrl = updateProductDto.ImageUrl;
            product.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

    // DELETE: api/products/5
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}
