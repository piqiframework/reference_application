using Newtonsoft.Json;

namespace PIQI.Components.CustomExceptionClasses
{
    public class CustomPIQIException : Exception
    {
        [JsonIgnore]
        public int? HTTPStatusCode { get; set; }
        public CustomErrorOutput? Error { get; set; }
        public string? Path { get; set; }
        public CustomPIQIException() { }
        public CustomPIQIException(int httpStatusCode, string code, string? message = null, string? path = null)
        {
            HTTPStatusCode = httpStatusCode;
            Error = new CustomErrorOutput(code, message, path);
        }
    }

    public class CustomErrorOutput 
    {
        public string Code { get; set; }
        public string? Message { get; set; }
        public string? Path { get; set; }
        public CustomErrorOutput() { }
        public CustomErrorOutput(string code, string? message = null, string? path = null)
        {
            Code = code;
            Message = message;
            Path = path;
        }
    }

}
