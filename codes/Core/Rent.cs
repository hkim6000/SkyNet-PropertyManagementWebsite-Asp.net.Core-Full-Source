using PropertyManagement.Models;

namespace PropertyManagement.Core
{
    public sealed class RentData
    {
        public SiteData Site { get; set; } = new SiteData();
        public List<PropertyRec> Properties { get; set; } = new List<PropertyRec>();
        public List<UnitRec> Units { get; set; } = new List<UnitRec>();
        public List<ShowingRec> Showings { get; set; } = new List<ShowingRec>();
        public List<ApplicationRec> Applications { get; set; } = new List<ApplicationRec>();
        public List<LeaseRec> Leases { get; set; } = new List<LeaseRec>();
        public List<ResidentRec> Residents { get; set; } = new List<ResidentRec>();
        public List<LedgerRec> Ledger { get; set; } = new List<LedgerRec>();
        public List<NoticeRec> Notices { get; set; } = new List<NoticeRec>();
        public List<WorkOrderRec> WorkOrders { get; set; } = new List<WorkOrderRec>();
        public List<VendorRec> Vendors { get; set; } = new List<VendorRec>();
        public List<InspectionRec> Inspections { get; set; } = new List<InspectionRec>();
        public List<TurnRec> Turns { get; set; } = new List<TurnRec>();
        public List<OwnerRec> Owners { get; set; } = new List<OwnerRec>();
        public List<OwnershipRec> Ownership { get; set; } = new List<OwnershipRec>();
        public List<ExpenseRec> Expenses { get; set; } = new List<ExpenseRec>();
        public List<PayoutRec> Payouts { get; set; } = new List<PayoutRec>();
        public List<DepositRec> Deposits { get; set; } = new List<DepositRec>();
        public bool CanPay { get; set; }
    }

    public static class Rent
    {
        public static readonly string[] UnitStatuses = { "Vacant", "Occupied", "Notice", "Make-ready", "Offline" };
        public static readonly string[] Live = { "Active", "Notice" };

        public static async Task<RentData> Load()
        {
            await Books.Migrate();
            await Seed.Keep();
            await Billing.Daily();
            return await Read();
        }

        public static async Task<RentData> Read()
        {
            RentData d = new RentData
            {
                Site = await Store.LoadSite(),
                Properties = (await Store.Rows<PropertyRec>("properties")).OrderBy(p => p.Order).ThenBy(p => p.Name).ToList(),
                Units = await Store.Rows<UnitRec>("units"),
                Showings = await Store.Rows<ShowingRec>("showings"),
                Applications = await Store.Rows<ApplicationRec>("applications"),
                Leases = await Store.Rows<LeaseRec>("leases"),
                Residents = await Store.Rows<ResidentRec>("residents"),
                Ledger = await Store.Rows<LedgerRec>("ledger"),
                Notices = await Store.Rows<NoticeRec>("notices"),
                WorkOrders = await Store.Rows<WorkOrderRec>("workorders"),
                Vendors = (await Store.Rows<VendorRec>("vendors")).OrderBy(v => v.Name).ToList(),
                Inspections = await Store.Rows<InspectionRec>("inspections"),
                Turns = await Store.Rows<TurnRec>("turns"),
                Owners = (await Store.Rows<OwnerRec>("owners")).OrderBy(o => o.Name).ToList(),
                Ownership = await Store.Rows<OwnershipRec>("ownership"),
                Expenses = await Store.Rows<ExpenseRec>("expenses"),
                Payouts = await Store.Rows<PayoutRec>("payouts"),
                Deposits = await Store.Rows<DepositRec>("deposits")
            };
            Settle(d);
            return d;
        }

