namespace Issue.Api.Common;

public class Resp
{
    public int Code { get; set; }
    public string Message { get; set; } = "success";
    public object? Data { get; set; }

    public static Resp Ok(object? data = null) => new() { Code = 200, Message = "success", Data = data };

    public static Resp Error(int code, string message, object? data = null) =>
        new() { Code = code, Message = message, Data = data };
}
