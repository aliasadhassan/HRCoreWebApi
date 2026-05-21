using System.Text.RegularExpressions;

namespace HR.Shared.Library.Helpers
{
    public static class ConvertExtensions
    {
        // SonarQube ka yeh rule aapko Regular Expression Denial of Service (ReDoS) attack se bachane ke liye hai,
        // jahan koi bohot bada input dekar aapka server hang kar sakta hai.Isko fix
        // karne ke liye Regex constructor mein teesra parameter TimeSpan (timeout) pass karna hota hai.
        public static bool IsValidJWT(this string token)
        {
            // Create a Regex  
            Regex rg = new Regex(@"^[A-Za-z0-9-_=]+\.[A-Za-z0-9-_=]+\.?[A-Za-z0-9-_.+/=]*$",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(250) // 250ms ka timeout set kiya
                );

            if (!rg.IsMatch(token))
                return false;

            return true;
        }
    }
}
