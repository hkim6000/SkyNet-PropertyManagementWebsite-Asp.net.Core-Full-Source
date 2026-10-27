using PropertyManagement.Models;

namespace PropertyManagement.Core
{
    public sealed class NewLease
    {
        public string UnitId { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public decimal Rent { get; set; }
        public decimal Deposit { get; set; }
        public string Pets { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public List<Applicant> People { get; set; } = new List<Applicant>();
        public List<string> ResidentIds { get; set; } = new List<string>();
        public string ApplicationId { get; set; } = string.Empty;
        public string RenewalOf { get; set; } = string.Empty;
        public bool Charges { get; set; } = true;
        public string By { get; set; } = string.Empty;
    }

    public static class Leasing
    {
        public static decimal FirstRent(decimal rent, DateTime start)
        {
            int days = DateTime.DaysInMonth(start.Year, start.Month);
            int left = days - start.Day + 1;
            return left >= days ? rent : Math.Round(rent * left / days, 2);
        }

        public static async Task<(string Error, LeaseRec? Lease, List<ResidentRec> Invited)> Create(NewLease n)
        {
            List<ResidentRec> invited = new List<ResidentRec>();
            using (await Store.Hold())
            {
                RentData d = await Rent.Read();
                UnitRec? u = d.Units.FirstOrDefault(x => x.Id == n.UnitId);
                if (u == null)
                {
                    return ("That unit was not found.", null, invited);
                }
                if (n.End <= n.Start)
                {
                    return ("The end date must be after the start date.", null, invited);
                }
                if (Rent.Overlaps(d, u.Id, n.Start, n.End, n.RenewalOf))
                {
                    return ("Another lease on " + Rent.UnitLabel(d, u.Id) + " covers some of these dates.", null, invited);
                }
                List<string> ids = new List<string>(n.ResidentIds.Where(id => d.Residents.Any(r => r.Id == id)));
                foreach (Applicant p in n.People)
                {
                    ResidentRec? r = d.Residents.FirstOrDefault(x => x.Email.Equals(p.Email, StringComparison.OrdinalIgnoreCase));
                    if (r == null)
                    {
                        r = new ResidentRec { Id = Fmt.NewId("r-"), First = p.First, Last = p.Last, Email = p.Email, Phone = p.Phone, Invite = Fmt.Code(string.Empty, d.Residents.Select(x => x.Invite)), Created = DateTime.Now };
                        d.Residents.Add(r);
                        invited.Add(r);
                    }
                    else if (r.Phone.Length == 0)
                    {
                        r.Phone = p.Phone;
                    }
                    if (!ids.Contains(r.Id))
                    {
                        ids.Add(r.Id);
                    }
                }
                if (ids.Count == 0)
                {
                    return ("Add at least one resident to the lease.", null, invited);
                }
                LeaseRec l = new LeaseRec { Id = Fmt.NewId("l-"), Code = Fmt.Code("L-", d.Leases.Select(x => x.Code)), UnitId = u.Id, Residents = ids, Start = n.Start.Date, End = n.End.Date, Rent = n.Rent, Deposit = n.Deposit, Status = "Upcoming", ApplicationId = n.ApplicationId, RenewalOf = n.RenewalOf, Pets = n.Pets, Note = n.Note, Created = DateTime.Now, By = n.By };
                d.Leases.Add(l);
                if (n.RenewalOf.Length > 0)
                {
                    LeaseRec? old = d.Leases.FirstOrDefault(x => x.Id == n.RenewalOf);
                    if (old != null)
                    {
                        old.RenewedBy = l.Id;
                    }
                }
                if (n.Charges)
                {
                    if (n.Deposit > 0)
                    {
                        d.Ledger.Add(new LedgerRec { Id = Fmt.NewId("g-"), LeaseId = l.Id, When = l.Start, Kind = "Deposit", Label = "Security deposit", Amount = n.Deposit, By = n.By });
                    }
                    decimal first = FirstRent(n.Rent, l.Start);
                    d.Ledger.Add(new LedgerRec { Id = Fmt.NewId("g-"), LeaseId = l.Id, When = l.Start, Kind = "Rent", Label = "Rent " + l.Start.ToString("MMMM yyyy", Fmt.Us) + (first < n.Rent ? " (from " + Fmt.Short(l.Start) + ")" : string.Empty), Amount = first, By = n.By });
                }
                if (n.ApplicationId.Length > 0)
                {
                    ApplicationRec? a = d.Applications.FirstOrDefault(x => x.Id == n.ApplicationId);
                    if (a != null)
                    {
                        a.Status = "Leased";
                        a.LeaseId = l.Id;
                    }
                    await Store.SaveRows("applications", d.Applications);
                }
                await Store.SaveRows("residents", d.Residents);
                await Store.SaveRows("leases", d.Leases);
                await Store.SaveRows("ledger", d.Ledger);
                return (string.Empty, l, invited);
            }
        }
    }
}
