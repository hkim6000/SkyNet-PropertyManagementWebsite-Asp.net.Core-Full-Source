using System.Globalization;
using System.Security.Cryptography;

namespace PropertyManagement.Core
{
    public static class Fmt
    {
        private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        public static string Money(decimal v)
        {
            return v.ToString("C2", Us);
        }

        public static string Whole(decimal v)
        {
            return v.ToString("C0", Us);
        }

        public static string When(DateTime d)
        {
            return d.ToString("ddd, MMM d, h:mm tt", Us);
        }

        public static string Day(DateTime d)
        {
            return d.ToString("MMM d, yyyy", Us);
        }

        public static string Short(DateTime d)
        {
            return d.ToString("MMM d", Us);
        }

        public static string NewId(string prefix)
        {
            return prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();
        }

        public static string Code(string prefix, IEnumerable<string> taken)
        {
            HashSet<string> used = new HashSet<string>(taken);
            while (true)
            {
                char[] c = new char[6];
                for (int i = 0; i < c.Length; i++)
                {
                    c[i] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
                }
                string code = prefix + new string(c);
                if (!used.Contains(code))
                {
                    return code;
                }
            }
        }

        public static int Age(DateTime birth, DateTime at)
        {
            int age = at.Year - birth.Year;
            if (birth.Date > at.Date.AddYears(-age))
            {
                age--;
            }
            return age;
        }

        public static bool ParseLocal(string? v, out DateTime d)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), new[] { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
        }

        public static bool ParseDate(string? v, out DateTime d)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
        }

        public static string Iso(DateTime d)
        {
            return d.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        }

        public static string IsoDay(DateTime d)
        {
            return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public static string Beds(int n)
        {
            return n == 0 ? "Studio" : n + (n == 1 ? " bed" : " beds");
        }

        public static string Baths(decimal n)
        {
            return n.ToString("0.#", CultureInfo.InvariantCulture) + (n == 1 ? " bath" : " baths");
        }

        public static decimal Dec(string? v)
        {
            return decimal.TryParse((v ?? string.Empty).Replace("$", string.Empty).Replace(",", string.Empty).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? d : -1m;
        }

        public static int Int(string? v)
        {
            return int.TryParse((v ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : -1;
        }
    }
}
