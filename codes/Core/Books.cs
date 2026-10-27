using PropertyManagement.Models;

namespace PropertyManagement.Core
{
    public sealed class StatementLine
    {
        public string PropertyId { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public decimal Share { get; set; } = 100m;
        public bool Spread { get; set; }
        public decimal Amount { get; set; }
    }

    public sealed class Statement
    {
        public OwnerRec Owner { get; set; } = new OwnerRec();
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public decimal Fee { get; set; }
        public List<string> PropertyIds { get; set; } = new List<string>();
        public List<StatementLine> Income { get; set; } = new List<StatementLine>();
        public List<StatementLine> Expenses { get; set; } = new List<StatementLine>();
        public List<StatementLine> Prepaid { get; set; } = new List<StatementLine>();
        public decimal RentIncome { get; set; }
        public decimal TotalIncome => Income.Sum(x => x.Amount);
        public decimal TotalExpenses => Expenses.Sum(x => x.Amount);
        public decimal TotalPrepaid => Prepaid.Sum(x => x.Amount);
        public decimal FeeAmount => Math.Round(RentIncome * Fee / 100m, 2);
        public decimal Net => TotalIncome - TotalExpenses - FeeAmount;
        public decimal PaidAhead { get; set; }
        public decimal HeldAhead { get; set; }
        public decimal Paid { get; set; }
        public decimal Due => Net - PaidAhead - Paid;
    }

    public static class Books
    {
        public static readonly string[] ExpenseKinds = { "Repairs", "Maintenance", "Utilities", "Insurance", "Property tax", "Landscaping", "Cleaning", "Supplies", "Legal and fees", "Other" };

        private static bool migrated;

        public static string Month(DateTime d)
        {
            return d.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool ParseMonth(string? v, out DateTime m)
        {
            bool ok = DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out m);
            return ok && m.Year > 2000 && m.Year < 2100;
        }

