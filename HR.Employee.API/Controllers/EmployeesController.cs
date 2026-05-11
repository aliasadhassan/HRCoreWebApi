using HR.Shared.Library.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace HR.Employee.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeesController(IConnectionMultiplexer redis) : ControllerBase
    {
        [HttpGet("check-my-secret")]
        public async Task<IActionResult> GetMySecret([FromServices] IKeyVaultHelper kvHelper)
        {
            try
            {
                var mySecret = await kvHelper.GetSecretValueAsync("HRProjectSecrets");
                return Ok(new { Message = "Portal se secret mil gaya!", Value = mySecret });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("test-redis")]
        public IActionResult SetAndGet()
        {
            var db = redis.GetDatabase();

            // 1. Redis mein data set karein
            db.StringSet("habibi_key", "Redis is working like a charm!");

            // 2. Redis se data fetch karein
            var value = db.StringGet("habibi_key");

            return Ok(new { Message = value.ToString() });
        }

        [HttpGet("all-employees")]
        public async Task<IActionResult> GetAllEmployees([FromServices] AppDbContext context,[FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 10)
        {
            var db = redis.GetDatabase();

            // Cache Key mein page info shamil ki taake har page alag save ho
            string cacheKey = $"employee_list_p{pageNumber}_s{pageSize}";

            // CamelCase ke liye options
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true, // Yeh line Redis se wapis aate waqt mapping sahi rakhti hai
                WriteIndented = true
            };

            try
            {
                // 1. Redis se data mangwayein
                var cachedData = await db.StringGetAsync(cacheKey);

                if (!cachedData.IsNull)
                {
                    var employeesFromCache = JsonSerializer.Deserialize<List<Models.Employees>>(cachedData!, jsonOptions);
                    return Ok(new
                    {
                        Source = "Redis Cache",
                        Page = pageNumber,
                        Data = employeesFromCache
                    });
                }

                // 2. SQL se Paged data lein
                // Skip: Pichle pages ka data chhorne ke liye
                // Take: Sirf utna data jitna manga gaya hai
                var employeesFromDb = await context.Employees
                    .OrderBy(e => e.Id) // Sorting zaroori hai pagination ke liye
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 3. Redis mein save karein
                var serializedData = JsonSerializer.Serialize(employeesFromDb, jsonOptions);
                await db.StringSetAsync(cacheKey, serializedData, TimeSpan.FromMinutes(10));

                return Ok(new
                {
                    Source = "SQL Database",
                    Page = pageNumber,
                    PageSize = pageSize,
                    Data = employeesFromDb
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
