using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminShowings : WebPage
    {
        private const string AreaKey = "showings";
        private const int Size = 12;
        private static readonly string[] Statuses = { "Requested", "Confirmed", "Done", "No-show", "Cancelled" };
        private static readonly (string Key, string Name)[] Tabs = { ("next", "Coming up"), ("ask", "To confirm"), ("past", "Past"), ("all", "All") };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Showings | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminShowingsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminShowingsJs.denied();");
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
            response.SetElementContents("sh-brand", Ui.Esc(site.Brand));
            response.SetElementContents("sh-menu", await Auth.Menu("sh", "AdminShowings", c));
            response.SetElementContents("sh-who", Auth.Who("sh", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminShowingsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"sh-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"sh-btn sh-btn-ghost sh-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminShowingsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"sh-btn sh-btn-ghost sh-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminShowingsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"sh-pill sh-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"sh-err\" id=\"sh-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"sh-field\" for=\"sh-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"sh-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static bool Open(ShowingRec s)
        {
            return s.Status is "Requested" or "Confirmed";
        }

        private static IEnumerable<ShowingRec> InTab(IEnumerable<ShowingRec> q, string tab)
        {
            DateTime today = DateTime.Today;
            return tab switch
            {
                "next" => q.Where(s => Open(s) && s.When >= today).OrderBy(s => s.When),
                "ask" => q.Where(s => s.Status == "Requested").OrderBy(s => s.When),
                "past" => q.Where(s => !Open(s) || s.When < today).OrderByDescending(s => s.When),
                _ => q.OrderByDescending(s => s.When)
            };
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
            string tab = Ui.Clip(GetDataValue("tab"), 10);
            tab = Tabs.Any(t => t.Key == tab) ? tab : "next";
            List<PropertyRec> mine = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).ToList();
            response.SetElementContents("sh-main", "<div class=\"sh-ph\"><div><h1>Showings</h1><p>Tours booked on the website and by phone. Confirm each request, then mark it done or a no-show afterwards.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"sh-btn\" onclick=\"AdminShowingsJs.add()\">Add a showing</button>" : string.Empty) + "</div><div id=\"sh-tabs\">" + TabsHtml(c, d, tab, string.Empty) + "</div><div class=\"sh-tools\"><input type=\"search\" id=\"sh-q\" placeholder=\"Search by name, email or phone\" maxlength=\"40\" oninput=\"AdminShowingsJs.type()\" aria-label=\"Search showings\"><select id=\"sh-fp\" onchange=\"AdminShowingsJs.list(true)\" aria-label=\"Property\">" + Opt(string.Empty, "All properties", string.Empty) + string.Concat(mine.Select(p => Opt(p.Id, p.Name, string.Empty))) + "</select></div><div id=\"sh-list\">" + ListHtml(c, d, await Store.LoadStaff(), string.Empty, tab, string.Empty, 1) + "</div>");
            return response;
        }

        private static IEnumerable<ShowingRec> Mine(Ctx c, RentData d, string prop)
        {
            return d.Showings.Where(s => Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, s.UnitId)) && (prop.Length == 0 || Rent.PropertyIdOfUnit(d, s.UnitId) == prop));
        }

        private static string TabsHtml(Ctx c, RentData d, string tab, string prop)
        {
            List<ShowingRec> all = Mine(c, d, prop).ToList();
            return "<div class=\"sh-tabs\" role=\"tablist\">" + string.Concat(Tabs.Select(t => "<button type=\"button\" role=\"tab\" class=\"sh-tab" + (t.Key == tab ? " sh-act" : string.Empty) + "\" aria-selected=\"" + (t.Key == tab ? "true" : "false") + "\" onclick=\"AdminShowingsJs.tab('" + t.Key + "')\">" + t.Name + (t.Key == "all" ? string.Empty : "<i>" + InTab(all, t.Key).Count() + "</i>") + "</button>")) + "</div>";
        }

        private static string ListHtml(Ctx c, RentData d, List<AdminUser> staff, string term, string tab, string prop, int page)
        {
            List<ShowingRec> rows = InTab(Mine(c, d, prop).Where(s => term.Length == 0 || Ui.Has(s.Name, term) || Ui.Has(s.Email, term) || Ui.Has(s.Phone, term) || Ui.Has(new string(s.Phone.Where(char.IsDigit).ToArray()), new string(term.Where(char.IsDigit).ToArray()))), tab).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"sh-panel sh-empty\">" + (tab == "ask" ? "No requests waiting. Good work." : "No showings here.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"sh-wrapt\"><table class=\"sh-table\"><thead><tr><th>When</th><th>Home</th><th>Visitor</th><th>Agent</th><th>Status</th></tr></thead><tbody>");
            foreach (ShowingRec s in rows.Skip((page - 1) * Size).Take(Size))
            {
                bool late = Open(s) && s.When < DateTime.Now;
                sb.Append("<tr class=\"sh-row\" onclick=\"AdminShowingsJs.edit('" + Ui.Js(s.Id) + "')\"><td class=\"sh-first\"><b>" + Ui.Esc(Fmt.When(s.When)) + "</b><small>" + Ui.Esc(s.Source == "website" ? "From the website" : "Added by staff") + "</small></td><td data-label=\"Home\">" + Ui.Esc(Rent.UnitLabel(d, s.UnitId)) + "</td><td data-label=\"Visitor\">" + Ui.Esc(s.Name) + "<small class=\"sh-mini\"><br>" + Ui.Esc(s.Phone.Length > 0 ? s.Phone : s.Email) + "</small></td><td data-label=\"Agent\">" + Ui.Esc(s.AgentId.Length > 0 ? staff.FirstOrDefault(u => u.Id == s.AgentId)?.Name ?? "Former staff" : "-") + "</td><td data-label=\"Status\">" + Pill(s.Status) + (late ? "<div class=\"sh-flags\"><span class=\"sh-flag\">Time passed</span></div>" : string.Empty) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "showing", "showings"));
            return sb.ToString();
        }

        private async Task<string> ListNow(Ctx c)
        {
            RentData d = await Rent.Load();
            return ListHtml(c, d, await Store.LoadStaff(), Term(GetDataValue("term")), Ui.Clip(GetDataValue("tab"), 10), Ui.Clip(GetDataValue("prop"), 60), PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("sh-list", await ListNow(c));
            RentData d = await Rent.Load();
            response.SetElementContents("sh-tabs", TabsHtml(c, d, Ui.Clip(GetDataValue("tab"), 10), Ui.Clip(GetDataValue("prop"), 60)));
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
            ShowingRec? s = d.Showings.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && (s == null || !Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, s.UnitId))))
            {
                return await Refuse(response, c, "open showing", id, "That showing was not found.");
            }
            if (s == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add showing", string.Empty, "You can view showings but not add them.");
            }
            response.SetElementContents("sh-mbody", FormHtml(c, d, s, await Store.LoadStaff()));
            response.ExecuteScript("AdminShowingsJs.opened('" + Ui.Js(s == null ? "New showing" : s.Name + ", " + Fmt.When(s.When)) + "');");
            return response;
        }

        private static string FormHtml(Ctx c, RentData d, ShowingRec? s, List<AdminUser> staff)
        {
            bool isNew = s == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            DateTime next = DateTime.Today.AddDays(1).AddHours(10);
            ShowingRec r = s ?? new ShowingRec { When = next, Status = "Confirmed", AgentId = c.U.Id, Source = "staff" };
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            List<UnitRec> units = d.Units.Where(u => Access.PropertyOk(c.U, u.PropertyId) && (u.Id == r.UnitId || Rent.Offered(u))).OrderBy(u => Rent.UnitLabel(d, u.Id)).ToList();
            StringBuilder sb = new StringBuilder("<form class=\"sh-stack\" id=\"sh-form\" onsubmit=\"return AdminShowingsJs.save()\" novalidate><input type=\"hidden\" id=\"sh-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            if (!isNew)
            {
                sb.Append("<div class=\"sh-bar\"><span>" + Pill(r.Status) + " " + Ui.Esc((r.Source == "website" ? "Requested on the website " : "Added by staff ") + Fmt.When(r.Created)) + "</span>" + (edit && Open(r) ? "<span class=\"sh-acts-row\">" + (r.Status == "Requested" ? "<button type=\"button\" class=\"sh-btn sh-btn-sm\" onclick=\"AdminShowingsJs.mark('Confirmed')\">Confirm</button>" : string.Empty) + "<button type=\"button\" class=\"sh-btn sh-btn-ghost sh-btn-sm\" onclick=\"AdminShowingsJs.mark('Done')\">Mark done</button><button type=\"button\" class=\"sh-btn sh-btn-ghost sh-btn-sm\" onclick=\"AdminShowingsJs.mark('No-show')\">No-show</button></span>" : string.Empty) + "</div>");
            }
            sb.Append(Field("unit", "Home", "<select id=\"sh-f-unit\" data-k=\"unit\"" + dis + ">" + (isNew ? Opt(string.Empty, "Choose a home", string.Empty) : string.Empty) + string.Concat(units.Select(u => Opt(u.Id, Rent.UnitLabel(d, u.Id) + " (" + Fmt.Beds(u.Beds) + ", " + Fmt.Whole(u.Rent) + ")", r.UnitId))) + "</select>"));
            sb.Append("<div class=\"sh-grid3\">" + Field("name", "Visitor name", Text("name", r.Name, 60, "text", ro)) + Field("email", "Email", Text("email", r.Email, 80, "email", ro)) + Field("phone", "Phone", Text("phone", r.Phone, 30, "tel", ro)) + "</div>");
            sb.Append("<div class=\"sh-grid3\">" + Field("when", "Date and time", Text("when", Fmt.Iso(r.When), 16, "datetime-local", ro)) + Field("status", "Status", "<select id=\"sh-f-status\" data-k=\"status\"" + dis + ">" + string.Concat(Statuses.Select(x => Opt(x, x, r.Status))) + "</select>") + Field("agent", "Agent", "<select id=\"sh-f-agent\" data-k=\"agent\"" + dis + ">" + Opt(string.Empty, "Not assigned", r.AgentId) + string.Concat(staff.Where(u => (u.Active && Access.Can(u, "showings", 'V')) || u.Id == r.AgentId).Select(u => Opt(u.Id, u.Name, r.AgentId))) + "</select>") + "</div>");
            sb.Append("<label class=\"sh-field\" for=\"sh-f-note\"><span>Note</span><textarea id=\"sh-f-note\" data-k=\"note\" rows=\"3\" maxlength=\"600\"" + ro + ">" + Ui.Esc(r.Note) + "</textarea>" + Err("note") + "</label>");
            sb.Append("<div class=\"sh-acts\">" + (edit ? "<button type=\"submit\" class=\"sh-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"sh-btn sh-btn-ghost\" onclick=\"AdminShowingsJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Access.Can(c.U, "applications", 'A') && Rent.Offered(d.Units.FirstOrDefault(u => u.Id == r.UnitId) ?? new UnitRec { Listed = false }) ? "<a class=\"sh-btn sh-btn-ghost\" href=\"Apply?u=" + Ui.Esc(r.UnitId) + "\" target=\"_blank\" rel=\"noopener\">Open the application form</a>" : string.Empty) + (!isNew && Can(c, 'D') ? "<button type=\"button\" class=\"sh-btn sh-btn-danger sh-push\" onclick=\"AdminShowingsJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Name) + "')\">Delete</button>" : string.Empty) + "</div></form>");
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
                return await Refuse(response, c, isNew ? "add showing" : "edit showing", id, "You do not have the right to " + (isNew ? "add" : "change") + " showings.");
            }
            RentData d = await Rent.Load();
            ShowingRec? old = d.Showings.FirstOrDefault(x => x.Id == id);
            if (!isNew && (old == null || !Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, old.UnitId))))
            {
                return await Refuse(response, c, "edit showing", id, "That showing was not found.");
            }
            string unit = Ui.Clip(GetDataValue("unit"), 60);
            string name = Ui.Clip(GetDataValue("name"), 60);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            bool whenOk = Fmt.ParseLocal(GetDataValue("when"), out DateTime when);
            string status = Ui.Clip(GetDataValue("status"), 20);
            string mark = Ui.Clip(GetDataValue("mark"), 20);
            if (Statuses.Contains(mark))
            {
                status = mark;
            }
            string agent = Ui.Clip(GetDataValue("agent"), 60);
            string note = Ui.Clip((GetDataValue("note") ?? string.Empty).Replace("\r", string.Empty), 600);
            List<AdminUser> staff = await Store.LoadStaff();
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == unit);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["unit"] = u == null || !Access.PropertyOk(c.U, u.PropertyId) ? "Choose a home." : string.Empty,
                ["name"] = name.Length < 2 ? "Enter the visitor's name." : string.Empty,
                ["email"] = email.Length > 0 && !Ui.IsEmail(email) ? "Enter a valid email address, or leave it empty." : string.Empty,
                ["phone"] = phone.Length > 0 && !Ui.IsPhone(phone) ? "Enter a valid phone number, or leave it empty." : email.Length == 0 && phone.Length == 0 ? "Enter a phone number or an email address." : string.Empty,
                ["when"] = !whenOk ? "Enter the date and time." : isNew && when < DateTime.Now.AddMinutes(-5) && status is "Requested" or "Confirmed" ? "That time has passed. Pick a later time, or mark it done." : string.Empty,
                ["status"] = !Statuses.Contains(status) ? "Choose a status." : string.Empty,
                ["agent"] = agent.Length > 0 && !staff.Any(x => x.Id == agent) ? "Choose an agent." : string.Empty,
                ["note"] = string.Empty
            };
            if (e.Values.All(v => v.Length == 0) && status is "Requested" or "Confirmed" && d.Showings.Any(x => x.Id != id && x.UnitId == unit && x.When == when && Open(x)))
            {
                e["when"] = "Another showing is booked for this home at that time.";
            }
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("sh-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                if (mark.Length > 0)
                {
                    response.ExecuteScript("AdminShowingsJs.toast('Fix the marked fields first.');");
                }
                return response;
            }
            using (await Store.Hold())
            {
                List<ShowingRec> all = await Store.Rows<ShowingRec>("showings");
                ShowingRec? s = all.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    s = new ShowingRec { Id = Fmt.NewId("s-"), Source = "staff", Created = DateTime.Now };
                    all.Add(s);
                }
                if (s == null)
                {
                    return await Refuse(response, c, "edit showing", id, "That showing was not found.");
                }
                s.UnitId = unit;
                s.Name = name;
                s.Email = email;
                s.Phone = phone;
                s.When = when;
                s.Status = status;
                s.AgentId = agent;
                s.Note = note;
                await Store.SaveRows("showings", all);
            }
            await Store.Log(c.U.Id, isNew ? "add showing" : mark.Length > 0 ? "showing " + mark.ToLowerInvariant() : "edit showing", name + " · " + Rent.UnitLabel(d, unit), true);
            response.SetElementContents("sh-list", await ListNow(c));
            RentData d2 = await Rent.Load();
            response.SetElementContents("sh-tabs", TabsHtml(c, d2, Ui.Clip(GetDataValue("tab"), 10), Ui.Clip(GetDataValue("prop"), 60)));
            response.ExecuteScript("AdminShowingsJs.saved();");
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
                return await Refuse(response, c, "delete showing", id, "You do not have the right to delete showings.");
            }
            string name;
            using (await Store.Hold())
            {
                RentData d = await Rent.Read();
                ShowingRec? s = d.Showings.FirstOrDefault(x => x.Id == id);
                if (s == null || !Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, s.UnitId)))
                {
                    return await Refuse(response, c, "delete showing", id, "That showing was not found.");
                }
                name = s.Name + " · " + Rent.UnitLabel(d, s.UnitId);
                List<ShowingRec> all = await Store.Rows<ShowingRec>("showings");
                all.RemoveAll(x => x.Id == id);
                await Store.SaveRows("showings", all);
            }
            await Store.Log(c.U.Id, "delete showing", name, true);
            response.SetElementContents("sh-list", await ListNow(c));
            RentData d2 = await Rent.Load();
            response.SetElementContents("sh-tabs", TabsHtml(c, d2, Ui.Clip(GetDataValue("tab"), 10), Ui.Clip(GetDataValue("prop"), 60)));
            response.ExecuteScript("AdminShowingsJs.deleted();");
            return response;
        }
    }
}
