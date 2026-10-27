using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminTurns : WebPage
    {
        private const string AreaKey = "turns";
        private const int Size = 12;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Make-ready | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminTurnsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminTurnsJs.denied();");
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
            response.SetElementContents("tn-brand", Ui.Esc(site.Brand));
            response.SetElementContents("tn-menu", await Auth.Menu("tn", "AdminTurns", c));
            response.SetElementContents("tn-who", Auth.Who("tn", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminTurnsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"tn-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"tn-btn tn-btn-ghost tn-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminTurnsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"tn-btn tn-btn-ghost tn-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminTurnsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"tn-pill tn-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"tn-err\" id=\"tn-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"tn-field\" for=\"tn-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"tn-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static IEnumerable<TurnRec> Mine(Ctx c, RentData d, string prop)
        {
            return d.Turns.Where(t => Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, t.UnitId)) && (prop.Length == 0 || Rent.PropertyIdOfUnit(d, t.UnitId) == prop));
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
            response.SetElementContents("tn-main", "<div class=\"tn-ph\"><div><h1>Make-ready</h1><p>Every home between a move-out and the next resident. A move-out starts the list by itself; tick each step as it is done, then mark the unit ready to rent.</p></div>" + (Can(c, 'E') ? "<button type=\"button\" class=\"tn-btn\" onclick=\"AdminTurnsJs.add()\">Start make-ready</button>" : string.Empty) + "</div><div id=\"tn-tabs\">" + TabsHtml(c, d, "Open", string.Empty) + "</div><div class=\"tn-tools\"><select id=\"tn-fp\" onchange=\"AdminTurnsJs.list(true)\" aria-label=\"Property\">" + Opt(string.Empty, "All properties", string.Empty) + string.Concat(mine.Select(p => Opt(p.Id, p.Name, string.Empty))) + "</select></div><div id=\"tn-list\">" + BoardHtml(c, d, await Store.LoadStaff(), "Open", string.Empty, 1) + "</div>");
            return response;
        }

        private static string TabsHtml(Ctx c, RentData d, string tab, string prop)
        {
            List<TurnRec> all = Mine(c, d, prop).ToList();
            return "<div class=\"tn-tabs\" role=\"tablist\">" + string.Concat(new[] { ("Open", "In progress"), ("Done", "Finished") }.Select(t => "<button type=\"button\" role=\"tab\" class=\"tn-tab" + (t.Item1 == tab ? " tn-act" : string.Empty) + "\" aria-selected=\"" + (t.Item1 == tab ? "true" : "false") + "\" onclick=\"AdminTurnsJs.tab('" + t.Item1 + "')\">" + t.Item2 + "<i>" + all.Count(x => x.Status == t.Item1) + "</i></button>")) + "</div>";
        }

        private static string BoardHtml(Ctx c, RentData d, List<AdminUser> staff, string tab, string prop, int page)
        {
            IEnumerable<TurnRec> q = Mine(c, d, prop).Where(t => t.Status == tab);
            List<TurnRec> rows = (tab == "Open" ? q.OrderBy(t => t.Target) : q.OrderByDescending(t => t.Finished ?? t.Start)).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"tn-panel tn-empty\">" + (tab == "Open" ? "No homes are being made ready. A move-out on Leases starts one here." : "Nothing finished yet.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            bool edit = Can(c, 'E');
            StringBuilder sb = new StringBuilder("<div class=\"tn-board\">");
            foreach (TurnRec t in rows.Skip((page - 1) * Size).Take(Size))
            {
                UnitRec? u = d.Units.FirstOrDefault(x => x.Id == t.UnitId);
                int done = t.Steps.Count(s => s.Done);
                int pct = t.Steps.Count == 0 ? 0 : (int)Math.Round(done * 100.0 / t.Steps.Count);
                int days = ((t.Finished ?? DateTime.Today).Date - t.Start.Date).Days;
                bool late = t.Status == "Open" && t.Target.Date < DateTime.Today;
                List<WorkOrderRec> jobs = d.WorkOrders.Where(w => w.UnitId == t.UnitId && (w.TurnId == t.Id || (Maint.Open(w) && w.Created >= t.Start))).ToList();
                LeaseRec? next = d.Leases.Where(l => l.UnitId == t.UnitId && l.Status == "Upcoming").OrderBy(l => l.Start).FirstOrDefault();
                sb.Append("<article class=\"tn-turn" + (late ? " tn-late" : string.Empty) + "\"><header><div><b>" + Ui.Esc(Rent.UnitLabel(d, t.UnitId)) + "</b><small>" + Ui.Esc(u == null ? string.Empty : Fmt.Beds(u.Beds) + ", " + Fmt.Baths(u.Baths)) + "</small></div>" + Pill(t.Status == "Done" ? "Ready" : late ? "Late" : "Open") + "</header>");
                sb.Append("<div class=\"tn-meter\" role=\"progressbar\" aria-valuenow=\"" + pct + "\" aria-valuemin=\"0\" aria-valuemax=\"100\" aria-label=\"Steps done\"><span style=\"width:" + pct + "%\"></span></div>");
                sb.Append("<p class=\"tn-mini\">" + done + " of " + t.Steps.Count + " steps &middot; " + Ui.Count(days, "day", "days") + " since move-out &middot; " + (t.Status == "Done" ? "ready " + Ui.Esc(Fmt.Day(t.Finished ?? t.Start)) : "target " + Ui.Esc(Fmt.Short(t.Target))) + "</p>");
                if (next != null)
                {
                    sb.Append("<p class=\"tn-next\">Next resident moves in " + Ui.Esc(Fmt.Day(next.Start)) + "</p>");
                }
                sb.Append("<ul class=\"tn-checks2\">");
                for (int k = 0; k < t.Steps.Count; k++)
                {
                    TurnStep s = t.Steps[k];
                    sb.Append("<li><label class=\"tn-check\"><input type=\"checkbox\"" + (s.Done ? " checked" : string.Empty) + (edit && t.Status == "Open" ? " onchange=\"AdminTurnsJs.step('" + Ui.Js(t.Id) + "'," + k + ",this.checked)\"" : " disabled") + "><span>" + Ui.Esc(s.Name) + "</span></label>" + (s.Done && s.DoneAt != null ? "<small>" + Ui.Esc(Fmt.Short(s.DoneAt.Value) + (staff.FirstOrDefault(x => x.Id == s.By) is AdminUser by ? " · " + by.Name : string.Empty)) + "</small>" : string.Empty) + "</li>");
                }
                sb.Append("</ul>");
                if (jobs.Count > 0)
                {
                    sb.Append("<div class=\"tn-jobs\">" + string.Concat(jobs.Select(w => "<a href=\"AdminWorkOrders?code=" + Ui.Esc(w.Code) + "\">" + Pill(w.Status) + " " + Ui.Esc(w.Title) + "</a>")) + "</div>");
                }
                if (t.Note.Length > 0)
                {
                    sb.Append("<p class=\"tn-tnote\">" + Ui.Esc(t.Note) + "</p>");
                }
                sb.Append("<footer>" + (edit ? "<button type=\"button\" class=\"tn-btn tn-btn-ghost tn-btn-sm\" onclick=\"AdminTurnsJs.edit('" + Ui.Js(t.Id) + "')\">Details</button>" : string.Empty) + (edit && t.Status == "Open" ? "<button type=\"button\" class=\"tn-btn tn-btn-sm\"" + (done < t.Steps.Count ? " disabled title=\"Tick every step first\"" : string.Empty) + " onclick=\"AdminTurnsJs.ready('" + Ui.Js(t.Id) + "','" + Ui.Js(Rent.UnitLabel(d, t.UnitId)) + "'," + jobs.Count(Maint.Open) + ")\">Unit is ready</button>" : string.Empty) + "</footer></article>");
            }
            sb.Append("</div>" + (pages > 1 ? Pager(page, pages, rows.Count, "home", "homes") : string.Empty));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            RentData d = await Rent.Load();
            string tab = Ui.Clip(GetDataValue("tab"), 6);
            tab = tab == "Done" ? "Done" : "Open";
            string prop = Ui.Clip(GetDataValue("prop"), 60);
            response.SetElementContents("tn-list", BoardHtml(c, d, await Store.LoadStaff(), tab, prop, PageNo(GetDataValue("pg"))));
            response.SetElementContents("tn-tabs", TabsHtml(c, d, tab, prop));
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

        private async Task<(RentData D, TurnRec? T)> Find(Ctx c)
        {
            string id = Ui.Clip(GetDataValue("id"), 60);
            RentData d = await Rent.Load();
            TurnRec? t = d.Turns.FirstOrDefault(x => x.Id == id);
            return t == null || !Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, t.UnitId)) ? (d, null) : (d, t);
        }

        public async Task<ApiResponse> Toggle()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, TurnRec? found) = await Find(c);
            int k = Int(GetDataValue("k"), -1);
            bool on = (GetDataValue("on") ?? string.Empty) == "1";
            if (found == null || !Can(c, 'E') || found.Status != "Open" || k < 0 || k >= found.Steps.Count)
            {
                await Refresh(response, c);
                return await Refuse(response, c, "make-ready step", Ui.Clip(GetDataValue("id"), 60), found == null ? "That make-ready was not found." : "You cannot change this make-ready.");
            }
            using (await Store.Hold())
            {
                List<TurnRec> all = await Store.Rows<TurnRec>("turns");
                TurnRec? t = all.FirstOrDefault(x => x.Id == found.Id);
                if (t != null)
                {
                    TurnStep s = t.Steps[k];
                    s.Done = on;
                    s.DoneAt = on ? DateTime.Now : null;
                    s.By = on ? c.U.Id : string.Empty;
                    await Store.SaveRows("turns", all);
                }
            }
            await Store.Log(c.U.Id, on ? "make-ready step done" : "make-ready step undone", found.Steps[k].Name + " · " + Rent.UnitLabel(d, found.UnitId), true);
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
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "make-ready", string.Empty, "You can view the board but not change it.");
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            RentData d = await Rent.Load();
            if (id.Length == 0)
            {
                HashSet<string> busy = new HashSet<string>(d.Turns.Where(t => t.Status == "Open").Select(t => t.UnitId));
                List<UnitRec> units = d.Units.Where(u => Access.PropertyOk(c.U, u.PropertyId) && !busy.Contains(u.Id) && u.Status is "Vacant" or "Make-ready" or "Offline").OrderBy(u => Rent.UnitLabel(d, u.Id)).ToList();
                StringBuilder nb = new StringBuilder("<form class=\"tn-stack\" id=\"tn-form\" onsubmit=\"return AdminTurnsJs.start()\" novalidate><p class=\"tn-mini\">For a home that needs work without a move-out, for example after a long vacancy or a repair. The unit is set to make-ready.</p>");
                nb.Append(units.Count == 0 ? "<p class=\"tn-panel tn-empty\">Every vacant home already has a make-ready list.</p>" : Field("unit", "Home", "<select id=\"tn-f-unit\" data-k=\"unit\">" + string.Concat(units.Select(u => Opt(u.Id, Rent.UnitLabel(d, u.Id) + " · " + u.Status, string.Empty))) + "</select>") + Field("target", "Ready by", Text("target", Fmt.IsoDay(DateTime.Today.AddDays(10)), 10, "date")));
                nb.Append("<div class=\"tn-acts\">" + (units.Count > 0 ? "<button type=\"submit\" class=\"tn-btn\">Start</button>" : string.Empty) + "<button type=\"button\" class=\"tn-btn tn-btn-ghost\" onclick=\"AdminTurnsJs.closeModal()\">Cancel</button></div></form>");
                response.SetElementContents("tn-mbody", nb.ToString());
                response.ExecuteScript("AdminTurnsJs.opened('Start make-ready');");
                return response;
            }
            TurnRec? t = d.Turns.FirstOrDefault(x => x.Id == id && Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, x.UnitId)));
            if (t == null)
            {
                return await Refuse(response, c, "make-ready", id, "That make-ready was not found.");
            }
            bool open = t.Status == "Open";
            StringBuilder sb = new StringBuilder("<form class=\"tn-stack\" id=\"tn-form\" onsubmit=\"return AdminTurnsJs.save()\" novalidate><input type=\"hidden\" id=\"tn-f-id\" value=\"" + Ui.Esc(t.Id) + "\">");
            sb.Append("<div class=\"tn-bar\"><span>" + Pill(open ? "Open" : "Ready") + " Started " + Ui.Esc(Fmt.Day(t.Start)) + (t.Finished != null ? " &middot; ready " + Ui.Esc(Fmt.Day(t.Finished.Value)) : string.Empty) + "</span>" + (Access.Can(c.U, "workorders", 'A') && open ? "<a class=\"tn-btn tn-btn-ghost tn-btn-sm\" href=\"AdminWorkOrders\">Work orders</a>" : string.Empty) + "</div>");
            sb.Append(Field("target", "Ready by", Text("target", Fmt.IsoDay(t.Target), 10, "date", open ? string.Empty : " readonly")));
            sb.Append("<label class=\"tn-field\" for=\"tn-f-note\"><span>Note</span><textarea id=\"tn-f-note\" data-k=\"note\" rows=\"3\" maxlength=\"600\">" + Ui.Esc(t.Note) + "</textarea>" + Err("note") + "</label>");
            sb.Append("<div class=\"tn-acts\"><button type=\"submit\" class=\"tn-btn\">Save</button><button type=\"button\" class=\"tn-btn tn-btn-ghost\" onclick=\"AdminTurnsJs.closeModal()\">Cancel</button>" + (open ? "<button type=\"button\" class=\"tn-btn tn-btn-danger tn-push\" onclick=\"AdminTurnsJs.stop('" + Ui.Js(t.Id) + "')\">Stop this list</button>" : string.Empty) + "</div></form>");
            response.SetElementContents("tn-mbody", sb.ToString());
            response.ExecuteScript("AdminTurnsJs.opened('" + Ui.Js("Make-ready · " + Rent.UnitLabel(d, t.UnitId)) + "');");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, TurnRec? found) = await Find(c);
            if (found == null || !Can(c, 'E'))
            {
                return await Refuse(response, c, "make-ready", Ui.Clip(GetDataValue("id"), 60), found == null ? "That make-ready was not found." : "You cannot change this make-ready.");
            }
            bool tOk = Fmt.ParseDate(GetDataValue("target"), out DateTime target);
            if (found.Status == "Open" && (!tOk || target < found.Start))
            {
                response.SetElementContents("tn-e-target", "Enter a date on or after the move-out.");
                return response;
            }
            using (await Store.Hold())
            {
                List<TurnRec> all = await Store.Rows<TurnRec>("turns");
                TurnRec? t = all.FirstOrDefault(x => x.Id == found.Id);
                if (t != null)
                {
                    if (t.Status == "Open")
                    {
                        t.Target = target.Date;
                    }
                    t.Note = Ui.Clip((GetDataValue("note") ?? string.Empty).Replace("\r", string.Empty), 600);
                    await Store.SaveRows("turns", all);
                }
            }
            await Store.Log(c.U.Id, "edit make-ready", Rent.UnitLabel(d, found.UnitId), true);
            await Refresh(response, c);
            response.ExecuteScript("AdminTurnsJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Start()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "start make-ready", string.Empty, "You cannot change the make-ready board.");
            }
            RentData d0 = await Rent.Load();
            string unit = Ui.Clip(GetDataValue("unit"), 60);
            UnitRec? u0 = d0.Units.FirstOrDefault(x => x.Id == unit && Access.PropertyOk(c.U, x.PropertyId));
            bool tOk = Fmt.ParseDate(GetDataValue("target"), out DateTime target);
            string e1 = u0 == null || d0.Turns.Any(t => t.UnitId == unit && t.Status == "Open") || !(u0.Status is "Vacant" or "Make-ready" or "Offline") ? "Choose a vacant home without a make-ready list." : string.Empty;
            string e2 = !tOk || target < DateTime.Today ? "Enter a date from today." : string.Empty;
            response.SetElementContents("tn-e-unit", e1);
            response.SetElementContents("tn-e-target", e2);
            if (e1.Length > 0 || e2.Length > 0 || u0 == null)
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<TurnRec> all = await Store.Rows<TurnRec>("turns");
                TurnRec t = Maint.NewTurn(u0, string.Empty, DateTime.Today, c.U.Id);
                t.Target = target.Date;
                t.Steps.First(s => s.Name == "Move-out inspection").Name = "Walk-through inspection";
                all.Add(t);
                await Store.SaveRows("turns", all);
                List<UnitRec> units = await Store.Rows<UnitRec>("units");
                UnitRec? u = units.FirstOrDefault(x => x.Id == u0.Id);
                if (u != null && u.Status != "Make-ready")
                {
                    u.Status = "Make-ready";
                    u.StatusSince = DateTime.Now;
                    await Store.SaveRows("units", units);
                }
            }
            await Store.Log(c.U.Id, "start make-ready", Rent.UnitLabel(d0, u0.Id), true);
            await Refresh(response, c);
            response.ExecuteScript("AdminTurnsJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Ready()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, TurnRec? found) = await Find(c);
            if (found == null || !Can(c, 'E') || found.Status != "Open")
            {
                return await Refuse(response, c, "unit ready", Ui.Clip(GetDataValue("id"), 60), found == null ? "That make-ready was not found." : "This make-ready is already finished.");
            }
            if (found.Steps.Any(s => !s.Done))
            {
                return await Refuse(response, c, "unit ready", Rent.UnitLabel(d, found.UnitId), "Tick every step before marking the unit ready.");
            }
            using (await Store.Hold())
            {
                List<TurnRec> all = await Store.Rows<TurnRec>("turns");
                TurnRec? t = all.FirstOrDefault(x => x.Id == found.Id);
                if (t != null)
                {
                    t.Status = "Done";
                    t.Finished = DateTime.Now;
                    await Store.SaveRows("turns", all);
                }
                List<UnitRec> units = await Store.Rows<UnitRec>("units");
                UnitRec? u = units.FirstOrDefault(x => x.Id == found.UnitId);
                if (u != null && u.Status is "Make-ready" or "Vacant" or "Offline")
                {
                    u.Status = "Vacant";
                    u.StatusSince = DateTime.Now;
                    u.Available = DateTime.Today;
                    await Store.SaveRows("units", units);
                }
            }
            UnitRec? unit = d.Units.FirstOrDefault(x => x.Id == found.UnitId);
            await Store.Log(c.U.Id, "unit ready", Rent.UnitLabel(d, found.UnitId), true);
            await Refresh(response, c);
            response.ExecuteScript("AdminTurnsJs.toast('" + Ui.Js(Rent.UnitLabel(d, found.UnitId) + " is ready to rent" + (unit != null && unit.Listed ? " and shows on the website." : ". Turn on Show on the website in Units to list it.")) + "');");
            return response;
        }

        public async Task<ApiResponse> Stop()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, TurnRec? found) = await Find(c);
            if (found == null || !Can(c, 'E') || found.Status != "Open")
            {
                return await Refuse(response, c, "stop make-ready", Ui.Clip(GetDataValue("id"), 60), "That make-ready was not found.");
            }
            using (await Store.Hold())
            {
                List<TurnRec> all = await Store.Rows<TurnRec>("turns");
                all.RemoveAll(x => x.Id == found.Id);
                await Store.SaveRows("turns", all);
                List<WorkOrderRec> wos = await Store.Rows<WorkOrderRec>("workorders");
                foreach (WorkOrderRec w in wos.Where(w => w.TurnId == found.Id))
                {
                    w.TurnId = string.Empty;
                }
                await Store.SaveRows("workorders", wos);
            }
            await Store.Log(c.U.Id, "stop make-ready", Rent.UnitLabel(d, found.UnitId), true);
            await Refresh(response, c);
            response.ExecuteScript("AdminTurnsJs.deleted();");
            return response;
        }
    }
}
