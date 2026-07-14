using System.Collections.Generic;

namespace delivery_system_dotnet.DTOs.Response;

public class ApiResponseDto<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    public ApiResponseDto() {}

    public ApiResponseDto(bool success, string message, T? data = default, List<string>? errors = null)
    {
        Success = success;
        Message = message;
        Data = data;
        Errors = errors;
    }
}

public class ApiResponseDto : ApiResponseDto<object>
{
    public ApiResponseDto() {}

    public ApiResponseDto(bool success, string message, object? data = null, List<string>? errors = null)
        : base(success, message, data, errors)
    {
    }
}
