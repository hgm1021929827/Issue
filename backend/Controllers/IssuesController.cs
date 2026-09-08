using Issue.Api.Dtos;
using Issue.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Issue.Api.Controllers;

[ApiController]
public class IssuesController(IssueService issues, TodoService todos, TrackTodoService tracks) : ApiControllerBase
{
    [HttpGet("/issues")]
    public async Task<IActionResult> List([FromQuery] long? majorCategoryId) =>
        OkData(await issues.ListAsync(majorCategoryId));

    [HttpGet("/issues/calendar")]
    public async Task<IActionResult> Calendar([FromQuery] int year, [FromQuery] int month) =>
        OkData(await issues.CalendarAsync(year, month));

    [HttpPost("/issues/{id:long}/plans")]
    public async Task<IActionResult> AddPlan(long id, [FromBody] IssuePlanWriteDto input)
    {
        var count = await issues.AddPlansAsync(id, input);
        return OkData(new { added = true, count });
    }

    [HttpDelete("/issues/{id:long}/plans")]
    public async Task<IActionResult> RemovePlan(long id, [FromQuery] DateOnly date)
    {
        await issues.RemovePlanAsync(id, date);
        return OkData(new { deleted = true });
    }

    [HttpGet("/issues/{id:long}")]
    public async Task<IActionResult> Get(long id) => OkData(await issues.GetAsync(id));

    [HttpPost("/issues")]
    public async Task<IActionResult> Create([FromBody] IssueWriteDto input) =>
        OkData(await issues.CreateAsync(input));

    [HttpPut("/issues/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] IssueWriteDto input) =>
        OkData(await issues.UpdateAsync(id, input));

    [HttpDelete("/issues/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await issues.DeleteAsync(id);
        return OkData(new { deleted = true });
    }

    [HttpGet("/issues/{issueId:long}/todos")]
    public async Task<IActionResult> Tree(long issueId) => OkData(await todos.GetTreeAsync(issueId));

    [HttpPost("/issues/{issueId:long}/todos")]
    public async Task<IActionResult> AddTodo(long issueId, [FromBody] TodoWriteDto input) =>
        OkData(await todos.CreateAsync(issueId, input));

    [HttpGet("/issues/{issueId:long}/track-todos")]
    public async Task<IActionResult> ListTrackTodos(long issueId) =>
        OkData(await tracks.ListForIssueAsync(issueId));

    [HttpPost("/issues/{issueId:long}/track-todos")]
    public async Task<IActionResult> AddTrackTodo(long issueId, [FromBody] TrackTodoWriteDto input) =>
        OkData(await tracks.CreateForIssueAsync(issueId, input));
}
