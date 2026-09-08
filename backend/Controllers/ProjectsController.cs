using Issue.Api.Dtos;
using Issue.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Issue.Api.Controllers;

[ApiController]
public class ProjectsController(
    ProjectService projects,
    TrackTodoService tracks,
    WorkItemService workItems,
    WorkHourService hours,
    ProjectImportService imports) : ApiControllerBase
{
    [HttpGet("/projects")]
    public async Task<IActionResult> List([FromQuery] long? majorCategoryId) =>
        OkData(await projects.ListAsync(majorCategoryId));

    [HttpGet("/projects/{id:long}")]
    public async Task<IActionResult> Get(long id) => OkData(await projects.GetAsync(id));

    [HttpPost("/projects")]
    public async Task<IActionResult> Create([FromBody] ProjectWriteDto input) =>
        OkData(await projects.CreateAsync(input));

    [HttpPut("/projects/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] ProjectWriteDto input) =>
        OkData(await projects.UpdateAsync(id, input));

    [HttpDelete("/projects/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await projects.DeleteAsync(id);
        return OkData(new { deleted = result.Deleted, itemCount = result.ItemCount, workItemCount = result.WorkItemCount });
    }

    [HttpGet("/projects/{projectId:long}/items")]
    public async Task<IActionResult> ListItems(long projectId) =>
        OkData(await projects.ListItemsAsync(projectId));

    [HttpPost("/projects/{projectId:long}/items")]
    public async Task<IActionResult> CreateItem(long projectId, [FromBody] ProjectIssueWriteDto input) =>
        OkData(await projects.CreateItemAsync(projectId, input));

    [HttpPut("/projects/{projectId:long}/items/{id:long}")]
    public async Task<IActionResult> UpdateItem(long projectId, long id, [FromBody] ProjectIssueWriteDto input) =>
        OkData(await projects.UpdateItemAsync(projectId, id, input));

    [HttpDelete("/projects/{projectId:long}/items/{id:long}")]
    public async Task<IActionResult> DeleteItem(long projectId, long id) =>
        OkData(await projects.DeleteItemAsync(projectId, id));

    [HttpGet("/projects/{projectId:long}/track-todos")]
    public async Task<IActionResult> ListTrackTodos(long projectId) =>
        OkData(await tracks.ListForProjectAsync(projectId));

    [HttpPost("/projects/{projectId:long}/track-todos")]
    public async Task<IActionResult> AddTrackTodo(long projectId, [FromBody] TrackTodoWriteDto input) =>
        OkData(await tracks.CreateForProjectAsync(projectId, input));

    [HttpGet("/projects/{projectId:long}/items/{itemId:long}/track-todos")]
    public async Task<IActionResult> ListItemTrackTodos(long projectId, long itemId) =>
        OkData(await tracks.ListForProjectItemAsync(projectId, itemId));

    [HttpPost("/projects/{projectId:long}/items/{itemId:long}/track-todos")]
    public async Task<IActionResult> AddItemTrackTodo(long projectId, long itemId, [FromBody] TrackTodoWriteDto input) =>
        OkData(await tracks.CreateForProjectItemAsync(projectId, itemId, input));

    [HttpPut("/projects/{projectId:long}/items/{id:long}/complete")]
    public async Task<IActionResult> CompleteItem(long projectId, long id, [FromBody] TodoCompleteDto input) =>
        OkData(await projects.SetItemCompletedAsync(projectId, id, input.IsCompleted));

    [HttpGet("/projects/{projectId:long}/items/{itemId:long}/hours")]
    public async Task<IActionResult> ListItemHours(long projectId, long itemId) =>
        OkData(await hours.ListForIssueAsync(projectId, itemId));

    [HttpPost("/projects/{projectId:long}/items/{itemId:long}/hours")]
    public async Task<IActionResult> CreateItemHour(long projectId, long itemId, [FromBody] WorkHourWriteDto input) =>
        OkData(await hours.CreateForIssueAsync(projectId, itemId, input));

    [HttpPut("/projects/{projectId:long}/items/{itemId:long}/hours/{hourId:long}")]
    public async Task<IActionResult> UpdateItemHour(long projectId, long itemId, long hourId, [FromBody] WorkHourWriteDto input) =>
        OkData(await hours.UpdateForIssueAsync(projectId, itemId, hourId, input));

    [HttpDelete("/projects/{projectId:long}/items/{itemId:long}/hours/{hourId:long}")]
    public async Task<IActionResult> DeleteItemHour(long projectId, long itemId, long hourId) =>
        OkData(await hours.DeleteForIssueAsync(projectId, itemId, hourId));

    [HttpGet("/projects/{projectId:long}/work-items")]
    public async Task<IActionResult> ListWorkItems(long projectId) =>
        OkData(await workItems.ListAsync(projectId));

    [HttpGet("/projects/{projectId:long}/work-items/{id:long}")]
    public async Task<IActionResult> GetWorkItem(long projectId, long id) =>
        OkData(await workItems.GetAsync(projectId, id));

    [HttpPut("/projects/{projectId:long}/work-items/{id:long}/complete")]
    public async Task<IActionResult> CompleteWorkItem(long projectId, long id, [FromBody] TodoCompleteDto input) =>
        OkData(await workItems.SetCompletedAsync(projectId, id, input.IsCompleted));

    [HttpPut("/projects/{projectId:long}/work-items/{id:long}/remark")]
    public async Task<IActionResult> UpdateWorkItemRemark(long projectId, long id, [FromBody] WorkItemRemarkWriteDto input) =>
        OkData(await workItems.UpdateRemarkAsync(projectId, id, input.Remark));

    [HttpPut("/projects/{projectId:long}/items/{id:long}/remark")]
    public async Task<IActionResult> UpdateItemRemark(long projectId, long id, [FromBody] WorkItemRemarkWriteDto input) =>
        OkData(await projects.UpdateItemRemarkAsync(projectId, id, input.Remark));

    [HttpDelete("/projects/{projectId:long}/work-items/{id:long}")]
    public async Task<IActionResult> DeleteWorkItem(long projectId, long id)
    {
        await workItems.DeleteAsync(projectId, id);
        return OkData(new { deleted = true });
    }

    [HttpGet("/projects/{projectId:long}/work-items/{id:long}/hours")]
    public async Task<IActionResult> ListWorkItemHours(long projectId, long id) =>
        OkData(await hours.ListForWorkItemAsync(projectId, id));

    [HttpPost("/projects/{projectId:long}/work-items/{id:long}/hours")]
    public async Task<IActionResult> CreateWorkItemHour(long projectId, long id, [FromBody] WorkHourWriteDto input) =>
        OkData(await hours.CreateForWorkItemAsync(projectId, id, input));

    [HttpPut("/projects/{projectId:long}/work-items/{id:long}/hours/{hourId:long}")]
    public async Task<IActionResult> UpdateWorkItemHour(long projectId, long id, long hourId, [FromBody] WorkHourWriteDto input) =>
        OkData(await hours.UpdateForWorkItemAsync(projectId, id, hourId, input));

    [HttpDelete("/projects/{projectId:long}/work-items/{id:long}/hours/{hourId:long}")]
    public async Task<IActionResult> DeleteWorkItemHour(long projectId, long id, long hourId) =>
        OkData(await hours.DeleteForWorkItemAsync(projectId, id, hourId));

    [HttpGet("/projects/{projectId:long}/work-items/{id:long}/track-todos")]
    public async Task<IActionResult> ListWorkItemTracks(long projectId, long id) =>
        OkData(await tracks.ListForWorkItemAsync(projectId, id));

    [HttpPost("/projects/{projectId:long}/work-items/{id:long}/track-todos")]
    public async Task<IActionResult> AddWorkItemTrack(long projectId, long id, [FromBody] TrackTodoWriteDto input) =>
        OkData(await tracks.CreateForWorkItemAsync(projectId, id, input));

    [HttpPost("/project-imports/resolve-project")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> ResolveImport([FromForm] IFormFile? file) =>
        OkData(await imports.ResolveProjectAsync(file));

    [HttpPost("/projects/{projectId:long}/imports")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Import(long projectId, [FromForm] IFormFile? file, [FromForm] long? currentUserMemberId) =>
        OkData(await imports.ImportAsync(projectId, file, currentUserMemberId));

    [HttpPost("/projects/{projectId:long}/import-decisions")]
    public async Task<IActionResult> ImportDecisions(long projectId, [FromBody] ImportDecisionRequestDto input)
    {
        await imports.ApplyDecisionsAsync(projectId, input);
        return OkData(new { applied = true });
    }
}
