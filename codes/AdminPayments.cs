using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminPayments : WebPage
    {
        private const string AreaKey = "payments";
        private const int Size = 15;
        private static readonly (string Key, string Name)[] Tabs = { ("bal", "Balances"), ("pay", "Payments"), ("chg", "Charges"), ("web", "Online") };
        private static readonly string[] Types = { "Payment", "Charge", "Credit", "Refund" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Rent and payments | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPaymentsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminPaymentsJs.denied();");
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
            response.SetElementContents("pa-brand", Ui.Esc(site.Brand));
            response.SetElementContents("pa-menu", await Auth.Menu("pa", "AdminPayments", c));
            response.SetElementContents("pa-who", Auth.Who("pa", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminPaymentsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"pa-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"pa-btn pa-btn-ghost pa-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminPaymentsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"pa-btn pa-btn-ghost pa-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminPaymentsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"pa-pill pa-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"pa-err\" id=\"pa-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"pa-field\" for=\"pa-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"pa-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static List<LeaseRec> Leases(Ctx c, RentData d, string prop)
        {
            return d.Leases.Where(l => l.Status != "Cancelled" && Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, l.UnitId)) && (prop.Length == 0 || Rent.PropertyIdOfUnit(d, l.UnitId) == prop)).ToList();
        }

        private static string MonthBox(string id, string value)
        {
            return "<input type=\"month\" id=\"" + id + "\" value=\"" + Ui.Esc(value) + "\" onchange=\"AdminPaymentsJs.list(true)\" aria-label=\"Month\">";
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
            string open = Ui.Clip(GetDataValue("lcode"), 12).ToUpperInvariant();
            List<PropertyRec> mine = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).ToList();
            StringBuilder head = new StringBuilder("<div class=\"pa-ph\"><div><h1>Rent and payments</h1><p>Rent is charged on the " + Nth(d.Site.RentDueDay) + " of each month and a " + Fmt.Whole(d.Site.LateFee) + " late fee after the " + Nth(d.Site.RentDueDay + d.Site.GraceDays - 1) + ", by themselves. Record payments taken at the office, add other charges and credits, and follow what each lease owes.</p></div><div class=\"pa-acts-row\">");
            if (Can(c, 'A'))
            {
                head.Append("<button type=\"button\" class=\"pa-btn\" onclick=\"AdminPaymentsJs.entry('','Payment')\">Record a payment</button><button type=\"button\" class=\"pa-btn pa-btn-ghost\" onclick=\"AdminPaymentsJs.entry('','Charge')\">Add a charge</button>");
            }
            if (Can(c, 'E'))
            {
                head.Append("<button type=\"button\" class=\"pa-btn pa-btn-ghost\" onclick=\"AdminPaymentsJs.post()\">Post rent now</button>");
            }
            head.Append("</div></div>");
            response.SetElementContents("pa-main", head + "<div id=\"pa-tabs\">" + TabsHtml("bal") + "</div><div class=\"pa-tools\"><input type=\"search\" id=\"pa-q\" placeholder=\"Search by lease, resident or unit\" maxlength=\"40\" oninput=\"AdminPaymentsJs.type()\" aria-label=\"Search\"><select id=\"pa-fp\" onchange=\"AdminPaymentsJs.list(true)\" aria-label=\"Property\">" + Opt(string.Empty, "All properties", string.Empty) + string.Concat(mine.Select(p => Opt(p.Id, p.Name, string.Empty))) + "</select><span id=\"pa-mbox\" hidden>" + MonthBox("pa-fm", Books.Month(DateTime.Today)) + "</span><label class=\"pa-check\" id=\"pa-obox\"><input type=\"checkbox\" id=\"pa-fo\" checked onchange=\"AdminPaymentsJs.list(true)\">Only leases that owe</label></div><div id=\"pa-sums\"></div><div id=\"pa-list\">" + BalancesHtml(c, d, string.Empty, string.Empty, true, 1) + "</div>");
            response.SetElementContents("pa-sums", SumsBalances(c, d, string.Empty));
            if (open.Length > 0)
            {
                LeaseRec? l = Leases(c, d, string.Empty).FirstOrDefault(x => x.Code == open);
                if (l != null)
                {
                    response.ExecuteScript("AdminPaymentsJs.card('" + Ui.Js(l.Id) + "');");
                }
            }
            return response;
        }

        private static string Nth(int n)
        {
            string end = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return n + end;
        }

        private static string TabsHtml(string tab)
        {
            return "<div class=\"pa-tabs\" role=\"tablist\">" + string.Concat(Tabs.Select(t => "<button type=\"button\" role=\"tab\" class=\"pa-tab" + (t.Key == tab ? " pa-act" : string.Empty) + "\" aria-selected=\"" + (t.Key == tab ? "true" : "false") + "\" onclick=\"AdminPaymentsJs.tab('" + t.Key + "')\">" + t.Name + "</button>")) + "</div>";
        }

        private static bool Match(RentData d, LeaseRec l, string term)
        {
            return term.Length == 0 || l.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || Ui.Has(Rent.Names(d, l), term) || Ui.Has(Rent.UnitLabel(d, l.UnitId), term);
        }

        private static string Money(decimal v)
        {
            return Math.Abs(v) < 0.005m ? "<span class=\"pa-mini\">-</span>" : Fmt.Money(v);
        }

        private static string SumsBalances(Ctx c, RentData d, string prop)
        {
            List<LeaseRec> ls = Leases(c, d, prop);
            HashSet<string> ids = new HashSet<string>(ls.Select(l => l.Id));
            decimal owed = ls.Sum(l => Math.Max(0, Rent.Balance(d, l.Id)));
            decimal credit = ls.Sum(l => Math.Min(0, Rent.Balance(d, l.Id)));
            DateTime m = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            decimal billed = d.Ledger.Where(x => ids.Contains(x.LeaseId) && x.Kind == "Rent" && x.When >= m).Sum(x => x.Amount);
            decimal got = -d.Ledger.Where(x => ids.Contains(x.LeaseId) && x.Kind == "Payment" && x.When >= m).Sum(x => x.Amount);
            return "<div class=\"pa-tiles\"><div class=\"pa-tile" + (owed > 0 ? " pa-warn" : string.Empty) + "\"><b>" + Fmt.Money(owed) + "</b><span>owed by " + Ui.Count(ls.Count(l => Rent.Balance(d, l.Id) > 0.004m), "lease", "leases") + "</span></div><div class=\"pa-tile\"><b>" + Fmt.Money(-credit) + "</b><span>credit held for residents</span></div><div class=\"pa-tile\"><b>" + Fmt.Money(billed) + "</b><span>rent charged this month</span></div><div class=\"pa-tile\"><b>" + Fmt.Money(got) + "</b><span>payments this month</span></div></div>";
        }

        private static string BalancesHtml(Ctx c, RentData d, string term, string prop, bool owing, int page)
        {
            var rows = Leases(c, d, prop).Where(l => Match(d, l, term)).Select(l => new { L = l, Bal = Rent.Balance(d, l.Id) }).Where(x => owing ? x.Bal > 0.004m : (Rent.Live.Contains(x.L.Status) || Math.Abs(x.Bal) > 0.004m)).OrderByDescending(x => x.Bal).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"pa-panel pa-empty\">" + (owing ? "Nobody owes anything. Every lease is paid up." : "No leases match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"pa-wrapt\"><table class=\"pa-table\"><thead><tr><th>Lease</th><th>Home</th><th class=\"pa-num\">0 to 30 days</th><th class=\"pa-num\">31 to 60</th><th class=\"pa-num\">61 to 90</th><th class=\"pa-num\">Over 90</th><th class=\"pa-num\">Balance</th><th>Last payment</th></tr></thead><tbody>");
            foreach (var x in rows.Skip((page - 1) * Size).Take(Size))
            {
                List<LedgerRec> lines = d.Ledger.Where(g => g.LeaseId == x.L.Id).ToList();
                (decimal a, decimal b, decimal cc, decimal e) = Billing.Aging(lines);
                LedgerRec? last = lines.Where(g => g.Kind == "Payment").OrderByDescending(g => g.When).FirstOrDefault();
                sb.Append("<tr class=\"pa-row\" onclick=\"AdminPaymentsJs.card('" + Ui.Js(x.L.Id) + "')\"><td class=\"pa-first\"><b>" + Ui.Esc(x.L.Code) + "</b><small>" + Ui.Esc(Rent.Names(d, x.L)) + (Rent.Live.Contains(x.L.Status) ? string.Empty : " · " + x.L.Status.ToLowerInvariant()) + "</small></td><td data-label=\"Home\">" + Ui.Esc(Rent.UnitLabel(d, x.L.UnitId)) + "</td><td class=\"pa-num\" data-label=\"0 to 30 days\">" + Money(a) + "</td><td class=\"pa-num\" data-label=\"31 to 60\">" + Money(b) + "</td><td class=\"pa-num\" data-label=\"61 to 90\">" + Money(cc) + "</td><td class=\"pa-num\" data-label=\"Over 90\">" + Money(e) + "</td><td class=\"pa-num\" data-label=\"Balance\">" + (x.Bal > 0.004m ? "<b class=\"pa-owe\">" + Fmt.Money(x.Bal) + "</b>" : Fmt.Money(x.Bal)) + "</td><td data-label=\"Last payment\">" + (last == null ? "-" : Ui.Esc(Fmt.Day(last.When)) + "<small class=\"pa-mini\"><br>" + Fmt.Money(-last.Amount) + "</small>") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "lease", "leases"));
            return sb.ToString();
        }

        private static string EntriesHtml(Ctx c, RentData d, List<AdminUser> staff, string term, string prop, DateTime month, bool pay, int page, out string sums)
        {
            DateTime from = month;
            DateTime to = month.AddMonths(1);
            Dictionary<string, LeaseRec> ls = Leases(c, d, prop).Where(l => Match(d, l, term)).ToDictionary(l => l.Id);
            List<LedgerRec> rows = d.Ledger.Where(x => ls.ContainsKey(x.LeaseId) && x.When >= from && x.When < to && (pay ? x.Amount < 0 || x.Kind == "Refund" : x.Amount > 0 && x.Kind != "Refund")).OrderByDescending(x => x.When).ToList();
            IEnumerable<IGrouping<string, LedgerRec>> groups = pay ? rows.GroupBy(x => x.Kind == "Payment" ? (x.Method.Length > 0 ? x.Method : "Other") : x.Kind) : rows.GroupBy(x => x.Kind);
            sums = "<div class=\"pa-tiles\"><div class=\"pa-tile\"><b>" + Fmt.Money(pay ? -rows.Sum(x => x.Amount) : rows.Sum(x => x.Amount)) + "</b><span>" + (pay ? "received and credited, less refunds," : "charged") + " in " + Ui.Esc(month.ToString("MMMM yyyy", Fmt.Us)) + "</span></div>" + string.Concat(groups.OrderByDescending(g => Math.Abs(g.Sum(x => x.Amount))).Take(5).Select(g => "<div class=\"pa-tile\"><b>" + Fmt.Money(Math.Abs(g.Sum(x => x.Amount))) + "</b><span>" + Ui.Esc(g.Key) + " &middot; " + g.Count() + "</span></div>")) + "</div>";
            if (rows.Count == 0)
            {
                return "<div class=\"pa-panel pa-empty\">Nothing " + (pay ? "received" : "charged") + " in this month.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"pa-wrapt\"><table class=\"pa-table\"><thead><tr><th>Date</th><th>Lease</th><th>Item</th><th>" + (pay ? "Method" : "Kind") + "</th><th class=\"pa-num\">Amount</th><th>By</th></tr></thead><tbody>");
            foreach (LedgerRec x in rows.Skip((page - 1) * Size).Take(Size))
            {
                LeaseRec l = ls[x.LeaseId];
                sb.Append("<tr class=\"pa-row\" onclick=\"AdminPaymentsJs.card('" + Ui.Js(l.Id) + "')\"><td class=\"pa-first\"><b>" + Ui.Esc(Fmt.Day(x.When)) + "</b></td><td data-label=\"Lease\">" + Ui.Esc(l.Code) + "<small class=\"pa-mini\"><br>" + Ui.Esc(Rent.UnitLabel(d, l.UnitId)) + "</small></td><td data-label=\"Item\">" + Ui.Esc(x.Label.Length > 0 ? x.Label : x.Kind) + "</td><td data-label=\"" + (pay ? "Method" : "Kind") + "\">" + Ui.Esc(pay ? (x.Method.Length > 0 ? x.Method : x.Kind) + (x.Reference.Length > 0 ? " · " + x.Reference : string.Empty) : x.Kind) + "</td><td class=\"pa-num\" data-label=\"Amount\">" + (pay && x.Amount > 0 ? "-" : string.Empty) + Fmt.Money(Math.Abs(x.Amount)) + "</td><td data-label=\"By\">" + Ui.Esc(x.By == "system" ? "Automatic" : x.By == "resident" ? "Resident online" : staff.FirstOrDefault(u => u.Id == x.By)?.Name ?? "Staff") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "entry", "entries"));
            return sb.ToString();
        }

        private static string OnlineHtml(Ctx c, RentData d, List<PayAttempt> all, string term, string prop, int page)
        {
            Dictionary<string, LeaseRec> ls = Leases(c, d, prop).Where(l => Match(d, l, term)).ToDictionary(l => l.Id);
            List<PayAttempt> rows = all.Where(a => ls.ContainsKey(a.LeaseId)).OrderByDescending(a => a.Created).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"pa-panel pa-empty\">No online payments yet. Residents pay from the resident portal once online payment is set up in Settings.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"pa-wrapt\"><table class=\"pa-table\"><thead><tr><th>Started</th><th>Lease</th><th>Resident</th><th class=\"pa-num\">Amount</th><th>Status</th></tr></thead><tbody>");
            foreach (PayAttempt a in rows.Skip((page - 1) * Size).Take(Size))
            {
                LeaseRec l = ls[a.LeaseId];
                ResidentRec? r = d.Residents.FirstOrDefault(x => x.Id == a.ResidentId);
                string st = a.Status == "Paid" ? "Paid" : a.Status == "Failed" ? "Failed" : a.Created < DateTime.Now.AddDays(-1) ? "Not finished" : "Open";
                sb.Append("<tr class=\"pa-row\" onclick=\"AdminPaymentsJs.card('" + Ui.Js(l.Id) + "')\"><td class=\"pa-first\"><b>" + Ui.Esc(Fmt.When(a.Created)) + "</b><small>" + Ui.Esc(a.Source == "stripe" ? "Stripe" : "Test payment") + "</small></td><td data-label=\"Lease\">" + Ui.Esc(l.Code) + "<small class=\"pa-mini\"><br>" + Ui.Esc(Rent.UnitLabel(d, l.UnitId)) + "</small></td><td data-label=\"Resident\">" + Ui.Esc(r == null ? "-" : Rent.Name(r)) + "</td><td class=\"pa-num\" data-label=\"Amount\">" + Fmt.Money(a.Amount) + "</td><td data-label=\"Status\"><span class=\"pa-pill pa-pill-" + (st == "Paid" ? "go" : st == "Failed" ? "bad" : st == "Open" ? "wait" : "off") + "\">" + Ui.Esc(st) + "</span></td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "online payment", "online payments"));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            RentData d = await Rent.Load();
            string tab = Ui.Clip(GetDataValue("tab"), 4);
            string term = Term(GetDataValue("term"));
            string prop = Ui.Clip(GetDataValue("prop"), 60);
            int page = PageNo(GetDataValue("pg"));
            DateTime month = Books.ParseMonth(GetDataValue("month"), out DateTime m) ? m : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            string list;
            string sums;
            if (tab is "pay" or "chg")
            {
                list = EntriesHtml(c, d, await Store.LoadStaff(), term, prop, month, tab == "pay", page, out sums);
            }
            else if (tab == "web")
            {
                list = OnlineHtml(c, d, await Store.Rows<PayAttempt>("payattempts"), term, prop, page);
                sums = string.Empty;
            }
            else
            {
                list = BalancesHtml(c, d, term, prop, (GetDataValue("owing") ?? string.Empty) == "1", page);
                sums = SumsBalances(c, d, prop);
            }
            response.SetElementContents("pa-list", list);
            response.SetElementContents("pa-sums", sums);
            response.SetElementContents("pa-tabs", TabsHtml(Tabs.Any(t => t.Key == tab) ? tab : "bal"));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            await Refresh(response, c);
            return response;
        }

        private static bool Manual(LedgerRec x)
        {
            return x.By is not ("system" or "resident") && x.Kind is "Payment" or "Credit" or "Refund" or "Charge" or "Utility" or "Parking" or "Pet fee" or "Key or lock" or "Cleaning" or "Damage";
        }

        private static string CardHtml(Ctx c, RentData d, LeaseRec l, string note)
        {
            List<LedgerRec> rows = d.Ledger.Where(x => x.LeaseId == l.Id).OrderBy(x => x.When.Date).ThenBy(x => x.Amount < 0 ? 1 : 0).ThenBy(x => x.When).ToList();
            decimal bal = rows.Sum(x => x.Amount);
            (decimal a, decimal b, decimal cc, decimal e) = Billing.Aging(rows);
            StringBuilder sb = new StringBuilder("<div class=\"pa-stack\" id=\"pa-form\"><input type=\"hidden\" id=\"pa-f-id\" value=\"" + Ui.Esc(l.Id) + "\">" + (note.Length > 0 ? "<p class=\"pa-ok\">" + Ui.Esc(note) + "</p>" : string.Empty));
            sb.Append("<div class=\"pa-bar\"><span>" + Pill(Rent.Holdover(l) ? "Month to month" : l.Status) + " " + Ui.Esc(Rent.UnitLabel(d, l.UnitId) + " · " + Rent.Names(d, l) + " · " + Fmt.Money(l.Rent) + " a month") + "</span><span class=\"pa-acts-row\">" + (Can(c, 'A') ? "<button type=\"button\" class=\"pa-btn pa-btn-sm\" onclick=\"AdminPaymentsJs.entry('" + Ui.Js(l.Id) + "','Payment')\">Record payment</button><button type=\"button\" class=\"pa-btn pa-btn-ghost pa-btn-sm\" onclick=\"AdminPaymentsJs.entry('" + Ui.Js(l.Id) + "','Charge')\">Charge</button><button type=\"button\" class=\"pa-btn pa-btn-ghost pa-btn-sm\" onclick=\"AdminPaymentsJs.entry('" + Ui.Js(l.Id) + "','Credit')\">Credit</button>" + (bal < -0.004m ? "<button type=\"button\" class=\"pa-btn pa-btn-ghost pa-btn-sm\" onclick=\"AdminPaymentsJs.entry('" + Ui.Js(l.Id) + "','Refund')\">Refund</button>" : string.Empty) : string.Empty) + "<button type=\"button\" class=\"pa-btn pa-btn-ghost pa-btn-sm\" onclick=\"window.print()\">Print</button></span></div>");
            sb.Append("<div class=\"pa-entrybox\" id=\"pa-entrybox\"></div>");
            sb.Append("<div class=\"pa-tiles pa-tiles5\"><div class=\"pa-tile" + (bal > 0.004m ? " pa-warn" : string.Empty) + "\"><b>" + Fmt.Money(bal) + "</b><span>" + (bal > 0.004m ? "owed" : bal < -0.004m ? "credit" : "balance") + "</span></div><div class=\"pa-tile\"><b>" + Fmt.Money(a) + "</b><span>0 to 30 days</span></div><div class=\"pa-tile\"><b>" + Fmt.Money(b) + "</b><span>31 to 60 days</span></div><div class=\"pa-tile\"><b>" + Fmt.Money(cc) + "</b><span>61 to 90 days</span></div><div class=\"pa-tile\"><b>" + Fmt.Money(e) + "</b><span>over 90 days</span></div></div>");
            List<(LedgerRec X, decimal Run)> run = new List<(LedgerRec, decimal)>();
            decimal t = 0;
            foreach (LedgerRec x in rows)
            {
                t += x.Amount;
                run.Add((x, t));
            }
            run.Reverse();
            sb.Append(rows.Count == 0 ? "<p class=\"pa-mini\">Nothing charged or paid yet.</p>" : "<div class=\"pa-wrapt\"><table class=\"pa-table pa-ledger\"><thead><tr><th>Date</th><th>Item</th><th class=\"pa-num\">Charge</th><th class=\"pa-num\">Paid or credit</th><th class=\"pa-num\">Balance</th><th></th></tr></thead><tbody>" + string.Concat(run.Select(r => "<tr><td class=\"pa-first\">" + Ui.Esc(Fmt.Day(r.X.When)) + "</td><td data-label=\"Item\">" + Ui.Esc(r.X.Label.Length > 0 ? r.X.Label : r.X.Kind) + (r.X.Method.Length > 0 || r.X.Reference.Length > 0 ? "<small class=\"pa-mini\"><br>" + Ui.Esc(string.Join(" · ", new[] { r.X.Method, r.X.Reference }.Where(v => v.Length > 0))) + "</small>" : string.Empty) + "</td><td class=\"pa-num\" data-label=\"Charge\">" + (r.X.Amount > 0 ? Fmt.Money(r.X.Amount) : string.Empty) + "</td><td class=\"pa-num\" data-label=\"Paid or credit\">" + (r.X.Amount < 0 ? Fmt.Money(-r.X.Amount) : string.Empty) + "</td><td class=\"pa-num\" data-label=\"Balance\">" + Fmt.Money(r.Run) + "</td><td class=\"pa-acts-cell\">" + (r.X.Kind == "Late fee" && Can(c, 'E') && !rows.Any(y => y.Kind == "Credit" && y.Reference == r.X.Id) ? "<button type=\"button\" class=\"pa-lnk\" onclick=\"AdminPaymentsJs.waive('" + Ui.Js(r.X.Id) + "')\">Waive</button>" : string.Empty) + (Manual(r.X) && Can(c, 'D') ? "<button type=\"button\" class=\"pa-lnk pa-lnk-bad\" onclick=\"AdminPaymentsJs.remove('" + Ui.Js(r.X.Id) + "')\">Remove</button>" : string.Empty) + "</td></tr>")) + "</tbody></table></div>");
            sb.Append("<div class=\"pa-acts\"><button type=\"button\" class=\"pa-btn pa-btn-ghost\" onclick=\"AdminPaymentsJs.closeModal()\">Close</button>" + (Access.Can(c.U, "leases", 'V') ? "<a class=\"pa-btn pa-btn-ghost pa-push\" href=\"AdminLeases?code=" + Ui.Esc(l.Code) + "\">Open the lease</a>" : string.Empty) + "</div></div>");
            return sb.ToString();
        }

        private async Task<(RentData D, LeaseRec? L)> Find(Ctx c, string key)
        {
            string id = Ui.Clip(GetDataValue(key), 60);
            RentData d = await Rent.Load();
            LeaseRec? l = d.Leases.FirstOrDefault(x => x.Id == id);
            return l == null || !Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, l.UnitId)) ? (d, null) : (d, l);
        }

        public async Task<ApiResponse> Card()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, LeaseRec? l) = await Find(c, "id");
            if (l == null)
            {
                return await Refuse(response, c, "open lease account", Ui.Clip(GetDataValue("id"), 60), "That lease was not found.");
            }
            response.SetElementContents("pa-mbody", CardHtml(c, d, l, string.Empty));
            response.ExecuteScript("AdminPaymentsJs.opened('" + Ui.Js(l.Code + " · account") + "');");
            return response;
        }

        public async Task<ApiResponse> EntryForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'A'))
            {
                return await Refuse(response, c, "add entry", string.Empty, "You can view accounts but not add entries.");
            }
            string type = Ui.Clip(GetDataValue("type"), 10);
            type = Types.Contains(type) ? type : "Payment";
            RentData d = await Rent.Load();
            string lid = Ui.Clip(GetDataValue("id"), 60);
            LeaseRec? l = d.Leases.FirstOrDefault(x => x.Id == lid && Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, x.UnitId)));
            StringBuilder sb = new StringBuilder("<form class=\"pa-stack pa-panel\" id=\"pa-entry\" onsubmit=\"return AdminPaymentsJs.saveEntry()\" novalidate><h3>" + (type == "Payment" ? "Record a payment" : type == "Charge" ? "Add a charge" : type == "Credit" ? "Give a credit" : "Refund to the resident") + "</h3><input type=\"hidden\" id=\"pa-f-etype\" data-k=\"etype\" value=\"" + type + "\">");
            if (l == null)
            {
                List<LeaseRec> ls = Leases(c, d, string.Empty).Where(x => Rent.Live.Contains(x.Status) || x.Status == "Upcoming" || Math.Abs(Rent.Balance(d, x.Id)) > 0.004m).OrderBy(x => Rent.UnitLabel(d, x.UnitId)).ToList();
                sb.Append(Field("elease", "Lease", "<select id=\"pa-f-elease\" data-k=\"elease\">" + Opt(string.Empty, "Choose a lease", string.Empty) + string.Concat(ls.Select(x => Opt(x.Id, Rent.UnitLabel(d, x.UnitId) + " · " + Rent.Names(d, x) + " · owes " + Fmt.Money(Rent.Balance(d, x.Id)), string.Empty))) + "</select>"));
            }
            else
            {
                sb.Append("<input type=\"hidden\" id=\"pa-f-elease\" data-k=\"elease\" value=\"" + Ui.Esc(l.Id) + "\">");
            }
            decimal bal = l == null ? 0 : Rent.Balance(d, l.Id);
            string amount = type == "Payment" && bal > 0 ? Num(bal) : type == "Refund" && bal < 0 ? Num(-bal) : string.Empty;
            sb.Append("<div class=\"pa-grid3\">" + Field("ewhen", "Date", Text("ewhen", Fmt.IsoDay(DateTime.Today), 10, "date")) + Field("eamt", "Amount ($)", Text("eamt", amount, 12, "text", " inputmode=\"decimal\"")));
            if (type is "Payment" or "Refund")
            {
                sb.Append(Field("emethod", type == "Payment" ? "Paid by" : "Paid back by", "<select id=\"pa-f-emethod\" data-k=\"emethod\">" + string.Concat(Billing.Methods.Select(m => Opt(m, m, "Check"))) + "</select>") + "</div>" + Field("eref", "Check number or reference (optional)", Text("eref", string.Empty, 40)));
            }
            else
            {
                sb.Append((type == "Charge" ? Field("ekind", "Kind", "<select id=\"pa-f-ekind\" data-k=\"ekind\">" + string.Concat(Billing.ChargeKinds.Select(k => Opt(k, k == "Charge" ? "Other charge" : k, "Charge"))) + "</select>") : "<div></div>") + "</div>" + Field("elabel", "What it is for (the resident sees this)", Text("elabel", string.Empty, 80, "text", " placeholder=\"" + (type == "Charge" ? "For example: October water" : "For example: Goodwill credit for the noise") + "\"")));
            }
            sb.Append("<div class=\"pa-acts\"><button type=\"submit\" class=\"pa-btn\">Save</button><button type=\"button\" class=\"pa-btn pa-btn-ghost\" onclick=\"AdminPaymentsJs.entryClose()\">Cancel</button></div></form>");
            if (l == null)
            {
                response.SetElementContents("pa-mbody", "<div class=\"pa-stack\" id=\"pa-form\"><input type=\"hidden\" id=\"pa-f-id\" value=\"\"><div class=\"pa-entrybox\" id=\"pa-entrybox\">" + sb + "</div></div>");
                response.ExecuteScript("AdminPaymentsJs.opened('" + Ui.Js(type == "Payment" ? "Record a payment" : "Add a charge") + "');");
            }
            else
            {
                response.SetElementContents("pa-entrybox", sb.ToString());
                response.ExecuteScript("AdminPaymentsJs.entryShown();");
            }
            return response;
        }

        public async Task<ApiResponse> SaveEntry()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'A'))
            {
                return await Refuse(response, c, "add entry", string.Empty, "You do not have the right to add entries.");
            }
            (RentData d, LeaseRec? l) = await Find(c, "elease");
            string type = Ui.Clip(GetDataValue("etype"), 10);
            bool wOk = Fmt.ParseDate(GetDataValue("ewhen"), out DateTime when);
            decimal amt = Dec(GetDataValue("eamt"));
            string method = Ui.Clip(GetDataValue("emethod"), 20);
            string kind = Ui.Clip(GetDataValue("ekind"), 20);
            string label = Ui.Clip(GetDataValue("elabel"), 80);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["elease"] = l == null ? "Choose a lease." : string.Empty,
                ["ewhen"] = !wOk ? "Enter the date." : when > DateTime.Today.AddDays(31) || when < DateTime.Today.AddYears(-2) ? "Enter a date in the last two years, up to a month ahead." : string.Empty,
                ["eamt"] = amt <= 0 || amt > 100000 ? "Enter an amount above zero." : type == "Refund" && l != null && amt > -Rent.Balance(d, l.Id) + 0.004m ? "A refund cannot be more than the credit on the account (" + Fmt.Money(Math.Max(0, -Rent.Balance(d, l.Id))) + ")." : string.Empty,
                ["emethod"] = type is "Payment" or "Refund" && !Billing.Methods.Contains(method) ? "Choose how it was paid." : string.Empty,
                ["ekind"] = type == "Charge" && !Billing.ChargeKinds.Contains(kind) ? "Choose the kind." : string.Empty,
                ["elabel"] = type is "Charge" or "Credit" && label.Length < 3 ? "Say what it is for." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("pa-e-" + kv.Key, kv.Value);
            }
            if (!Types.Contains(type) || e.Values.Any(v => v.Length > 0) || l == null)
            {
                return response;
            }
            LedgerRec g = new LedgerRec { Id = Fmt.NewId("g-"), LeaseId = l.Id, When = when.Date.Add(DateTime.Now.TimeOfDay), By = c.U.Id };
            switch (type)
            {
                case "Payment":
                    g.Kind = "Payment";
                    g.Label = "Payment received";
                    g.Amount = -amt;
                    g.Method = method;
                    g.Reference = Ui.Clip(GetDataValue("eref"), 40);
                    break;
                case "Refund":
                    g.Kind = "Refund";
                    g.Label = "Refund of credit";
                    g.Amount = amt;
                    g.Method = method;
                    g.Reference = Ui.Clip(GetDataValue("eref"), 40);
                    break;
                case "Credit":
                    g.Kind = "Credit";
                    g.Label = label;
                    g.Amount = -amt;
                    break;
                default:
                    g.Kind = kind;
                    g.Label = label;
                    g.Amount = amt;
                    break;
            }
            using (await Store.Hold())
            {
                List<LedgerRec> all = await Store.Rows<LedgerRec>("ledger");
                all.Add(g);
                await Store.SaveRows("ledger", all);
            }
            await Store.Log(c.U.Id, type == "Payment" ? "record payment" : type == "Refund" ? "record refund" : type == "Credit" ? "add credit" : "add charge", l.Code + " · " + Fmt.Money(amt), true);
            await Refresh(response, c);
            RentData d2 = await Rent.Load();
            response.SetElementContents("pa-mbody", CardHtml(c, d2, l, (type == "Payment" ? "Payment of " : type == "Refund" ? "Refund of " : type == "Credit" ? "Credit of " : "Charge of ") + Fmt.Money(amt) + " saved. The balance is now " + Fmt.Money(Rent.Balance(d2, l.Id)) + "."));
            response.ExecuteScript("AdminPaymentsJs.opened('" + Ui.Js(l.Code + " · account") + "');AdminPaymentsJs.clean();");
            return response;
        }

        public async Task<ApiResponse> Waive()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "waive late fee", string.Empty, "You do not have the right to waive fees.");
            }
            string gid = Ui.Clip(GetDataValue("gid"), 60);
            RentData d = await Rent.Load();
            LedgerRec? fee = d.Ledger.FirstOrDefault(x => x.Id == gid && x.Kind == "Late fee");
            LeaseRec? l = fee == null ? null : d.Leases.FirstOrDefault(x => x.Id == fee.LeaseId && Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, x.UnitId)));
            if (fee == null || l == null || d.Ledger.Any(x => x.Kind == "Credit" && x.Reference == fee.Id))
            {
                return await Refuse(response, c, "waive late fee", gid, "That late fee was not found or is already waived.");
            }
            using (await Store.Hold())
            {
                List<LedgerRec> all = await Store.Rows<LedgerRec>("ledger");
                all.Add(new LedgerRec { Id = Fmt.NewId("g-"), LeaseId = l.Id, When = DateTime.Now, Kind = "Credit", Label = fee.Label + " waived", Amount = -fee.Amount, Reference = fee.Id, By = c.U.Id });
                await Store.SaveRows("ledger", all);
            }
            await Store.Log(c.U.Id, "waive late fee", l.Code + " · " + fee.Label, true);
            await Refresh(response, c);
            RentData d2 = await Rent.Load();
            response.SetElementContents("pa-mbody", CardHtml(c, d2, l, fee.Label + " waived."));
            response.ExecuteScript("AdminPaymentsJs.clean();");
            return response;
        }

        public async Task<ApiResponse> RemoveEntry()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'D'))
            {
                return await Refuse(response, c, "remove entry", string.Empty, "You do not have the right to remove entries.");
            }
            string gid = Ui.Clip(GetDataValue("gid"), 60);
            RentData d = await Rent.Load();
            LedgerRec? g = d.Ledger.FirstOrDefault(x => x.Id == gid);
            LeaseRec? l = g == null ? null : d.Leases.FirstOrDefault(x => x.Id == g.LeaseId && Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, x.UnitId)));
            if (g == null || l == null || !Manual(g))
            {
                return await Refuse(response, c, "remove entry", gid, "Only entries staff added by hand can be removed.");
            }
            using (await Store.Hold())
            {
                List<LedgerRec> all = await Store.Rows<LedgerRec>("ledger");
                all.RemoveAll(x => x.Id == gid);
                await Store.SaveRows("ledger", all);
            }
            await Store.Log(c.U.Id, "remove entry", l.Code + " · " + (g.Label.Length > 0 ? g.Label : g.Kind) + " · " + Fmt.Money(Math.Abs(g.Amount)), true);
            await Refresh(response, c);
            RentData d2 = await Rent.Load();
            response.SetElementContents("pa-mbody", CardHtml(c, d2, l, "Entry removed."));
            response.ExecuteScript("AdminPaymentsJs.clean();");
            return response;
        }

        public async Task<ApiResponse> PostNow()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "post rent", string.Empty, "You do not have the right to post rent.");
            }
            int added = await Billing.Post(c.U.Id);
            await Refresh(response, c);
            response.ExecuteScript("AdminPaymentsJs.toast('" + Ui.Js(added == 0 ? "Everything is already charged. Nothing new to post." : Ui.Count(added, "charge was", "charges were") + " added.") + "');");
            return response;
        }
    }
}
