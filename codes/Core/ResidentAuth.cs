using System.Security.Cryptography;
using PropertyManagement.Models;

namespace PropertyManagement.Core
{
    public static class ResidentAuth
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

        public static string SessionFile => Path.Combine(Store.Root, "temp", "portal", "sessions.json");

        private static async Task<List<SessionRec>> Sessions()
        {
            List<SessionRec> list = await Store.Read(SessionFile, () => new List<SessionRec>());
            list.RemoveAll(s => s.Expires < DateTime.UtcNow);
            return list;
        }

        private static async Task<string> Open(string residentId)
        {
            await Gate.WaitAsync();
            try
            {
                List<SessionRec> list = await Sessions();
                string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                list.Add(new SessionRec { Token = token, UserId = residentId, Expires = DateTime.UtcNow.AddHours(12) });
                await Store.Write(SessionFile, list);
                return token;
            }
            finally
            {
                Gate.Release();
            }
        }

        public static async Task CloseAll(string residentId)
        {
            await Gate.WaitAsync();
            try
            {
                List<SessionRec> list = await Sessions();
                list.RemoveAll(s => s.UserId == residentId);
                await Store.Write(SessionFile, list);
            }
            finally
            {
                Gate.Release();
            }
        }

        public static async Task SignOut(string? tok)
        {
            await Gate.WaitAsync();
            try
            {
                List<SessionRec> list = await Sessions();
                list.RemoveAll(s => s.Token == (tok ?? string.Empty));
                await Store.Write(SessionFile, list);
            }
            finally
            {
                Gate.Release();
            }
        }

        public static async Task<ResidentRec?> Check(string? tok)
        {
            string t = (tok ?? string.Empty).Trim();
            if (t.Length < 16 || t.Length > 64)
            {
                return null;
            }
            List<SessionRec> list = await Sessions();
            SessionRec? s = list.FirstOrDefault(x => x.Token == t);
            if (s == null)
            {
                return null;
            }
            List<ResidentRec> rs = await Store.Rows<ResidentRec>("residents");
            return rs.FirstOrDefault(r => r.Id == s.UserId);
        }

        private static string Key(string email)
        {
            return "r:" + email.Trim().ToLowerInvariant();
        }

        public static async Task<(string Error, string Token)> SignIn(string email, string pass)
        {
            string k = Key(email);
            if (Auth.Paused(k))
            {
                return ("Too many tries. Wait a few minutes and try again.", string.Empty);
            }
            using (await Store.Hold())
            {
                List<ResidentRec> rs = await Store.Rows<ResidentRec>("residents");
                ResidentRec? r = rs.FirstOrDefault(x => x.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));
                bool ok = r != null && (Store.Demo() || (r.Hash.Length > 0 && Auth.VerifyPassword(pass, r.Hash)));
                if (!ok || r == null)
                {
                    Auth.Failed(k);
                    return (r != null && r.Hash.Length == 0 && !Store.Demo() ? "This account is not active yet. Use Activate my account with the code from the office." : "The email or password is not right.", string.Empty);
                }
                r.LastSeen = DateTime.Now;
                await Store.SaveRows("residents", rs);
                return (string.Empty, await Open(r.Id));
            }
        }

        public static async Task<(string Error, string Token)> Activate(string email, string code, string pass, string again)
        {
            string k = Key(email);
            if (Auth.Paused(k))
            {
                return ("Too many tries. Wait a few minutes and try again.", string.Empty);
            }
            string weak = Auth.Weak(pass, string.Empty, again);
            using (await Store.Hold())
            {
                List<ResidentRec> rs = await Store.Rows<ResidentRec>("residents");
                ResidentRec? r = rs.FirstOrDefault(x => x.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));
                if (r == null || r.Invite.Length == 0 || !r.Invite.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    Auth.Failed(k);
                    return ("The email or activation code is not right. Ask the office for a new code.", string.Empty);
                }
                if (weak.Length > 0)
                {
                    return (weak, string.Empty);
                }
                r.Hash = Auth.HashPassword(pass);
                r.Invite = string.Empty;
                r.LastSeen = DateTime.Now;
                await Store.SaveRows("residents", rs);
                return (string.Empty, await Open(r.Id));
            }
        }

        public static async Task<(ResidentRec? Resident, string Token)> StartReset(string email)
        {
            using (await Store.Hold())
            {
                List<ResidentRec> rs = await Store.Rows<ResidentRec>("residents");
                ResidentRec? r = rs.FirstOrDefault(x => x.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase) && x.Hash.Length > 0);
                if (r == null)
                {
                    return (null, string.Empty);
                }
                r.ResetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();
                r.ResetUntil = DateTime.Now.AddHours(1);
                await Store.SaveRows("residents", rs);
                return (r, r.ResetToken);
            }
        }

        public static async Task<bool> ResetValid(string token)
        {
            if (token.Length < 20)
            {
                return false;
            }
            List<ResidentRec> rs = await Store.Rows<ResidentRec>("residents");
            return rs.Any(x => x.ResetToken == token && x.ResetUntil > DateTime.Now);
        }

        public static async Task<(string Error, string Token)> Reset(string token, string pass, string again)
        {
            string weak = Auth.Weak(pass, string.Empty, again);
            if (weak.Length > 0)
            {
                return (weak, string.Empty);
            }
            string id;
            using (await Store.Hold())
            {
                List<ResidentRec> rs = await Store.Rows<ResidentRec>("residents");
                ResidentRec? r = token.Length < 20 ? null : rs.FirstOrDefault(x => x.ResetToken == token && x.ResetUntil > DateTime.Now);
                if (r == null)
                {
                    return ("This link has expired. Ask for a new one.", string.Empty);
                }
                r.Hash = Auth.HashPassword(pass);
                r.ResetToken = string.Empty;
                r.ResetUntil = null;
                id = r.Id;
                await Store.SaveRows("residents", rs);
            }
            await CloseAll(id);
            return (string.Empty, await Open(id));
        }

        public static async Task<string> ChangeOwn(string residentId, string current, string next, string again)
        {
            string weak = Auth.Weak(next, current, again);
            using (await Store.Hold())
            {
                List<ResidentRec> rs = await Store.Rows<ResidentRec>("residents");
                ResidentRec? r = rs.FirstOrDefault(x => x.Id == residentId);
                if (r == null || !Auth.VerifyPassword(current, r.Hash))
                {
                    return "The current password is not right.";
                }
                if (weak.Length > 0)
                {
                    return weak;
                }
                r.Hash = Auth.HashPassword(next);
                await Store.SaveRows("residents", rs);
            }
            return string.Empty;
        }
    }
}
