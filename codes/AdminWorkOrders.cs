using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminWorkOrders : WebPage
    {
        private const string AreaKey = "workorders";
        private const int Size = 12;
        private static readonly (string Key, string Name)[] Tabs = { ("open", "Open"), ("New", "New"), ("overdue", "Overdue"), ("On hold", "On hold"), ("Done", "Done"), ("all", "All") };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Work orders | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminWorkOrdersJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminWorkOrdersJs.denied();");
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
            response.SetElementContents("wo-brand", Ui.Esc(site.Brand));
            response.SetElementContents("wo-menu", await Auth.Menu("wo", "AdminWorkOrders", c));
            response.SetElementContents("wo-who", Auth.Who("wo", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminWorkOrdersJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"wo-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"wo-btn wo-btn-ghost wo-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminWorkOrdersJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"wo-btn wo-btn-ghost wo-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminWorkOrdersJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"wo-pill wo-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"wo-err\" id=\"wo-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"wo-field\" for=\"wo-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"wo-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static IEnumerable<WorkOrderRec> InTab(IEnumerable<WorkOrderRec> q, string tab)
        {
            return tab switch
            {
                "open" => q.Where(Maint.Open).OrderBy(w => Array.IndexOf(Maint.Priorities, w.Priority)).ThenBy(w => w.Due),
                "overdue" => q.Where(Maint.Overdue).OrderBy(w => w.Due),
                "Done" => q.Where(w => w.Status is "Done" or "Cancelled").OrderByDescending(w => w.Done ?? w.Created),
                "all" => q.OrderByDescending(w => w.Created),
                _ => q.Where(w => w.Status == tab).OrderBy(w => w.Due)
            };
        }

        private static IEnumerable<WorkOrderRec> Mine(Ctx c, RentData d, string prop, string pri)
        {
            return d.WorkOrders.Where(w => Access.PropertyOk(c.U, w.PropertyId) && (prop.Length == 0 || w.PropertyId == prop) && (pri.Length == 0 || w.Priority == pri));
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
            string code = Ui.Clip(GetDataValue("wcode"), 12).ToUpperInvariant();
            string tab = Ui.Clip(GetDataValue("view"), 10);
            tab = code.Length > 0 ? "all" : Tabs.Any(t => t.Key == tab) ? tab : "open";
            List<PropertyRec> mine = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).ToList();
            response.SetElementContents("wo-main", "<div class=\"wo-ph\"><div><h1>Work orders</h1><p>Repair requests from the resident portal and jobs staff add. Assign each one to a team member or a vendor, keep the resident updated, and close it with the cost.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"wo-btn\" onclick=\"AdminWorkOrdersJs.add()\">New work order</button>" : string.Empty) + "</div><div id=\"wo-tabs\">" + TabsHtml(c, d, tab, string.Empty, string.Empty) + "</div><div class=\"wo-tools\"><input type=\"search\" id=\"wo-q\" placeholder=\"Search by WO code, issue, unit or resident\" maxlength=\"40\" value=\"" + Ui.Esc(code) + "\" oninput=\"AdminWorkOrdersJs.type()\" aria-label=\"Search work orders\"><select id=\"wo-fp\" onchange=\"AdminWorkOrdersJs.list(true)\" aria-label=\"Property\">" + Opt(string.Empty, "All properties", string.Empty) + string.Concat(mine.Select(p => Opt(p.Id, p.Name, string.Empty))) + "</select><select id=\"wo-fr\" onchange=\"AdminWorkOrdersJs.list(true)\" aria-label=\"Priority\">" + Opt(string.Empty, "Any priority", string.Empty) + string.Concat(Maint.Priorities.Select(p => Opt(p, p, string.Empty))) + "</select></div><div id=\"wo-list\">" + ListHtml(c, d, await Store.LoadStaff(), code, tab, string.Empty, string.Empty, 1) + "</div>");
            response.ExecuteScript("AdminWorkOrdersJs.setTab('" + tab + "');");
            if (code.Length > 0)
            {
                WorkOrderRec? w = Mine(c, d, string.Empty, string.Empty).FirstOrDefault(x => x.Code == code);
                if (w != null)
                {
                    response.ExecuteScript("AdminWorkOrdersJs.edit('" + Ui.Js(w.Id) + "');");
                }
            }
            return response;
        }

        private static string TabsHtml(Ctx c, RentData d, string tab, string prop, string pri)
        {
            List<WorkOrderRec> all = Mine(c, d, prop, pri).ToList();
            return "<div class=\"wo-tabs\" role=\"tablist\">" + string.Concat(Tabs.Select(t => "<button type=\"button\" role=\"tab\" class=\"wo-tab" + (t.Key == tab ? " wo-act" : string.Empty) + "\" aria-selected=\"" + (t.Key == tab ? "true" : "false") + "\" onclick=\"AdminWorkOrdersJs.tab('" + t.Key + "')\">" + t.Name + (t.Key is "Done" or "all" ? string.Empty : "<i>" + InTab(all, t.Key).Count() + "</i>") + "</button>")) + "</div>";
        }

        private static string Who(RentData d, List<AdminUser> staff, WorkOrderRec w)
        {
            string a = staff.FirstOrDefault(u => u.Id == w.AssignedTo)?.Name ?? string.Empty;
            string v = Maint.Vendor(d, w.VendorId);
            return a.Length > 0 && v.Length > 0 ? a + " · " + v : a.Length > 0 ? a : v.Length > 0 ? v : "Not assigned";
        }

        private static string Reporter(RentData d, List<AdminUser> staff, WorkOrderRec w)
        {
            ResidentRec? r = d.Residents.FirstOrDefault(x => x.Id == w.ResidentId);
            return r != null ? Rent.Name(r) : staff.FirstOrDefault(u => u.Id == w.By)?.Name ?? "Staff";
        }

        private static string ListHtml(Ctx c, RentData d, List<AdminUser> staff, string term, string tab, string prop, string pri, int page)
        {
            List<WorkOrderRec> rows = InTab(Mine(c, d, prop, pri).Where(w => term.Length == 0 || w.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || Ui.Has(w.Title, term) || Ui.Has(Maint.Place(d, w), term) || Ui.Has(Reporter(d, staff, w), term)), tab).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"wo-panel wo-empty\">" + (tab is "overdue" or "New" ? "Nothing waiting here." : "No work orders here.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"wo-wrapt\"><table class=\"wo-table\"><thead><tr><th>Work order</th><th>Place</th><th>Issue</th><th>Priority</th><th>Assigned</th><th>Due</th><th>Status</th></tr></thead><tbody>");
            foreach (WorkOrderRec w in rows.Skip((page - 1) * Size).Take(Size))
            {
                bool late = Maint.Overdue(w);
                sb.Append("<tr class=\"wo-row\" onclick=\"AdminWorkOrdersJs.edit('" + Ui.Js(w.Id) + "')\"><td class=\"wo-first\"><b>" + Ui.Esc(w.Code) + "</b><small>" + Ui.Esc((w.Source == "portal" ? "Portal · " : w.Source == "inspection" ? "Inspection · " : string.Empty) + Fmt.Day(w.Created)) + "</small></td><td data-label=\"Place\">" + Ui.Esc(Maint.Place(d, w)) + "</td><td data-label=\"Issue\">" + Ui.Esc(w.Title) + "<small class=\"wo-mini\"><br>" + Ui.Esc(w.Category) + "</small></td><td data-label=\"Priority\">" + Pill(w.Priority) + "</td><td data-label=\"Assigned\">" + Ui.Esc(Who(d, staff, w)) + "</td><td data-label=\"Due\">" + (Maint.Open(w) ? Ui.Esc(Fmt.Short(w.Due)) + (late ? "<div class=\"wo-flags\"><span class=\"wo-flag\">Overdue</span></div>" : string.Empty) : w.Done != null ? "<span class=\"wo-mini\">Done " + Ui.Esc(Fmt.Short(w.Done.Value)) + "</span>" : "-") + "</td><td data-label=\"Status\">" + Pill(w.Status) + (w.Photos.Count > 0 ? "<small class=\"wo-mini\"><br>" + Ui.Count(w.Photos.Count, "photo", "photos") + "</small>" : string.Empty) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "work order", "work orders"));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            RentData d = await Rent.Load();
            string tab = Ui.Clip(GetDataValue("tab"), 10);
            string prop = Ui.Clip(GetDataValue("prop"), 60);
            string pri = Ui.Clip(GetDataValue("pri"), 12);
            response.SetElementContents("wo-list", ListHtml(c, d, await Store.LoadStaff(), Term(GetDataValue("term")), tab, prop, pri, PageNo(GetDataValue("pg"))));
            response.SetElementContents("wo-tabs", TabsHtml(c, d, tab, prop, pri));
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

        private async Task<(RentData D, WorkOrderRec? W)> Find(Ctx c)
        {
            string id = Ui.Clip(GetDataValue("id"), 60);
            RentData d = await Rent.Load();
            WorkOrderRec? w = d.WorkOrders.FirstOrDefault(x => x.Id == id);
            return w == null || !Access.PropertyOk(c.U, w.PropertyId) ? (d, null) : (d, w);
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
            if (id.Length == 0)
            {
                if (!Can(c, 'A'))
                {
                    return await Refuse(response, c, "add work order", string.Empty, "You can view work orders but not add them.");
                }
                RentData d0 = await Rent.Load();
                response.SetElementContents("wo-mbody", FormHtml(c, d0, await Store.LoadStaff(), null, string.Empty));
                response.ExecuteScript("AdminWorkOrdersJs.opened('New work order');");
                return response;
            }
            (RentData d, WorkOrderRec? w) = await Find(c);
            if (w == null)
            {
                return await Refuse(response, c, "open work order", id, "That work order was not found.");
            }
            response.SetElementContents("wo-mbody", FormHtml(c, d, await Store.LoadStaff(), w, string.Empty));
            response.ExecuteScript("AdminWorkOrdersJs.opened('" + Ui.Js(w.Code + " · " + w.Title) + "');");
            return response;
        }

        private static string PlaceOptions(Ctx c, RentData d, string current)
        {
            StringBuilder sb = new StringBuilder();
            foreach (PropertyRec p in d.Properties.Where(x => Access.PropertyOk(c.U, x.Id)))
            {
                sb.Append("<optgroup label=\"" + Ui.Esc(p.Name) + "\">" + Opt("p:" + p.Id, p.Name + " · common area", current));
                foreach (UnitRec u in d.Units.Where(x => x.PropertyId == p.Id).OrderBy(x => x.Number, StringComparer.OrdinalIgnoreCase))
                {
                    LeaseRec? l = Rent.CurrentLease(d, u.Id);
                    sb.Append(Opt("u:" + u.Id, p.Name + " · " + u.Number + (l != null ? " (" + Rent.Names(d, l) + ")" : " (" + u.Status.ToLowerInvariant() + ")"), current));
                }
                sb.Append("</optgroup>");
            }
            return sb.ToString();
        }

        private static string FormHtml(Ctx c, RentData d, List<AdminUser> staff, WorkOrderRec? w, string note)
        {
            bool isNew = w == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            WorkOrderRec r = w ?? new WorkOrderRec { Priority = "Normal", Status = "New", Category = "Other", Due = Maint.DueFor("Normal", DateTime.Now), BillTo = "Owner" };
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            string place = r.UnitId.Length > 0 ? "u:" + r.UnitId : r.PropertyId.Length > 0 ? "p:" + r.PropertyId : string.Empty;
            StringBuilder sb = new StringBuilder("<div class=\"wo-stack\" id=\"wo-form\"><input type=\"hidden\" id=\"wo-f-id\" value=\"" + Ui.Esc(r.Id) + "\">" + (note.Length > 0 ? "<p class=\"wo-ok\">" + Ui.Esc(note) + "</p>" : string.Empty));
            if (!isNew)
            {
                sb.Append("<div class=\"wo-bar\"><span>" + Pill(r.Status) + " " + Pill(r.Priority) + " " + Ui.Esc("Reported by " + Reporter(d, staff, r) + " " + Fmt.When(r.Created)) + (Maint.Overdue(r) ? " <span class=\"wo-flag\">Overdue</span>" : string.Empty) + "</span>");
                if (edit)
                {
                    sb.Append("<span class=\"wo-acts-row\">");
                    if (r.Status is "New" or "Scheduled" or "On hold")
                    {
                        sb.Append("<button type=\"button\" class=\"wo-btn wo-btn-ghost wo-btn-sm\" onclick=\"AdminWorkOrdersJs.mark('In progress')\">Start work</button>");
                    }
                    if (Maint.Open(r))
                    {
                        sb.Append("<button type=\"button\" class=\"wo-btn wo-btn-sm\" onclick=\"AdminWorkOrdersJs.finish()\">Mark done</button>");
                    }
                    if (r.Status is "Done" or "Cancelled")
                    {
                        sb.Append("<button type=\"button\" class=\"wo-btn wo-btn-ghost wo-btn-sm\" onclick=\"AdminWorkOrdersJs.mark('In progress')\">Reopen</button>");
                    }
                    sb.Append("</span>");
                }
                sb.Append("</div>");
                ResidentRec? res = d.Residents.FirstOrDefault(x => x.Id == r.ResidentId);
                sb.Append("<div class=\"wo-grid2 wo-cols\"><div class=\"wo-sum\"><div><span>Place</span><b>" + Ui.Esc(Maint.Place(d, r)) + "</b></div>" + (res != null ? "<div><span>Resident</span><span>" + (Access.Can(c.U, "residents", 'V') ? "<a href=\"AdminResidents?r=" + Ui.Esc(res.Id) + "\">" + Ui.Esc(Rent.Name(res)) + "</a>" : Ui.Esc(Rent.Name(res))) + "</span></div><div><span>Contact</span><span>" + Ui.Esc(res.Phone.Length > 0 ? res.Phone : res.Email) + "</span></div>" : string.Empty) + "<div><span>Entry if nobody is home</span><span>" + (r.EntryOk ? "Allowed" : "Call first") + "</span></div>" + (r.Pets.Length > 0 ? "<div><span>Pets</span><span>" + Ui.Esc(r.Pets) + "</span></div>" : string.Empty) + "</div><div class=\"wo-sum\"><div><span>Due</span><span>" + Ui.Esc(Fmt.When(r.Due)) + "</span></div><div><span>Visit</span><span>" + Ui.Esc(r.Scheduled != null ? Fmt.When(r.Scheduled.Value) : "Not booked") + "</span></div>" + (r.Done != null ? "<div><span>Done</span><span>" + Ui.Esc(Fmt.When(r.Done.Value)) + "</span></div>" : string.Empty) + "<div><span>Cost</span><span>" + (r.Cost > 0 ? Fmt.Money(r.Cost) + " · " + Ui.Esc(r.BillTo == "Resident" ? "charged to the resident" : r.BillTo == "Owner" ? "paid by the owner" : "no charge") : "-") + "</span></div>" + (r.InspectionId.Length > 0 && Access.Can(c.U, "inspections", 'V') ? "<div><span>From inspection</span><span><a href=\"AdminInspections?i=" + Ui.Esc(r.InspectionId) + "\">Open</a></span></div>" : string.Empty) + (r.TurnId.Length > 0 && Access.Can(c.U, "turns", 'V') ? "<div><span>Make-ready</span><span><a href=\"AdminTurns\">Board</a></span></div>" : string.Empty) + "</div></div>");
                if (r.Photos.Count > 0)
                {
                    sb.Append("<div class=\"wo-field\"><span>Photos from the resident</span><div class=\"wo-shots\">" + string.Concat(r.Photos.Select(p => "<a href=\"" + Ui.Esc(p) + "\" target=\"_blank\" rel=\"noopener\"><img src=\"" + Ui.Esc(p) + "\" alt=\"Photo of the problem\" loading=\"lazy\"></a>")) + "</div></div>");
                }
                if (r.Detail.Length > 0)
                {
                    sb.Append("<div class=\"wo-field\"><span>What was reported</span><p class=\"wo-quote\">" + Ui.Esc(r.Detail).Replace("\n", "<br>") + "</p></div>");
                }
            }
            sb.Append("<div class=\"wo-steps\" id=\"wo-stepbox\"></div>");
            sb.Append("<form class=\"wo-stack" + (isNew ? string.Empty : " wo-panel") + "\" id=\"wo-edit\" onsubmit=\"return AdminWorkOrdersJs.save()\" novalidate>" + (isNew ? string.Empty : "<h3>Details</h3>"));
            sb.Append(Field("place", "Place", "<select id=\"wo-f-place\" data-k=\"place\"" + dis + ">" + (isNew ? Opt(string.Empty, "Choose a unit or common area", string.Empty) : string.Empty) + PlaceOptions(c, d, place) + "</select>"));
            sb.Append("<div class=\"wo-grid2\">" + Field("cat", "Category", "<select id=\"wo-f-cat\" data-k=\"cat\"" + dis + ">" + string.Concat(Maint.Categories.Select(x => Opt(x, x, r.Category))) + "</select>") + Field("pri", "Priority", "<select id=\"wo-f-pri\" data-k=\"wpri\"" + dis + ">" + string.Concat(Maint.Priorities.Select(x => Opt(x, x, r.Priority))) + "</select>", "Emergency 24 hours, high 3 days, normal 7 days, low 14 days.") + "</div>");
            sb.Append(Field("title", "Issue", Text("title", r.Title, 90, "text", ro)));
            if (isNew)
            {
                sb.Append("<label class=\"wo-field\" for=\"wo-f-detail\"><span>Details</span><textarea id=\"wo-f-detail\" data-k=\"detail\" rows=\"3\" maxlength=\"1500\"" + ro + ">" + Ui.Esc(r.Detail) + "</textarea>" + Err("detail") + "</label>");
            }
            sb.Append("<div class=\"wo-grid2\">" + Field("asg", "Team member", "<select id=\"wo-f-asg\" data-k=\"asg\"" + dis + ">" + Opt(string.Empty, "Nobody", r.AssignedTo) + string.Concat(staff.Where(u => (u.Active && (u.Manager || Access.Can(u, "workorders", 'E'))) || u.Id == r.AssignedTo).Select(u => Opt(u.Id, u.Name, r.AssignedTo))) + "</select>") + Field("vend", "Vendor", "<select id=\"wo-f-vend\" data-k=\"vend\"" + dis + ">" + Opt(string.Empty, "No vendor", r.VendorId) + string.Concat(d.Vendors.Where(v => v.Active || v.Id == r.VendorId).Select(v => Opt(v.Id, v.Name + " (" + v.Trade + ")", r.VendorId))) + "</select>") + "</div>");
            sb.Append("<div class=\"wo-grid3\">" + Field("sched", "Visit booked for", Text("sched", r.Scheduled == null ? string.Empty : Fmt.Iso(r.Scheduled.Value), 16, "datetime-local", ro)) + Field("due", "Due", Text("due", Fmt.Iso(r.Due), 16, "datetime-local", ro), isNew ? "Leave as is to use the priority." : string.Empty) + Field("st", "Status", "<select id=\"wo-f-st\" data-k=\"st\"" + dis + ">" + string.Concat(Maint.Statuses.Select(x => Opt(x, x, r.Status))) + "</select>") + "</div>");
            sb.Append("<div class=\"wo-grid3\">" + Field("cost", "Cost ($)", Text("cost", r.Cost > 0 ? Num(r.Cost) : string.Empty, 10, "text", ro + " inputmode=\"decimal\"")) + Field("bill", "Who pays", "<select id=\"wo-f-bill\" data-k=\"bill\"" + dis + ">" + string.Concat(Maint.Payers.Select(x => Opt(x, x == "Resident" ? "Resident (added to their balance when done)" : x == "Owner" ? "Owner" : "Nobody", r.BillTo))) + "</select>") + Field("pets", "Pets in the home", Text("pets", r.Pets, 80, "text", ro)) + "</div>");
            sb.Append("<label class=\"wo-check\"><input type=\"checkbox\" id=\"wo-f-entry\" data-k=\"entry\"" + (r.EntryOk ? " checked" : string.Empty) + dis + ">The resident allows entry when nobody is home</label>");
            sb.Append("<div class=\"wo-acts\">" + (edit ? "<button type=\"submit\" class=\"wo-btn\">" + (isNew ? "Create work order" : "Save details") + "</button>" : string.Empty) + (isNew ? "<button type=\"button\" class=\"wo-btn wo-btn-ghost\" onclick=\"AdminWorkOrdersJs.closeModal()\">Cancel</button>" : string.Empty) + "</div></form>");
            if (!isNew)
            {
                sb.Append("<div class=\"wo-field\"><span>Updates</span><ul class=\"wo-notes\">" + (r.Notes.Count == 0 ? "<li class=\"wo-mini\">No updates yet.</li>" : string.Concat(r.Notes.OrderByDescending(n => n.When).Select(n => "<li><div><b>" + Ui.Esc(staff.FirstOrDefault(u => u.Id == n.By)?.Name ?? (d.Residents.FirstOrDefault(x => x.Id == n.By) is ResidentRec rr ? Rent.Name(rr) : "Staff")) + "</b><small>" + Ui.Esc(Fmt.When(n.When)) + " · " + (n.Shared ? "resident sees this" : "staff only") + "</small></div><p>" + Ui.Esc(n.Text).Replace("\n", "<br>") + "</p></li>"))) + "</ul></div>");
                if (edit)
                {
                    sb.Append("<div class=\"wo-addnote\"><label class=\"wo-field\" for=\"wo-f-note\"><span>Add an update</span><textarea id=\"wo-f-note\" rows=\"2\" maxlength=\"1000\"></textarea>" + Err("note") + "</label><div class=\"wo-acts\"><label class=\"wo-check\"><input type=\"checkbox\" id=\"wo-f-share\"" + (r.ResidentId.Length > 0 ? " checked" : string.Empty) + ">Show it to the resident in the portal</label><button type=\"button\" class=\"wo-btn wo-btn-ghost wo-push\" onclick=\"AdminWorkOrdersJs.addNote()\">Add update</button></div></div>");
                }
                sb.Append("<div class=\"wo-acts\"><button type=\"button\" class=\"wo-btn wo-btn-ghost\" onclick=\"AdminWorkOrdersJs.closeModal()\">Close</button>" + (Can(c, 'D') && r.ChargeId.Length == 0 ? "<button type=\"button\" class=\"wo-btn wo-btn-danger wo-push\" onclick=\"AdminWorkOrdersJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Code) + "')\">Delete</button>" : string.Empty) + "</div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static void SyncCharge(RentData d, WorkOrderRec w, List<LedgerRec> ledger, string by)
        {
            LedgerRec? g = ledger.FirstOrDefault(x => x.Id == w.ChargeId);
            bool want = w.Status == "Done" && w.BillTo == "Resident" && w.Cost > 0 && w.UnitId.Length > 0;
            if (!want)
            {
                if (g != null)
                {
                    ledger.Remove(g);
                }
                w.ChargeId = string.Empty;
                return;
            }
            LeaseRec? l = Maint.LeaseFor(d, w);
            if (l == null)
            {
                return;
            }
            if (g == null)
            {
                g = new LedgerRec { Id = Fmt.NewId("g-"), Kind = "Repair", By = by };
                ledger.Add(g);
                w.ChargeId = g.Id;
            }
            g.LeaseId = l.Id;
            g.When = w.Done ?? DateTime.Now;
            g.Label = "Repair " + w.Code + ": " + w.Title;
            g.Amount = w.Cost;
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
                return await Refuse(response, c, isNew ? "add work order" : "edit work order", id, "You do not have the right to " + (isNew ? "add" : "change") + " work orders.");
            }
            RentData d = await Rent.Load();
            WorkOrderRec? old = d.WorkOrders.FirstOrDefault(x => x.Id == id);
            if (!isNew && (old == null || !Access.PropertyOk(c.U, old.PropertyId)))
            {
                return await Refuse(response, c, "edit work order", id, "That work order was not found.");
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
            string cat = Ui.Clip(GetDataValue("cat"), 30);
            string pri = Ui.Clip(GetDataValue("wpri"), 12);
            string title = Ui.Clip(GetDataValue("title"), 90);
            string asg = Ui.Clip(GetDataValue("asg"), 60);
            string vend = Ui.Clip(GetDataValue("vend"), 60);
            string schedText = Ui.Clip(GetDataValue("sched"), 16);
            bool schedOk = Fmt.ParseLocal(schedText, out DateTime sched);
            bool dueOk = Fmt.ParseLocal(GetDataValue("due"), out DateTime due);
            string st = Ui.Clip(GetDataValue("st"), 20);
            string mark = Ui.Clip(GetDataValue("mark"), 20);
            if (Maint.Statuses.Contains(mark))
            {
                st = mark;
            }
            string costText = Ui.Clip(GetDataValue("cost"), 12);
            decimal cost = costText.Length == 0 ? 0 : Dec(costText);
            string bill = Ui.Clip(GetDataValue("bill"), 10);
            List<AdminUser> staff = await Store.LoadStaff();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["place"] = pid.Length == 0 || !Access.PropertyOk(c.U, pid) ? "Choose a unit or a common area." : string.Empty,
                ["cat"] = !Maint.Categories.Contains(cat) ? "Choose a category." : string.Empty,
                ["pri"] = !Maint.Priorities.Contains(pri) ? "Choose a priority." : string.Empty,
                ["title"] = title.Length < 3 ? "Describe the issue in a few words." : string.Empty,
                ["detail"] = string.Empty,
                ["asg"] = asg.Length > 0 && !staff.Any(u => u.Id == asg) ? "Choose a team member." : string.Empty,
                ["vend"] = vend.Length > 0 && !d.Vendors.Any(v => v.Id == vend) ? "Choose a vendor." : string.Empty,
                ["sched"] = schedText.Length > 0 && !schedOk ? "Enter a valid date and time, or leave it empty." : string.Empty,
                ["due"] = !dueOk ? "Enter the due date and time." : string.Empty,
                ["st"] = !Maint.Statuses.Contains(st) ? "Choose a status." : st == "Scheduled" && schedText.Length == 0 ? "Book the visit time to set it to scheduled." : string.Empty,
                ["cost"] = cost < 0 || cost > 100000 ? "Enter the cost in dollars, or leave it empty." : string.Empty,
                ["bill"] = !Maint.Payers.Contains(bill) ? "Choose who pays." : bill == "Resident" && uid.Length == 0 && cost > 0 ? "A common-area job cannot be charged to a resident." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("wo-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                if (mark.Length > 0)
                {
                    response.ExecuteScript("AdminWorkOrdersJs.toast('Fix the marked fields first.');");
                }
                return response;
            }
            string code;
            string wid;
            using (await Store.Hold())
            {
                RentData f = await Rent.Read();
                List<WorkOrderRec> all = f.WorkOrders;
                WorkOrderRec? w = all.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    DateTime now = DateTime.Now;
                    w = new WorkOrderRec { Id = Fmt.NewId("w-"), Code = Fmt.Code("WO-", all.Select(x => x.Code)), Source = "staff", Created = now, By = c.U.Id, Detail = Ui.Clip((GetDataValue("detail") ?? string.Empty).Replace("\r", string.Empty), 1500) };
                    if (Math.Abs((due - Maint.DueFor("Normal", now)).TotalMinutes) < 30 && pri != "Normal")
                    {
                        due = Maint.DueFor(pri, now);
                    }
                    all.Add(w);
                }
                if (w == null)
                {
                    return await Refuse(response, c, "edit work order", id, "That work order was not found.");
                }
                w.PropertyId = pid;
                w.UnitId = uid;
                w.Category = cat;
                if (!isNew && w.Priority != pri && Math.Abs((due - w.Due).TotalMinutes) < 1)
                {
                    due = Maint.DueFor(pri, w.Created);
                }
                w.Priority = pri;
                w.Title = title;
                w.AssignedTo = asg;
                w.VendorId = vend;
                w.Scheduled = schedText.Length == 0 ? null : sched;
                w.Due = due;
                if (st == "Done" && w.Done == null)
                {
                    w.Done = DateTime.Now;
                }
                if (st != "Done")
                {
                    w.Done = null;
                }
                w.Status = st;
                w.Cost = cost;
                w.BillTo = bill;
                w.Pets = Ui.Clip(GetDataValue("pets"), 80);
                w.EntryOk = (GetDataValue("entry") ?? string.Empty) == "1";
                SyncCharge(f, w, f.Ledger, c.U.Id);
                Books.SyncExpense(f, w, c.U.Id);
                await Store.SaveRows("workorders", all);
                await Store.SaveRows("ledger", f.Ledger);
                await Store.SaveRows("expenses", f.Expenses);
                code = w.Code;
                wid = w.Id;
            }
            await Store.Log(c.U.Id, isNew ? "add work order" : mark.Length > 0 ? "work order " + mark.ToLowerInvariant() : "edit work order", code + " · " + title, true);
            await Reopen(response, c, wid, isNew ? "Work order " + code + " created." : mark == "Done" ? "Marked done." : "Saved.");
            return response;
        }

        private async Task Reopen(ApiResponse response, Ctx c, string id, string note)
        {
            await Refresh(response, c);
            RentData d = await Rent.Load();
            WorkOrderRec? w = d.WorkOrders.FirstOrDefault(x => x.Id == id);
            if (w != null)
            {
                response.SetElementContents("wo-mbody", FormHtml(c, d, await Store.LoadStaff(), w, note));
                response.ExecuteScript("AdminWorkOrdersJs.opened('" + Ui.Js(w.Code + " · " + w.Title) + "');AdminWorkOrdersJs.clean();");
            }
        }

        public async Task<ApiResponse> AddNote()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "work order update", string.Empty, "You do not have the right to change work orders.");
            }
            (RentData d, WorkOrderRec? found) = await Find(c);
            if (found == null)
            {
                return await Refuse(response, c, "work order update", Ui.Clip(GetDataValue("id"), 60), "That work order was not found.");
            }
            string text = Ui.Clip((GetDataValue("wnote") ?? string.Empty).Replace("\r", string.Empty), 1000);
            if (text.Length < 2)
            {
                response.SetElementContents("wo-e-note", "Write the update first.");
                return response;
            }
            bool shared = (GetDataValue("share") ?? string.Empty) == "1";
            using (await Store.Hold())
            {
                List<WorkOrderRec> all = await Store.Rows<WorkOrderRec>("workorders");
                WorkOrderRec? w = all.FirstOrDefault(x => x.Id == found.Id);
                if (w != null)
                {
                    w.Notes.Add(new WoNote { When = DateTime.Now, By = c.U.Id, Text = text, Shared = shared });
                    await Store.SaveRows("workorders", all);
                }
            }
            await Store.Log(c.U.Id, "work order update", found.Code, true);
            await Reopen(response, c, found.Id, shared ? "Update added. The resident sees it in the portal." : "Update added.");
            return response;
        }

        public async Task<ApiResponse> FinishForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, WorkOrderRec? w) = await Find(c);
            if (w == null || !Can(c, 'E'))
            {
                return await Refuse(response, c, "finish work order", Ui.Clip(GetDataValue("id"), 60), w == null ? "That work order was not found." : "You do not have the right to change work orders.");
            }
            response.SetElementContents("wo-stepbox", "<div class=\"wo-panel wo-stack\"><h3>Mark " + Ui.Esc(w.Code) + " done</h3><div class=\"wo-grid2\">" + Field("fcost", "Final cost ($)", Text("fcost", w.Cost > 0 ? Num(w.Cost) : string.Empty, 10, "text", " inputmode=\"decimal\"")) + Field("fbill", "Who pays", "<select id=\"wo-f-fbill\">" + string.Concat(Maint.Payers.Select(x => Opt(x, x == "Resident" ? "Resident (added to their balance)" : x == "Owner" ? "Owner" : "Nobody", w.BillTo))) + "</select>") + "</div><label class=\"wo-field\" for=\"wo-f-fnote\"><span>What was done (the resident sees it)</span><textarea id=\"wo-f-fnote\" rows=\"2\" maxlength=\"1000\"></textarea></label><div class=\"wo-acts\"><button type=\"button\" class=\"wo-btn\" onclick=\"AdminWorkOrdersJs.finishGo()\">Mark done</button><button type=\"button\" class=\"wo-btn wo-btn-ghost\" onclick=\"AdminWorkOrdersJs.stepClose()\">Back</button></div></div>");
            response.ExecuteScript("AdminWorkOrdersJs.stepShown();");
            return response;
        }

        public async Task<ApiResponse> Finish()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, WorkOrderRec? found) = await Find(c);
            if (found == null || !Can(c, 'E') || !Maint.Open(found))
            {
                return await Refuse(response, c, "finish work order", Ui.Clip(GetDataValue("id"), 60), found == null ? "That work order was not found." : "This work order cannot be finished.");
            }
            string costText = Ui.Clip(GetDataValue("fcost"), 12);
            decimal cost = costText.Length == 0 ? 0 : Dec(costText);
            string bill = Ui.Clip(GetDataValue("fbill"), 10);
            string text = Ui.Clip((GetDataValue("fnote") ?? string.Empty).Replace("\r", string.Empty), 1000);
            string err = cost < 0 || cost > 100000 ? "Enter the cost in dollars, or leave it empty." : !Maint.Payers.Contains(bill) ? "Choose who pays." : bill == "Resident" && cost > 0 && found.UnitId.Length == 0 ? "A common-area job cannot be charged to a resident." : bill == "Resident" && cost > 0 && Maint.LeaseFor(d, found) == null ? "There is no lease on this unit to charge." : string.Empty;
            response.SetElementContents("wo-e-fcost", err);
            if (err.Length > 0)
            {
                return response;
            }
            using (await Store.Hold())
            {
                RentData f = await Rent.Read();
                WorkOrderRec? w = f.WorkOrders.FirstOrDefault(x => x.Id == found.Id);
                if (w != null)
                {
                    w.Status = "Done";
                    w.Done = DateTime.Now;
                    w.Cost = cost;
                    w.BillTo = bill;
                    if (text.Length > 0)
                    {
                        w.Notes.Add(new WoNote { When = DateTime.Now, By = c.U.Id, Text = text, Shared = true });
                    }
                    SyncCharge(f, w, f.Ledger, c.U.Id);
                    Books.SyncExpense(f, w, c.U.Id);
                    await Store.SaveRows("workorders", f.WorkOrders);
                    await Store.SaveRows("ledger", f.Ledger);
                    await Store.SaveRows("expenses", f.Expenses);
                }
            }
            await Store.Log(c.U.Id, "work order done", found.Code + (cost > 0 ? " · " + Fmt.Money(cost) : string.Empty), true);
            await Reopen(response, c, found.Id, "Marked done." + (bill == "Resident" && cost > 0 ? " " + Fmt.Money(cost) + " was added to the resident's balance." : bill == "Owner" && cost > 0 ? " " + Fmt.Money(cost) + " was added to the owner's expenses." : string.Empty));
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
            if (!Can(c, 'D'))
            {
                return await Refuse(response, c, "delete work order", string.Empty, "You do not have the right to delete work orders.");
            }
            (RentData d, WorkOrderRec? w) = await Find(c);
            if (w == null || w.ChargeId.Length > 0)
            {
                return await Refuse(response, c, "delete work order", Ui.Clip(GetDataValue("id"), 60), w == null ? "That work order was not found." : "This work order charged the resident, so it stays in the records.");
            }
            using (await Store.Hold())
            {
                List<WorkOrderRec> all = await Store.Rows<WorkOrderRec>("workorders");
                all.RemoveAll(x => x.Id == w.Id);
                await Store.SaveRows("workorders", all);
                List<ExpenseRec> ex = await Store.Rows<ExpenseRec>("expenses");
                if (ex.RemoveAll(x => x.WorkOrderId == w.Id) > 0)
                {
                    await Store.SaveRows("expenses", ex);
                }
            }
            foreach (string p in w.Photos)
            {
                string file = Path.Combine(Store.Root, p.Replace('/', Path.DirectorySeparatorChar));
                if (p.StartsWith("images/uploads/requests/", StringComparison.Ordinal) && File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            await Store.Log(c.U.Id, "delete work order", w.Code, true);
            await Refresh(response, c);
            response.ExecuteScript("AdminWorkOrdersJs.deleted();");
            return response;
        }
    }
}
