using System;

namespace dotnet.core.utils
{
    public class BaseException : Exception
    {
        public int Code { get; }
        public new string Source { get; }

        public BaseException(int code, string source, string message) : base(message)
        {
            Code = code;
            Source = source;
        }

        public static BaseException NotFound(int code, string source)
            => new BaseException(code, source, $"Ressource introuvable ({source})");

        public static BaseException InvalidModel(int code, string source)
            => new BaseException(code, source, $"Données invalides ({source})");

        public static BaseException AlreadyInDb(int code, string source)
            => new BaseException(code, source, $"Existe déjà en base ({source})");
    }
}
