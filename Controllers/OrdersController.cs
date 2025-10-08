using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ClothingStore.API.Data;
using ClothingStore.API.Models;

namespace ClothingStore.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly ClothingStoreContext _context;

        public OrdersController(ClothingStoreContext context)
        {
            _context = context;
        }

        // POST: api/orders
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Order>> CreateOrder(CreateOrderDto dto)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.Sid);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                // try sub
                var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
                if (string.IsNullOrEmpty(sub) || !int.TryParse(sub, out userId))
                {
                    return Unauthorized();
                }
            }

            if (dto.Items == null || !dto.Items.Any())
            {
                return BadRequest("No items in order");
            }

            var order = new Order { UserId = userId, Status = "pending" };

            decimal total = 0m;

            foreach (var it in dto.Items)
            {
                var product = await _context.Products.FindAsync(it.ProductId);
                if (product == null)
                {
                    return BadRequest($"Product {it.ProductId} not found");
                }

                var item = new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = it.Quantity
                };

                order.Items.Add(item);
                total += product.Price * it.Quantity;
            }

            order.TotalAmount = total;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
        }

        // GET: api/orders
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersForUser()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null) return Unauthorized();

            var orders = await _context.Orders
                .Where(o => o.UserId == userId.Value)
                .Include(o => o.Items)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orders);
        }

        // GET: api/orders/5
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<Order>> GetOrderById(int id)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null) return Unauthorized();

            var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId.Value);
            if (order == null) return NotFound();

            return Ok(order);
        }

        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid") ?? User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userIdClaim)) return null;
            if (int.TryParse(userIdClaim, out var uid)) return uid;
            return null;
        }
    }

    public class CreateOrderDto
    {
        public List<CreateOrderItemDto> Items { get; set; } = new List<CreateOrderItemDto>();
    }

    public class CreateOrderItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
