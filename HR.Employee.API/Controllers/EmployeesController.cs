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
    public async Task<IActionResult> GetAllEmployees([FromServices] AppDbContext context)
    {
        var db = redis.GetDatabase();
        string cacheKey = "employee_list";

        // 1. Pehle Redis se data mangwayein
        var cachedData = await db.StringGetAsync(cacheKey);

        if (!cachedData.IsNullOrEmpty)
        {
            // Agar cache mein mil gaya toh SQL tak jane ki zaroorat hi nahi!
            var employeesFromCache = JsonSerializer.Deserialize<List<Models.Employees>>(cachedData!);
            return Ok(new { Source = "Redis Cache", Data = employeesFromCache });
        }

        // 2. Agar Redis mein nahi hai (Cache Miss), toh SQL se lein
        var employeesFromDb = await context.Employees.ToListAsync();

        // 3. SQL se milne wala data Redis mein save karein (taake agli baar fast ho)
        // Hum isay 5 minute ke liye cache kar rahe hain
        var serializedData = JsonSerializer.Serialize(employeesFromDb);
        await db.StringSetAsync(cacheKey, serializedData, TimeSpan.FromMinutes(5));

        return Ok(new { Source = "SQL Database", Data = employeesFromDb });
    }

}

}
