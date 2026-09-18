using Issue.Api.Dtos;
using Issue.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Issue.Api.Controllers;

[ApiController]
public class AppointmentsController(AppointmentService appointments) : ApiControllerBase
{
    [HttpGet("/appointments")]
    public async Task<IActionResult> List() => OkData(await appointments.ListAsync());

    [HttpGet("/appointments/future")]
    public async Task<IActionResult> Future([FromQuery] long clientCompanyId, [FromQuery] long? excludeId) =>
        OkData(await appointments.ListFutureAsync(clientCompanyId, excludeId));

    [HttpGet("/appointments/{id:long}")]
    public async Task<IActionResult> Get(long id) => OkData(await appointments.GetAsync(id));

    [HttpPost("/appointments")]
    public async Task<IActionResult> Create([FromBody] AppointmentWriteDto input) =>
        OkData(await appointments.CreateAsync(input));

    [HttpPut("/appointments/{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] AppointmentWriteDto input) =>
        OkData(await appointments.UpdateAsync(id, input));

    [HttpDelete("/appointments/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await appointments.DeleteAsync(id);
        return OkData(new { deleted = true });
    }

    [HttpPost("/appointments/{id:long}/items")]
    public async Task<IActionResult> AddItems(long id, [FromBody] AppointmentItemsWriteDto input) =>
        OkData(await appointments.AddItemsAsync(id, input));

    [HttpDelete("/appointments/{id:long}/items/{itemId:long}")]
    public async Task<IActionResult> DeleteItem(long id, long itemId) =>
        OkData(await appointments.DeleteItemAsync(id, itemId));
}
