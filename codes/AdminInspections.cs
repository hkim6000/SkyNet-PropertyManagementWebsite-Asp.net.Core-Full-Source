using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminInspections : WebPage
    {
        private const string AreaKey = "inspections";
        private const int Size = 12;
        private static readonly (string Key, string Name)[] Tabs = { ("Scheduled", "Scheduled"), ("Done", "Done"), ("all", "All") };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Inspections | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminInspectionsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminInspectionsJs.denied();");
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
            response.SetElementContents("in-brand", Ui.Esc(site.Brand));
            response.SetElementContents("in-menu", await Auth.Menu("in", "AdminInspections", c));
            response.SetElementContents("in-who", Auth.Who("in", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminInspectionsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"in-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"in-btn in-btn-ghost in-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminInspectionsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"in-btn in-btn-ghost in-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminInspectionsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"in-pill in-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"in-err\" id=\"in-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"in-field\" for=\"in-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"in-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static IEnumerable<InspectionRec> InTab(IEnumerable<InspectionRec> q, string tab)
        {
            return tab switch
            {
                "Scheduled" => q.Where(i => i.Status == "Scheduled").OrderBy(i => i.Date),
                "Done" => q.Where(i => i.Status == "Done").OrderByDescending(i => i.Date),
                _ => q.OrderByDescending(i => i.Date)
            };
        }

        private static IEnumerable<InspectionRec> Mine(Ctx c, RentData d, string prop, string kind)
        {
            return d.Inspections.Where(i => Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, i.UnitId)) && (prop.Length == 0 || Rent.PropertyIdOfUnit(d, i.UnitId) == prop) && (kind.Length == 0 || i.Kind == kind));
        }

        private static int Flagged(InspectionRec i)
        {
            return i.Rooms.Sum(r => r.Items.Count(x => x.Condition is "Poor" or "Damaged" or "Not there"));
        }

        private static string Display(InspectionRec i)
        {
            return i.Status == "Scheduled" && i.Date.Date < DateTime.Today ? "Late" : i.Status;
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
            string open = Ui.Clip(GetDataValue("iid"), 60);
            List<PropertyRec> mine = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).ToList();
            response.SetElementContents("in-main", "<div class=\"in-ph\"><div><h1>Inspections</h1><p>Move-in, move-out, routine and annual checks room by room. Damaged items become work orders in one step, and a move-out inspection ticks off its make-ready step.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"in-btn\" onclick=\"AdminInspectionsJs.add()\">Schedule an inspection</button>" : string.Empty) + "</div><div id=\"in-tabs\">" + TabsHtml(c, d, "Scheduled", string.Empty, string.Empty) + "</div><div class=\"in-tools\"><input type=\"search\" id=\"in-q\" placeholder=\"Search by unit or inspector\" maxlength=\"40\" oninput=\"AdminInspectionsJs.type()\" aria-label=\"Search inspections\"><select id=\"in-fp\" onchange=\"AdminInspectionsJs.list(true)\" aria-label=\"Property\">" + Opt(string.Empty, "All properties", string.Empty) + string.Concat(mine.Select(p => Opt(p.Id, p.Name, string.Empty))) + "</select><select id=\"in-fk\" onchange=\"AdminInspectionsJs.list(true)\" aria-label=\"Kind\">" + Opt(string.Empty, "Every kind", string.Empty) + string.Concat(Maint.Kinds.Select(k => Opt(k, k, string.Empty))) + "</select></div><div id=\"in-list\">" + ListHtml(c, d, await Store.LoadStaff(), string.Empty, "Scheduled", string.Empty, string.Empty, 1) + "</div>");
            InspectionRec? target = Mine(c, d, string.Empty, string.Empty).FirstOrDefault(i => i.Id == open);
            if (target != null)
            {
                response.ExecuteScript("AdminInspectionsJs.edit('" + Ui.Js(target.Id) + "');");
            }
            return response;
        }

        private static string TabsHtml(Ctx c, RentData d, string tab, string prop, string kind)
        {
            List<InspectionRec> all = Mine(c, d, prop, kind).ToList();
            return "<div class=\"in-tabs\" role=\"tablist\">" + string.Concat(Tabs.Select(t => "<button type=\"button\" role=\"tab\" class=\"in-tab" + (t.Key == tab ? " in-act" : string.Empty) + "\" aria-selected=\"" + (t.Key == tab ? "true" : "false") + "\" onclick=\"AdminInspectionsJs.tab('" + t.Key + "')\">" + t.Name + (t.Key == "Scheduled" ? "<i>" + InTab(all, t.Key).Count() + "</i>" : string.Empty) + "</button>")) + "</div>";
        }

        private static string ListHtml(Ctx c, RentData d, List<AdminUser> staff, string term, string tab, string prop, string kind, int page)
        {
            List<InspectionRec> rows = InTab(Mine(c, d, prop, kind).Where(i => term.Length == 0 || Ui.Has(Rent.UnitLabel(d, i.UnitId), term) || Ui.Has(staff.FirstOrDefault(u => u.Id == i.Inspector)?.Name, term)), tab).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"in-panel in-empty\">No inspections here.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"in-wrapt\"><table class=\"in-table\"><thead><tr><th>Date</th><th>Home</th><th>Kind</th><th>Inspector</th><th>Result</th><th>Status</th></tr></thead><tbody>");
            foreach (InspectionRec i in rows.Skip((page - 1) * Size).Take(Size))
            {
                int flagged = Flagged(i);
                sb.Append("<tr class=\"in-row\" onclick=\"AdminInspectionsJs.edit('" + Ui.Js(i.Id) + "')\"><td class=\"in-first\"><b>" + Ui.Esc(Fmt.When(i.Date)) + "</b></td><td data-label=\"Home\">" + Ui.Esc(Rent.UnitLabel(d, i.UnitId)) + "</td><td data-label=\"Kind\">" + Ui.Esc(i.Kind) + "</td><td data-label=\"Inspector\">" + Ui.Esc(staff.FirstOrDefault(u => u.Id == i.Inspector)?.Name ?? "-") + "</td><td data-label=\"Result\">" + (i.Status == "Done" ? (flagged == 0 ? "All good" : Ui.Count(flagged, "item needs work", "items need work")) : "-") + "</td><td data-label=\"Status\">" + Pill(Display(i)) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "inspection", "inspections"));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            RentData d = await Rent.Load();
            string tab = Ui.Clip(GetDataValue("tab"), 10);
            string prop = Ui.Clip(GetDataValue("prop"), 60);
            string kind = Ui.Clip(GetDataValue("fkind"), 12);
            response.SetElementContents("in-list", ListHtml(c, d, await Store.LoadStaff(), Term(GetDataValue("term")), tab, prop, kind, PageNo(GetDataValue("pg"))));
            response.SetElementContents("in-tabs", TabsHtml(c, d, tab, prop, kind));
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

        private async Task<(RentData D, InspectionRec? I)> Find(Ctx c)
        {
            string id = Ui.Clip(GetDataValue("id"), 60);
            RentData d = await Rent.Load();
            InspectionRec? i = d.Inspections.FirstOrDefault(x => x.Id == id);
            return i == null || !Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, i.UnitId)) ? (d, null) : (d, i);
        }

        private static string Inspectors(List<AdminUser> staff, string current)
        {
            return string.Concat(staff.Where(u => (u.Active && (u.Manager || Access.Can(u, "inspections", 'E'))) || u.Id == current).Select(u => Opt(u.Id, u.Name, current)));
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
            List<AdminUser> staff = await Store.LoadStaff();
            if (id.Length == 0)
            {
                if (!Can(c, 'A'))
                {
                    return await Refuse(response, c, "add inspection", string.Empty, "You can view inspections but not add them.");
                }
                RentData d0 = await Rent.Load();
                List<UnitRec> units = d0.Units.Where(u => Access.PropertyOk(c.U, u.PropertyId)).OrderBy(u => Rent.UnitLabel(d0, u.Id)).ToList();
                StringBuilder nb = new StringBuilder("<form class=\"in-stack\" id=\"in-new\" onsubmit=\"return AdminInspectionsJs.create()\" novalidate><p class=\"in-mini\">The checklist is built from the unit's rooms: living area, kitchen, each bedroom and bathroom, and the whole home.</p>");
                nb.Append(Field("unit", "Home", "<select id=\"in-f-unit\" data-k=\"unit\">" + Opt(string.Empty, "Choose a home", string.Empty) + string.Concat(units.Select(u => Opt(u.Id, Rent.UnitLabel(d0, u.Id) + " · " + u.Status, string.Empty))) + "</select>"));
                nb.Append("<div class=\"in-grid3\">" + Field("ikind", "Kind", "<select id=\"in-f-ikind\" data-k=\"ikind\">" + string.Concat(Maint.Kinds.Select(k => Opt(k, k, "Routine"))) + "</select>") + Field("idate", "Date and time", Text("idate", Fmt.Iso(DateTime.Today.AddDays(2).AddHours(10)), 16, "datetime-local")) + Field("insp", "Inspector", "<select id=\"in-f-insp\" data-k=\"insp\">" + Inspectors(staff, c.U.Id) + "</select>") + "</div>");
                nb.Append("<div class=\"in-acts\"><button type=\"submit\" class=\"in-btn\">Schedule</button><button type=\"button\" class=\"in-btn in-btn-ghost\" onclick=\"AdminInspectionsJs.closeModal()\">Cancel</button></div></form>");
                response.SetElementContents("in-mbody", nb.ToString());
                response.ExecuteScript("AdminInspectionsJs.opened('Schedule an inspection');");
                return response;
            }
            (RentData d, InspectionRec? i) = await Find(c);
            if (i == null)
            {
                return await Refuse(response, c, "open inspection", id, "That inspection was not found.");
            }
            response.SetElementContents("in-mbody", SheetHtml(c, d, staff, i, string.Empty));
            response.ExecuteScript("AdminInspectionsJs.opened('" + Ui.Js(i.Kind + " inspection · " + Rent.UnitLabel(d, i.UnitId)) + "');");
            return response;
        }

        private static string ItemKey(InspRoom r, InspItem x)
        {
            return r.Room + ", " + x.Name;
        }

        private static bool HasOrder(RentData d, InspectionRec i, InspRoom r, InspItem x)
        {
            string key = ": " + ItemKey(r, x) + ".";
            return d.WorkOrders.Any(w => w.InspectionId == i.Id && w.Detail.Contains(key, StringComparison.Ordinal));
        }

        private static string SheetHtml(Ctx c, RentData d, List<AdminUser> staff, InspectionRec i, string note)
        {
            bool edit = Can(c, 'E') && i.Status == "Scheduled";
            string dis = edit ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<div class=\"in-stack\" id=\"in-form\"><input type=\"hidden\" id=\"in-f-id\" value=\"" + Ui.Esc(i.Id) + "\">" + (note.Length > 0 ? "<p class=\"in-ok\">" + Ui.Esc(note) + "</p>" : string.Empty));
            LeaseRec? l = d.Leases.FirstOrDefault(x => x.Id == i.LeaseId);
            sb.Append("<div class=\"in-bar\"><span>" + Pill(Display(i)) + " " + Ui.Esc(i.Kind + " · " + Rent.UnitLabel(d, i.UnitId) + (l != null ? " · " + Rent.Names(d, l) : string.Empty)) + (i.Finished != null ? " &middot; finished " + Ui.Esc(Fmt.When(i.Finished.Value)) : string.Empty) + "</span><span class=\"in-acts-row\">" + (i.Status == "Done" && i.Rooms.Any(r => r.Items.Any(x => x.Condition is "Poor" or "Damaged" or "Not there" && !HasOrder(d, i, r, x))) && Access.Can(c.U, "workorders", 'A') ? "<button type=\"button\" class=\"in-btn in-btn-sm\" onclick=\"AdminInspectionsJs.orders()\">Create work orders</button>" : string.Empty) + "<button type=\"button\" class=\"in-btn in-btn-ghost in-btn-sm\" onclick=\"window.print()\">Print</button></span></div>");
            sb.Append("<div class=\"in-grid3\">" + Field("idate", "Date and time", Text("idate", Fmt.Iso(i.Date), 16, "datetime-local", edit ? string.Empty : " readonly")) + Field("ikind", "Kind", "<select id=\"in-f-ikind\" data-k=\"ikind\"" + dis + ">" + string.Concat(Maint.Kinds.Select(k => Opt(k, k, i.Kind))) + "</select>") + Field("insp", "Inspector", "<select id=\"in-f-insp\" data-k=\"insp\"" + dis + ">" + Inspectors(staff, i.Inspector) + "</select>") + "</div>");
            sb.Append("<div class=\"in-sheet\">");
            for (int ri = 0; ri < i.Rooms.Count; ri++)
            {
                InspRoom r = i.Rooms[ri];
                sb.Append("<section class=\"in-room\"><h3>" + Ui.Esc(r.Room) + "</h3><div class=\"in-items\">");
                for (int xi = 0; xi < r.Items.Count; xi++)
                {
                    InspItem x = r.Items[xi];
                    bool bad = x.Condition is "Poor" or "Damaged" or "Not there";
                    sb.Append("<div class=\"in-item" + (bad ? " in-bad" : string.Empty) + "\"><b>" + Ui.Esc(x.Name) + "</b><select data-k=\"c_" + ri + "_" + xi + "\" aria-label=\"" + Ui.Esc(r.Room + " " + x.Name + " condition") + "\"" + dis + ">" + Opt(string.Empty, "Condition", x.Condition) + string.Concat(Maint.Conditions.Select(k => Opt(k, k, x.Condition))) + "</select>" + (edit || x.Note.Length > 0 ? "<input type=\"text\" data-k=\"n_" + ri + "_" + xi + "\" maxlength=\"200\" placeholder=\"Note\" value=\"" + Ui.Esc(x.Note) + "\" aria-label=\"" + Ui.Esc(r.Room + " " + x.Name + " note") + "\"" + (edit ? string.Empty : " readonly") + ">" : string.Empty) + (i.Status == "Done" && bad ? (HasOrder(d, i, r, x) ? "<small class=\"in-mini\">Work order made</small>" : "<small class=\"in-mini\">Needs a work order</small>") : string.Empty) + "</div>");
                }
                sb.Append("</div></section>");
            }
            sb.Append("</div>" + Err("sheet"));
            sb.Append("<label class=\"in-field\" for=\"in-f-summary\"><span>Summary</span><textarea id=\"in-f-summary\" data-k=\"summary\" rows=\"3\" maxlength=\"1000\"" + (edit ? string.Empty : " readonly") + ">" + Ui.Esc(i.Summary) + "</textarea></label>");
            sb.Append("<div class=\"in-acts\">" + (edit ? "<button type=\"button\" class=\"in-btn in-btn-ghost\" onclick=\"AdminInspectionsJs.saveSheet('save')\">Save progress</button><button type=\"button\" class=\"in-btn\" onclick=\"AdminInspectionsJs.saveSheet('finish')\">Finish inspection</button>" : string.Empty) + "<button type=\"button\" class=\"in-btn in-btn-ghost\" onclick=\"AdminInspectionsJs.closeModal()\">Close</button>" + (Can(c, 'E') && i.Status == "Scheduled" ? "<button type=\"button\" class=\"in-btn in-btn-ghost in-push\" onclick=\"AdminInspectionsJs.cancelIt()\">Cancel inspection</button>" : string.Empty) + (Can(c, 'D') && i.Status != "Done" ? "<button type=\"button\" class=\"in-btn in-btn-danger" + (Can(c, 'E') && i.Status == "Scheduled" ? string.Empty : " in-push") + "\" onclick=\"AdminInspectionsJs.del('" + Ui.Js(i.Id) + "')\">Delete</button>" : string.Empty) + "</div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Create()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'A'))
            {
                return await Refuse(response, c, "add inspection", string.Empty, "You do not have the right to add inspections.");
            }
            RentData d = await Rent.Load();
            List<AdminUser> staff = await Store.LoadStaff();
            string unit = Ui.Clip(GetDataValue("unit"), 60);
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == unit && Access.PropertyOk(c.U, x.PropertyId));
            string kind = Ui.Clip(GetDataValue("ikind"), 12);
            bool dOk = Fmt.ParseLocal(GetDataValue("idate"), out DateTime date);
            string insp = Ui.Clip(GetDataValue("insp"), 60);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["unit"] = u == null ? "Choose a home." : string.Empty,
                ["ikind"] = !Maint.Kinds.Contains(kind) ? "Choose the kind." : string.Empty,
                ["idate"] = !dOk ? "Enter the date and time." : date < DateTime.Today.AddDays(-30) ? "That date is too far in the past." : string.Empty,
                ["insp"] = !staff.Any(x => x.Id == insp) ? "Choose the inspector." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("in-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0) || u == null)
            {
                return response;
            }
            LeaseRec? l = kind == "Move-in" ? d.Leases.Where(x => x.UnitId == u.Id && x.Status == "Upcoming").OrderBy(x => x.Start).FirstOrDefault() ?? Rent.CurrentLease(d, u.Id) : kind == "Move-out" ? Rent.CurrentLease(d, u.Id) ?? d.Leases.Where(x => x.UnitId == u.Id && x.Status == "Ended").OrderByDescending(x => x.MovedOut ?? x.End).FirstOrDefault() : Rent.CurrentLease(d, u.Id);
            InspectionRec rec = Maint.NewInspection(u, l?.Id ?? string.Empty, kind, date, insp, c.U.Id);
            using (await Store.Hold())
            {
                List<InspectionRec> all = await Store.Rows<InspectionRec>("inspections");
                all.Add(rec);
                await Store.SaveRows("inspections", all);
            }
            await Store.Log(c.U.Id, "schedule inspection", kind + " · " + Rent.UnitLabel(d, u.Id), true);
            await Reopen(response, c, rec.Id, "Inspection scheduled. Fill in the checklist on the day.");
            return response;
        }

        private async Task Reopen(ApiResponse response, Ctx c, string id, string note)
        {
            await Refresh(response, c);
            RentData d = await Rent.Load();
            InspectionRec? i = d.Inspections.FirstOrDefault(x => x.Id == id);
            if (i != null)
            {
                response.SetElementContents("in-mbody", SheetHtml(c, d, await Store.LoadStaff(), i, note));
                response.ExecuteScript("AdminInspectionsJs.opened('" + Ui.Js(i.Kind + " inspection · " + Rent.UnitLabel(d, i.UnitId)) + "');AdminInspectionsJs.clean();");
            }
        }

        public async Task<ApiResponse> SaveSheet()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, InspectionRec? found) = await Find(c);
            if (found == null || !Can(c, 'E') || found.Status != "Scheduled")
            {
                return await Refuse(response, c, "edit inspection", Ui.Clip(GetDataValue("id"), 60), found == null ? "That inspection was not found." : "This inspection can no longer be changed.");
            }
            bool finish = (GetDataValue("act") ?? string.Empty) == "finish";
            List<AdminUser> staff = await Store.LoadStaff();
            string kind = Ui.Clip(GetDataValue("ikind"), 12);
            bool dOk = Fmt.ParseLocal(GetDataValue("idate"), out DateTime date);
            string insp = Ui.Clip(GetDataValue("insp"), 60);
            int missing = 0;
            List<InspRoom> rooms = found.Rooms.Select((r, ri) => new InspRoom
            {
                Room = r.Room,
                Items = r.Items.Select((x, xi) =>
                {
                    string cond = Ui.Clip(GetDataValue("c_" + ri + "_" + xi), 20);
                    cond = Maint.Conditions.Contains(cond) ? cond : string.Empty;
                    if (cond.Length == 0)
                    {
                        missing++;
                    }
                    return new InspItem { Name = x.Name, Condition = cond, Note = Ui.Clip(GetDataValue("n_" + ri + "_" + xi), 200) };
                }).ToList()
            }).ToList();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["ikind"] = !Maint.Kinds.Contains(kind) ? "Choose the kind." : string.Empty,
                ["idate"] = !dOk ? "Enter the date and time." : finish && date > DateTime.Now.AddHours(12) ? "An inspection cannot be finished before its date. Change the date first." : string.Empty,
                ["insp"] = !staff.Any(x => x.Id == insp) ? "Choose the inspector." : string.Empty,
                ["sheet"] = finish && missing > 0 ? "Choose a condition for every item (" + Ui.Count(missing, "item is", "items are") + " still empty)." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("in-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            bool stepped = false;
            using (await Store.Hold())
            {
                List<InspectionRec> all = await Store.Rows<InspectionRec>("inspections");
                InspectionRec? i = all.FirstOrDefault(x => x.Id == found.Id);
                if (i != null)
                {
                    i.Kind = kind;
                    i.Date = date;
                    i.Inspector = insp;
                    i.Rooms = rooms;
                    i.Summary = Ui.Clip((GetDataValue("summary") ?? string.Empty).Replace("\r", string.Empty), 1000);
                    if (finish)
                    {
                        i.Status = "Done";
                        i.Finished = DateTime.Now;
                        if (i.Kind == "Move-out")
                        {
                            List<TurnRec> turns = await Store.Rows<TurnRec>("turns");
                            TurnRec? t = turns.FirstOrDefault(x => x.UnitId == i.UnitId && x.Status == "Open");
                            TurnStep? s = t?.Steps.FirstOrDefault(x => x.Name == "Move-out inspection" && !x.Done);
                            if (s != null)
                            {
                                s.Done = true;
                                s.DoneAt = DateTime.Now;
                                s.By = c.U.Id;
                                stepped = true;
                                await Store.SaveRows("turns", turns);
                            }
                        }
                    }
                    await Store.SaveRows("inspections", all);
                }
            }
            await Store.Log(c.U.Id, finish ? "finish inspection" : "edit inspection", kind + " · " + Rent.UnitLabel(d, found.UnitId), true);
            int flagged = rooms.Sum(r => r.Items.Count(x => x.Condition is "Poor" or "Damaged" or "Not there"));
            await Reopen(response, c, found.Id, finish ? "Inspection finished." + (flagged > 0 ? " " + Ui.Count(flagged, "item needs", "items need") + " work: press Create work orders." : " Everything is in order.") + (stepped ? " The make-ready step is ticked off." : string.Empty) : "Progress saved.");
            return response;
        }

        public async Task<ApiResponse> CancelIt()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, InspectionRec? found) = await Find(c);
            if (found == null || !Can(c, 'E') || found.Status != "Scheduled")
            {
                return await Refuse(response, c, "cancel inspection", Ui.Clip(GetDataValue("id"), 60), found == null ? "That inspection was not found." : "Only a scheduled inspection can be cancelled.");
            }
            using (await Store.Hold())
            {
                List<InspectionRec> all = await Store.Rows<InspectionRec>("inspections");
                InspectionRec? i = all.FirstOrDefault(x => x.Id == found.Id);
                if (i != null)
                {
                    i.Status = "Cancelled";
                    await Store.SaveRows("inspections", all);
                }
            }
            await Store.Log(c.U.Id, "cancel inspection", found.Kind + " · " + Rent.UnitLabel(d, found.UnitId), true);
            await Reopen(response, c, found.Id, "Inspection cancelled.");
            return response;
        }

        public async Task<ApiResponse> MakeOrders()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, "workorders", 'A'))
            {
                return await Refuse(response, c, "inspection work orders", string.Empty, "You do not have the right to add work orders.");
            }
            (RentData d0, InspectionRec? i) = await Find(c);
            if (i == null || i.Status != "Done")
            {
                return await Refuse(response, c, "inspection work orders", Ui.Clip(GetDataValue("id"), 60), i == null ? "That inspection was not found." : "Finish the inspection first.");
            }
            int made = 0;
            using (await Store.Hold())
            {
                RentData d = await Rent.Read();
                UnitRec? u = d.Units.FirstOrDefault(x => x.Id == i.UnitId);
                TurnRec? t = d.Turns.FirstOrDefault(x => x.UnitId == i.UnitId && x.Status == "Open");
                if (u != null)
                {
                    foreach (InspRoom r in i.Rooms)
                    {
                        foreach (InspItem x in r.Items.Where(x => x.Condition is "Poor" or "Damaged" or "Not there"))
                        {
                            if (HasOrder(d, i, r, x))
                            {
                                continue;
                            }
                            DateTime now = DateTime.Now;
                            string cat = x.Name.Contains("Toilet") || x.Name.Contains("Sink") || x.Name.Contains("Tub") || x.Name.Contains("Water heater") ? "Plumbing" : x.Name.Contains("Lights") || x.Name.Contains("Smoke") || x.Name.Contains("Fan") ? "Electrical" : x.Name.Contains("Heating") ? "Heating and cooling" : x.Name.Contains("Stove") || x.Name.Contains("Fridge") || x.Name.Contains("Dishwasher") ? "Appliance" : x.Name.Contains("Doors") || x.Name.Contains("Keys") ? "Doors and locks" : x.Name.Contains("Windows") ? "Windows" : x.Name.Contains("Walls") || x.Name.Contains("Floors") || x.Name.Contains("Cabinets") || x.Name.Contains("Closet") ? "Walls and floors" : "Other";
                            d.WorkOrders.Add(new WorkOrderRec { Id = Fmt.NewId("w-"), Code = Fmt.Code("WO-", d.WorkOrders.Select(w => w.Code)), PropertyId = u.PropertyId, UnitId = u.Id, Category = cat, Title = (x.Condition == "Not there" ? "Missing: " : "Fix: ") + r.Room.ToLowerInvariant() + " " + x.Name.ToLowerInvariant(), Detail = "From the " + i.Kind.ToLowerInvariant() + " inspection: " + ItemKey(r, x) + "." + (x.Note.Length > 0 ? " " + x.Note : string.Empty), Priority = "Normal", Status = "New", Source = "inspection", EntryOk = u.Status != "Occupied", Due = Maint.DueFor("Normal", now), BillTo = "Owner", InspectionId = i.Id, TurnId = t?.Id ?? string.Empty, Created = now, By = c.U.Id });
                            made++;
                        }
                    }
                    await Store.SaveRows("workorders", d.WorkOrders);
                }
            }
            await Store.Log(c.U.Id, "inspection work orders", Ui.Count(made, "work order", "work orders") + " · " + Rent.UnitLabel(d0, i.UnitId), true);
            await Reopen(response, c, i.Id, made == 0 ? "Every item that needs work already has a work order." : Ui.Count(made, "work order was", "work orders were") + " created. Find them on Work orders.");
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
                return await Refuse(response, c, "delete inspection", string.Empty, "You do not have the right to delete inspections.");
            }
            (RentData d, InspectionRec? i) = await Find(c);
            if (i == null || i.Status == "Done")
            {
                return await Refuse(response, c, "delete inspection", Ui.Clip(GetDataValue("id"), 60), i == null ? "That inspection was not found." : "A finished inspection stays in the records.");
            }
            using (await Store.Hold())
            {
                List<InspectionRec> all = await Store.Rows<InspectionRec>("inspections");
                all.RemoveAll(x => x.Id == i.Id);
                await Store.SaveRows("inspections", all);
            }
            await Store.Log(c.U.Id, "delete inspection", i.Kind + " · " + Rent.UnitLabel(d, i.UnitId), true);
            await Refresh(response, c);
            response.ExecuteScript("AdminInspectionsJs.deleted();");
            return response;
        }
    }
}
