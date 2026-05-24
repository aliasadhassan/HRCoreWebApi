using HR.Employee.API.Application.Employees.Commands;
using HR.Employee.API.Application.Employees.Queries;
using HR.Shared.Library.Helpers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Text.Json;
using HR.Employee.API.Infrastructure.Persistence;

namespace HR.Employee.API.Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly ISender _mediator; // MediatR ki lightweight interface standard separation ke liye
    private readonly IConnectionMultiplexer _redis;
    public EmployeesController(
        ISender mediator,
        IConnectionMultiplexer redis)
    {
        _mediator = mediator;
        _redis = redis;
    }

    [HttpPost]
    public async Task<IActionResult> CreateEmployee(
        [FromBody] CreateEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        // Request direct MediatR handler tak jaye gi aur automatic handle hogi
        var employeeId = await _mediator.Send(command, cancellationToken);

        // Standard REST API convention ke mutabiq ID return karna 200 OK ke sath
        return Ok(employeeId);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEmployeeById(Guid id, CancellationToken cancellationToken)
    {
        // Query command handle karne ke liye send karna
        var result = await _mediator.Send(new GetEmployeeByIdQuery(id), cancellationToken);

        // Agar employee nahi mila to 404 Not Found return karein
        if (result == null) return NotFound();

        // Data milne par 200 OK ke sath response bhejein
        return Ok(result);
    }

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
        var db = _redis.GetDatabase();

        // 1. Redis mein data set karein
        db.StringSet("habibi_key", "Redis is working like a charm!");

        // 2. Redis se data fetch karein
        var value = db.StringGet("habibi_key");

        return Ok(new { Message = value.ToString() });
    }

    [HttpGet("all-employees")]
    public async Task<IActionResult> GetAllEmployees(
                                    [FromServices] AppDbContext context,
                                    [FromServices] IMemoryCache memoryCache, // L1 Cache injection
                                    [FromQuery] int pageNumber = 1,
                                    [FromQuery] int pageSize = 10)
    {
        var db = _redis.GetDatabase();
        string cacheKey = $"employee_list_p{pageNumber}_s{pageSize}";

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        try
        {
            // --- LEVEL 1: Check Local RAM (L1) ---
            if (memoryCache.TryGetValue(cacheKey, out List<Models.Employees>? l1Data))
            {
                return Ok(new { Source = "L1 Cache (RAM)", Page = pageNumber, Data = l1Data });
            }

            // --- LEVEL 2: Check Redis (L2) ---
            var cachedData = await db.StringGetAsync(cacheKey);

            if (!cachedData.IsNull)
            {
                var l2Data = JsonSerializer.Deserialize<List<Models.Employees>>(cachedData!, jsonOptions);

                // L2 se mila, toh isay L1 (RAM) mein bhi save kar dein 1 min ke liye
                memoryCache.Set(cacheKey, l2Data, TimeSpan.FromMinutes(1));

                return Ok(new { Source = "L2 Cache (Redis)", Page = pageNumber, Data = l2Data });
            }

            // --- LEVEL 3: Check SQL Database ---
            var employeesFromDb = await context.Employees
                .OrderBy(e => e.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Save in L2 (Redis) - 10 mins
            var serializedData = JsonSerializer.Serialize(employeesFromDb, jsonOptions);
            await db.StringSetAsync(cacheKey, serializedData, TimeSpan.FromMinutes(10));

            // Save in L1 (RAM) - 1 min (Short TTL for safety)
            memoryCache.Set(cacheKey, employeesFromDb, TimeSpan.FromMinutes(1));

            return Ok(new { Source = "SQL Database", Page = pageNumber, Data = employeesFromDb });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }
    private async Task ClearEmployeeCache(IMemoryCache memoryCache, int pageNumber, int pageSize)
    {
        string cacheKey = $"employee_list_p{pageNumber}_s{pageSize}";

        // 1. L1 (RAM) se delete karein
        memoryCache.Remove(cacheKey);

        // 2. L2 (Redis) se delete karein (Pattern wala logic jo pehle likha tha)
        var endpoints = _redis.GetEndPoints();
        var server = _redis.GetServer(endpoints[0]);
        var db = _redis.GetDatabase();
        var keys = server.Keys(pattern: "employee_list_*").ToArray();
        if (keys.Length > 0) await db.KeyDeleteAsync(keys);
    }
}
