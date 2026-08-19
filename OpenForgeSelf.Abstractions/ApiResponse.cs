namespace OpenForgeSelf.Abstractions;

public class ApiResponse
{
    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;

    public bool Success { get; set; }

    public static ApiResponse Ok(string message = "操作成功")
    {
        return new ApiResponse
        {
            Code = 0,
            Message = message,
            Success = true
        };
    }

    public static ApiResponse Error(string message, int code = 500)
    {
        return new ApiResponse
        {
            Code = code,
            Message = message,
            Success = false
        };
    }
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "操作成功")
    {
        return new ApiResponse<T>
        {
            Code = 0,
            Message = message,
            Success = true,
            Data = data
        };
    }

    public static new ApiResponse<T> Error(string message, int code = 500)
    {
        return new ApiResponse<T>
        {
            Code = code,
            Message = message,
            Success = false,
            Data = default
        };
    }
}
