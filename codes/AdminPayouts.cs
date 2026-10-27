using System.Globalization;
using System.Text;
using System.Text.Json;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminPayouts : WebPage
    {
        private const string AreaKey = "payouts";
        private const int Size = 25;
        private static readonly string[] PayMethods = { "Bank transfer", "Check" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Payouts | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPayoutsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminPayoutsJs.denied();");
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
            response.SetElementContents("py-brand", Ui.Esc(site.Brand));
            response.SetElementContents("py-menu", await Auth.Menu("py", "AdminPayouts", c));
            response.SetElementContents("py-who", Auth.Who("py", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminPayoutsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static int PageNo(string? v)
        {
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
        }

        private static string Pager(int page, int pages, int count, string one, string many)
        {
            return "<div class=\"py-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"py-btn py-btn-ghost py-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminPayoutsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"py-btn py-btn-ghost py-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminPayoutsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
        }

        private static string Opt(string value, string label, string current)
        {
            return "<option value=\"" + Ui.Esc(value) + "\"" + (value == current ? " selected" : string.Empty) + ">" + Ui.Esc(label) + "</option>";
        }

        private static string Err(string key)
        {
            return "<em class=\"py-err\" id=\"py-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"py-field\" for=\"py-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"py-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
        }

        private static string Num(decimal v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static decimal Dec(string? v)
        {
            return decimal.TryParse((v ?? string.Empty).Trim().TrimStart('$').Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? Math.Round(d, 2) : -1;
        }

        private static string MonthName(string key)
        {
            return Books.ParseMonth(key, out DateTime m) ? m.ToString("MMMM yyyy", Fmt.Us) : key;
        }

        private static string Who(RentData d, string id)
        {
            return d.Owners.FirstOrDefault(o => o.Id == id)?.Name ?? "Removed owner";
        }

        private sealed class Due
        {
            public OwnerRec Owner { get; init; } = new OwnerRec();
            public DateTime Month { get; init; }
            public Statement S { get; init; } = new Statement();
        }

        private static DateTime LastFull()
        {
            return new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
        }

        private static List<Due> Dues(RentData d, string term, bool settled, DateTime from, DateTime to)
        {
            List<Due> list = new List<Due>();
            DateTime top = to > LastFull() ? LastFull() : to;
            for (DateTime m = top; m >= from; m = m.AddMonths(-1))
            {
                DateTime end = m.AddMonths(1).AddDays(-1);
                string key = Books.Month(m);
                foreach (OwnerRec o in d.Owners.Where(o => term.Length == 0 || Ui.Has(o.Name, term) || Ui.Has(o.Company, term)))
                {
                    if (!Books.HadStake(d, o, m, end) && !d.Payouts.Any(p => p.OwnerId == o.Id && p.Month == key))
                    {
                        continue;
                    }
                    Statement s = Books.For(d, o, m);
                    if (!settled && Math.Abs(s.Due) < 0.005m)
                    {
                        continue;
                    }
                    list.Add(new Due { Owner = o, Month = m, S = s });
                }
            }
            return list.OrderByDescending(x => x.Month).ThenBy(x => x.Owner.Name).ToList();
        }

        private static List<PayoutRec> History(RentData d, string term, string method, DateTime from, DateTime to)
        {
            string a = Books.Month(from);
            string b = Books.Month(to);
            return d.Payouts.Where(p => (term.Length == 0 || Ui.Has(Who(d, p.OwnerId), term)) && (method.Length == 0 || p.Method == method) && string.CompareOrdinal(p.Month, a) >= 0 && string.CompareOrdinal(p.Month, b) <= 0).OrderByDescending(p => p.Month, StringComparer.Ordinal).ThenByDescending(p => p.When).ThenBy(p => Who(d, p.OwnerId)).ToList();
        }

        private static string State(Statement s)
        {
            return s.Due > 0.004m ? "<span class=\"py-pill py-pill-wait\">" + Fmt.Money(s.Due) + " to pay</span>" : s.Due < -0.004m ? "<span class=\"py-pill py-pill-bad\">Owner owes " + Fmt.Money(-s.Due) + "</span>" : "<span class=\"py-pill py-pill-go\">Paid</span>";
        }

        private static string StatementHtml(Ctx c, RentData d, OwnerRec o, DateTime month)
        {
            Statement s = Books.For(d, o, month);
            StringBuilder sb = new StringBuilder("<div class=\"py-stmt\" id=\"py-stmt\"><div class=\"py-stmt-h\"><div><b>" + Ui.Esc(d.Site.Legal) + "</b><small>Owner statement · " + Ui.Esc(month.ToString("MMMM yyyy", Fmt.Us)) + "</small></div><div><b>" + Ui.Esc(o.Name) + "</b><small>" + Ui.Esc(o.Company) + "</small></div></div>");
            foreach (string pid in s.PropertyIds)
            {
                List<StatementLine> inc = s.Income.Where(x => x.PropertyId == pid).ToList();
                List<StatementLine> exp = s.Expenses.Where(x => x.PropertyId == pid).ToList();
                sb.Append("<h4>" + Ui.Esc(Rent.PropertyName(d, pid)) + ShareNote(d, o, pid, s.From, s.To) + "</h4><table class=\"py-st\"><tbody>");
                List<string> rows = new List<string>();
                foreach (IGrouping<(DateTime From, DateTime To, decimal Share), StatementLine> g in inc.Where(x => x.Spread).GroupBy(x => (x.From, x.To, x.Share)).OrderBy(g => g.Key.From).ThenByDescending(g => g.Key.Share))
                {
                    List<string> bits = new List<string>();
                    if (g.Key.From != s.From || g.Key.To != s.To)
                    {
                        bits.Add(Span(g.Key.From, g.Key.To));
                    }
                    if (g.Key.Share < 100m)
                    {
                        bits.Add(Books.Pct(g.Key.Share));
                    }
                    bits.Add(Ui.Count(g.Select(x => x.Detail).Distinct().Count(), "home", "homes"));
                    rows.Add("<tr><td>Rent <small class=\"py-mini\">" + Ui.Esc(string.Join(" · ", bits)) + "</small></td><td class=\"py-num\">" + Fmt.Money(g.Sum(x => x.Amount)) + "</td></tr>");
                }
                foreach (IGrouping<(string Label, decimal Share), StatementLine> g in inc.Where(x => !x.Spread).GroupBy(x => (x.Label, x.Share)).OrderBy(g => g.Key.Label).ThenByDescending(g => g.Key.Share))
                {
                    rows.Add("<tr><td>" + Ui.Esc(g.Key.Label) + " <small class=\"py-mini\">" + Ui.Esc((g.Key.Share < 100m ? Books.Pct(g.Key.Share) + " · " : string.Empty) + Ui.Count(g.Select(x => x.Detail).Distinct().Count(), "home", "homes")) + "</small></td><td class=\"py-num\">" + Fmt.Money(g.Sum(x => x.Amount)) + "</td></tr>");
                }
                sb.Append("<tr class=\"py-st-g\"><td colspan=\"2\">Income</td></tr>" + (rows.Count == 0 ? "<tr><td class=\"py-mini\">No income</td><td></td></tr>" : string.Concat(rows)));
                sb.Append("<tr class=\"py-st-g\"><td colspan=\"2\">Expenses</td></tr>" + (exp.Count == 0 ? "<tr><td class=\"py-mini\">No expenses</td><td></td></tr>" : string.Concat(exp.Select(x => "<tr><td>" + Ui.Esc((x.Spread ? Span(x.From, x.To) : Fmt.Short(x.When)) + " · " + x.Label) + " <small class=\"py-mini\">" + Ui.Esc(x.Detail + (x.Spread ? " · paid " + Fmt.Short(x.When) : string.Empty) + (x.Share < 100m ? " · " + Books.Pct(x.Share) : string.Empty)) + "</small></td><td class=\"py-num\">-" + Fmt.Money(x.Amount) + "</td></tr>"))));
                decimal rent = inc.Where(x => x.Label == "Rent").Sum(x => x.Amount);
                decimal fee = Math.Round(rent * s.Fee / 100m, 2);
                if (pid == s.PropertyIds[s.PropertyIds.Count - 1])
                {
                    fee = s.FeeAmount - s.PropertyIds.Take(s.PropertyIds.Count - 1).Sum(q => Math.Round(s.Income.Where(x => x.PropertyId == q && x.Label == "Rent").Sum(x => x.Amount) * s.Fee / 100m, 2));
                }
                sb.Append("<tr><td>Management fee " + s.Fee.ToString("0.##", Fmt.Us) + "% of rent earned</td><td class=\"py-num\">-" + Fmt.Money(fee) + "</td></tr></tbody></table>");
            }
            if (s.PropertyIds.Count == 0)
            {
                sb.Append("<p class=\"py-mini\">No property in this month.</p>");
            }
            sb.Append("<table class=\"py-st py-st-t\"><tbody><tr><td>Total income</td><td class=\"py-num\">" + Fmt.Money(s.TotalIncome) + "</td></tr><tr><td>Total expenses</td><td class=\"py-num\">-" + Fmt.Money(s.TotalExpenses) + "</td></tr><tr><td>Management fee</td><td class=\"py-num\">-" + Fmt.Money(s.FeeAmount) + "</td></tr><tr class=\"py-st-net\"><td>Net to owner</td><td class=\"py-num\">" + Fmt.Money(s.Net) + "</td></tr>" + (s.PaidAhead != 0 ? "<tr><td>" + (s.PaidAhead > 0 ? "Held back: expenses paid ahead for later days" : "Released: expenses paid ahead now deducted") + "</td><td class=\"py-num\">" + (s.PaidAhead > 0 ? "-" : string.Empty) + Fmt.Money(Math.Abs(s.PaidAhead)) + "</td></tr>" : string.Empty) + "<tr><td>Paid out</td><td class=\"py-num\">" + Fmt.Money(s.Paid) + "</td></tr><tr class=\"py-st-net\"><td>" + (s.Due >= 0 ? "Still to pay" : "Owner owes") + "</td><td class=\"py-num\">" + Fmt.Money(Math.Abs(s.Due)) + "</td></tr></tbody></table>");
            if (s.TotalPrepaid > 0)
            {
                sb.Append("<p class=\"py-mini py-held\">Rent collected for days after " + Ui.Esc(Fmt.Day(s.To)) + ", held until it is earned: " + Fmt.Money(s.TotalPrepaid) + "</p>");
            }
            if (s.HeldAhead > 0)
            {
                sb.Append("<p class=\"py-mini py-held\">Bills paid from this owner's share for days after " + Ui.Esc(Fmt.Day(s.To)) + ", held back until those days come: " + Fmt.Money(s.HeldAhead) + "</p>");
            }
            List<PayoutRec> paid = d.Payouts.Where(p => p.OwnerId == o.Id && p.Month == Books.Month(month)).OrderBy(p => p.When).ToList();
            if (paid.Count > 0)
            {
                sb.Append("<ul class=\"py-paid\">" + string.Concat(paid.Select(p => "<li>" + Ui.Esc(Fmt.Day(p.When) + " · " + p.Method + (p.Reference.Length > 0 ? " " + p.Reference : string.Empty) + " · " + Fmt.Money(p.Amount)) + (Can(c, 'D') ? " <button type=\"button\" class=\"py-lnk py-lnk-bad\" onclick=\"AdminPayoutsJs.remove('" + Ui.Js(p.Id) + "','" + Ui.Js(o.Name + ", " + Fmt.Money(p.Amount)) + "')\">Remove</button>" : string.Empty) + "</li>")) + "</ul>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Span(DateTime from, DateTime to)
        {
            if (from == to)
            {
                return Fmt.Short(from);
            }
            return from.Year == to.Year && from.Month == to.Month ? Fmt.Short(from) + "–" + to.Day.ToString(CultureInfo.InvariantCulture) : Fmt.Short(from) + " – " + Fmt.Short(to);
        }

        private static string ShareNote(RentData d, OwnerRec o, string pid, DateTime from, DateTime to)
        {
            List<(DateTime From, DateTime To, decimal Share)> list = Books.Periods(d, o.Id, pid, from, to);
            if (list.Count == 0)
            {
                return " <small class=\"py-mini\">earlier share</small>";
            }
            if (list.Count == 1 && list[0].From == from.Date && list[0].To == to.Date)
            {
                return list[0].Share < 100m ? " <small class=\"py-mini\">" + Ui.Esc(Books.Pct(list[0].Share)) + " share</small>" : string.Empty;
            }
            return " <small class=\"py-mini\">" + Ui.Esc(string.Join(", ", list.Select(x => Books.Pct(x.Share) + " " + Span(x.From, x.To)))) + "</small>";
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
            response.SetElementContents("py-main", "<div class=\"py-ph\"><div><h1>Payouts</h1><p>What each owner is owed from their monthly statements, and every payout sent. A statement's amount to pay already holds back bills paid ahead for later days.</p></div></div><div class=\"py-tabs\" role=\"tablist\"><button type=\"button\" role=\"tab\" class=\"py-tab py-act\" id=\"py-t-due\" onclick=\"AdminPayoutsJs.tab('due')\">To pay</button><button type=\"button\" role=\"tab\" class=\"py-tab\" id=\"py-t-hist\" onclick=\"AdminPayoutsJs.tab('hist')\">History</button></div><div class=\"py-tools\" id=\"py-tools\">" + ToolsHtml(d, "due") + "</div><div id=\"py-list\">" + DueHtml(c, d, Dues(d, string.Empty, false, LastFull().AddMonths(-11), LastFull()), 1) + "</div>");
            return response;
        }

        private static string ToolsHtml(RentData d, string tab)
        {
            string tail = "<button type=\"button\" class=\"py-btn py-btn-ghost\" onclick=\"AdminPayoutsJs.csv()\">Download spreadsheet</button><button type=\"button\" class=\"py-btn py-btn-ghost\" onclick=\"window.print()\">Print</button>";
            string head = "<input type=\"search\" id=\"py-q\" placeholder=\"Search owner\" maxlength=\"40\" oninput=\"AdminPayoutsJs.type()\" aria-label=\"Search owner\"><label class=\"py-dl\">Statements from <input type=\"month\" id=\"py-pf\" value=\"" + Books.Month(LastFull().AddMonths(-11)) + "\" onchange=\"AdminPayoutsJs.list(true)\"></label><label class=\"py-dl\">to <input type=\"month\" id=\"py-pt\" value=\"" + Books.Month(LastFull()) + "\" onchange=\"AdminPayoutsJs.list(true)\"></label>";
            if (tab == "hist")
            {
                return head + "<select id=\"py-fm\" onchange=\"AdminPayoutsJs.list(true)\" aria-label=\"Method\">" + Opt(string.Empty, "Every method", string.Empty) + string.Concat(PayMethods.Select(m => Opt(m, m, string.Empty))) + "</select>" + tail;
            }
            return head + "<label class=\"py-check\"><input type=\"checkbox\" id=\"py-fs\" onchange=\"AdminPayoutsJs.list(true)\">Show settled months</label>" + tail;
        }

        private static string DueHtml(Ctx c, RentData d, List<Due> rows, int page)
        {
            if (rows.Count == 0)
            {
                return "<div class=\"py-panel py-empty\">Nothing to pay in this period. Tick Show settled months to see every statement.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"py-wrapt\"><table class=\"py-table\"><thead><tr><th>Owner</th><th>Statement</th><th class=\"py-num\">Net</th><th class=\"py-num\">Held back</th><th class=\"py-num\">Paid</th><th>Still to pay</th><th></th></tr></thead><tbody>");
            foreach (Due x in rows.Skip((page - 1) * Size).Take(Size))
            {
                bool rec = Can(c, 'A') && x.S.Due > 0.004m;
                sb.Append("<tr><td class=\"py-first\"><b>" + Ui.Esc(x.Owner.Name) + "</b>" + (x.Owner.PayTo.Length > 0 ? "<small>" + Ui.Esc(x.Owner.PayTo) + "</small>" : string.Empty) + "</td><td data-label=\"Statement\">" + Ui.Esc(x.Month.ToString("MMMM yyyy", Fmt.Us)) + "</td><td class=\"py-num\" data-label=\"Net\">" + Fmt.Money(x.S.Net) + "</td><td class=\"py-num\" data-label=\"Held back\">" + (x.S.PaidAhead == 0 ? "-" : (x.S.PaidAhead > 0 ? "-" : "+") + Fmt.Money(Math.Abs(x.S.PaidAhead))) + "</td><td class=\"py-num\" data-label=\"Paid\">" + Fmt.Money(x.S.Paid) + "</td><td data-label=\"Still to pay\">" + State(x.S) + "</td><td class=\"py-act-cell\"><button type=\"button\" class=\"py-btn py-btn-ghost py-btn-sm\" onclick=\"AdminPayoutsJs.stmt('" + Ui.Js(x.Owner.Id) + "','" + Books.Month(x.Month) + "')\">Statement</button>" + (rec ? "<button type=\"button\" class=\"py-btn py-btn-sm\" onclick=\"AdminPayoutsJs.record('" + Ui.Js(x.Owner.Id) + "','" + Books.Month(x.Month) + "')\">Record payout</button>" : string.Empty) + "</td></tr>");
            }
            decimal due = rows.Where(x => x.S.Due > 0).Sum(x => x.S.Due);
            sb.Append("<tr class=\"py-total\"><td class=\"py-first\"><b>Total</b><small>" + Ui.Count(rows.Count(x => x.S.Due > 0.004m), "statement to pay", "statements to pay") + "</small></td><td></td><td class=\"py-num\" data-label=\"Net\">" + Fmt.Money(rows.Sum(x => x.S.Net)) + "</td><td></td><td class=\"py-num\" data-label=\"Paid\">" + Fmt.Money(rows.Sum(x => x.S.Paid)) + "</td><td class=\"py-num\" data-label=\"Still to pay\"><b>" + Fmt.Money(due) + "</b></td><td></td></tr>");
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "statement", "statements") + "<p class=\"py-mini\">Complete months only. The month in progress can be paid once it ends.</p>");
            return sb.ToString();
        }

        private static string HistHtml(Ctx c, RentData d, List<PayoutRec> rows, int page, List<AdminUser> staff)
        {
            if (rows.Count == 0)
            {
                return "<div class=\"py-panel py-empty\">No payouts match.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"py-wrapt\"><table class=\"py-table\"><thead><tr><th>Statement</th><th>Owner</th><th>Paid on</th><th>Method</th><th>Reference</th><th>Recorded by</th><th class=\"py-num\">Amount</th><th></th></tr></thead><tbody>");
            foreach (PayoutRec p in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<tr><td class=\"py-first\"><b>" + Ui.Esc(MonthName(p.Month)) + "</b></td><td data-label=\"Owner\">" + Ui.Esc(Who(d, p.OwnerId)) + "</td><td data-label=\"Paid on\">" + Ui.Esc(Fmt.Day(p.When)) + "</td><td data-label=\"Method\">" + Ui.Esc(p.Method) + "</td><td data-label=\"Reference\">" + Ui.Esc(p.Reference.Length > 0 ? p.Reference : "-") + "</td><td data-label=\"Recorded by\">" + Ui.Esc(StaffName(staff, p.By)) + "</td><td class=\"py-num\" data-label=\"Amount\">" + Fmt.Money(p.Amount) + "</td><td class=\"py-act-cell\"><button type=\"button\" class=\"py-lnk\" onclick=\"AdminPayoutsJs.stmt('" + Ui.Js(p.OwnerId) + "','" + Ui.Esc(p.Month) + "')\">Statement</button>" + (Can(c, 'D') ? "<button type=\"button\" class=\"py-lnk py-lnk-bad\" onclick=\"AdminPayoutsJs.remove('" + Ui.Js(p.Id) + "','" + Ui.Js(Who(d, p.OwnerId) + ", " + Fmt.Money(p.Amount)) + "')\">Remove</button>" : string.Empty) + "</td></tr>");
            }
            sb.Append("<tr class=\"py-total\"><td class=\"py-first\"><b>Total</b><small>" + Ui.Count(rows.Count, "payout", "payouts") + "</small></td><td></td><td></td><td></td><td></td><td></td><td class=\"py-num\" data-label=\"Amount\"><b>" + Fmt.Money(rows.Sum(p => p.Amount)) + "</b></td><td></td></tr>");
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "payout", "payouts"));
            return sb.ToString();
        }

        private static string StaffName(List<AdminUser> staff, string id)
        {
            return id.Length == 0 ? "-" : staff.FirstOrDefault(u => u.Id == id)?.Name ?? id;
        }

        private (string Tab, string Term, bool Settled, string Method, DateTime From, DateTime To, int Page) Args()
        {
            string tab = GetDataValue("tab") == "hist" ? "hist" : "due";
            DateTime to = Books.ParseMonth(GetDataValue("pto"), out DateTime t) ? t : LastFull();
            DateTime from = Books.ParseMonth(GetDataValue("pfrom"), out DateTime f) ? f : to.AddMonths(-11);
            if (from > to)
            {
                (from, to) = (to, from);
            }
            if (from < to.AddMonths(-119))
            {
                from = to.AddMonths(-119);
            }
            string method = Ui.Clip(GetDataValue("meth"), 20);
            return (tab, Ui.Clip(GetDataValue("q"), 40), GetDataValue("settled") == "1", PayMethods.Contains(method) ? method : string.Empty, from, to, PageNo(GetDataValue("pg")));
        }

        private async Task<string> ListFor(Ctx c, RentData d)
        {
            (string tab, string term, bool settled, string method, DateTime from, DateTime to, int page) = Args();
            if (tab == "hist")
            {
                return HistHtml(c, d, History(d, term, method, from, to), page, await Store.LoadStaff());
            }
            return DueHtml(c, d, Dues(d, term, settled, from, to), page);
        }

        public async Task<ApiResponse> Tab()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            RentData d = await Rent.Load();
            string tab = GetDataValue("tab") == "hist" ? "hist" : "due";
            response.SetElementContents("py-tools", ToolsHtml(d, tab));
            response.ExecuteScript("AdminPayoutsJs.list(true);");
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            RentData d = await Rent.Load();
            response.SetElementContents("py-list", await ListFor(c, d));
            return response;
        }

        public async Task<ApiResponse> Stmt()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            RentData d = await Rent.Load();
            OwnerRec? o = d.Owners.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("oid"), 60));
            if (o == null || !Books.ParseMonth(GetDataValue("month"), out DateTime m))
            {
                return await Refuse(response, c, "owner statement", string.Empty, "That statement was not found.");
            }
            Statement s = Books.For(d, o, m);
            bool rec = Can(c, 'A') && s.Due > 0.004m && m <= LastFull();
            response.SetElementContents("py-mbody", StatementHtml(c, d, o, m) + "<div class=\"py-acts py-noprint\"><button type=\"button\" class=\"py-btn py-btn-ghost\" onclick=\"window.print()\">Print</button>" + (rec ? "<button type=\"button\" class=\"py-btn\" onclick=\"AdminPayoutsJs.record('" + Ui.Js(o.Id) + "','" + Books.Month(m) + "')\">Record payout</button>" : string.Empty) + "<button type=\"button\" class=\"py-btn py-btn-ghost\" onclick=\"AdminPayoutsJs.closeModal()\">Close</button></div>");
            response.ExecuteScript("AdminPayoutsJs.opened('" + Ui.Js("Statement · " + o.Name + " · " + m.ToString("MMMM yyyy", Fmt.Us)) + "');");
            return response;
        }

        public async Task<ApiResponse> Record()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'A'))
            {
                return await Refuse(response, c, "owner payout", string.Empty, "You do not have the right to record payouts.");
            }
            RentData d = await Rent.Load();
            OwnerRec? o = d.Owners.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("oid"), 60));
            if (o == null || !Books.ParseMonth(GetDataValue("month"), out DateTime m))
            {
                return await Refuse(response, c, "owner payout", string.Empty, "That statement was not found.");
            }
            Statement s = Books.For(d, o, m);
            if (s.Due <= 0.004m)
            {
                return await Refuse(response, c, "owner payout", o.Name, "Nothing is left to pay on this statement.");
            }
            StringBuilder sb = new StringBuilder("<form class=\"py-stack\" id=\"py-pay\" onsubmit=\"return AdminPayoutsJs.pay()\" novalidate><input type=\"hidden\" id=\"py-f-oid\" value=\"" + Ui.Esc(o.Id) + "\"><input type=\"hidden\" id=\"py-f-month\" value=\"" + Books.Month(m) + "\">");
            sb.Append("<dl class=\"py-dl2\"><dt>Statement</dt><dd>" + Ui.Esc(m.ToString("MMMM yyyy", Fmt.Us)) + "</dd><dt>Net to owner</dt><dd>" + Fmt.Money(s.Net) + "</dd>" + (s.PaidAhead != 0 ? "<dt>" + (s.PaidAhead > 0 ? "Held back" : "Released") + "</dt><dd>" + (s.PaidAhead > 0 ? "-" : string.Empty) + Fmt.Money(Math.Abs(s.PaidAhead)) + "</dd>" : string.Empty) + "<dt>Paid so far</dt><dd>" + Fmt.Money(s.Paid) + "</dd><dt>Still to pay</dt><dd><b>" + Fmt.Money(s.Due) + "</b></dd>" + (o.PayTo.Length > 0 ? "<dt>Pay to</dt><dd>" + Ui.Esc(o.PayTo) + "</dd>" : string.Empty) + "</dl>");
            sb.Append("<div class=\"py-grid2\">" + Field("pamt", "Amount ($)", Text("pamt", Num(s.Due), 12, "text", " inputmode=\"decimal\"")) + Field("pdate", "Date", Text("pdate", Fmt.IsoDay(DateTime.Today), 10, "date")) + "</div>");
            sb.Append("<div class=\"py-grid2\">" + Field("pmethod", "Method", "<select id=\"py-f-pmethod\" data-k=\"pmethod\">" + string.Concat(PayMethods.Select(x => Opt(x, x, o.PayTo.Contains("heck", StringComparison.OrdinalIgnoreCase) ? "Check" : "Bank transfer"))) + "</select>") + Field("pref", "Reference (optional)", Text("pref", string.Empty, 40)) + "</div>");
            sb.Append("<div class=\"py-acts\"><button type=\"submit\" class=\"py-btn\">Record payout</button><button type=\"button\" class=\"py-btn py-btn-ghost\" onclick=\"AdminPayoutsJs.closeModal()\">Cancel</button></div></form>");
            response.SetElementContents("py-mbody", sb.ToString());
            response.ExecuteScript("AdminPayoutsJs.opened('" + Ui.Js("Payout to " + o.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> Pay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'A'))
            {
                return await Refuse(response, c, "owner payout", string.Empty, "You do not have the right to record payouts.");
            }
            RentData d = await Rent.Load();
            OwnerRec? o = d.Owners.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("oid"), 60));
            if (o == null || !Books.ParseMonth(GetDataValue("month"), out DateTime m))
            {
                return await Refuse(response, c, "owner payout", string.Empty, "That statement was not found.");
            }
            decimal amt = Dec(GetDataValue("pamt"));
            bool dOk = Fmt.ParseDate(GetDataValue("pdate"), out DateTime when);
            string method = Ui.Clip(GetDataValue("pmethod"), 20);
            Statement s = Books.For(d, o, m);
            string e1 = amt <= 0 ? "Enter the amount." : amt > s.Due + 0.004m ? "That is more than the " + Fmt.Money(s.Due) + " still to pay." : string.Empty;
            string e2 = !dOk ? "Enter the date." : when.Date > DateTime.Today ? "The date cannot be in the future." : string.Empty;
            response.SetElementContents("py-e-pamt", e1);
            response.SetElementContents("py-e-pdate", e2);
            if (e1.Length > 0 || e2.Length > 0 || !PayMethods.Contains(method))
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<PayoutRec> all = await Store.Rows<PayoutRec>("payouts");
                all.Add(new PayoutRec { Id = Fmt.NewId("o-"), OwnerId = o.Id, Month = Books.Month(m), Amount = amt, When = when.Date, Method = method, Reference = Ui.Clip(GetDataValue("pref"), 40), By = c.U.Id });
                await Store.SaveRows("payouts", all);
            }
            await Store.Log(c.U.Id, "owner payout", o.Name + " · " + m.ToString("MMMM yyyy", Fmt.Us) + " · " + Fmt.Money(amt), true);
            RentData d2 = await Rent.Load();
            response.SetElementContents("py-list", await ListFor(c, d2));
            response.ExecuteScript("AdminPayoutsJs.paid('" + Ui.Js("Payout of " + Fmt.Money(amt) + " recorded") + "');");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'D'))
            {
                return await Refuse(response, c, "remove payout", string.Empty, "You do not have the right to remove payouts.");
            }
            string pid = Ui.Clip(GetDataValue("pid"), 60);
            PayoutRec? p;
            using (await Store.Hold())
            {
                List<PayoutRec> all = await Store.Rows<PayoutRec>("payouts");
                p = all.FirstOrDefault(x => x.Id == pid);
                if (p == null)
                {
                    return await Refuse(response, c, "remove payout", pid, "That payout was not found.");
                }
                all.Remove(p);
                await Store.SaveRows("payouts", all);
            }
            RentData d = await Rent.Load();
            await Store.Log(c.U.Id, "remove payout", Who(d, p.OwnerId) + " · " + Fmt.Money(p.Amount), true);
            response.SetElementContents("py-list", await ListFor(c, d));
            response.ExecuteScript("AdminPayoutsJs.toast('Payout removed');");
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
            RentData d = await Rent.Load();
            (string tab, string term, bool settled, string method, DateTime from, DateTime to, int page) = Args();
            List<string[]> rows = new List<string[]>();
            if (tab == "hist")
            {
                List<AdminUser> staff = await Store.LoadStaff();
                rows.Add(new[] { "Statement", "Owner", "Paid on", "Method", "Reference", "Recorded by", "Amount" });
                foreach (PayoutRec p in History(d, term, method, from, to))
                {
                    rows.Add(new[] { MonthName(p.Month), Who(d, p.OwnerId), Fmt.IsoDay(p.When), p.Method, p.Reference, StaffName(staff, p.By), p.Amount.ToString("0.00", CultureInfo.InvariantCulture) });
                }
            }
            else
            {
                rows.Add(new[] { "Owner", "Statement", "Net", "Held back", "Paid", "Still to pay" });
                foreach (Due x in Dues(d, term, settled, from, to))
                {
                    rows.Add(new[] { x.Owner.Name, x.Month.ToString("MMMM yyyy", Fmt.Us), x.S.Net.ToString("0.00", CultureInfo.InvariantCulture), (-x.S.PaidAhead).ToString("0.00", CultureInfo.InvariantCulture), x.S.Paid.ToString("0.00", CultureInfo.InvariantCulture), x.S.Due.ToString("0.00", CultureInfo.InvariantCulture) });
                }
            }
            StringBuilder sb = new StringBuilder();
            foreach (string[] r in rows)
            {
                sb.Append(string.Join(",", r.Select(v => "\"" + v.Replace("\"", "\"\"") + "\"")) + "\r\n");
            }
            string name = (tab == "hist" ? "payout-history-" : "payouts-to-pay-") + Fmt.IsoDay(DateTime.Today) + ".csv";
            await Store.Log(c.U.Id, "download report", tab == "hist" ? "Payout history" : "Payouts to pay", true);
            response.ExecuteScript("AdminPayoutsJs.save(" + JsonSerializer.Serialize(name) + "," + JsonSerializer.Serialize(sb.ToString()) + ");");
            return response;
        }
    }
}
