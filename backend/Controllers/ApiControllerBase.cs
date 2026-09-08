using Issue.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Issue.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult OkData(object? data) => StatusCode(200, Resp.Ok(data));

    protected IActionResult Fail(int status, string message) => StatusCode(status, Resp.Error(status, message));
}
