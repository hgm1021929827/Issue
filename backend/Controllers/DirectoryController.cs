using Issue.Api.Dtos;
using Issue.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Issue.Api.Controllers;

[ApiController]
public class DirectoryController(DirectoryService directory) : ApiControllerBase
{
    [HttpGet("/company-members")]
    public async Task<IActionResult> ListMembers([FromQuery] string? q) =>
        OkData(await directory.ListMembersAsync(q));

    [HttpPost("/company-members")]
    public async Task<IActionResult> CreateMember([FromBody] CompanyMemberWriteDto input) =>
        OkData(await directory.CreateMemberAsync(input));

    [HttpPut("/company-members/{id:long}")]
    public async Task<IActionResult> UpdateMember(long id, [FromBody] CompanyMemberWriteDto input) =>
        OkData(await directory.UpdateMemberAsync(id, input));

    [HttpDelete("/company-members/{id:long}")]
    public async Task<IActionResult> DeleteMember(long id)
    {
        await directory.DeleteMemberAsync(id);
        return OkData(new { deleted = true });
    }

    [HttpGet("/client-companies")]
    public async Task<IActionResult> ListCompanies([FromQuery] string? q) =>
        OkData(await directory.ListCompaniesAsync(q));

    [HttpGet("/client-companies/tree")]
    public async Task<IActionResult> Tree([FromQuery] string? q) =>
        OkData(await directory.TreeAsync(q));

    [HttpPost("/client-companies")]
    public async Task<IActionResult> CreateCompany([FromBody] ClientCompanyWriteDto input) =>
        OkData(await directory.CreateCompanyAsync(input));

    [HttpPut("/client-companies/{id:long}")]
    public async Task<IActionResult> UpdateCompany(long id, [FromBody] ClientCompanyWriteDto input) =>
        OkData(await directory.UpdateCompanyAsync(id, input));

    [HttpDelete("/client-companies/{id:long}")]
    public async Task<IActionResult> DeleteCompany(long id)
    {
        await directory.DeleteCompanyAsync(id);
        return OkData(new { deleted = true });
    }

    [HttpGet("/client-companies/{companyId:long}/contacts")]
    public async Task<IActionResult> ListContacts(long companyId) =>
        OkData(await directory.ListContactOptionsAsync(companyId));

    [HttpPost("/client-companies/{companyId:long}/contacts")]
    public async Task<IActionResult> CreateContact(long companyId, [FromBody] ClientContactWriteDto input) =>
        OkData(await directory.CreateContactAsync(companyId, input));

    [HttpPut("/client-companies/{companyId:long}/contacts/{id:long}")]
    public async Task<IActionResult> UpdateContact(long companyId, long id, [FromBody] ClientContactWriteDto input) =>
        OkData(await directory.UpdateContactAsync(companyId, id, input));

    [HttpDelete("/client-companies/{companyId:long}/contacts/{id:long}")]
    public async Task<IActionResult> DeleteContact(long companyId, long id) =>
        OkData(await directory.DeleteContactAsync(companyId, id));

    [HttpPost("/client-companies/{companyId:long}/contacts/{id:long}/channels")]
    public async Task<IActionResult> CreateChannel(long companyId, long id, [FromBody] ContactChannelWriteDto input) =>
        OkData(await directory.CreateChannelAsync(companyId, id, input));

    [HttpPut("/client-companies/{companyId:long}/contacts/{id:long}/channels/{channelId:long}")]
    public async Task<IActionResult> UpdateChannel(long companyId, long id, long channelId, [FromBody] ContactChannelWriteDto input) =>
        OkData(await directory.UpdateChannelAsync(companyId, id, channelId, input));

    [HttpDelete("/client-companies/{companyId:long}/contacts/{id:long}/channels/{channelId:long}")]
    public async Task<IActionResult> DeleteChannel(long companyId, long id, long channelId) =>
        OkData(await directory.DeleteChannelAsync(companyId, id, channelId));
}