        public static string Pct(decimal v)
        {
            return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        public static async Task Migrate()
        {
            if (migrated)
            {
                return;
            }
            using (await Store.Hold())
            {
                string file = Path.Combine(Store.DataDir, "ownership.json");
                List<OwnerRec> owners = await Store.Rows<OwnerRec>("owners");
                if (!File.Exists(file))
                {
                    List<OwnershipRec> rows = new List<OwnershipRec>();
                    foreach (OwnerRec o in owners)
                    {
                        foreach (string pid in (o.Properties ?? new List<string>()).Distinct())
                        {
                            decimal share = o.Shares != null && o.Shares.TryGetValue(pid, out decimal v) && v > 0 && v <= 100 ? v : 100m;
                            rows.Add(new OwnershipRec { Id = Fmt.NewId("w-"), PropertyId = pid, OwnerId = o.Id, Share = share, Reason = "Moved from the owner record", By = "system", Created = DateTime.Now });
                        }
                    }
                    await Store.SaveRows("ownership", rows);
                }
                List<OwnershipRec> stakes = await Store.Rows<OwnershipRec>("ownership");
                if (stakes.Any(x => x.From == null))
                {
                    List<UnitRec> units = await Store.Rows<UnitRec>("units");
                    List<LeaseRec> leases = await Store.Rows<LeaseRec>("leases");
                    List<LedgerRec> ledger = await Store.Rows<LedgerRec>("ledger");
                    foreach (OwnershipRec x in stakes.Where(x => x.From == null))
                    {
                        HashSet<string> unitIds = new HashSet<string>(units.Where(u => u.PropertyId == x.PropertyId).Select(u => u.Id));
                        HashSet<string> leaseIds = new HashSet<string>(leases.Where(l => unitIds.Contains(l.UnitId)).Select(l => l.Id));
                        DateTime? first = ledger.Where(g => leaseIds.Contains(g.LeaseId)).Select(g => (DateTime?)g.When.Date).Min();
                        DateTime start = first ?? (x.Created == default ? DateTime.Today : x.Created.Date);
                        x.From = new DateTime(start.Year, start.Month, 1);
                    }
                    await Store.SaveRows("ownership", stakes);
                }
                if (owners.Any(o => o.Properties != null || o.Shares != null))
                {
                    foreach (OwnerRec o in owners)
                    {
                        o.Properties = null;
                        o.Shares = null;
                    }
                    await Store.SaveRows("owners", owners);
                }
            }
            migrated = true;
        }

        public static bool Covers(OwnershipRec x, DateTime day)
        {
            return (x.From == null || x.From.Value.Date <= day.Date) && (x.Until == null || x.Until.Value.Date >= day.Date);
        }

        public static bool Overlaps(OwnershipRec x, DateTime from, DateTime to)
        {
            return (x.From == null || x.From.Value.Date <= to.Date) && (x.Until == null || x.Until.Value.Date >= from.Date);
        }

        public static decimal ShareOn(RentData d, string ownerId, string propertyId, DateTime day)
        {
            return d.Ownership.Where(x => x.OwnerId == ownerId && x.PropertyId == propertyId && Covers(x, day)).Sum(x => x.Share);
        }

        public static List<(string OwnerId, decimal Share)> Holders(RentData d, string propertyId, DateTime day)
        {
            List<(string OwnerId, decimal Share)> list = d.Ownership.Where(x => x.PropertyId == propertyId && Covers(x, day)).GroupBy(x => x.OwnerId).Select(g => (g.Key, g.Sum(x => x.Share))).Where(x => x.Item2 > 0).OrderByDescending(x => x.Item2).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
            decimal free = 100m - list.Sum(x => x.Share);
            if (free > 0)
            {
                list.Add((string.Empty, free));
            }
            return list;
        }

        private static string Key(List<(string OwnerId, decimal Share)> h)
        {
            return string.Join("|", h.Select(x => x.OwnerId + ":" + x.Share.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        public static List<(string PropertyId, decimal Share)> HoldingsOn(RentData d, string ownerId, DateTime day)
        {
            return d.Ownership.Where(x => x.OwnerId == ownerId && Covers(x, day)).GroupBy(x => x.PropertyId).Select(g => (g.Key, g.Sum(x => x.Share))).Where(x => x.Item2 > 0).OrderBy(x => d.Properties.FindIndex(p => p.Id == x.Key)).ToList();
        }

        public static decimal HeldOn(RentData d, string propertyId, DateTime day)
        {
            return d.Ownership.Where(x => x.PropertyId == propertyId && Covers(x, day)).Sum(x => x.Share);
        }

        public static List<(DateTime From, DateTime To, decimal Share)> Periods(RentData d, string ownerId, string propertyId, DateTime from, DateTime to)
        {
            List<(DateTime From, DateTime To, decimal Share)> list = new List<(DateTime, DateTime, decimal)>();
            for (DateTime day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                decimal s = ShareOn(d, ownerId, propertyId, day);
                if (list.Count > 0 && list[list.Count - 1].Share == s && list[list.Count - 1].To == day.AddDays(-1))
                {
                    list[list.Count - 1] = (list[list.Count - 1].From, day, s);
                }
                else
                {
                    list.Add((day, day, s));
                }
            }
            return list.Where(x => x.Share > 0).ToList();
        }

        public static (DateTime From, DateTime To) Cover(LedgerRec charge, LeaseRec lease)
        {
            DateTime first = new DateTime(charge.When.Year, charge.When.Month, 1);
            DateTime last = first.AddMonths(1).AddDays(-1);
            DateTime from = lease.Start.Date > first && lease.Start.Date <= last ? lease.Start.Date : first;
            DateTime to = last;
            DateTime? stop = lease.MoveOut?.Date;
            if (stop == null && charge.Amount < lease.Rent)
            {
                stop = lease.End.Date;
            }
            if (stop != null && stop.Value >= from && stop.Value < to)
            {
                to = stop.Value;
            }
            return (from, to);
        }

        private static string IncomeLabel(LedgerRec charge)
        {
            return charge.Kind == "Late fee" ? "Late fees" : charge.Kind == "Rent" ? "Rent" : charge.Kind == "Repair" || charge.Kind == "Deduction" ? "Repairs and deductions charged" : "Other charges";
        }

        private static void Divide(List<StatementLine> output, string group, string propertyId, string label, string detail, DateTime when, DateTime from, DateTime to, bool spread, decimal exact, List<(string OwnerId, decimal Share)> holders)
        {
            decimal total = Math.Round(exact, 2);
            if (total == 0)
            {
                return;
            }
            List<decimal> parts = holders.Select(h => Math.Round(exact * h.Share / 100m, 2)).ToList();
            parts[0] = total - parts.Skip(1).Sum();
            for (int i = 0; i < holders.Count; i++)
            {
                if (parts[i] == 0)
                {
                    continue;
                }
                output.Add(new StatementLine { Group = group, PropertyId = propertyId, OwnerId = holders[i].OwnerId, Label = label, Detail = detail, When = when, From = from, To = to, Share = holders[i].Share, Spread = spread, Amount = parts[i] });
            }
        }

        public static List<StatementLine> Split(RentData d, IEnumerable<string> props, DateTime from, DateTime to)
        {
            HashSet<string> set = new HashSet<string>(props);
            List<StatementLine> output = new List<StatementLine>();
            from = from.Date;
            to = to.Date;
            foreach (LeaseRec l in d.Leases)
            {
                string pid = Rent.PropertyIdOfUnit(d, l.UnitId);
                if (!set.Contains(pid))
                {
                    continue;
                }
                string detail = Rent.UnitLabel(d, l.UnitId) + " · " + Rent.Names(d, l);
                foreach (Billing.Slice s in Billing.Allocate(d.Ledger.Where(x => x.LeaseId == l.Id)))
                {
                    if (!Billing.Cash(s.Payer) || !Billing.OwnerIncome(s.Charge))
                    {
                        continue;
                    }
                    DateTime paid = s.Payer.When.Date;
                    if (s.Charge.Kind != "Rent")
                    {
                        if (paid >= from && paid <= to)
                        {
                            Divide(output, "Income", pid, IncomeLabel(s.Charge), detail, paid, s.Charge.When.Date, s.Charge.When.Date, false, s.Amount, Holders(d, pid, s.Charge.When.Date));
                        }
                        continue;
                    }
                    if (paid > to)
                    {
                        continue;
                    }
                    (DateTime cs, DateTime ce) = Cover(s.Charge, l);
                    SpreadOver(d, output, pid, "Rent", detail, paid, cs, ce, s.Amount, from, to, "Income", "Prepaid");
                }
            }
            foreach (ExpenseRec e in d.Expenses.Where(e => set.Contains(e.PropertyId)).OrderBy(e => e.When))
            {
                string detail = (e.Payee.Length > 0 ? e.Payee + " · " : string.Empty) + (e.Note.Length > 0 ? e.Note : e.UnitId.Length > 0 ? Rent.UnitLabel(d, e.UnitId) : Rent.PropertyName(d, e.PropertyId));
                DateTime paid = e.When.Date;
                if (Spans(e))
                {
                    if (paid <= to)
                    {
                        SpreadOver(d, output, e.PropertyId, e.Category, detail, paid, e.CoverFrom!.Value.Date, e.CoverTo!.Value.Date, e.Amount, from, to, "Expense", "PrepaidExpense");
                    }
                    continue;
                }
                if (paid >= from && paid <= to)
                {
                    Divide(output, "Expense", e.PropertyId, e.Category, detail, paid, paid, paid, false, e.Amount, Holders(d, e.PropertyId, paid));
                }
            }
            return output;
        }

        public static decimal PaidAheadOf(RentData d, string ownerId, DateTime cutoff, IEnumerable<string>? props = null)
        {
            decimal total = 0;
            HashSet<string>? set = props == null ? null : new HashSet<string>(props);
            foreach (ExpenseRec e in d.Expenses.Where(e => Spans(e) && (set == null || set.Contains(e.PropertyId)) && e.When.Date <= cutoff.Date && e.CoverTo!.Value.Date > cutoff.Date))
            {
                decimal share = ShareOn(d, ownerId, e.PropertyId, e.When.Date);
                if (ownerId.Length == 0)
                {
                    share = Math.Max(0m, 100m - HeldOn(d, e.PropertyId, e.When.Date));
                }
                if (share <= 0)
                {
                    continue;
                }
                DateTime cs = e.CoverFrom!.Value.Date;
                DateTime ce = e.CoverTo!.Value.Date;
                DateTime start = cutoff.Date.AddDays(1) > cs ? cutoff.Date.AddDays(1) : cs;
                int days = (ce - cs).Days + 1;
                int left = (ce - start).Days + 1;
                total += Math.Round(e.Amount * left / days * share / 100m, 2);
            }
            return total;
        }

        public static bool Spans(ExpenseRec e)
        {
            return e.CoverFrom != null && e.CoverTo != null && e.CoverTo.Value.Date >= e.CoverFrom.Value.Date;
        }

        private static void SpreadOver(RentData d, List<StatementLine> output, string pid, string label, string detail, DateTime paid, DateTime cs, DateTime ce, decimal amount, DateTime from, DateTime to, string inGroup, string laterGroup)
        {
            int days = (ce - cs).Days + 1;
            if (days <= 0)
            {
                return;
            }
            decimal perDay = amount / days;
            string runGroup = string.Empty;
            string runKey = string.Empty;
            DateTime runFrom = cs;
            DateTime runTo = cs;
            decimal runSum = 0;
            List<(string OwnerId, decimal Share)> runHolders = new List<(string, decimal)>();
            for (DateTime day = cs; day <= ce; day = day.AddDays(1))
            {
                DateTime counted = day > paid ? day : paid;
                string group = counted >= from && counted <= to ? inGroup : counted > to ? laterGroup : string.Empty;
                List<(string OwnerId, decimal Share)> h = Holders(d, pid, day);
                string key = Key(h) + (day < from ? "<" : ">");
                if (group != runGroup || key != runKey)
                {
                    if (runGroup.Length > 0)
                    {
                        Divide(output, runGroup, pid, label, detail, paid, runFrom, runTo, true, runSum, runHolders);
                    }
                    runGroup = group;
                    runKey = key;
                    runHolders = h;
                    runFrom = day;
                    runSum = 0;
                }
                runTo = day;
                runSum += perDay;
            }
            if (runGroup.Length > 0)
            {
                Divide(output, runGroup, pid, label, detail, paid, runFrom, runTo, true, runSum, runHolders);
            }
        }

        public static List<StatementLine> Collected(RentData d, IEnumerable<string> props, DateTime from, DateTime to)
        {
            HashSet<string> set = new HashSet<string>(props);
            List<StatementLine> lines = new List<StatementLine>();
            foreach (LeaseRec l in d.Leases.Where(x => set.Contains(Rent.PropertyIdOfUnit(d, x.UnitId))))
            {
                List<Billing.Slice> slices = Billing.Allocate(d.Ledger.Where(x => x.LeaseId == l.Id));
                foreach (IGrouping<string, Billing.Slice> g in slices.Where(s => Billing.Cash(s.Payer) && s.Payer.When.Date >= from && s.Payer.When.Date <= to && Billing.OwnerIncome(s.Charge)).GroupBy(s => IncomeLabel(s.Charge)))
                {
                    lines.Add(new StatementLine { PropertyId = Rent.PropertyIdOfUnit(d, l.UnitId), Label = g.Key, Detail = Rent.UnitLabel(d, l.UnitId) + " · " + Rent.Names(d, l), When = g.Max(s => s.Payer.When), Amount = g.Sum(s => s.Amount) });
                }
            }
            return lines;
        }

        public static List<StatementLine> Spent(RentData d, IEnumerable<string> props, DateTime from, DateTime to)
        {
            HashSet<string> set = new HashSet<string>(props);
            return d.Expenses.Where(e => set.Contains(e.PropertyId) && e.When.Date >= from && e.When.Date <= to).OrderBy(e => e.When).Select(e => new StatementLine { PropertyId = e.PropertyId, Label = e.Category, Detail = (e.Payee.Length > 0 ? e.Payee + " · " : string.Empty) + (e.Note.Length > 0 ? e.Note : e.UnitId.Length > 0 ? Rent.UnitLabel(d, e.UnitId) : Rent.PropertyName(d, e.PropertyId)), When = e.When, Amount = e.Amount }).ToList();
        }

        public static decimal FeeOf(RentData d, OwnerRec o)
        {
            return o.Fee ?? d.Site.ManagementFee;
        }

        public static decimal FeeRate(RentData d, string propertyId, DateTime day)
        {
            decimal rate = 0;
            foreach ((string ownerId, decimal share) in Holders(d, propertyId, day))
            {
                OwnerRec? o = d.Owners.FirstOrDefault(x => x.Id == ownerId);
                rate += share * (o == null ? d.Site.ManagementFee : FeeOf(d, o));
            }
            return rate / 100m;
        }

        public static bool HadStake(RentData d, OwnerRec o, DateTime from, DateTime to)
        {
            return d.Ownership.Any(x => x.OwnerId == o.Id && Overlaps(x, from, to));
        }

        public static Statement For(RentData d, OwnerRec o, DateTime month)
        {
            DateTime from = new DateTime(month.Year, month.Month, 1);
            return For(d, o, from, from.AddMonths(1).AddDays(-1));
        }

        public static Statement For(RentData d, OwnerRec o, DateTime from, DateTime to)
        {
            List<string> ever = d.Ownership.Where(x => x.OwnerId == o.Id && (x.From == null || x.From.Value.Date <= to.Date)).Select(x => x.PropertyId).Distinct().ToList();
            List<StatementLine> mine = Split(d, ever, from, to).Where(x => x.OwnerId == o.Id).ToList();
            decimal aheadBefore = PaidAheadOf(d, o.Id, from.Date.AddDays(-1));
            decimal aheadAfter = PaidAheadOf(d, o.Id, to.Date);
            List<StatementLine> income = mine.Where(x => x.Group == "Income").ToList();
            List<string> shown = d.Ownership.Where(x => x.OwnerId == o.Id && Overlaps(x, from, to)).Select(x => x.PropertyId).Concat(mine.Select(x => x.PropertyId)).Distinct().OrderBy(id => d.Properties.FindIndex(p => p.Id == id)).ToList();
            string key = Month(from);
            bool wholeMonth = from.Day == 1 && to.Date == from.AddMonths(1).AddDays(-1).Date;
            return new Statement
            {
                Owner = o,
                From = from,
                To = to,
                Fee = FeeOf(d, o),
                PropertyIds = shown,
                Income = income,
                RentIncome = income.Where(x => x.Label == "Rent").Sum(x => x.Amount),
                Expenses = mine.Where(x => x.Group == "Expense").ToList(),
                Prepaid = mine.Where(x => x.Group == "Prepaid").ToList(),
                PaidAhead = aheadAfter - aheadBefore,
                HeldAhead = aheadAfter,
                Paid = wholeMonth ? d.Payouts.Where(p => p.OwnerId == o.Id && p.Month == key).Sum(p => p.Amount) : 0
            };
        }

        public static void SyncExpense(RentData d, WorkOrderRec w, string by)
        {
            ExpenseRec? e = d.Expenses.FirstOrDefault(x => x.WorkOrderId == w.Id);
            bool want = w.Status == "Done" && w.BillTo == "Owner" && w.Cost > 0;
            if (!want)
            {
                if (e != null)
                {
                    d.Expenses.Remove(e);
                }
                return;
            }
            if (e == null)
            {
                e = new ExpenseRec { Id = Fmt.NewId("e-"), WorkOrderId = w.Id, Created = DateTime.Now, By = by };
                d.Expenses.Add(e);
            }
            e.PropertyId = w.PropertyId;
            e.UnitId = w.UnitId;
            e.When = (w.Done ?? DateTime.Now).Date;
            e.Category = "Repairs";
            e.Payee = Maint.Vendor(d, w.VendorId);
            e.Amount = w.Cost;
            e.Note = w.Code + ": " + w.Title;
        }
    }
}
