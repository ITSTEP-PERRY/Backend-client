using AuthService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/health/database")]
public class DatabaseHealthController : ControllerBase
{
    private readonly AuthDbContext _dbContext;

    public DatabaseHealthController(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var canConnect = await _dbContext.Database
            .CanConnectAsync(cancellationToken);

        if (!canConnect)
        {
            return StatusCode(503, new
            {
                status = "unhealthy",
                database = "unavailable"
            });
        }

        return Ok(new
        {
            status = "healthy",
            database = "postgresql"
        });
    }
}