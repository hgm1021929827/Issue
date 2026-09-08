using Issue.Api.Dtos;
using Issue.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Issue.Api.Controllers;

[ApiController]
public class TodosController(TodoService todos) : ApiControllerBase
{
    [HttpPut("/todos/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] TodoUpdateDto input) =>
        OkData(await todos.UpdateAsync(id, input));

    [HttpPut("/todos/{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, [FromBody] TodoCompleteDto input) =>
        OkData(await todos.SetCompletedAsync(id, input.IsCompleted));

    [HttpPut("/todos/{id:long}/reorder")]
    public async Task<IActionResult> Reorder(long id, [FromBody] TodoReorderDto input) =>
        OkData(await todos.ReorderAsync(id, input));

    [HttpDelete("/todos/{id:long}")]
    public async Task<IActionResult> Delete(long id) => OkData(await todos.DeleteAsync(id));
}
