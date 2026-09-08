using Issue.Api.Common;
using Issue.Api.Dtos;
using Issue.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Issue.Api.Controllers;

[ApiController]
public class CategoryController(CategoryService categories) : ApiControllerBase
{
    [HttpGet("/majorCategories")]
    public async Task<IActionResult> Major() => OkData(await categories.ListMajorAsync());

    [HttpPost("/majorCategories")]
    public async Task<IActionResult> CreateMajor([FromBody] MajorCategoryWriteDto input) =>
        OkData(await categories.CreateMajorAsync(input));

    [HttpPut("/majorCategories/{id:long}")]
    public async Task<IActionResult> UpdateMajor(long id, [FromBody] MajorCategoryWriteDto input) =>
        OkData(await categories.UpdateMajorAsync(id, input));

    [HttpDelete("/majorCategories/{id:long}")]
    public async Task<IActionResult> DeleteMajor(long id) => OkData(await categories.DeleteMajorAsync(id));

    [HttpGet("/colorPresets")]
    public IActionResult Presets() => OkData(ColorPresets.All);

    [HttpGet("/subCategories")]
    public async Task<IActionResult> Sub([FromQuery] long? majorCategoryId) =>
        OkData(await categories.ListSubAsync(majorCategoryId));

    [HttpPost("/subCategories")]
    public async Task<IActionResult> Create([FromBody] SubCategoryWriteDto input) =>
        OkData(await categories.CreateSubAsync(input));

    [HttpPut("/subCategories/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SubCategoryUpdateDto input) =>
        OkData(await categories.UpdateSubAsync(id, input));

    [HttpDelete("/subCategories/{id:long}")]
    public async Task<IActionResult> Delete(long id) => OkData(await categories.DeleteSubAsync(id));
}
