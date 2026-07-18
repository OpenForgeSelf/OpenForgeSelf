using OpenForgeSelf.Backend.Plugins.AIAgent.Models;
using OpenForgeSelf.Backend.Plugins.AIAgent.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlanningController : ControllerBase
{
    private readonly IProactivePlanningService _planningService;

    public PlanningController(IProactivePlanningService planningService)
    {
        _planningService = planningService;
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult<List<SuggestionEntity>>> GetSuggestions([FromQuery] int limit = 10)
    {
        var userId = GetUserId();
        var suggestions = await _planningService.GenerateSuggestionsAsync(userId, limit);
        return Ok(suggestions);
    }

    [HttpGet("suggestions/pending")]
    public async Task<ActionResult<List<SuggestionEntity>>> GetPendingSuggestions([FromQuery] int limit = 20)
    {
        var userId = GetUserId();
        var suggestions = await _planningService.GetPendingSuggestionsAsync(userId, limit);
        return Ok(suggestions);
    }

    [HttpPost("suggestions/{id}/action")]
    public async Task<IActionResult> MarkSuggestionActioned(long id)
    {
        await _planningService.MarkSuggestionActionedAsync(id);
        return Ok(new { success = true });
    }

    [HttpPost("suggestions/{id}/dismiss")]
    public async Task<IActionResult> MarkSuggestionDismissed(long id)
    {
        await _planningService.MarkSuggestionDismissedAsync(id);
        return Ok(new { success = true });
    }

    [HttpGet("patterns")]
    public async Task<ActionResult<List<UsagePatternEntity>>> GetPatterns([FromQuery] int days = 30)
    {
        var userId = GetUserId();
        var patterns = await _planningService.AnalyzePatternsAsync(userId, days);
        return Ok(patterns);
    }

    [HttpGet("profile")]
    public async Task<ActionResult<UserProfileSummary>> GetUserProfile()
    {
        var userId = GetUserId();
        var profile = await _planningService.GetUserProfileAsync(userId);
        return Ok(profile);
    }

    [HttpGet("skills")]
    public async Task<ActionResult<List<UserSkillEntity>>> GetUserSkills()
    {
        var userId = GetUserId();
        var skills = await _planningService.GetUserSkillsAsync(userId);
        return Ok(skills);
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<List<UserPreferenceEntity>>> GetUserPreferences()
    {
        var userId = GetUserId();
        var prefs = await _planningService.GetUserPreferencesAsync(userId);
        return Ok(prefs);
    }

    [HttpPost("event")]
    public async Task<IActionResult> RecordEvent([FromBody] UsageEventEntity evt)
    {
        evt.UserId = GetUserId();
        await _planningService.RecordUsageEventAsync(evt);
        return Ok(new { success = true });
    }

    private string GetUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        return userIdClaim ?? "default_user";
    }
}
