using PropertyManagement.Models;

namespace PropertyManagement.Core
{
    public static class Billing
    {
        private static DateTime checkedDay = DateTime.MinValue;

        public static readonly string[] ChargeKinds = { "Charge", "Utility", "Parking", "Pet fee", "Key or lock", "Cleaning", "Damage" };
        public static readonly string[] Methods = { "Check", "Card", "Bank transfer", "Cash", "Money order" };

        public static bool IsCharge(LedgerRec x)
        {
            return x.Amount > 0;
        }

        public static DateTime DueDate(SiteData s, int year, int month)
        {
            return new DateTime(year, month, Math.Min(Math.Max(1, s.RentDueDay), DateTime.DaysInMonth(year, month)));
        }

        public static DateTime LateDate(SiteData s, DateTime due)
        {
            return due.AddDays(Math.Max(1, s.GraceDays));
        }

        public static async Task Daily()
        {
            if (checkedDay == DateTime.Today)
            {
                return;
            }
            await Post("system");
            checkedDay = DateTime.Today;
        }

        public static async Task<int> Post(string by)
        {
            int added = 0;
            using (await Store.Hold())
            {
                RentData d = await Rent.Read();
                DateTime today = DateTime.Today;
                foreach (LeaseRec l in d.Leases.Where(x => x.Status is "Active" or "Notice"))
                {
                    List<LedgerRec> mine = d.Ledger.Where(x => x.LeaseId == l.Id).ToList();
                    DateTime first = new DateTime(l.Start.Year, l.Start.Month, 1);
                    DateTime? firstRent = mine.Where(x => x.Kind == "Rent" && x.Amount > 0).Select(x => (DateTime?)x.When).Min();
                    if (firstRent != null && firstRent.Value > first)
                    {
                        first = new DateTime(firstRent.Value.Year, firstRent.Value.Month, 1);
                    }
                    DateTime stop = l.MoveOut ?? (Rent.Holdover(l) ? DateTime.MaxValue.Date : l.End.Date);
                    for (DateTime m = first; m <= today; m = m.AddMonths(1))
                    {
                        DateTime monthEnd = m.AddMonths(1).AddDays(-1);
                        if (m > stop || monthEnd < l.Start.Date)
                        {
                            continue;
                        }
                        DateTime due = DueDate(d.Site, m.Year, m.Month);
                        DateTime when = l.Start.Date > due ? l.Start.Date : due;
                        if (when > today)
                        {
                            continue;
                        }
                        if (mine.Any(x => x.Kind == "Rent" && x.Amount > 0 && x.When.Year == m.Year && x.When.Month == m.Month))
                        {
                            continue;
                        }
                        int days = DateTime.DaysInMonth(m.Year, m.Month);
                        DateTime from = l.Start.Date > m ? l.Start.Date : m;
                        DateTime to = stop < monthEnd ? stop : monthEnd;
                        int used = (to - from).Days + 1;
                        decimal amount = used >= days ? l.Rent : Math.Round(l.Rent * used / days, 2);
                        if (amount <= 0)
                        {
                            continue;
                        }
                        LedgerRec g = new LedgerRec { Id = Fmt.NewId("g-"), LeaseId = l.Id, When = when, Kind = "Rent", Label = "Rent " + m.ToString("MMMM yyyy", Fmt.Us) + (used < days ? " (" + Fmt.Short(from) + " to " + Fmt.Short(to) + ")" : string.Empty), Amount = amount, By = by };
                        d.Ledger.Add(g);
                        mine.Add(g);
                        added++;
                    }
                    if (d.Site.LateFee > 0)
                    {
                        foreach (DateTime m in new[] { new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1) })
                        {
                            DateTime late = LateDate(d.Site, DueDate(d.Site, m.Year, m.Month));
                            if (late > today || !mine.Any(x => x.Kind == "Rent" && x.Amount > 0 && x.When.Year == m.Year && x.When.Month == m.Month))
                            {
                                continue;
                            }
                            if (mine.Any(x => x.Kind == "Late fee" && x.When.Year == m.Year && x.When.Month == m.Month))
                            {
                                continue;
                            }
                            decimal owed = mine.Where(x => x.When.Date < late).Sum(x => x.Amount);
                            if (owed <= 0.004m)
                            {
                                continue;
                            }
                            LedgerRec g = new LedgerRec { Id = Fmt.NewId("g-"), LeaseId = l.Id, When = late, Kind = "Late fee", Label = "Late fee " + m.ToString("MMMM yyyy", Fmt.Us), Amount = d.Site.LateFee, By = by };
                            d.Ledger.Add(g);
                            mine.Add(g);
                            added++;
                        }
                    }
                }
                if (added > 0)
                {
                    await Store.SaveRows("ledger", d.Ledger);
                }
            }
            if (added > 0)
            {
                await Store.Log(by, "post rent and late fees", Ui.Count(added, "charge", "charges"), true);
            }
            return added;
        }

