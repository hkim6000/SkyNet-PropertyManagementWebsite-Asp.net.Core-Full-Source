using PropertyManagement.Models;

namespace PropertyManagement.Core
{
    public sealed class Area
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Page { get; init; } = string.Empty;
        public string Group { get; init; } = string.Empty;
        public string Letters { get; init; } = "VAED";
        public bool Scoped { get; init; }
    }

    public static class Access
    {
        public static readonly Area[] Areas =
        {
            new Area { Key = "properties", Name = "Properties", Page = "AdminProperties", Group = "Properties", Scoped = true },
            new Area { Key = "units", Name = "Units", Page = "AdminUnits", Group = "Properties", Scoped = true },
            new Area { Key = "showings", Name = "Showings", Page = "AdminShowings", Group = "Leasing", Scoped = true },
            new Area { Key = "applications", Name = "Applications", Page = "AdminApplications", Group = "Leasing", Scoped = true },
            new Area { Key = "leases", Name = "Leases", Page = "AdminLeases", Group = "Leasing", Scoped = true },
            new Area { Key = "residents", Name = "Residents", Page = "AdminResidents", Group = "Leasing", Scoped = true },
            new Area { Key = "workorders", Name = "Work orders", Page = "AdminWorkOrders", Group = "Maintenance", Scoped = true },
            new Area { Key = "turns", Name = "Make-ready", Page = "AdminTurns", Group = "Maintenance", Letters = "VE", Scoped = true },
            new Area { Key = "inspections", Name = "Inspections", Page = "AdminInspections", Group = "Maintenance", Scoped = true },
            new Area { Key = "vendors", Name = "Vendors", Page = "AdminVendors", Group = "Maintenance" },
            new Area { Key = "payments", Name = "Rent and payments", Page = "AdminPayments", Group = "Money", Scoped = true },
            new Area { Key = "expenses", Name = "Expenses", Page = "AdminExpenses", Group = "Money", Scoped = true },
            new Area { Key = "reports", Name = "Reports", Page = "AdminReports", Group = "Money", Letters = "V", Scoped = true },
            new Area { Key = "owners", Name = "Owners", Page = "AdminOwners", Group = "Ownership & payouts" },
            new Area { Key = "ownership", Name = "Ownership", Page = "AdminOwnership", Group = "Ownership & payouts", Letters = "VED", Scoped = true },
            new Area { Key = "payouts", Name = "Payouts", Page = "AdminPayouts", Group = "Ownership & payouts", Letters = "VAD" },
            new Area { Key = "pages", Name = "Pages", Page = "AdminPages", Group = "Website", Letters = "VE" },
            new Area { Key = "media", Name = "Media library", Page = "AdminMedia", Group = "Website" },
            new Area { Key = "messages", Name = "Messages", Page = "AdminMessages", Group = "Website", Letters = "VED" },
            new Area { Key = "notices", Name = "Notices", Page = "AdminNotices", Group = "Website" },
            new Area { Key = "staff", Name = "Staff", Page = "AdminStaff", Group = "Admin" },
            new Area { Key = "settings", Name = "Settings", Page = "AdminSettings", Group = "Admin", Letters = "VE" },
            new Area { Key = "activity", Name = "Activity log", Page = "AdminActivity", Group = "Admin", Letters = "V" }
        };

        public static readonly (char Letter, string Name)[] Rights = { ('V', "View"), ('A', "Add"), ('E', "Edit"), ('D', "Delete") };

        public static Area? Find(string key)
        {
            return Areas.FirstOrDefault(a => a.Key == key);
        }

        public static string Level(AdminUser u, string area)
        {
            Area? a = Find(area);
            if (a == null || !u.Active)
            {
                return string.Empty;
            }
            if (u.Manager)
            {
                return a.Letters;
            }
            return u.Perms.TryGetValue(area, out string? v) ? Clean(a, v) : string.Empty;
        }

        public static bool Can(AdminUser u, string area, char letter)
        {
            return Level(u, area).Contains(letter);
        }

        public static string Clean(Area a, string? v)
        {
            string s = new string((v ?? string.Empty).ToUpperInvariant().Where(ch => a.Letters.Contains(ch)).Distinct().ToArray());
            if (s.Length > 0 && !s.Contains('V'))
            {
                s = "V" + s;
            }
            return new string(Rights.Select(r => r.Letter).Where(ch => s.Contains(ch)).ToArray());
        }

        public static Dictionary<string, string> CleanAll(Dictionary<string, string> raw)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach (Area a in Areas)
            {
                string v = Clean(a, raw.TryGetValue(a.Key, out string? x) ? x : string.Empty);
                if (v.Length > 0)
                {
                    d[a.Key] = v;
                }
            }
            return d;
        }

        public static bool PropertyOk(AdminUser u, string propertyId)
        {
            return u.Manager || u.AllProperties || u.Properties.Contains(propertyId);
        }

        public static List<string> PropertyIds(AdminUser u, List<PropertyRec> all)
        {
            return all.Where(p => PropertyOk(u, p.Id)).Select(p => p.Id).ToList();
        }

        public static string Describe(AdminUser u, List<PropertyRec> all)
        {
            if (u.Manager || u.AllProperties)
            {
                return "All properties";
            }
            List<string> names = all.Where(p => u.Properties.Contains(p.Id)).Select(p => p.Name).ToList();
            return names.Count == 0 ? "No property" : string.Join(", ", names);
        }
    }
}
