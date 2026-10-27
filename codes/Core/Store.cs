using System.Text.Encodings.Web;
using System.Text.Json;
using PropertyManagement.Models;

namespace PropertyManagement.Core
{
    public static class Store
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim LogGate = new SemaphoreSlim(1, 1);
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        private static DateTime configStamp = DateTime.MinValue;
        private static bool demoMode = true;

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim gate;

            public Releaser(SemaphoreSlim gate)
            {
                this.gate = gate;
            }

            public void Dispose()
            {
                gate.Release();
            }
        }

        public static string Root => Directory.GetCurrentDirectory();

        public static string DataDir => Path.Combine(Root, "data");

        public static string ConfigFile => Path.Combine(Root, "appConfig", "application.cfg");

        public static string SessionFile => Path.Combine(Root, "temp", "admin", "sessions.json");

        public static string StaffFile => Path.Combine(DataDir, "staff.json");

        public static string InitialFile => Path.Combine(Root, "appConfig", "initial-admin.txt");

        public static async Task<IDisposable> Hold()
        {
            await Gate.WaitAsync();
            return new Releaser(Gate);
        }

        public static async Task<T> Read<T>(string file, Func<T> make)
        {
            if (!File.Exists(file))
            {
                return make();
            }
            try
            {
                string json = await File.ReadAllTextAsync(file);
                return JsonSerializer.Deserialize<T>(json, Opts) ?? make();
            }
            catch (JsonException)
            {
                return make();
            }
        }

        public static async Task Write<T>(string file, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? Root);
            string tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(value, Opts));
            File.Move(tmp, file, true);
        }

        public static async Task<SiteData> LoadSite()
        {
            SiteData site = await Read(Path.Combine(DataDir, "site.json"), () => new SiteData());
            Blocks.Ensure(site);
            return site;
        }

        public static async Task<PublicData> LoadPublic()
        {
            PublicData d = new PublicData();
            d.Site = await LoadSite();
            d.Media = await Media.Load();
            d.Properties = (await Rows<PropertyRec>("properties")).Where(p => p.Show).OrderBy(p => p.Order).ThenBy(p => p.Name).ToList();
            d.Units = (await Rows<UnitRec>("units")).Where(u => d.Properties.Any(p => p.Id == u.PropertyId)).ToList();
            List<LeaseRec> leases = await Rows<LeaseRec>("leases");
            foreach (UnitRec u in d.Units)
            {
                LeaseRec? live = leases.FirstOrDefault(l => l.UnitId == u.Id && Rent.Live.Contains(l.Status));
                u.Status = live != null ? (live.Status == "Notice" ? "Notice" : "Occupied") : u.Status is "Make-ready" or "Offline" ? u.Status : "Vacant";
                if (live != null && live.Status == "Notice" && live.MoveOut != null && (u.Available == null || u.Available < live.MoveOut))
                {
                    u.Available = live.MoveOut.Value.Date.AddDays(1);
                }
                if (leases.Any(l => l.UnitId == u.Id && l.Status == "Upcoming"))
                {
                    u.Listed = false;
                }
            }
            return d;
        }

        public static Task SaveSite(SiteData site)
        {
            return Write(Path.Combine(DataDir, "site.json"), site);
        }

        public static Task<List<T>> Rows<T>(string name)
        {
            return Read(Path.Combine(DataDir, name + ".json"), () => new List<T>());
        }

        public static Task SaveRows<T>(string name, List<T> rows)
        {
            return Write(Path.Combine(DataDir, name + ".json"), rows);
        }

        public static async Task Log(string userId, string action, string target, bool ok)
        {
            await LogGate.WaitAsync();
            try
            {
                List<ActivityRec> list = await Rows<ActivityRec>("activity");
                list.Insert(0, new ActivityRec { When = DateTime.UtcNow, UserId = userId, Action = action, Target = target, Ok = ok });
                if (list.Count > 5000)
                {
                    list.RemoveRange(5000, list.Count - 5000);
                }
                await SaveRows("activity", list);
            }
            finally
            {
                LogGate.Release();
            }
        }

        public static async Task<string> Who(string id)
        {
            switch (id)
            {
                case "system": return "System";
                case "visitor": return "Website visitor";
            }
            List<AdminUser> staff = await LoadStaff();
            return staff.FirstOrDefault(s => s.Id == id)?.Name ?? id;
        }

        public static string ConfigValue(string key)
        {
            if (!File.Exists(ConfigFile))
            {
                return string.Empty;
            }
            foreach (string raw in File.ReadLines(ConfigFile))
            {
                string line = raw.Trim();
                if (line.StartsWith(key + " ", StringComparison.Ordinal) || line.StartsWith(key + "|", StringComparison.Ordinal))
                {
                    string[] part = line.Split('|', 2);
                    return part.Length == 2 ? part[1].Trim() : string.Empty;
                }
            }
            return string.Empty;
        }

        public static bool Demo()
        {
            DateTime stamp = File.Exists(ConfigFile) ? File.GetLastWriteTimeUtc(ConfigFile) : DateTime.MinValue;
            if (stamp != configStamp)
            {
                demoMode = ConfigValue("app.app.runmode") != "1";
                configStamp = stamp;
            }
            return demoMode;
        }

        public static Task<List<AdminUser>> LoadStaff()
        {
            return Read(StaffFile, () => new List<AdminUser>());
        }

        public static Task SaveStaff(List<AdminUser> staff)
        {
            return Write(StaffFile, staff);
        }

        public static async Task EnsureFirstManager()
        {
            if (Demo())
            {
                return;
            }
            using (await Hold())
            {
                List<AdminUser> staff = await LoadStaff();
                if (staff.Any(u => u.Manager && u.Active && !u.MustChange && u.Hash != string.Empty))
                {
                    return;
                }
                if (File.Exists(InitialFile) && staff.Any(u => u.MustChange))
                {
                    return;
                }
                string pass = Auth.NewPassword();
                AdminUser? u = staff.FirstOrDefault(x => x.Login == "admin");
                if (u == null)
                {
                    u = new AdminUser { Id = "u-admin", Login = "admin", Name = "Administrator", Title = "Manager", Manager = true, AllProperties = true };
                    staff.Insert(0, u);
                }
                u.MustChange = true;
                u.Active = true;
                u.Manager = true;
                u.Hash = Auth.HashPassword(pass);
                await SaveStaff(staff);
                Directory.CreateDirectory(Path.GetDirectoryName(InitialFile) ?? Root);
                await File.WriteAllTextAsync(InitialFile, "user: admin" + Environment.NewLine + "password: " + pass + Environment.NewLine);
            }
        }

        public static string InitialPassword()
        {
            if (!File.Exists(InitialFile))
            {
                return string.Empty;
            }
            string line = File.ReadAllLines(InitialFile).FirstOrDefault(l => l.StartsWith("password: ", StringComparison.Ordinal)) ?? string.Empty;
            return line.Length > 10 ? line.Substring(10).Trim() : string.Empty;
        }
    }
}
