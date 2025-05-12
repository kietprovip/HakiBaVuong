using HakiBaVuong.Data;
using HakiBaVuong.DTOs;
using HakiBaVuong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HakiBaVuong.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CustomerAddressController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly ILogger<CustomerAddressController> _logger;

        public CustomerAddressController(DataContext context, ILogger<CustomerAddressController> logger)
        {
            _context = context;
            _logger = logger;
        }

 
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerAddressDTO>>> GetAddresses()
        {
            _logger.LogInformation("GetAddresses called");

            var customerId = GetCustomerId();
            if (!customerId.HasValue)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var addresses = await _context.CustomerAddresses
                .Where(a => a.CustomerId == customerId.Value)
                .Select(a => new CustomerAddressDTO
                {
                    CustomerId = a.CustomerId,
                    FullName = a.FullName,
                    Phone = a.Phone,
                    Address = a.Address,
                    IsDefault = a.IsDefault
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} addresses for customer {CustomerId}", addresses.Count, customerId);
            return Ok(addresses);
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerAddressDTO>> GetAddress(int id)
        {
            _logger.LogInformation("GetAddress called for address {AddressId}", id);

            var customerId = GetCustomerId();
            if (!customerId.HasValue)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var address = await _context.CustomerAddresses
                .Where(a => a.AddressId == id && a.CustomerId == customerId.Value)
                .Select(a => new CustomerAddressDTO
                {
                    CustomerId = a.CustomerId,
                    FullName = a.FullName,
                    Phone = a.Phone,
                    Address = a.Address,
                    IsDefault = a.IsDefault
                })
                .FirstOrDefaultAsync();

            if (address == null)
            {
                _logger.LogWarning("Address not found: {AddressId}", id);
                return NotFound(new { message = "Địa chỉ không tồn tại." });
            }

            return Ok(address);
        }


        [HttpPost]
        public async Task<ActionResult<CustomerAddressDTO>> CreateAddress([FromBody] CustomerAddressDTO model)
        {
            _logger.LogInformation("CreateAddress called with FullName {FullName}", model.FullName);

            var customerId = GetCustomerId();
            if (!customerId.HasValue)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Phone) || string.IsNullOrWhiteSpace(model.Address))
            {
                _logger.LogWarning("Invalid input data: FullName, Phone, or Address is empty");
                return BadRequest(new { message = "Vui lòng điền đầy đủ thông tin địa chỉ." });
            }

            var address = new CustomerAddress
            {
                CustomerId = customerId.Value,
                FullName = model.FullName,
                Phone = model.Phone,
                Address = model.Address,
                IsDefault = model.IsDefault
            };


            if (model.IsDefault)
            {
                var existingDefault = await _context.CustomerAddresses
                    .Where(a => a.CustomerId == customerId.Value && a.IsDefault)
                    .ToListAsync();
                foreach (var addr in existingDefault)
                {
                    addr.IsDefault = false;
                }
            }

            _context.CustomerAddresses.Add(address);
            await _context.SaveChangesAsync();

            var addressDto = new CustomerAddressDTO
            {
                CustomerId = address.CustomerId,
                FullName = address.FullName,
                Phone = address.Phone,
                Address = address.Address,
                IsDefault = address.IsDefault
            };

            _logger.LogInformation("Created address for customer {CustomerId}", customerId);
            return CreatedAtAction(nameof(GetAddress), new { id = address.AddressId }, addressDto);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAddress(int id, [FromBody] CustomerAddressDTO model)
        {
            _logger.LogInformation("UpdateAddress called for address {AddressId}", id);

            var customerId = GetCustomerId();
            if (!customerId.HasValue)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var address = await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.AddressId == id && a.CustomerId == customerId.Value);

            if (address == null)
            {
                _logger.LogWarning("Address not found: {AddressId}", id);
                return NotFound(new { message = "Địa chỉ không tồn tại." });
            }

            if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.Phone) || string.IsNullOrWhiteSpace(model.Address))
            {
                _logger.LogWarning("Invalid input data: FullName, Phone, or Address is empty");
                return BadRequest(new { message = "Vui lòng điền đầy đủ thông tin địa chỉ." });
            }

            address.FullName = model.FullName;
            address.Phone = model.Phone;
            address.Address = model.Address;
            address.IsDefault = model.IsDefault;


            if (model.IsDefault)
            {
                var existingDefault = await _context.CustomerAddresses
                    .Where(a => a.CustomerId == customerId.Value && a.IsDefault && a.AddressId != id)
                    .ToListAsync();
                foreach (var addr in existingDefault)
                {
                    addr.IsDefault = false;
                }
            }

            _context.CustomerAddresses.Update(address);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated address {AddressId} for customer {CustomerId}", id, customerId);
            return NoContent();
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            _logger.LogInformation("DeleteAddress called for address {AddressId}", id);

            var customerId = GetCustomerId();
            if (!customerId.HasValue)
            {
                _logger.LogWarning("Invalid customerId from token");
                return Unauthorized(new { message = "Token không hợp lệ." });
            }

            var address = await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.AddressId == id && a.CustomerId == customerId.Value);

            if (address == null)
            {
                _logger.LogWarning("Address not found: {AddressId}", id);
                return NotFound(new { message = "Địa chỉ không tồn tại." });
            }

            _context.CustomerAddresses.Remove(address);
            await _context.SaveChangesAsync();


            if (address.IsDefault)
            {
                var anotherAddress = await _context.CustomerAddresses
                    .Where(a => a.CustomerId == customerId.Value)
                    .FirstOrDefaultAsync();
                if (anotherAddress != null)
                {
                    anotherAddress.IsDefault = true;
                    _context.CustomerAddresses.Update(anotherAddress);
                    await _context.SaveChangesAsync();
                }
            }

            _logger.LogInformation("Deleted address {AddressId} for customer {CustomerId}", id, customerId);
            return Ok(new { message = "Xóa địa chỉ thành công." });
        }

        private int? GetCustomerId()
        {
            var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(customerIdClaim, out int customerId) ? customerId : null;
        }
    }
}