using System.Text.RegularExpressions;

namespace LearningCoreWebApi.Helpers
{
    public static class ConvertExtensions
    {
        public static bool IsValidJWT(this string token)
        {
            // Create a Regex  
            Regex rg = new Regex(@"^[A-Za-z0-9-_=]+\.[A-Za-z0-9-_=]+\.?[A-Za-z0-9-_.+/=]*$");

            if (!rg.IsMatch(token))
                return false;

            return true;
        }
    }
}
