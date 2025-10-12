using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeManagementBackend.Data;
using TimeManagementBackend.Models;

namespace TimeManagementBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TimeLogsController : Controller
{
    private readonly AppDbContext _context;

    public TimeLogsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TimeLog>>> GetTimeLogs()
    {
        return await _context.TimeLogs.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TimeLog>> GetTimeLog(int id)
    {
        var log = await _context.TimeLogs.FindAsync(id);
        if (log == null) return NotFound();
        return log;
    }

    [HttpPost]
    public async Task<ActionResult<TimeLog>> CreateTimeLog(TimeLog log)
    {
        _context.TimeLogs.Add(log);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTimeLog), new { id = log.Id }, log);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTimeLog(int id, TimeLog log)
    {
        if (id != log.Id) return BadRequest();

        _context.Entry(log).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTimeLog(int id)
    {
        var log = await _context.TimeLogs.FindAsync(id);
        if (log == null) return NotFound();

        _context.TimeLogs.Remove(log);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
