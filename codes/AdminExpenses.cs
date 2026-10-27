using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminExpenses : WebPage
    {
        private const string AreaKey = "expenses";
        private const int Size = 15;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Expenses | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminExpensesJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminExpensesJs.denied();");
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
            response.SetElementContents("ex-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ex-menu", await Auth.Menu("ex", "AdminExpenses", c));
            response.SetElementContents("ex-who", Auth.Who("ex", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminExpensesJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"ex-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"ex-btn ex-btn-ghost ex-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminExpensesJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"ex-btn ex-btn-ghost ex-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminExpensesJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"ex-pill ex-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"ex-err\" id=\"ex-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"ex-field\" for=\"ex-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"ex-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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
            string month = Books.Month(DateTime.Today);
            List<PropertyRec> mine = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).ToList();
            response.SetElementContents("ex-main", "<div class=\"ex-ph\"><div><h1>Expenses</h1><p>What each property costs: utilities, insurance, taxes, landscaping and supplies. Finished work orders paid by the owner are added here by themselves. Expenses show on the owner statements.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"ex-btn\" onclick=\"AdminExpensesJs.add()\">Add an expense</button>" : string.Empty) + "</div><div class=\"ex-tools\"><input type=\"search\" id=\"ex-q\" placeholder=\"Search by payee or note\" maxlength=\"40\" oninput=\"AdminExpensesJs.type()\" aria-label=\"Search expenses\"><select id=\"ex-fp\" onchange=\"AdminExpensesJs.list(true)\" aria-label=\"Property\">" + Opt(string.Empty, "All properties", string.Empty) + string.Concat(mine.Select(p => Opt(p.Id, p.Name, string.Empty))) + "</select><select id=\"ex-fc\" onchange=\"AdminExpensesJs.list(true)\" aria-label=\"Category\">" + Opt(string.Empty, "Every category", string.Empty) + string.Concat(Books.ExpenseKinds.Select(k => Opt(k, k, string.Empty))) + "</select><input type=\"month\" id=\"ex-fm\" value=\"" + month + "\" onchange=\"AdminExpensesJs.list(true)\" aria-label=\"Month\"></div><div id=\"ex-sums\">" + SumsHtml(c, d, string.Empty, string.Empty, string.Empty, month) + "</div><div id=\"ex-list\">" + ListHtml(c, d, string.Empty, string.Empty, string.Empty, month, 1) + "</div>");
            return response;
        }

        private static List<ExpenseRec> Rows(Ctx c, RentData d, string term, string prop, string cat, string month)
        {
            bool all = !Books.ParseMonth(month, out DateTime m);
            return d.Expenses.Where(e => Access.PropertyOk(c.U, e.PropertyId) && (prop.Length == 0 || e.PropertyId == prop) && (cat.Length == 0 || e.Category == cat) && (all || (e.When.Year == m.Year && e.When.Month == m.Month)) && (term.Length == 0 || Ui.Has(e.Payee, term) || Ui.Has(e.Note, term))).OrderByDescending(e => e.When).ToList();
        }

        private static string SumsHtml(Ctx c, RentData d, string term, string prop, string cat, string month)
        {
            List<ExpenseRec> rows = Rows(c, d, term, prop, cat, month);
            return "<div class=\"ex-tiles\"><div class=\"ex-tile\"><b>" + Fmt.Money(rows.Sum(e => e.Amount)) + "</b><span>" + Ui.Count(rows.Count, "expense", "expenses") + (Books.ParseMonth(month, out DateTime m) ? " in " + Ui.Esc(m.ToString("MMMM yyyy", Fmt.Us)) : string.Empty) + "</span></div>" + string.Concat(rows.GroupBy(e => e.Category).OrderByDescending(g => g.Sum(e => e.Amount)).Take(5).Select(g => "<div class=\"ex-tile\"><b>" + Fmt.Money(g.Sum(e => e.Amount)) + "</b><span>" + Ui.Esc(g.Key) + "</span></div>")) + "</div>";
        }

        private static string ListHtml(Ctx c, RentData d, string term, string prop, string cat, string month, int page)
        {
            List<ExpenseRec> rows = Rows(c, d, term, prop, cat, month);
            if (rows.Count == 0)
            {
                return "<div class=\"ex-panel ex-empty\">No expenses here.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"ex-wrapt\"><table class=\"ex-table\"><thead><tr><th>Date</th><th>Property</th><th>Category</th><th>Payee and note</th><th class=\"ex-num\">Amount</th></tr></thead><tbody>");
            foreach (ExpenseRec e in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<tr class=\"ex-row\" onclick=\"AdminExpensesJs.edit('" + Ui.Js(e.Id) + "')\"><td class=\"ex-first\"><b>" + Ui.Esc(Fmt.Day(e.When)) + "</b>" + (e.WorkOrderId.Length > 0 ? "<small>From a work order</small>" : string.Empty) + "</td><td data-label=\"Property\">" + Ui.Esc(e.UnitId.Length > 0 ? Rent.UnitLabel(d, e.UnitId) : Rent.PropertyName(d, e.PropertyId)) + "</td><td data-label=\"Category\">" + Ui.Esc(e.Category) + "</td><td data-label=\"Payee\">" + Ui.Esc(e.Payee.Length > 0 ? e.Payee : "-") + (e.Note.Length > 0 ? "<small class=\"ex-mini\"><br>" + Ui.Esc(e.Note) + "</small>" : string.Empty) + (Books.Spans(e) ? "<small class=\"ex-mini\"><br>Covers " + Ui.Esc(Fmt.Day(e.CoverFrom!.Value) + " to " + Fmt.Day(e.CoverTo!.Value)) + "</small>" : string.Empty) + "</td><td class=\"ex-num\" data-label=\"Amount\">" + Fmt.Money(e.Amount) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "expense", "expenses"));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            RentData d = await Rent.Load();
            string term = Term(GetDataValue("term"));
            string prop = Ui.Clip(GetDataValue("prop"), 60);
            string cat = Ui.Clip(GetDataValue("cat"), 30);
            string month = Ui.Clip(GetDataValue("month"), 7);
            response.SetElementContents("ex-list", ListHtml(c, d, term, prop, cat, month, PageNo(GetDataValue("pg"))));
            response.SetElementContents("ex-sums", SumsHtml(c, d, term, prop, cat, month));
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

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            RentData d = await Rent.Load();
            ExpenseRec? e = d.Expenses.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && (e == null || !Access.PropertyOk(c.U, e.PropertyId)))
            {
                return await Refuse(response, c, "open expense", id, "That expense was not found.");
            }
            if (e == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add expense", string.Empty, "You can view expenses but not add them.");
            }
            response.SetElementContents("ex-mbody", FormHtml(c, d, e));
            response.ExecuteScript("AdminExpensesJs.opened('" + Ui.Js(e == null ? "New expense" : e.Category + ", " + Fmt.Day(e.When)) + "');");
            return response;
        }

        private static string FormHtml(Ctx c, RentData d, ExpenseRec? e)
        {
            bool isNew = e == null;
            ExpenseRec r = e ?? new ExpenseRec { When = DateTime.Today, Category = "Utilities" };
            bool linked = r.WorkOrderId.Length > 0;
            bool edit = !linked && (isNew ? Can(c, 'A') : Can(c, 'E'));
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            string place = r.UnitId.Length > 0 ? "u:" + r.UnitId : r.PropertyId.Length > 0 ? "p:" + r.PropertyId : string.Empty;
            StringBuilder places = new StringBuilder();
            foreach (PropertyRec p in d.Properties.Where(x => Access.PropertyOk(c.U, x.Id)))
            {
                places.Append("<optgroup label=\"" + Ui.Esc(p.Name) + "\">" + Opt("p:" + p.Id, p.Name + " · whole property", place) + string.Concat(d.Units.Where(u => u.PropertyId == p.Id).OrderBy(u => u.Number, StringComparer.OrdinalIgnoreCase).Select(u => Opt("u:" + u.Id, p.Name + " · " + u.Number, place))) + "</optgroup>");
            }
            StringBuilder sb = new StringBuilder("<form class=\"ex-stack\" id=\"ex-form\" onsubmit=\"return AdminExpensesJs.save()\" novalidate><input type=\"hidden\" id=\"ex-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            if (linked)
            {
                WorkOrderRec? w = d.WorkOrders.FirstOrDefault(x => x.Id == r.WorkOrderId);
                sb.Append("<p class=\"ex-note\">This expense comes from " + (w != null && Access.Can(c.U, "workorders", 'V') ? "<a href=\"AdminWorkOrders?code=" + Ui.Esc(w.Code) + "\">work order " + Ui.Esc(w.Code) + "</a>" : "a work order") + ". Change the cost or who pays on the work order.</p>");
            }
            sb.Append(Field("place", "Property or unit", "<select id=\"ex-f-place\" data-k=\"place\"" + dis + ">" + (isNew ? Opt(string.Empty, "Choose", string.Empty) : string.Empty) + places + "</select>"));
            sb.Append("<div class=\"ex-grid3\">" + Field("when", "Date", Text("when", Fmt.IsoDay(r.When), 10, "date", ro)) + Field("cat", "Category", "<select id=\"ex-f-cat\" data-k=\"ecat\"" + dis + ">" + string.Concat(Books.ExpenseKinds.Select(k => Opt(k, k, r.Category))) + "</select>") + Field("amt", "Amount ($)", Text("amt", r.Amount > 0 ? Num(r.Amount) : string.Empty, 12, "text", ro + " inputmode=\"decimal\"")) + "</div>");
            sb.Append(Field("payee", "Paid to", Text("payee", r.Payee, 80, "text", ro + " list=\"ex-vendors\"")) + "<datalist id=\"ex-vendors\">" + string.Concat(d.Vendors.Where(v => v.Active).Select(v => "<option value=\"" + Ui.Esc(v.Name) + "\">")) + "</datalist>");
            sb.Append(Field("note", "Note", Text("note", r.Note, 160, "text", ro)));
            sb.Append("<div class=\"ex-grid2\">" + Field("cfrom", "Covers from (optional)", Text("cfrom", r.CoverFrom == null ? string.Empty : Fmt.IsoDay(r.CoverFrom.Value), 10, "date", ro)) + Field("cto", "Covers to (optional)", Text("cto", r.CoverTo == null ? string.Empty : Fmt.IsoDay(r.CoverTo.Value), 10, "date", ro)) + "</div><p class=\"ex-mini\">For a bill that covers a period, like insurance or property tax. Owner statements then spread it by day over that period, and co-owners share each day by their share on that day.</p>");
            sb.Append("<div class=\"ex-acts\">" + (edit ? "<button type=\"submit\" class=\"ex-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"ex-btn ex-btn-ghost\" onclick=\"AdminExpensesJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && !linked && Can(c, 'D') ? "<button type=\"button\" class=\"ex-btn ex-btn-danger ex-push\" onclick=\"AdminExpensesJs.del('" + Ui.Js(r.Id) + "')\">Delete</button>" : string.Empty) + "</div></form>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            bool isNew = id.Length == 0;
            if (!Can(c, isNew ? 'A' : 'E'))
            {
                return await Refuse(response, c, isNew ? "add expense" : "edit expense", id, "You do not have the right to " + (isNew ? "add" : "change") + " expenses.");
            }
            RentData d = await Rent.Load();
            ExpenseRec? old = d.Expenses.FirstOrDefault(x => x.Id == id);
            if (!isNew && (old == null || !Access.PropertyOk(c.U, old.PropertyId) || old.WorkOrderId.Length > 0))
            {
                return await Refuse(response, c, "edit expense", id, old != null && old.WorkOrderId.Length > 0 ? "Change this expense on its work order." : "That expense was not found.");
            }
            string place = Ui.Clip(GetDataValue("place"), 70);
            string pid = string.Empty;
            string uid = string.Empty;
            if (place.StartsWith("u:", StringComparison.Ordinal))
            {
                UnitRec? u = d.Units.FirstOrDefault(x => x.Id == place.Substring(2));
                if (u != null)
                {
                    uid = u.Id;
                    pid = u.PropertyId;
                }
            }
            else if (place.StartsWith("p:", StringComparison.Ordinal) && d.Properties.Any(p => p.Id == place.Substring(2)))
            {
                pid = place.Substring(2);
            }
            bool wOk = Fmt.ParseDate(GetDataValue("when"), out DateTime when);
            string cat = Ui.Clip(GetDataValue("ecat"), 30);
            decimal amt = Dec(GetDataValue("amt"));
            string cfText = Ui.Clip(GetDataValue("cfrom"), 10);
            string ctText = Ui.Clip(GetDataValue("cto"), 10);
            bool cfOk = Fmt.ParseDate(cfText, out DateTime cfrom);
            bool ctOk = Fmt.ParseDate(ctText, out DateTime cto);
            string coverErr = cfText.Length == 0 && ctText.Length == 0 ? string.Empty : !cfOk || !ctOk ? "Enter both dates, or leave both empty." : cto < cfrom ? "The end date must be on or after the start date." : (cto - cfrom).TotalDays > 1096 ? "A period can be 3 years at most." : string.Empty;
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["place"] = pid.Length == 0 || !Access.PropertyOk(c.U, pid) ? "Choose the property or unit." : string.Empty,
                ["when"] = !wOk ? "Enter the date." : when > DateTime.Today.AddDays(60) ? "That date is too far ahead." : string.Empty,
                ["cat"] = !Books.ExpenseKinds.Contains(cat) ? "Choose a category." : string.Empty,
                ["amt"] = amt <= 0 || amt > 1000000 ? "Enter the amount." : string.Empty,
                ["payee"] = string.Empty,
                ["note"] = string.Empty,
                ["cfrom"] = string.Empty,
                ["cto"] = coverErr
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("ex-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<ExpenseRec> all = await Store.Rows<ExpenseRec>("expenses");
                ExpenseRec? x = all.FirstOrDefault(r => r.Id == id);
                if (isNew)
                {
                    x = new ExpenseRec { Id = Fmt.NewId("e-"), Created = DateTime.Now, By = c.U.Id };
                    all.Add(x);
                }
                if (x == null)
                {
                    return await Refuse(response, c, "edit expense", id, "That expense was not found.");
                }
                x.PropertyId = pid;
                x.UnitId = uid;
                x.When = when.Date;
                x.Category = cat;
                x.Amount = amt;
                x.Payee = Ui.Clip(GetDataValue("payee"), 80);
                x.Note = Ui.Clip(GetDataValue("note"), 160);
                x.CoverFrom = coverErr.Length == 0 && cfOk && ctOk ? cfrom.Date : null;
                x.CoverTo = coverErr.Length == 0 && cfOk && ctOk ? cto.Date : null;
                await Store.SaveRows("expenses", all);
            }
            await Store.Log(c.U.Id, isNew ? "add expense" : "edit expense", cat + " · " + Fmt.Money(amt) + " · " + Rent.PropertyName(d, pid), true);
            await Refresh(response, c);
            response.ExecuteScript("AdminExpensesJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Delete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            if (!Can(c, 'D'))
            {
                return await Refuse(response, c, "delete expense", id, "You do not have the right to delete expenses.");
            }
            ExpenseRec? gone;
            using (await Store.Hold())
            {
                List<ExpenseRec> all = await Store.Rows<ExpenseRec>("expenses");
                gone = all.FirstOrDefault(x => x.Id == id && Access.PropertyOk(c.U, x.PropertyId) && x.WorkOrderId.Length == 0);
                if (gone == null)
                {
                    return await Refuse(response, c, "delete expense", id, "That expense was not found or comes from a work order.");
                }
                all.Remove(gone);
                await Store.SaveRows("expenses", all);
            }
            await Store.Log(c.U.Id, "delete expense", gone.Category + " · " + Fmt.Money(gone.Amount), true);
            await Refresh(response, c);
            response.ExecuteScript("AdminExpensesJs.deleted();");
            return response;
        }
    }
}
