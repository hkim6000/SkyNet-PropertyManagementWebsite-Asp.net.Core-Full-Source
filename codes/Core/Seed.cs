using System.Globalization;
using PropertyManagement.Models;

namespace PropertyManagement.Core
{
    public static class Seed
    {
        private static DateTime checkedDay = DateTime.MinValue;

        private static string Mark => Path.Combine(Store.DataDir, "demo-day.txt");

        private static DateTime? Move(DateTime? d, int days)
        {
            return d == null ? null : d.Value.AddDays(days);
        }

        private static DateTime? Months(DateTime? d, int months)
        {
            return d == null ? null : d.Value.AddMonths(months);
        }

        public static async Task Keep()
        {
            if (!Store.Demo() || checkedDay == DateTime.Today)
            {
                return;
            }
            using (await Store.Hold())
            {
                DateTime today = DateTime.Today;
                DateTime day = today;
                if (File.Exists(Mark))
                {
                    DateTime.TryParseExact((await File.ReadAllTextAsync(Mark)).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
                }
                int n = day == default ? 0 : (today - day.Date).Days;
                int m = day == default ? 0 : (today.Year * 12 + today.Month) - (day.Year * 12 + day.Month);
                if (n > 0)
                {
                    await ShiftDays(n);
                }
                if (m > 0)
                {
                    await ShiftMonths(m);
                }
                if (n != 0 || !File.Exists(Mark))
                {
                    Directory.CreateDirectory(Store.DataDir);
                    await File.WriteAllTextAsync(Mark, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
            }
            checkedDay = DateTime.Today;
        }

        private static async Task ShiftDays(int n)
        {
            List<ShowingRec> sh = await Store.Rows<ShowingRec>("showings");
            foreach (ShowingRec x in sh)
            {
                x.When = x.When.AddDays(n);
                x.Created = x.Created.AddDays(n);
            }
            List<ApplicationRec> ap = await Store.Rows<ApplicationRec>("applications");
            foreach (ApplicationRec x in ap)
            {
                x.MoveIn = x.MoveIn.AddDays(n);
                x.Created = x.Created.AddDays(n);
                x.DecidedAt = Move(x.DecidedAt, n);
            }
            List<NoticeRec> nt = await Store.Rows<NoticeRec>("notices");
            foreach (NoticeRec x in nt)
            {
                x.From = x.From.AddDays(n);
                x.Until = Move(x.Until, n);
                x.Created = x.Created.AddDays(n);
            }
            List<ResidentRec> rs = await Store.Rows<ResidentRec>("residents");
            foreach (ResidentRec x in rs)
            {
                x.Created = x.Created.AddDays(n);
                x.LastSeen = Move(x.LastSeen, n);
            }
            List<InquiryRec> msgs = await Store.Rows<InquiryRec>("messages");
            foreach (InquiryRec x in msgs)
            {
                x.When = x.When.AddDays(n);
            }
            List<WorkOrderRec> wo = await Store.Rows<WorkOrderRec>("workorders");
            foreach (WorkOrderRec x in wo)
            {
                x.Created = x.Created.AddDays(n);
                x.Due = x.Due.AddDays(n);
                x.Scheduled = Move(x.Scheduled, n);
                x.Done = Move(x.Done, n);
                foreach (WoNote t in x.Notes)
                {
                    t.When = t.When.AddDays(n);
                }
            }
            List<InspectionRec> ins = await Store.Rows<InspectionRec>("inspections");
            foreach (InspectionRec x in ins.Where(x => x.LeaseId.Length == 0))
            {
                x.Date = x.Date.AddDays(n);
                x.Created = x.Created.AddDays(n);
                x.Finished = Move(x.Finished, n);
            }
            List<VendorRec> vd = await Store.Rows<VendorRec>("vendors");
            foreach (VendorRec x in vd)
            {
                x.InsuredUntil = Move(x.InsuredUntil, n);
            }
            List<ExpenseRec> ex = await Store.Rows<ExpenseRec>("expenses");
            foreach (ExpenseRec x in ex.Where(x => x.WorkOrderId.Length > 0))
            {
                x.When = x.When.AddDays(n);
                x.Created = x.Created.AddDays(n);
            }
            await Store.SaveRows("showings", sh);
            await Store.SaveRows("applications", ap);
            await Store.SaveRows("notices", nt);
            await Store.SaveRows("residents", rs);
            await Store.SaveRows("messages", msgs);
            await Store.SaveRows("workorders", wo);
            await Store.SaveRows("inspections", ins);
            await Store.SaveRows("vendors", vd);
            await Store.SaveRows("expenses", ex);
        }

        private static async Task ShiftMonths(int m)
        {
            List<UnitRec> units = await Store.Rows<UnitRec>("units");
            foreach (UnitRec u in units)
            {
                u.Available = Months(u.Available, m);
                u.StatusSince = u.StatusSince.AddMonths(m);
            }
            List<LeaseRec> ls = await Store.Rows<LeaseRec>("leases");
            foreach (LeaseRec x in ls)
            {
                x.Start = x.Start.AddMonths(m);
                x.End = x.End.AddMonths(m);
                x.Created = x.Created.AddMonths(m);
                x.MovedIn = Months(x.MovedIn, m);
                x.NoticeOn = Months(x.NoticeOn, m);
                x.MoveOut = Months(x.MoveOut, m);
                x.MovedOut = Months(x.MovedOut, m);
            }
            List<LedgerRec> lg = await Store.Rows<LedgerRec>("ledger");
            foreach (LedgerRec x in lg)
            {
                x.When = x.When.AddMonths(m);
                x.Label = Relabel(x.Label, m);
            }
            List<InspectionRec> ins = await Store.Rows<InspectionRec>("inspections");
            foreach (InspectionRec x in ins.Where(x => x.LeaseId.Length > 0))
            {
                x.Date = x.Date.AddMonths(m);
                x.Created = x.Created.AddMonths(m);
                x.Finished = Months(x.Finished, m);
            }
            List<TurnRec> tr = await Store.Rows<TurnRec>("turns");
            foreach (TurnRec x in tr)
            {
                x.Start = x.Start.AddMonths(m);
                x.Target = x.Target.AddMonths(m);
                x.Finished = Months(x.Finished, m);
                foreach (TurnStep t in x.Steps)
                {
                    t.DoneAt = Months(t.DoneAt, m);
                }
            }
            List<ExpenseRec> ex = await Store.Rows<ExpenseRec>("expenses");
            foreach (ExpenseRec x in ex.Where(x => x.WorkOrderId.Length == 0))
            {
                x.When = x.When.AddMonths(m);
                x.Created = x.Created.AddMonths(m);
                x.CoverFrom = Months(x.CoverFrom, m);
                x.CoverTo = Months(x.CoverTo, m);
            }
            List<PayoutRec> po = await Store.Rows<PayoutRec>("payouts");
            foreach (PayoutRec x in po)
            {
                x.When = x.When.AddMonths(m);
                if (Books.ParseMonth(x.Month, out DateTime pm))
                {
                    x.Month = Books.Month(pm.AddMonths(m));
                }
            }
            List<DepositRec> dp = await Store.Rows<DepositRec>("deposits");
            foreach (DepositRec x in dp)
            {
                x.Created = x.Created.AddMonths(m);
                x.Finished = Months(x.Finished, m);
            }
            List<PayAttempt> pa = await Store.Rows<PayAttempt>("payattempts");
            foreach (PayAttempt x in pa)
            {
                x.Created = x.Created.AddMonths(m);
                x.Paid = Months(x.Paid, m);
            }
            List<OwnershipRec> ow = await Store.Rows<OwnershipRec>("ownership");
            foreach (OwnershipRec x in ow)
            {
                x.From = Months(x.From, m);
                x.Until = Months(x.Until, m);
                x.Created = x.Created.AddMonths(m);
            }
            await Store.SaveRows("units", units);
            await Store.SaveRows("leases", ls);
            await Store.SaveRows("ledger", lg);
            await Store.SaveRows("inspections", ins);
            await Store.SaveRows("turns", tr);
            await Store.SaveRows("expenses", ex);
            await Store.SaveRows("payouts", po);
            await Store.SaveRows("deposits", dp);
            await Store.SaveRows("payattempts", pa);
            await Store.SaveRows("ownership", ow);
        }

        private static string Relabel(string label, int m)
        {
            label = System.Text.RegularExpressions.Regex.Replace(label, @"\b([A-Z][a-z]{2}) (\d{1,2})\b(?! \d{4})", x =>
            {
                if (!DateTime.TryParseExact(x.Groups[1].Value + " " + x.Groups[2].Value + " 2000", "MMM d yyyy", CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out DateTime d))
                {
                    return x.Value;
                }
                return d.AddMonths(m).ToString("MMM d", CultureInfo.GetCultureInfo("en-US"));
            });
            foreach (string month in CultureInfo.GetCultureInfo("en-US").DateTimeFormat.MonthNames.Where(x => x.Length > 0))
            {
                int at = label.IndexOf(month + " ", StringComparison.Ordinal);
                if (at < 0 || at + month.Length + 5 > label.Length || !int.TryParse(label.AsSpan(at + month.Length + 1, 4), NumberStyles.None, CultureInfo.InvariantCulture, out int year))
                {
                    continue;
                }
                DateTime d = new DateTime(year, DateTime.ParseExact(month, "MMMM", CultureInfo.GetCultureInfo("en-US")).Month, 1).AddMonths(m);
                return label.Substring(0, at) + d.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("en-US")) + label.Substring(at + month.Length + 5);
            }
            return label;
        }
    }
}
