using System.Text.Json;

namespace dotnet.core.utils.Helpers
{
    public static class JsonHelper
    {
        public static JsonSerializerOptions TextJsonIgnoreCaseOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }
    }
}
