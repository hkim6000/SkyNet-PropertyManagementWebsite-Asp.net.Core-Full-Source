using System.Globalization;
using System.Text;
using System.Text.Json;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminReports : WebPage
    {
        private const string AreaKey = "reports";
        private static readonly (string Key, string Name, bool Period)[] Kinds =
        {
            ("income", "Income statement", true),
            ("ownpl", "Profit and loss by owner", true),
            ("roll", "Rent roll", false),
            ("late", "Delinquency", false),
            ("occ", "Occupancy", false),
            ("maint", "Maintenance", true)
        };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Reports | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminReportsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminReportsJs.denied();");
                return null;
            }
            return c;
        }

        private static bool Can(Ctx c, char letter)
        {
            return Access.Can(c.U, AreaKey, letter);
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("rp-brand", Ui.Esc(site.Brand));
            response.SetElementContents("rp-menu", await Auth.Menu("rp", "AdminReports", c));
            response.SetElementContents("rp-who", Auth.Who("rp", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminReportsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string Term(string? v)
        {
            return Ui.Clip(v, 40);
        }

        private static int PageNo(string? v)
        {
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
        }

        private static string Pager(int page, int pages, int count, string one, string many)
        {
            return "<div class=\"rp-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"rp-btn rp-btn-ghost rp-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminReportsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"rp-btn rp-btn-ghost rp-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminReportsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
        }

        private static string Opt(string value, string label, string current)
        {
            return "<option value=\"" + Ui.Esc(value) + "\"" + (value == current ? " selected" : string.Empty) + ">" + Ui.Esc(label) + "</option>";
        }

        private static string Pill(string status)
        {
            string k = status switch
            {
                "Active" or "Occupied" or "Done" or "Approved" or "Leased" or "Live" or "Ready" or "Insured" => "go",
                "New" or "Upcoming" or "Confirmed" or "Scheduled" or "Vacant" => "new",
                "Requested" or "Reviewing" or "Notice" or "Invited" or "Must set password" or "Month to month" or "Move-in due" or "In progress" or "Open" or "High" or "Insurance ending" => "wait",
                "Make-ready" or "No-show" or "Declined" or "Emergency" or "Overdue" or "Insurance expired" or "Late" => "bad",
                _ => "off"
            };
            return "<span class=\"rp-pill rp-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"rp-err\" id=\"rp-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"rp-field\" for=\"rp-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"rp-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
        }

        private static string Num(decimal v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static decimal Dec(string? v)
        {
            return decimal.TryParse((v ?? string.Empty).Trim().TrimStart('$'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? Math.Round(d, 2) : -1;
        }

        private static int Int(string? v, int fallback)
        {
            return int.TryParse((v ?? string.Empty).Trim().Replace(",", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : fallback;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            RentData d = await Rent.Load();
            await Shell(response, c, d.Site);
            List<PropertyRec> mine = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).ToList();
            DateTime from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
            DateTime to = from.AddMonths(1).AddDays(-1);
            response.SetElementContents("rp-main", "<div class=\"rp-ph\"><div><h1>Reports</h1><p>Income, profit and loss by owner, rent roll, delinquency, occupancy and maintenance for any period and property. Every report downloads as a spreadsheet file.</p></div></div><div class=\"rp-tools rp-rtools\"><select id=\"rp-fr\" onchange=\"AdminReportsJs.run()\" aria-label=\"Report\">" + string.Concat(Kinds.Select(k => Opt(k.Key, k.Name, "income"))) + "</select><select id=\"rp-fp\" onchange=\"AdminReportsJs.run()\" aria-label=\"Property\">" + Opt(string.Empty, "All properties", string.Empty) + string.Concat(mine.Select(p => Opt(p.Id, p.Name, string.Empty))) + "</select><select id=\"rp-fo\" onchange=\"AdminReportsJs.run()\" aria-label=\"Owner\" hidden>" + Opt(string.Empty, "All owners", string.Empty) + string.Concat(d.Owners.Select(o => Opt(o.Id, o.Name, string.Empty))) + "</select><span id=\"rp-period\"><label>From <input type=\"date\" id=\"rp-ff\" value=\"" + Fmt.IsoDay(from) + "\" onchange=\"AdminReportsJs.run()\"></label><label>to <input type=\"date\" id=\"rp-ft\" value=\"" + Fmt.IsoDay(to) + "\" onchange=\"AdminReportsJs.run()\"></label></span><button type=\"button\" class=\"rp-btn rp-btn-ghost\" onclick=\"AdminReportsJs.csv()\">Download spreadsheet</button><button type=\"button\" class=\"rp-btn rp-btn-ghost\" onclick=\"window.print()\">Print</button></div><div id=\"rp-list\"></div>");
            List<string[]> rows;
            response.SetElementContents("rp-list", Run(c, d, "income", string.Empty, from, to, out rows));
            return response;
        }

        private static string Cell(string v, bool num)
        {
            return "<td" + (num ? " class=\"rp-num\"" : string.Empty) + ">" + v + "</td>";
        }

        private static string Table(string title, string note, string[] head, List<string[]> body, int numFrom, List<string[]> csv)
        {
            csv.Add(new[] { title });
            csv.Add(head);
            csv.AddRange(body.Select(r => r.Select(Plain).ToArray()));
            csv.Add(Array.Empty<string>());
            StringBuilder sb = new StringBuilder("<section class=\"rp-panel rp-rep\"><h2>" + Ui.Esc(title) + "</h2>" + (note.Length > 0 ? "<p class=\"rp-mini\">" + Ui.Esc(note) + "</p>" : string.Empty) + "<div class=\"rp-wrapt\"><table class=\"rp-table rp-rtable\"><thead><tr>" + string.Concat(head.Select((h, i) => "<th" + (i >= numFrom ? " class=\"rp-num\"" : string.Empty) + ">" + Ui.Esc(h) + "</th>")) + "</tr></thead><tbody>");
            if (body.Count == 0)
            {
                sb.Append("<tr><td colspan=\"" + head.Length + "\" class=\"rp-mini\">Nothing in this period.</td></tr>");
            }
            foreach (string[] r in body)
            {
                bool total = r.Length > 0 && (r[0].StartsWith("Total", StringComparison.Ordinal) || r[0].StartsWith("Net", StringComparison.Ordinal));
                sb.Append("<tr" + (total ? " class=\"rp-total\"" : string.Empty) + ">" + string.Concat(r.Select((v, i) => i == 0 ? "<td class=\"rp-first\" data-label=\"" + Ui.Esc(head[0]) + "\">" + v + "</td>" : "<td" + (i >= numFrom ? " class=\"rp-num\"" : string.Empty) + " data-label=\"" + Ui.Esc(head[i]) + "\">" + Shown(v) + "</td>")) + "</tr>");
            }
            sb.Append("</tbody></table></div></section>");
            return sb.ToString();
        }

        private static string M(decimal v)
        {
            return "\u00A4" + v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string Shown(string v)
        {
            if (v.StartsWith("\u00A4", StringComparison.Ordinal) && decimal.TryParse(v.Substring(1), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal m))
            {
                return Math.Abs(m) < 0.005m ? "<span class=\"rp-mini\">-</span>" : (m < 0 ? "-" : string.Empty) + Fmt.Money(Math.Abs(m));
            }
            return v;
        }

        private static string Plain(string v)
        {
            string x = v.StartsWith("\u00A4", StringComparison.Ordinal) ? v.Substring(1) : v;
            return System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(x, "<[^>]+>", string.Empty));
        }

        private static decimal Rate(RentData d, string ownerId)
        {
            OwnerRec? o = d.Owners.FirstOrDefault(x => x.Id == ownerId);
            return o == null ? d.Site.ManagementFee : Books.FeeOf(d, o);
        }

        private static string Span(DateTime from, DateTime to)
        {
            if (from == to)
            {
                return Fmt.Short(from);
            }
            return from.Year == to.Year && from.Month == to.Month ? Fmt.Short(from) + "–" + to.Day.ToString(CultureInfo.InvariantCulture) : Fmt.Short(from) + " – " + Fmt.Short(to);
        }

        private static List<(DateTime From, DateTime To, decimal Share)> FreePeriods(RentData d, string pid, DateTime from, DateTime to)
        {
            List<(DateTime From, DateTime To, decimal Share)> list = new List<(DateTime, DateTime, decimal)>();
            for (DateTime day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                decimal s = Math.Max(0m, 100m - Books.HeldOn(d, pid, day));
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

        private static void OwnerPl(RentData d, List<PropertyRec> props, DateTime from, DateTime to, string ownerFilter, StringBuilder sb, List<string[]> csv)
        {
            string period = Fmt.Day(from) + " to " + Fmt.Day(to);
            List<string> ids = props.Select(p => p.Id).ToList();
            List<StatementLine> all = Books.Split(d, ids, from, to);
            List<string> owners = d.Ownership.Where(x => ids.Contains(x.PropertyId) && Books.Overlaps(x, from, to)).Select(x => x.OwnerId).Concat(all.Select(x => x.OwnerId)).Distinct().Where(id => id.Length > 0 || all.Any(x => x.OwnerId.Length == 0 && x.Group is "Income" or "Expense")).OrderBy(id => id.Length == 0 ? 1 : 0).ThenBy(id => d.Owners.FirstOrDefault(o => o.Id == id)?.Name ?? id).ToList();
            if (ownerFilter.Length > 0)
            {
                owners = owners.Where(id => id == ownerFilter).ToList();
            }
            if (owners.Count == 0)
            {
                sb.Append("<section class=\"rp-panel rp-rep\"><h2>Profit and loss by owner</h2><p class=\"rp-mini\">" + Ui.Esc(period) + ". No owner shares in these properties for this period.</p></section>");
                csv.Add(new[] { "Profit and loss by owner", period, "No owner shares in these properties for this period." });
                return;
            }
            foreach (string oid in owners)
            {
                OwnerRec? o = d.Owners.FirstOrDefault(x => x.Id == oid);
                decimal rate = Rate(d, oid);
                List<StatementLine> mine = all.Where(x => x.OwnerId == oid).ToList();
                List<(string Pid, DateTime From, DateTime To, decimal Share, string Head)> cols = new List<(string, DateTime, DateTime, decimal, string)>();
                foreach (PropertyRec p in props)
                {
                    List<(DateTime From, DateTime To, decimal Share)> segs = oid.Length == 0 ? FreePeriods(d, p.Id, from, to) : Books.Periods(d, oid, p.Id, from, to);
                    if (mine.Any(x => x.PropertyId == p.Id && x.From < from.Date && x.Group is "Income" or "Expense"))
                    {
                        cols.Add((p.Id, DateTime.MinValue, from.Date.AddDays(-1), 0m, p.Name + " · before " + Fmt.Short(from)));
                    }
                    foreach ((DateTime f, DateTime t, decimal sh) in segs)
                    {
                        cols.Add((p.Id, f, t, sh, p.Name + " · " + Span(f, t) + " · " + Books.Pct(sh)));
                    }
                }
                List<StatementLine> In((string Pid, DateTime From, DateTime To, decimal Share, string Head) col, string group)
                {
                    return mine.Where(x => x.Group == group && x.PropertyId == col.Pid && x.From >= col.From && x.From <= col.To).ToList();
                }
                List<string> labels = mine.Where(x => x.Group == "Income").Select(x => x.Label).Distinct().OrderBy(x => x == "Rent" ? 0 : 1).ThenBy(x => x).ToList();
                List<string> cats = mine.Where(x => x.Group == "Expense").Select(x => x.Label).Distinct().OrderBy(x => x).ToList();
                List<string> head = new List<string> { "Line" };
                head.AddRange(cols.Select(x => x.Head));
                head.Add("Total");
                List<string[]> body = new List<string[]>();
                string[] Row(string name, Func<(string Pid, DateTime From, DateTime To, decimal Share, string Head), decimal> f)
                {
                    List<string> r = new List<string> { Ui.Esc(name) };
                    List<decimal> v = cols.Select(f).ToList();
                    r.AddRange(v.Select(M));
                    r.Add(M(v.Sum()));
                    return r.ToArray();
                }
                List<decimal> colRent = cols.Select(col => In(col, "Income").Where(x => x.Label == "Rent").Sum(x => x.Amount)).ToList();
                List<decimal> colFee = colRent.Select(r => Math.Round(r * rate / 100m, 2)).ToList();
                if (colFee.Count > 0)
                {
                    int big = colRent.IndexOf(colRent.Max());
                    colFee[big] += Math.Round(colRent.Sum() * rate / 100m, 2) - colFee.Sum();
                }
                decimal Fee((string Pid, DateTime From, DateTime To, decimal Share, string Head) col)
                {
                    return colFee[cols.IndexOf(col)];
                }
                foreach (string l in labels)
                {
                    body.Add(Row(l == "Rent" ? "Rent earned" : l, col => In(col, "Income").Where(x => x.Label == l).Sum(x => x.Amount)));
                }
                body.Add(Row("Total income", col => In(col, "Income").Sum(x => x.Amount)));
                foreach (string k in cats)
                {
                    body.Add(Row(k, col => -In(col, "Expense").Where(x => x.Label == k).Sum(x => x.Amount)));
                }
                body.Add(Row("Total expenses", col => -In(col, "Expense").Sum(x => x.Amount)));
                body.Add(Row("Management fee " + Books.Pct(rate), col => -Fee(col)));
                body.Add(Row("Net", col => In(col, "Income").Sum(x => x.Amount) - In(col, "Expense").Sum(x => x.Amount) - Fee(col)));
                decimal heldRent = mine.Where(x => x.Group == "Prepaid").Sum(x => x.Amount);
                decimal heldExp = Books.PaidAheadOf(d, oid, to.Date, ids);
                if (heldRent != 0)
                {
                    List<string> r1 = new List<string> { "Held: rent collected for later days" };
                    r1.AddRange(cols.Select(_ => string.Empty));
                    r1.Add(M(heldRent));
                    body.Add(r1.ToArray());
                }
                if (heldExp != 0)
                {
                    List<string> r2 = new List<string> { "Held back: bills paid ahead for later days" };
                    r2.AddRange(cols.Select(_ => string.Empty));
                    r2.Add(M(-heldExp));
                    body.Add(r2.ToArray());
                }
                string title = oid.Length == 0 ? "Share with no owner" : (o?.Name ?? oid) + (o != null && o.Company.Length > 0 ? " · " + o.Company : string.Empty);
                string note = period + ". " + (oid.Length == 0 ? "The part of each property no owner held on those days, at the standard fee." : "Each column is one property at one share. Rent counts for the days it covers once collected; expenses on their date or over the period they cover.");
                sb.Append(Table(title, note, head.ToArray(), body, 1, csv));
            }
            if (ownerFilter.Length == 0)
            {
                List<string[]> check = new List<string[]>();
                foreach (PropertyRec p in props)
                {
                    List<StatementLine> pl = all.Where(x => x.PropertyId == p.Id).ToList();
                    decimal inc = pl.Where(x => x.Group == "Income").Sum(x => x.Amount);
                    decimal exp = pl.Where(x => x.Group == "Expense").Sum(x => x.Amount);
                    decimal incO = pl.Where(x => x.Group == "Income" && x.OwnerId.Length > 0 && owners.Contains(x.OwnerId)).Sum(x => x.Amount);
                    decimal expO = pl.Where(x => x.Group == "Expense" && x.OwnerId.Length > 0 && owners.Contains(x.OwnerId)).Sum(x => x.Amount);
                    decimal incU = pl.Where(x => x.Group == "Income" && x.OwnerId.Length == 0).Sum(x => x.Amount);
                    decimal expU = pl.Where(x => x.Group == "Expense" && x.OwnerId.Length == 0).Sum(x => x.Amount);
                    decimal diff = (inc - incO - incU) - (exp - expO - expU);
                    check.Add(new[] { Ui.Esc(p.Name), M(inc), M(incO), M(incU), M(-exp), M(-expO), M(-expU), diff == 0 ? "Matches" : "<span class=\"rp-bad\">" + Ui.Esc(Fmt.Money(diff)) + "</span>" });
                }
                sb.Append(Table("Check: owners add up to each property", period + ". Property income and expenses against the owners' shares plus any share with no owner. They match when the last column says so.", new[] { "Property", "Income", "Owners' income", "No owner", "Expenses", "Owners' expenses", "No owner", "Result" }, check, 1, csv));
            }
        }

        private static string Run(Ctx c, RentData d, string kind, string prop, DateTime from, DateTime to, out List<string[]> csv, string owner = "")
        {
            csv = new List<string[]>();
            List<PropertyRec> props = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id) && (prop.Length == 0 || p.Id == prop)).ToList();
            List<string> ids = props.Select(p => p.Id).ToList();
            string period = Fmt.Day(from) + " to " + Fmt.Day(to);
            StringBuilder sb = new StringBuilder();
            switch (kind)
            {
                case "roll":
                    {
                        List<string[]> body = new List<string[]>();
                        foreach (UnitRec u in d.Units.Where(x => ids.Contains(x.PropertyId)).OrderBy(x => ids.IndexOf(x.PropertyId)).ThenBy(x => x.Number, StringComparer.OrdinalIgnoreCase))
                        {
                            LeaseRec? l = Rent.CurrentLease(d, u.Id);
                            body.Add(new[] { Ui.Esc(Rent.UnitLabel(d, u.Id)), Ui.Esc(Fmt.Beds(u.Beds) + ", " + Fmt.Baths(u.Baths)), Ui.Esc(u.Status), Ui.Esc(l == null ? "-" : Rent.Names(d, l)), l == null ? "-" : Ui.Esc(Fmt.Day(l.Start)), l == null ? "-" : Ui.Esc(Rent.Holdover(l) ? "Month to month" : Fmt.Day(l.MoveOut ?? l.End)), M(u.Rent), l == null ? "0.00" : M(l.Rent), l == null ? "0.00" : M(l.Deposit), l == null ? "0.00" : M(Rent.Balance(d, l.Id)) });
                        }
                        List<UnitRec> us = d.Units.Where(x => ids.Contains(x.PropertyId)).ToList();
                        List<LeaseRec> live = us.Select(u => Rent.CurrentLease(d, u.Id)).Where(l => l != null).Select(l => l!).ToList();
                        body.Add(new[] { "Total", Ui.Count(us.Count, "unit", "units"), us.Count(u => u.Status is "Occupied" or "Notice") + " occupied", Ui.Count(live.Count, "lease", "leases"), string.Empty, string.Empty, M(us.Sum(u => u.Rent)), M(live.Sum(l => l.Rent)), M(live.Sum(l => l.Deposit)), M(live.Sum(l => Rent.Balance(d, l.Id))) });
                        sb.Append(Table("Rent roll", "As of " + Fmt.Day(DateTime.Today) + ". Market rent is the rent set on the unit; lease rent is what the resident pays.", new[] { "Unit", "Size", "Status", "Residents", "Lease start", "Lease end", "Market rent", "Lease rent", "Deposit", "Balance" }, body, 6, csv));
                        break;
                    }
                case "late":
                    {
                        List<string[]> body = new List<string[]>();
                        decimal ta = 0, tb = 0, tc = 0, td = 0;
                        foreach (LeaseRec l in d.Leases.Where(x => ids.Contains(Rent.PropertyIdOfUnit(d, x.UnitId)) && Rent.Balance(d, x.Id) > 0.004m).OrderByDescending(x => Rent.Balance(d, x.Id)))
                        {
                            List<LedgerRec> lines = d.Ledger.Where(g => g.LeaseId == l.Id).ToList();
                            (decimal a, decimal b, decimal cc, decimal e) = Billing.Aging(lines);
                            ta += a;
                            tb += b;
                            tc += cc;
                            td += e;
                            LedgerRec? last = lines.Where(g => g.Kind == "Payment").OrderByDescending(g => g.When).FirstOrDefault();
                            body.Add(new[] { Ui.Esc(l.Code), Ui.Esc(Rent.UnitLabel(d, l.UnitId)), Ui.Esc(Rent.Names(d, l)), Ui.Esc(l.Status), last == null ? "-" : Ui.Esc(Fmt.Day(last.When)), M(a), M(b), M(cc), M(e), M(a + b + cc + e) });
                        }
                        body.Add(new[] { "Total", string.Empty, string.Empty, string.Empty, string.Empty, M(ta), M(tb), M(tc), M(td), M(ta + tb + tc + td) });
                        sb.Append(Table("Delinquency", "Unpaid charges by age, as of " + Fmt.Day(DateTime.Today) + ". Payments pay the oldest charges first.", new[] { "Lease", "Home", "Residents", "Status", "Last payment", "0 to 30 days", "31 to 60", "61 to 90", "Over 90", "Total owed" }, body, 5, csv));
                        break;
                    }
                case "occ":
                    {
                        List<string[]> body = new List<string[]>();
                        foreach (PropertyRec p in props)
                        {
                            List<UnitRec> us = d.Units.Where(u => u.PropertyId == p.Id).ToList();
                            int occ = us.Count(u => u.Status is "Occupied" or "Notice");
                            List<UnitRec> empty = us.Where(u => u.Status is "Vacant" or "Make-ready" or "Offline").ToList();
                            decimal potential = us.Sum(u => u.Rent);
                            decimal actual = us.Select(u => Rent.CurrentLease(d, u.Id)).Where(l => l != null).Sum(l => l!.Rent);
                            body.Add(new[] { Ui.Esc(p.Name), us.Count.ToString(CultureInfo.InvariantCulture), occ.ToString(CultureInfo.InvariantCulture), us.Count(u => u.Status == "Notice").ToString(CultureInfo.InvariantCulture), us.Count(u => u.Status == "Vacant").ToString(CultureInfo.InvariantCulture), us.Count(u => u.Status == "Make-ready").ToString(CultureInfo.InvariantCulture), us.Count(u => u.Status == "Offline").ToString(CultureInfo.InvariantCulture), (us.Count == 0 ? 0 : occ * 100.0 / us.Count).ToString("0.0", CultureInfo.InvariantCulture) + "%", empty.Count == 0 ? "-" : empty.Average(u => (DateTime.Today - u.StatusSince.Date).Days).ToString("0", CultureInfo.InvariantCulture), M(potential), M(actual), M(potential - actual) });
                        }
                        List<UnitRec> all = d.Units.Where(u => ids.Contains(u.PropertyId)).ToList();
                        int o2 = all.Count(u => u.Status is "Occupied" or "Notice");
                        decimal pt = all.Sum(u => u.Rent);
                        decimal ac = all.Select(u => Rent.CurrentLease(d, u.Id)).Where(l => l != null).Sum(l => l!.Rent);
                        body.Add(new[] { "Total", all.Count.ToString(CultureInfo.InvariantCulture), o2.ToString(CultureInfo.InvariantCulture), all.Count(u => u.Status == "Notice").ToString(CultureInfo.InvariantCulture), all.Count(u => u.Status == "Vacant").ToString(CultureInfo.InvariantCulture), all.Count(u => u.Status == "Make-ready").ToString(CultureInfo.InvariantCulture), all.Count(u => u.Status == "Offline").ToString(CultureInfo.InvariantCulture), (all.Count == 0 ? 0 : o2 * 100.0 / all.Count).ToString("0.0", CultureInfo.InvariantCulture) + "%", string.Empty, M(pt), M(ac), M(pt - ac) });
                        sb.Append(Table("Occupancy", "As of " + Fmt.Day(DateTime.Today) + ". Days empty is the average for homes that are vacant, make-ready or offline. Vacancy loss is market rent minus lease rent.", new[] { "Property", "Units", "Occupied", "On notice", "Vacant", "Make-ready", "Offline", "Occupancy", "Days empty", "Market rent", "Lease rent", "Vacancy loss" }, body, 1, csv));
                        break;
                    }
                case "maint":
                    {
                        List<WorkOrderRec> ws = d.WorkOrders.Where(w => ids.Contains(w.PropertyId)).ToList();
                        List<string[]> body = new List<string[]>();
                        foreach (PropertyRec p in props)
                        {
                            List<WorkOrderRec> pw = ws.Where(w => w.PropertyId == p.Id).ToList();
                            List<WorkOrderRec> done = pw.Where(w => w.Done != null && w.Done.Value.Date >= from && w.Done.Value.Date <= to).ToList();
                            body.Add(new[] { Ui.Esc(p.Name), pw.Count(w => w.Created.Date >= from && w.Created.Date <= to).ToString(CultureInfo.InvariantCulture), done.Count.ToString(CultureInfo.InvariantCulture), pw.Count(Maint.Open).ToString(CultureInfo.InvariantCulture), pw.Count(Maint.Overdue).ToString(CultureInfo.InvariantCulture), done.Count == 0 ? "-" : done.Average(w => (w.Done!.Value - w.Created).TotalDays).ToString("0.0", CultureInfo.InvariantCulture), M(done.Where(w => w.BillTo == "Owner").Sum(w => w.Cost)), M(done.Where(w => w.BillTo == "Resident").Sum(w => w.Cost)) });
                        }
                        sb.Append(Table("Work orders by property", period + ". Days to finish counts from the request to the finished job.", new[] { "Property", "Opened", "Finished", "Open now", "Overdue now", "Days to finish", "Owner paid", "Charged to residents" }, body, 1, csv));
                        List<string[]> cat = ws.Where(w => w.Done != null && w.Done.Value.Date >= from && w.Done.Value.Date <= to).GroupBy(w => w.Category).OrderByDescending(g => g.Sum(w => w.Cost)).Select(g => new[] { Ui.Esc(g.Key), g.Count().ToString(CultureInfo.InvariantCulture), M(g.Sum(w => w.Cost)), M(g.Average(w => w.Cost)) }).ToList();
                        sb.Append(Table("Finished work by category", period, new[] { "Category", "Jobs", "Cost", "Average cost" }, cat, 1, csv));
                        break;
                    }
                case "ownpl":
                    {
                        OwnerPl(d, props, from, to, owner, sb, csv);
                        break;
                    }
                default:
                    {
                        List<StatementLine> all = Books.Split(d, ids, from, to);
                        List<StatementLine> inc = all.Where(x => x.Group == "Income").ToList();
                        List<StatementLine> exp = all.Where(x => x.Group == "Expense").ToList();
                        List<string> labels = inc.Select(x => x.Label).Distinct().OrderBy(x => x == "Rent" ? 0 : 1).ThenBy(x => x).ToList();
                        List<string> cats = exp.Select(x => x.Label).Distinct().OrderBy(x => x).ToList();
                        List<string> head = new List<string> { "Line" };
                        head.AddRange(props.Select(p => p.Name));
                        head.Add("Total");
                        List<string[]> body = new List<string[]>();
                        string[] Row(string name, Func<string, decimal> f)
                        {
                            List<string> r = new List<string> { Ui.Esc(name) };
                            r.AddRange(props.Select(p => M(f(p.Id))));
                            r.Add(M(props.Sum(p => f(p.Id))));
                            return r.ToArray();
                        }
                        decimal Fee(string pid)
                        {
                            return inc.Where(x => x.PropertyId == pid && x.Label == "Rent").GroupBy(x => x.OwnerId).Sum(g => Math.Round(g.Sum(x => x.Amount) * Rate(d, g.Key) / 100m, 2));
                        }
                        foreach (string l in labels)
                        {
                            body.Add(Row(l, pid => inc.Where(x => x.PropertyId == pid && x.Label == l).Sum(x => x.Amount)));
                        }
                        body.Add(Row("Total income", pid => inc.Where(x => x.PropertyId == pid).Sum(x => x.Amount)));
                        foreach (string k in cats)
                        {
                            body.Add(Row(k, pid => -exp.Where(x => x.PropertyId == pid && x.Label == k).Sum(x => x.Amount)));
                        }
                        body.Add(Row("Total expenses", pid => -exp.Where(x => x.PropertyId == pid).Sum(x => x.Amount)));
                        body.Add(Row("Management fees", pid => -Fee(pid)));
                        body.Add(Row("Net to owners", pid => inc.Where(x => x.PropertyId == pid).Sum(x => x.Amount) - exp.Where(x => x.PropertyId == pid).Sum(x => x.Amount) - Fee(pid)));
                        if (all.Any(x => x.Group == "Prepaid" || x.Group == "PrepaidExpense"))
                        {
                            body.Add(Row("Held: rent collected for later days", pid => all.Where(x => x.PropertyId == pid && x.Group == "Prepaid").Sum(x => x.Amount)));
                            body.Add(Row("Held: expenses paid for later days", pid => -all.Where(x => x.PropertyId == pid && x.Group == "PrepaidExpense").Sum(x => x.Amount)));
                        }
                        sb.Append(Table("Income statement", period + ". Earned basis: collected rent counts for the days it covers, and expenses on their date or spread over the period they cover. Deposits held for residents are not income. Management fees use each owner's rate.", head.ToArray(), body, 1, csv));
                        break;
                    }
            }
            return sb.ToString();
        }

        private (string Kind, string Prop, DateTime From, DateTime To, string Owner) Args()
        {
            string kind = Ui.Clip(GetDataValue("rep"), 10);
            kind = Kinds.Any(k => k.Key == kind) ? kind : "income";
            DateTime from = Fmt.ParseDate(GetDataValue("dfrom"), out DateTime f) ? f : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime to = Fmt.ParseDate(GetDataValue("dto"), out DateTime t) ? t : DateTime.Today;
            if (to < from)
            {
                (from, to) = (to, from);
            }
            if ((to - from).TotalDays > 3660)
            {
                from = to.AddYears(-10);
            }
            return (kind, Ui.Clip(GetDataValue("prop"), 60), from, to, Ui.Clip(GetDataValue("owner"), 60));
        }

        public async Task<ApiResponse> Show()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (string kind, string prop, DateTime from, DateTime to, string owner) = Args();
            RentData d = await Rent.Load();
            List<string[]> rows;
            response.SetElementContents("rp-list", Run(c, d, kind, prop, from, to, out rows, owner));
            response.ExecuteScript("AdminReportsJs.period(" + (Kinds.First(k => k.Key == kind).Period ? "true" : "false") + "," + (kind == "ownpl" ? "true" : "false") + ");");
            return response;
        }

        public async Task<ApiResponse> Csv()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (string kind, string prop, DateTime from, DateTime to, string owner) = Args();
            RentData d = await Rent.Load();
            Run(c, d, kind, prop, from, to, out List<string[]> rows, owner);
            StringBuilder sb = new StringBuilder();
            foreach (string[] r in rows)
            {
                sb.Append(string.Join(",", r.Select(v => "\"" + v.Replace("\"", "\"\"") + "\"")) + "\r\n");
            }
            string name = Kinds.First(k => k.Key == kind).Name.ToLowerInvariant().Replace(' ', '-') + "-" + Fmt.IsoDay(DateTime.Today) + ".csv";
            await Store.Log(c.U.Id, "download report", Kinds.First(k => k.Key == kind).Name, true);
            response.ExecuteScript("AdminReportsJs.save(" + JsonSerializer.Serialize(name) + "," + JsonSerializer.Serialize(sb.ToString()) + ");");
            return response;
        }
    }
}
