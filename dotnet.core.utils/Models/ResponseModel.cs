using System;
using dotnet.core.utils;

namespace dotnet.core.utils.Models
{
    public class ResponseModel<T>
    {
        public int Code { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }

        public ResponseModel() { }

        public ResponseModel(int code, T data)
        {
            Code = code;
            Success = code == 0;
            Data = data;
        }

        public static ResponseModel<T> CreateDefault()
        {
            return new ResponseModel<T> { Code = 0, Success = true };
        }

        public static ResponseModel<T> Exception(Exception ex)
        {
            if (ex is BaseException be)
            {
                return new ResponseModel<T>
                {
                    Code = be.Code,
                    Success = false,
                    Message = be.Message
                };
            }
            return new ResponseModel<T>
            {
                Code = -1,
                Success = false,
                Message = ex.Message
            };
        }
    }
}
