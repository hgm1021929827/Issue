using Issue.Api.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Issue.Api.Filters;

public class AppExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is AppException app)
        {
            context.Result = new ObjectResult(Resp.Error(app.StatusCode, app.Message, app.Data))
            {
                StatusCode = app.StatusCode
            };
            context.ExceptionHandled = true;
            return;
        }

        context.Result = new ObjectResult(Resp.Error(500, "系統發生錯誤"))
        {
            StatusCode = 500
        };
        context.ExceptionHandled = true;
    }
}
