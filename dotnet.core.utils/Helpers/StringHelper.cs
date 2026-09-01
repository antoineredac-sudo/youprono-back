using System.Globalization;
using System.Text;

namespace dotnet.core.utils.Helpers
{
    public static class StringHelper
    {
        public static bool IsNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        // Enlève les accents et passe en minuscules (utile pour comparer des noms de joueurs/équipes)
        public static string NormalizeString(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }
    }
}
