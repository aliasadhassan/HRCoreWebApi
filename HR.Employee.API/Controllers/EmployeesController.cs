using HR.Shared.Library.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HR.Employee.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeesController : ControllerBase
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

    }
}