        public static void Settle(RentData d)
        {
            foreach (LeaseRec next in d.Leases.Where(l => l.Status == "Upcoming" && l.RenewalOf.Length > 0 && l.Start.Date <= DateTime.Today).ToList())
            {
                LeaseRec? old = d.Leases.FirstOrDefault(l => l.Id == next.RenewalOf);
                next.Status = "Active";
                next.MovedIn = old?.MovedIn ?? next.Start;
                if (old != null && Live.Contains(old.Status))
                {
                    old.Status = "Ended";
                }
            }
            foreach (UnitRec u in d.Units)
            {
                LeaseRec? live = d.Leases.FirstOrDefault(l => l.UnitId == u.Id && Live.Contains(l.Status));
                string want = live != null ? (live.Status == "Notice" ? "Notice" : "Occupied") : u.Status is "Make-ready" or "Offline" ? u.Status : "Vacant";
                if (want != u.Status)
                {
                    u.Status = want;
                    u.StatusSince = DateTime.Now;
                }
                if (live != null && live.Status == "Notice" && live.MoveOut != null && (u.Available == null || u.Available < live.MoveOut))
                {
                    u.Available = live.MoveOut.Value.Date.AddDays(1);
                }
            }
        }

        public static string PropertyName(RentData d, string id)
        {
            return d.Properties.FirstOrDefault(p => p.Id == id)?.Name ?? "Unknown property";
        }

        public static PropertyRec? PropertyOf(RentData d, string unitId)
        {
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == unitId);
            return u == null ? null : d.Properties.FirstOrDefault(p => p.Id == u.PropertyId);
        }

        public static string UnitLabel(RentData d, string unitId)
        {
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == unitId);
            return u == null ? "Unknown unit" : PropertyName(d, u.PropertyId) + " · " + u.Number;
        }

        public static string Name(ResidentRec r)
        {
            return (r.First + " " + r.Last).Trim();
        }

        public static string Names(RentData d, LeaseRec l)
        {
            List<string> n = l.Residents.Select(id => d.Residents.FirstOrDefault(r => r.Id == id)).Where(r => r != null).Select(r => Name(r!)).ToList();
            return n.Count == 0 ? "No resident" : string.Join(", ", n);
        }

        public static string ApplicantName(ApplicationRec a)
        {
            Applicant? p = a.People.FirstOrDefault();
            return p == null ? "Applicant" : (p.First + " " + p.Last).Trim() + (a.People.Count > 1 ? " +" + (a.People.Count - 1) : string.Empty);
        }

        public static string PropertyIdOfUnit(RentData d, string unitId)
        {
            return d.Units.FirstOrDefault(x => x.Id == unitId)?.PropertyId ?? string.Empty;
        }

        public static decimal Balance(RentData d, string leaseId)
        {
            return d.Ledger.Where(x => x.LeaseId == leaseId).Sum(x => x.Amount);
        }

        public static LeaseRec? LeaseOf(RentData d, string residentId)
        {
            return d.Leases.Where(l => l.Residents.Contains(residentId) && l.Status != "Cancelled").OrderByDescending(l => Live.Contains(l.Status) ? 2 : l.Status == "Upcoming" ? 1 : 0).ThenByDescending(l => l.Start).FirstOrDefault();
        }

        public static LeaseRec? CurrentLease(RentData d, string unitId)
        {
            return d.Leases.FirstOrDefault(l => l.UnitId == unitId && Live.Contains(l.Status));
        }

        public static bool Expiring(LeaseRec l, int days)
        {
            return Live.Contains(l.Status) && l.Status != "Notice" && l.RenewedBy.Length == 0 && l.End >= DateTime.Today && l.End <= DateTime.Today.AddDays(days);
        }

        public static bool Holdover(LeaseRec l)
        {
            return Live.Contains(l.Status) && l.End < DateTime.Today && l.MovedOut == null;
        }

        public static DateTime MoveInDate(UnitRec u)
        {
            return u.Available == null || u.Available.Value.Date < DateTime.Today ? DateTime.Today : u.Available.Value.Date;
        }

        public static bool Offered(UnitRec u)
        {
            return u.Listed && u.Status is "Vacant" or "Notice" or "Make-ready";
        }

        public static bool Overlaps(RentData d, string unitId, DateTime start, DateTime end, string exceptId)
        {
            return d.Leases.Any(l => l.Id != exceptId && l.UnitId == unitId && l.Status is "Upcoming" or "Active" or "Notice" && start <= (l.MoveOut ?? l.End) && end >= l.Start);
        }
    }
}
