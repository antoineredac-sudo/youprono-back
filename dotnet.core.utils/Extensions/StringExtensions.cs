using System;
using System.Text;

namespace dotnet.core.utils.Extensions
{
    public static class StringExtensions
    {
        public static string EncodeTo64(this string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            var bytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(bytes);
        }

        public static string DecodeFrom64(this string encodedText)
        {
            if (string.IsNullOrEmpty(encodedText)) return string.Empty;
            var bytes = Convert.FromBase64String(encodedText);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
