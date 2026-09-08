using Issue.Api.Dtos;
using Issue.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Issue.Api.Controllers;

[ApiController]
public class TrackTodosController(TrackTodoService tracks) : ApiControllerBase
{
    [HttpGet("/track-todos")]
    public async Task<IActionResult> Home() => OkData(await tracks.ListHomeAsync());

    [HttpPut("/track-todos/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] TrackTodoWriteDto input) =>
        OkData(await tracks.UpdateAsync(id, input));

    [HttpPut("/track-todos/{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, [FromBody] TodoCompleteDto input) =>
        OkData(await tracks.SetCompletedAsync(id, input.IsCompleted));

    [HttpPut("/track-todos/{id:long}/reorder")]
    public async Task<IActionResult> Reorder(long id, [FromBody] TrackTodoReorderDto input) =>
        OkData(await tracks.ReorderAsync(id, input));

    [HttpDelete("/track-todos/{id:long}")]
    public async Task<IActionResult> Delete(long id) => OkData(await tracks.DeleteAsync(id));
}