        public sealed class Slice
        {
            public LedgerRec Charge { get; init; } = new LedgerRec();
            public LedgerRec Payer { get; init; } = new LedgerRec();
            public decimal Amount { get; init; }
        }

        public static List<Slice> Allocate(IEnumerable<LedgerRec> lines)
        {
            List<LedgerRec> all = lines.OrderBy(x => x.When).ThenBy(x => x.Amount < 0 ? 1 : 0).ToList();
            List<(LedgerRec Line, decimal Left)> open = new List<(LedgerRec, decimal)>();
            List<(LedgerRec Line, decimal Left)> spare = new List<(LedgerRec, decimal)>();
            List<Slice> slices = new List<Slice>();
            foreach (LedgerRec x in all)
            {
                if (x.Amount > 0)
                {
                    decimal left = x.Amount;
                    while (left > 0 && spare.Count > 0)
                    {
                        (LedgerRec p, decimal have) = spare[0];
                        decimal take = Math.Min(left, have);
                        slices.Add(new Slice { Charge = x, Payer = p, Amount = take });
                        left -= take;
                        have -= take;
                        if (have <= 0)
                        {
                            spare.RemoveAt(0);
                        }
                        else
                        {
                            spare[0] = (p, have);
                        }
                    }
                    if (left > 0)
                    {
                        open.Add((x, left));
                    }
                }
                else if (x.Amount < 0)
                {
                    decimal have = -x.Amount;
                    while (have > 0 && open.Count > 0)
                    {
                        (LedgerRec ch, decimal left) = open[0];
                        decimal take = Math.Min(left, have);
                        slices.Add(new Slice { Charge = ch, Payer = x, Amount = take });
                        left -= take;
                        have -= take;
                        if (left <= 0)
                        {
                            open.RemoveAt(0);
                        }
                        else
                        {
                            open[0] = (ch, left);
                        }
                    }
                    if (have > 0)
                    {
                        spare.Add((x, have));
                    }
                }
            }
            return slices;
        }

        public static (decimal A, decimal B, decimal C, decimal D) Aging(IEnumerable<LedgerRec> lines)
        {
            List<LedgerRec> list = lines.ToList();
            List<Slice> paid = Allocate(list);
            decimal a = 0, b = 0, c = 0, e = 0;
            DateTime today = DateTime.Today;
            foreach (LedgerRec x in list.Where(x => x.Amount > 0))
            {
                decimal left = x.Amount - paid.Where(s => s.Charge.Id == x.Id).Sum(s => s.Amount);
                if (left <= 0)
                {
                    continue;
                }
                int age = (today - x.When.Date).Days;
                if (age <= 30)
                {
                    a += left;
                }
                else if (age <= 60)
                {
                    b += left;
                }
                else if (age <= 90)
                {
                    c += left;
                }
                else
                {
                    e += left;
                }
            }
            return (a, b, c, e);
        }

        public static bool Cash(LedgerRec x)
        {
            return x.Amount < 0 && x.Kind is "Payment" or "Deposit applied";
        }

        public static bool OwnerIncome(LedgerRec charge)
        {
            return charge.Kind is not ("Deposit" or "Refund");
        }

        public static string PropertyOfLease(RentData d, string leaseId)
        {
            LeaseRec? l = d.Leases.FirstOrDefault(x => x.Id == leaseId);
            return l == null ? string.Empty : Rent.PropertyIdOfUnit(d, l.UnitId);
        }
    }
}
