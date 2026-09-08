using Issue.Api.Dtos;
using Issue.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Issue.Api.Controllers;

[ApiController]
public class IssueImportsController(IssueImportService imports) : ApiControllerBase
{
    [HttpPost("/issue-imports")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Import(
        [FromForm] IFormFile? file,
        [FromForm] long? currentUserMemberId,
        [FromForm] long? inProgressSubCategoryId,
        [FromForm] long? countersignSubCategoryId,
        [FromForm] long? doneSubCategoryId) =>
        OkData(await imports.ImportAsync(
            file, currentUserMemberId, inProgressSubCategoryId, countersignSubCategoryId, doneSubCategoryId));

    [HttpPost("/issue-imports/decisions")]
    public async Task<IActionResult> Decisions([FromBody] IssueImportDecisionRequestDto input)
    {
        await imports.ApplyDecisionsAsync(input);
        return OkData(new { applied = true });
    }
}
