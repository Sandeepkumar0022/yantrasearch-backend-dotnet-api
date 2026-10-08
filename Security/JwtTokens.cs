using System;
using System.Collections.Generic;
using System.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Dashboards.Security
{
    public static class JwtTokens
    {
        public static string CreateAccess(string userId, string name, string javaRole, bool active, string tier, bool paymentEnabled)
        {
            return Create(new Dictionary<string, object>
            {
                ["sub"] = userId,
                ["name"] = name ?? "",
                ["role"] = javaRole,
                ["active"] = active,
                ["tier"] = string.IsNullOrWhiteSpace(tier) ? "FREE" : tier,
                ["paymentEnabled"] = paymentEnabled,
                ["typ"] = "access"
            }, 60 * 60 * 12);
        }

        public static string CreateRefresh(string userId)
        {
            return Create(new Dictionary<string, object>
            {
                ["sub"] = userId,
                ["typ"] = "refresh"
            }, 60 * 60 * 24 * 14);
        }

        public static ClaimsPrincipal ReadAccess(string token)
        {
            var json = Read(token, "access");
            if (json == null) return null;
            var id = new ClaimsIdentity("Bearer");
            id.AddClaim(new Claim(ClaimTypes.NameIdentifier, (string)json["sub"]));
            id.AddClaim(new Claim(ClaimTypes.Name, (string)(json["name"] ?? "")));
            var role = (string)(json["role"] ?? "");
            if (!string.IsNullOrEmpty(role))
            {
                id.AddClaim(new Claim(ClaimTypes.Role, role));
                id.AddClaim(new Claim("role", role));
            }
            id.AddClaim(new Claim("tier", (string)(json["tier"] ?? "FREE")));
            id.AddClaim(new Claim("paymentEnabled", json["paymentEnabled"] != null && (bool)json["paymentEnabled"] ? "true" : "false"));
            return new ClaimsPrincipal(id);
        }

        public static string ReadRefreshUserId(string token)
        {
            var json = Read(token, "refresh");
            return json == null ? null : (string)json["sub"];
        }

        static JObject Read(string token, string expectedType)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            var parts = token.Split('.');
            if (parts.Length != 3) return null;
            var signed = parts[0] + "." + parts[1];
            var expected = Sign(signed);
            if (!FixedEquals(expected, parts[2])) return null;
            JObject json;
            try { json = JObject.Parse(Encoding.UTF8.GetString(FromB64(parts[1]))); }
            catch { return null; }
            var exp = json["exp"] != null ? (long)json["exp"] : 0;
            if (exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
            var typ = (string)json["typ"];
            if (!string.Equals(typ, expectedType, StringComparison.Ordinal)) return null;
            return json;
        }

        static string Create(Dictionary<string, object> claims, int seconds)
        {
            claims["exp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + seconds;
            var header = B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
            var payload = B64(Newtonsoft.Json.JsonConvert.SerializeObject(claims));
            var signed = header + "." + payload;
            return signed + "." + Sign(signed);
        }

        static string Sign(string value)
        {
            var secret = ConfigurationManager.AppSettings["JwtSecret"];
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
                throw new InvalidOperationException("JwtSecret is missing. Set a value of at least 32 characters in secrets.config.");
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
                return B64(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        static string B64(string text) { return B64(Encoding.UTF8.GetBytes(text)); }

        static string B64(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        static byte[] FromB64(string text)
        {
            var s = text.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
            return Convert.FromBase64String(s);
        }

        static bool FixedEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
