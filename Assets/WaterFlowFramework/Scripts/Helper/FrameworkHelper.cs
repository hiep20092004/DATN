using System.Text.RegularExpressions;

namespace WaterFlow.Framework.Helper
{
    public static class FrameworkHelper
    {
        public static string SplitByUppercase(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return Regex.Replace(input, "(?<!^)([A-Z])", " $1");
        }
    }
}
