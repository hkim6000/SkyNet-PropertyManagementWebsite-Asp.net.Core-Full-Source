using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminLeases : WebPage
    {
        private const string AreaKey = "leases";
        private const int Size = 12;
        private static readonly (string Key, string Name)[] Tabs = { ("Active", "Active"), ("Upcoming", "Upcoming"), ("Notice", "Notice"), ("expiring", "Ending in 90 days"), ("holdover", "Month to month"), ("ended", "Ended"), ("all", "All") };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Leases | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminLeasesJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminLeasesJs.denied();");
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
            response.SetElementContents("ls-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ls-menu", await Auth.Menu("ls", "AdminLeases", c));
            response.SetElementContents("ls-who", Auth.Who("ls", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminLeasesJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"ls-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"ls-btn ls-btn-ghost ls-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminLeasesJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"ls-btn ls-btn-ghost ls-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminLeasesJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"ls-pill ls-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"ls-err\" id=\"ls-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"ls-field\" for=\"ls-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"ls-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static string Disp(LeaseRec l)
        {
            return l.Status == "Upcoming" && l.Start.Date < DateTime.Today ? "Move-in due" : Rent.Holdover(l) ? "Month to month" : l.Status;
        }

        private static IEnumerable<LeaseRec> InTab(IEnumerable<LeaseRec> q, string tab)
        {
            return tab switch
            {
                "Active" => q.Where(l => l.Status == "Active" && !Rent.Holdover(l)).OrderBy(l => l.End),
                "Upcoming" => q.Where(l => l.Status == "Upcoming").OrderBy(l => l.Start),
                "Notice" => q.Where(l => l.Status == "Notice").OrderBy(l => l.MoveOut ?? l.End),
                "expiring" => q.Where(l => Rent.Expiring(l, 90)).OrderBy(l => l.End),
                "holdover" => q.Where(Rent.Holdover).OrderBy(l => l.End),
                "ended" => q.Where(l => l.Status is "Ended" or "Cancelled").OrderByDescending(l => l.MovedOut ?? l.End),
                _ => q.OrderByDescending(l => l.Start)
            };
        }

        private static IEnumerable<LeaseRec> Mine(Ctx c, RentData d, string prop, string unit)
        {
            return d.Leases.Where(l => Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, l.UnitId)) && (prop.Length == 0 || Rent.PropertyIdOfUnit(d, l.UnitId) == prop) && (unit.Length == 0 || l.UnitId == unit));
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
            string code = Ui.Clip(GetDataValue("lcode"), 12).ToUpperInvariant();
            string unit = Ui.Clip(GetDataValue("unit"), 60);
            unit = d.Units.Any(u => u.Id == unit && Access.PropertyOk(c.U, u.PropertyId)) ? unit : string.Empty;
            string tab = code.Length > 0 || unit.Length > 0 ? "all" : "Active";
            List<PropertyRec> mine = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).ToList();
            response.SetElementContents("ls-main", "<div class=\"ls-ph\"><div><h1>Leases</h1><p>Who lives where, for how long and at what rent. Move residents in and out, take notice and renew from the lease itself.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"ls-btn\" onclick=\"AdminLeasesJs.add()\">New lease</button>" : string.Empty) + "</div><div id=\"ls-tabs\">" + TabsHtml(c, d, tab, string.Empty, unit) + "</div><div class=\"ls-tools\"><input type=\"search\" id=\"ls-q\" placeholder=\"Search by lease code, resident or unit\" maxlength=\"40\" value=\"" + Ui.Esc(code) + "\" oninput=\"AdminLeasesJs.type()\" aria-label=\"Search leases\"><select id=\"ls-fp\" onchange=\"AdminLeasesJs.list(true)\" aria-label=\"Property\">" + Opt(string.Empty, "All properties", string.Empty) + string.Concat(mine.Select(p => Opt(p.Id, p.Name, string.Empty))) + "</select>" + (unit.Length > 0 ? "<span class=\"ls-chip ls-unitchip\" id=\"ls-uchip\">" + Ui.Esc(Rent.UnitLabel(d, unit)) + " <button type=\"button\" class=\"ls-ibtn\" aria-label=\"Show all units\" onclick=\"AdminLeasesJs.allUnits()\">&times;</button></span>" : string.Empty) + "</div><div id=\"ls-list\">" + ListHtml(c, d, code, tab, string.Empty, unit, 1) + "</div>");
            response.ExecuteScript("AdminLeasesJs.setTab('" + tab + "','" + Ui.Js(unit) + "');");
            if (code.Length > 0)
            {
                LeaseRec? l = Mine(c, d, string.Empty, string.Empty).FirstOrDefault(x => x.Code == code);
                if (l != null)
                {
                    response.ExecuteScript("AdminLeasesJs.edit('" + Ui.Js(l.Id) + "');");
                }
            }
            return response;
        }

        private static string TabsHtml(Ctx c, RentData d, string tab, string prop, string unit)
        {
            List<LeaseRec> all = Mine(c, d, prop, unit).ToList();
            return "<div class=\"ls-tabs\" role=\"tablist\">" + string.Concat(Tabs.Select(t => "<button type=\"button\" role=\"tab\" class=\"ls-tab" + (t.Key == tab ? " ls-act" : string.Empty) + "\" aria-selected=\"" + (t.Key == tab ? "true" : "false") + "\" onclick=\"AdminLeasesJs.tab('" + t.Key + "')\">" + t.Name + (t.Key is "ended" or "all" ? string.Empty : "<i>" + InTab(all, t.Key).Count() + "</i>") + "</button>")) + "</div>";
        }

        private static string ListHtml(Ctx c, RentData d, string term, string tab, string prop, string unit, int page)
        {
            List<LeaseRec> rows = InTab(Mine(c, d, prop, unit).Where(l => term.Length == 0 || l.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || Ui.Has(Rent.Names(d, l), term) || Ui.Has(Rent.UnitLabel(d, l.UnitId), term)), tab).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ls-panel ls-empty\">No leases here.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"ls-wrapt\"><table class=\"ls-table\"><thead><tr><th>Lease</th><th>Home</th><th>Residents</th><th>Term</th><th class=\"ls-num\">Rent</th><th class=\"ls-num\">Balance</th><th>Status</th></tr></thead><tbody>");
            foreach (LeaseRec l in rows.Skip((page - 1) * Size).Take(Size))
            {
                decimal bal = Rent.Balance(d, l.Id);
                string st = Disp(l);
                sb.Append("<tr class=\"ls-row\" onclick=\"AdminLeasesJs.edit('" + Ui.Js(l.Id) + "')\"><td class=\"ls-first\"><b>" + Ui.Esc(l.Code) + "</b><small>" + (l.RenewalOf.Length > 0 ? "Renewal" : "Since " + Ui.Esc(Fmt.Day(l.Created))) + "</small></td><td data-label=\"Home\">" + Ui.Esc(Rent.UnitLabel(d, l.UnitId)) + "</td><td data-label=\"Residents\">" + Ui.Esc(Rent.Names(d, l)) + "</td><td data-label=\"Term\">" + Ui.Esc(Fmt.Day(l.Start)) + "<small class=\"ls-mini\"><br>to " + Ui.Esc(Fmt.Day(l.End)) + (l.Status == "Notice" && l.MoveOut != null ? ", out " + Ui.Esc(Fmt.Short(l.MoveOut.Value)) : string.Empty) + "</small></td><td class=\"ls-num\" data-label=\"Rent\">" + Fmt.Whole(l.Rent) + "</td><td class=\"ls-num\" data-label=\"Balance\">" + (bal > 0.004m ? "<b class=\"ls-owe\">" + Fmt.Money(bal) + "</b>" : bal < -0.004m ? Fmt.Money(bal) : "<span class=\"ls-mini\">$0.00</span>") + "</td><td data-label=\"Status\">" + Pill(st) + (Rent.Expiring(l, 90) ? "<div class=\"ls-flags\"><span class=\"ls-flag\">Ends in " + (l.End.Date - DateTime.Today).Days + " days</span></div>" : string.Empty) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "lease", "leases"));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            RentData d = await Rent.Load();
            string tab = Ui.Clip(GetDataValue("tab"), 12);
            string prop = Ui.Clip(GetDataValue("prop"), 60);
            string unit = Ui.Clip(GetDataValue("unit"), 60);
            response.SetElementContents("ls-list", ListHtml(c, d, Term(GetDataValue("term")), tab, prop, unit, PageNo(GetDataValue("pg"))));
            response.SetElementContents("ls-tabs", TabsHtml(c, d, tab, prop, unit));
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

        private async Task<(RentData D, LeaseRec? L)> Find(Ctx c)
        {
            string id = Ui.Clip(GetDataValue("id"), 60);
            RentData d = await Rent.Load();
            LeaseRec? l = d.Leases.FirstOrDefault(x => x.Id == id);
            return l == null || !Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, l.UnitId)) ? (d, null) : (d, l);
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
                    return await Refuse(response, c, "add lease", string.Empty, "You can view leases but not add them.");
                }
                RentData d0 = await Rent.Load();
                response.SetElementContents("ls-mbody", NewHtml(c, d0, Ui.Clip(GetDataValue("unit"), 60)));
                response.ExecuteScript("AdminLeasesJs.opened('New lease');");
                return response;
            }
            (RentData d, LeaseRec? l) = await Find(c);
            if (l == null)
            {
                return await Refuse(response, c, "open lease", id, "That lease was not found.");
            }
            response.SetElementContents("ls-mbody", FormHtml(c, d, l, string.Empty));
            response.ExecuteScript("AdminLeasesJs.opened('" + Ui.Js(l.Code + " · " + Rent.UnitLabel(d, l.UnitId)) + "');");
            return response;
        }

        private static string ResidentOptions(RentData d, string current, IEnumerable<string> skip)
        {
            HashSet<string> busy = new HashSet<string>(d.Leases.Where(l => l.Status is "Active" or "Notice" or "Upcoming").SelectMany(l => l.Residents));
            return string.Concat(d.Residents.Where(r => !skip.Contains(r.Id)).OrderBy(r => busy.Contains(r.Id)).ThenBy(r => r.Last).ThenBy(r => r.First).Select(r => Opt(r.Id, Rent.Name(r) + " (" + r.Email + ")" + (busy.Contains(r.Id) ? " · has a lease" : string.Empty), current)));
        }

        private static string NewHtml(Ctx c, RentData d, string unit)
        {
            List<UnitRec> units = d.Units.Where(u => Access.PropertyOk(c.U, u.PropertyId) && u.Status != "Offline").OrderBy(u => Rent.UnitLabel(d, u.Id)).ToList();
            StringBuilder sb = new StringBuilder("<form class=\"ls-stack\" id=\"ls-new\" onsubmit=\"return AdminLeasesJs.create()\" novalidate><p class=\"ls-mini\">For an approved application, create the lease from Applications instead: it fills everything in. Add new people on Residents first.</p>");
            sb.Append(Field("cunit", "Home", "<select id=\"ls-f-cunit\" data-k=\"cunit\" onchange=\"AdminLeasesJs.unitPick(this)\">" + Opt(string.Empty, "Choose a home", unit) + string.Concat(units.Select(u => "<option value=\"" + Ui.Esc(u.Id) + "\" data-rent=\"" + Num(u.Rent) + "\" data-dep=\"" + Num(u.Deposit) + "\" data-from=\"" + Fmt.IsoDay(Rent.MoveInDate(u)) + "\"" + (u.Id == unit ? " selected" : string.Empty) + ">" + Ui.Esc(Rent.UnitLabel(d, u.Id) + " · " + u.Status) + "</option>")) + "</select>"));
            sb.Append("<div class=\"ls-grid2\">" + Field("cr0", "Main resident", "<select id=\"ls-f-cr0\" data-k=\"cr0\">" + Opt(string.Empty, "Choose a resident", string.Empty) + ResidentOptions(d, string.Empty, Array.Empty<string>()) + "</select>") + Field("cr1", "Second resident (optional)", "<select id=\"ls-f-cr1\" data-k=\"cr1\">" + Opt(string.Empty, "None", string.Empty) + ResidentOptions(d, string.Empty, Array.Empty<string>()) + "</select>") + "</div>");
            sb.Append("<div class=\"ls-grid2\">" + Field("cr2", "Third resident (optional)", "<select id=\"ls-f-cr2\" data-k=\"cr2\">" + Opt(string.Empty, "None", string.Empty) + ResidentOptions(d, string.Empty, Array.Empty<string>()) + "</select>") + Field("cr3", "Fourth resident (optional)", "<select id=\"ls-f-cr3\" data-k=\"cr3\">" + Opt(string.Empty, "None", string.Empty) + ResidentOptions(d, string.Empty, Array.Empty<string>()) + "</select>") + "</div>");
            DateTime start = DateTime.Today.AddDays(7);
            sb.Append("<div class=\"ls-grid2\">" + Field("cstart", "Start", Text("cstart", Fmt.IsoDay(start), 10, "date")) + Field("cend", "End", Text("cend", Fmt.IsoDay(start.AddYears(1).AddDays(-1)), 10, "date")) + "</div>");
            sb.Append("<div class=\"ls-grid2\">" + Field("crent", "Rent a month ($)", Text("crent", string.Empty, 10, "text", " inputmode=\"decimal\"")) + Field("cdep", "Deposit ($)", Text("cdep", string.Empty, 10, "text", " inputmode=\"decimal\"")) + "</div>");
            sb.Append("<div class=\"ls-grid2\">" + Field("cpets", "Pets on the lease", Text("cpets", string.Empty, 120)) + Field("cnote", "Note", Text("cnote", string.Empty, 300)) + "</div>");
            sb.Append("<label class=\"ls-check\"><input type=\"checkbox\" id=\"ls-f-ccharge\" data-k=\"ccharge\" checked>Charge the deposit and first month's rent on the start date</label>");
            sb.Append("<div class=\"ls-acts\"><button type=\"submit\" class=\"ls-btn\">Create lease</button><button type=\"button\" class=\"ls-btn ls-btn-ghost\" onclick=\"AdminLeasesJs.closeModal()\">Cancel</button></div></form>");
            return sb.ToString();
        }

        private static string FormHtml(Ctx c, RentData d, LeaseRec l, string note)
        {
            bool edit = Can(c, 'E');
            string st = Disp(l);
            decimal bal = Rent.Balance(d, l.Id);
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == l.UnitId);
            bool closed = l.Status is "Ended" or "Cancelled";
            StringBuilder sb = new StringBuilder("<div class=\"ls-stack\" id=\"ls-form\"><input type=\"hidden\" id=\"ls-f-id\" value=\"" + Ui.Esc(l.Id) + "\">" + (note.Length > 0 ? "<p class=\"ls-ok\">" + Ui.Esc(note) + "</p>" : string.Empty));
            sb.Append("<div class=\"ls-bar\"><span>" + Pill(st) + " " + Ui.Esc(Fmt.Day(l.Start) + " to " + Fmt.Day(l.End)) + (l.MovedIn != null ? " &middot; moved in " + Ui.Esc(Fmt.Day(l.MovedIn.Value)) : string.Empty) + (l.MoveOut != null ? " &middot; " + (l.MovedOut != null ? "moved out " + Ui.Esc(Fmt.Day(l.MovedOut.Value)) : "moving out " + Ui.Esc(Fmt.Day(l.MoveOut.Value))) : string.Empty) + "</span><span class=\"ls-acts-row\">");
            if (edit)
            {
                if (l.Status == "Upcoming" && l.RenewalOf.Length == 0)
                {
                    sb.Append("<button type=\"button\" class=\"ls-btn ls-btn-sm\" onclick=\"AdminLeasesJs.step('movein')\">Move in</button><button type=\"button\" class=\"ls-btn ls-btn-ghost ls-btn-sm\" onclick=\"AdminLeasesJs.step('cancel')\">Cancel lease</button>");
                }
                if (l.Status == "Upcoming" && l.RenewalOf.Length > 0)
                {
                    sb.Append("<button type=\"button\" class=\"ls-btn ls-btn-ghost ls-btn-sm\" onclick=\"AdminLeasesJs.step('cancel')\">Cancel renewal</button>");
                }
                if (l.Status == "Active")
                {
                    sb.Append("<button type=\"button\" class=\"ls-btn ls-btn-ghost ls-btn-sm\" onclick=\"AdminLeasesJs.step('notice')\">Record notice</button>" + (l.RenewedBy.Length == 0 ? "<button type=\"button\" class=\"ls-btn ls-btn-ghost ls-btn-sm\" onclick=\"AdminLeasesJs.step('renew')\">Renew</button>" : string.Empty));
                }
                if (l.Status == "Notice")
                {
                    sb.Append("<button type=\"button\" class=\"ls-btn ls-btn-sm\" onclick=\"AdminLeasesJs.step('moveout')\">Move out</button><button type=\"button\" class=\"ls-btn ls-btn-ghost ls-btn-sm\" onclick=\"AdminLeasesJs.step('unnotice')\">Cancel notice</button>");
                }
                if (l.Status == "Active" && Rent.Holdover(l))
                {
                    sb.Append("<button type=\"button\" class=\"ls-btn ls-btn-ghost ls-btn-sm\" onclick=\"AdminLeasesJs.step('moveout')\">Move out</button>");
                }
                if (l.Status == "Ended" && !d.Deposits.Any(x => x.LeaseId == l.Id && x.Status == "Final") && Access.Can(c.U, "payments", 'A'))
                {
                    sb.Append("<button type=\"button\" class=\"ls-btn ls-btn-sm\" onclick=\"AdminLeasesJs.deposit()\">Deposit statement</button>");
                }
            }
            sb.Append("</span></div><div class=\"ls-steps\" id=\"ls-stepbox\"></div>");
            if (l.RenewalOf.Length > 0 || l.RenewedBy.Length > 0)
            {
                LeaseRec? other = d.Leases.FirstOrDefault(x => x.Id == (l.RenewedBy.Length > 0 ? l.RenewedBy : l.RenewalOf));
                if (other != null)
                {
                    sb.Append("<p class=\"ls-mini\">" + (l.RenewedBy.Length > 0 ? "Renewed by " : "Renewal of ") + "<a href=\"#\" onclick=\"AdminLeasesJs.edit('" + Ui.Js(other.Id) + "');return false;\">" + Ui.Esc(other.Code) + "</a> (" + Ui.Esc(Fmt.Day(other.Start) + " to " + Fmt.Day(other.End) + ", " + Fmt.Whole(other.Rent)) + ").</p>");
                }
            }
            sb.Append("<div class=\"ls-grid2 ls-cols\"><div class=\"ls-sum\"><div><span>Home</span><b>" + Ui.Esc(Rent.UnitLabel(d, l.UnitId)) + "</b></div>" + (u != null ? "<div><span>Unit</span><span>" + Ui.Esc(Fmt.Beds(u.Beds) + ", " + Fmt.Baths(u.Baths) + " · " + u.Status) + "</span></div>" : string.Empty) + "<div><span>Rent</span><span>" + Fmt.Money(l.Rent) + " a month</span></div><div><span>Deposit</span><span>" + Fmt.Money(l.Deposit) + "</span></div>" + (l.NoticeOn != null ? "<div><span>Notice given</span><span>" + Ui.Esc(Fmt.Day(l.NoticeOn.Value)) + "</span></div>" : string.Empty) + "<div class=\"ls-tot\"><span>Balance</span><span" + (bal > 0.004m ? " class=\"ls-owe\"" : string.Empty) + ">" + Fmt.Money(bal) + "</span></div></div>");
            sb.Append("<div class=\"ls-sum\"><div><span>Residents</span><span></span></div>" + string.Concat(l.Residents.Select(id => d.Residents.FirstOrDefault(r => r.Id == id)).Where(r => r != null).Select(r => "<div><span>" + (Access.Can(c.U, "residents", 'V') ? "<a href=\"AdminResidents?r=" + Ui.Esc(r!.Id) + "\">" + Ui.Esc(Rent.Name(r)) + "</a>" : Ui.Esc(Rent.Name(r!))) + "</span><span class=\"ls-mini\">" + Ui.Esc(r!.Phone.Length > 0 ? r.Phone : r.Email) + " · " + (r.Hash.Length > 0 ? "portal active" : r.Invite.Length > 0 ? "code " + r.Invite : "no portal") + "</span></div>")) + "</div></div>");
            if (edit && !closed)
            {
                bool early = l.Status == "Upcoming";
                sb.Append("<form class=\"ls-stack ls-panel\" id=\"ls-edit\" onsubmit=\"return AdminLeasesJs.save()\" novalidate><h3>Lease details</h3><div class=\"ls-grid2\">" + Field("lstart", "Start", Text("lstart", Fmt.IsoDay(l.Start), 10, "date", early ? string.Empty : " readonly")) + Field("lend", "End", Text("lend", Fmt.IsoDay(l.End), 10, "date")) + "</div><div class=\"ls-grid2\">" + Field("lrent", "Rent a month ($)", Text("lrent", Num(l.Rent), 10, "text", " inputmode=\"decimal\""), early ? string.Empty : "A new rent applies to charges from now on.") + Field("ldep", "Deposit ($)", Text("ldep", Num(l.Deposit), 10, "text", " inputmode=\"decimal\"" + (early ? string.Empty : " readonly"))) + "</div>");
                sb.Append("<div class=\"ls-field\"><span>Residents on this lease</span><div class=\"ls-checks\">" + string.Concat(l.Residents.Select(id => d.Residents.FirstOrDefault(r => r.Id == id)).Where(r => r != null).Select(r => "<label class=\"ls-check\"><input type=\"checkbox\" data-k=\"rs_" + Ui.Esc(r!.Id) + "\" checked>" + Ui.Esc(Rent.Name(r)) + "</label>")) + "</div>" + Err("res") + "</div>");
                sb.Append(Field("radd", "Add a resident", "<select id=\"ls-f-radd\" data-k=\"radd\">" + Opt(string.Empty, "Nobody", string.Empty) + ResidentOptions(d, string.Empty, l.Residents) + "</select>"));
                sb.Append("<div class=\"ls-grid2\">" + Field("lpets", "Pets", Text("lpets", l.Pets, 120)) + Field("lnote", "Note", Text("lnote", l.Note, 300)) + "</div><div class=\"ls-acts\"><button type=\"submit\" class=\"ls-btn\">Save details</button></div></form>");
            }
            DepositRec? dep = d.Deposits.FirstOrDefault(x => x.LeaseId == l.Id && x.Status == "Final");
            if (dep != null)
            {
                sb.Append(DepositView(d, l, dep));
            }
            List<LedgerRec> lines = d.Ledger.Where(x => x.LeaseId == l.Id).OrderByDescending(x => x.When).ThenBy(x => x.Amount < 0 ? 0 : 1).Take(12).ToList();
            sb.Append("<div class=\"ls-field\"><span>Latest charges and payments" + (Access.Can(c.U, "payments", 'V') ? " &middot; <a class=\"ls-inl\" href=\"AdminPayments?code=" + Ui.Esc(l.Code) + "\">Open the account</a>" : string.Empty) + "</span>" + (lines.Count == 0 ? "<p class=\"ls-mini\">Nothing yet.</p>" : "<div class=\"ls-wrapt\"><table class=\"ls-table\"><thead><tr><th>Date</th><th>Item</th><th class=\"ls-num\">Amount</th></tr></thead><tbody>" + string.Concat(lines.Select(x => "<tr><td class=\"ls-first\">" + Ui.Esc(Fmt.Day(x.When)) + "</td><td data-label=\"Item\">" + Ui.Esc(x.Label.Length > 0 ? x.Label : x.Kind) + (x.Method.Length > 0 ? "<small class=\"ls-mini\"> · " + Ui.Esc(x.Method) + "</small>" : string.Empty) + "</td><td class=\"ls-num\" data-label=\"Amount\">" + (x.Amount < 0 ? "<span class=\"ls-goodtxt\">-" + Fmt.Money(-x.Amount) + "</span>" : Fmt.Money(x.Amount)) + "</td></tr>")) + "</tbody></table></div>") + "</div>");
            sb.Append("<div class=\"ls-acts\"><button type=\"button\" class=\"ls-btn ls-btn-ghost\" onclick=\"AdminLeasesJs.closeModal()\">Close</button>" + (Can(c, 'D') && (l.Status == "Cancelled" || (l.Status == "Upcoming" && !d.Ledger.Any(x => x.LeaseId == l.Id && x.Amount < 0))) ? "<button type=\"button\" class=\"ls-btn ls-btn-danger ls-push\" onclick=\"AdminLeasesJs.del('" + Ui.Js(l.Id) + "','" + Ui.Js(l.Code) + "')\">Delete</button>" : string.Empty) + "</div></div>");
            return sb.ToString();
        }

        private static decimal Held(RentData d, LeaseRec l)
        {
            return d.Ledger.Where(x => x.LeaseId == l.Id && x.Kind == "Deposit" && x.Amount > 0).Sum(x => x.Amount);
        }

        private static string DepositView(RentData d, LeaseRec l, DepositRec dep)
        {
            StringBuilder sb = new StringBuilder("<section class=\"ls-panel ls-stack ls-dep\" id=\"ls-dep\"><div class=\"ls-lhead2\"><h3>Deposit statement</h3><button type=\"button\" class=\"ls-btn ls-btn-ghost ls-btn-sm\" onclick=\"AdminLeasesJs.printDep()\">Print</button></div><p class=\"ls-mini\">" + Ui.Esc(Rent.Names(d, l) + " · " + Rent.UnitLabel(d, l.UnitId) + " · moved out " + (l.MovedOut != null ? Fmt.Day(l.MovedOut.Value) : "-") + " · finished " + (dep.Finished != null ? Fmt.Day(dep.Finished.Value) : "-")) + "</p><table class=\"ls-dt\"><tbody><tr><td>Deposit held</td><td class=\"ls-num\">" + Fmt.Money(dep.Held) + "</td></tr>");
            if (dep.Owed > 0.004m)
            {
                sb.Append("<tr><td>Unpaid balance at move-out</td><td class=\"ls-num\">-" + Fmt.Money(dep.Owed) + "</td></tr>");
            }
            foreach (DepositLine x in dep.Lines)
            {
                sb.Append("<tr><td>" + Ui.Esc(x.Label) + "</td><td class=\"ls-num\">-" + Fmt.Money(x.Amount) + "</td></tr>");
            }
            sb.Append("<tr class=\"ls-dt-t\"><td>" + (dep.Refund >= 0 ? "Refund to the resident" : "Still owed by the resident") + "</td><td class=\"ls-num\">" + Fmt.Money(Math.Abs(dep.Refund)) + "</td></tr></tbody></table>" + (dep.Refund > 0 ? "<p class=\"ls-mini\">Refunded by " + Ui.Esc(dep.Method + (dep.Reference.Length > 0 ? " " + dep.Reference : string.Empty)) + ".</p>" : string.Empty) + (dep.Note.Length > 0 ? "<p class=\"ls-mini\">" + Ui.Esc(dep.Note) + "</p>" : string.Empty) + "</section>");
            return sb.ToString();
        }

        public async Task<ApiResponse> DepositForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, LeaseRec? l) = await Find(c);
            if (l == null || l.Status != "Ended" || !Access.Can(c.U, "payments", 'A') || d.Deposits.Any(x => x.LeaseId == l.Id && x.Status == "Final"))
            {
                return await Refuse(response, c, "deposit statement", Ui.Clip(GetDataValue("id"), 60), "A deposit statement is made once, after the move-out, by staff who may record payments.");
            }
            decimal held = Held(d, l);
            decimal bal = Rent.Balance(d, l.Id);
            List<WorkOrderRec> charged = d.WorkOrders.Where(w => w.UnitId == l.UnitId && w.BillTo == "Resident" && w.Status == "Done" && w.ChargeId.Length > 0 && d.Ledger.Any(g => g.Id == w.ChargeId && g.LeaseId == l.Id)).ToList();
            StringBuilder sb = new StringBuilder("<form class=\"ls-panel ls-stack ls-dep\" id=\"ls-depform\" onsubmit=\"return AdminLeasesJs.depositGo()\" novalidate data-held=\"" + Num(held) + "\" data-bal=\"" + Num(bal) + "\"><h3>Deposit statement</h3><p class=\"ls-mini\">List what is kept from the deposit for damage or cleaning beyond normal wear. The unpaid balance" + (charged.Count > 0 ? ", including " + Ui.Count(charged.Count, "repair", "repairs") + " already charged," : string.Empty) + " is taken first. The rest is refunded.</p>");
            sb.Append("<table class=\"ls-dt\"><tbody><tr><td>Deposit held</td><td class=\"ls-num\">" + Fmt.Money(held) + "</td></tr><tr><td>Balance on the account now</td><td class=\"ls-num\">" + Fmt.Money(bal) + "</td></tr></tbody></table>");
            for (int i = 0; i < 5; i++)
            {
                sb.Append("<div class=\"ls-dline\"><input type=\"text\" data-k=\"dl" + i + "\" maxlength=\"80\" placeholder=\"" + (i == 0 ? "For example: carpet cleaning in the bedroom" : "Another deduction (optional)") + "\" aria-label=\"Deduction " + (i + 1) + "\"><input type=\"text\" data-k=\"da" + i + "\" maxlength=\"10\" inputmode=\"decimal\" placeholder=\"$\" aria-label=\"Amount " + (i + 1) + "\" oninput=\"AdminLeasesJs.depSum()\"></div>");
            }
            sb.Append(Err("dlines") + "<p class=\"ls-depsum\" id=\"ls-depsum\"></p><div class=\"ls-grid2\">" + Field("dmethod", "Refund by", "<select id=\"ls-f-dmethod\" data-k=\"dmethod\">" + string.Concat(new[] { "Check", "Bank transfer" }.Select(m => Opt(m, m, "Check"))) + "</select>") + Field("dref", "Check number or reference (optional)", Text("dref", string.Empty, 40)) + "</div>" + Field("dnote", "Note on the statement (optional)", Text("dnote", string.Empty, 200)));
            sb.Append("<div class=\"ls-acts\"><button type=\"submit\" class=\"ls-btn\">Finish the statement</button><button type=\"button\" class=\"ls-btn ls-btn-ghost\" onclick=\"AdminLeasesJs.stepClose()\">Back</button></div></form>");
            response.SetElementContents("ls-stepbox", sb.ToString());
            response.ExecuteScript("AdminLeasesJs.stepShown();AdminLeasesJs.depSum();");
            return response;
        }

        public async Task<ApiResponse> DepositSave()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, LeaseRec? found) = await Find(c);
            if (found == null || found.Status != "Ended" || !Access.Can(c.U, "payments", 'A') || d.Deposits.Any(x => x.LeaseId == found.Id && x.Status == "Final"))
            {
                return await Refuse(response, c, "deposit statement", Ui.Clip(GetDataValue("id"), 60), "This deposit statement cannot be made.");
            }
            List<DepositLine> lines = new List<DepositLine>();
            string err = string.Empty;
            for (int i = 0; i < 5; i++)
            {
                string label = Ui.Clip(GetDataValue("dl" + i), 80);
                string amtText = Ui.Clip(GetDataValue("da" + i), 10);
                if (label.Length == 0 && amtText.Length == 0)
                {
                    continue;
                }
                decimal amt = Dec(amtText);
                if (label.Length < 3 || amt <= 0 || amt > 100000)
                {
                    err = "Each deduction needs a description and an amount above zero.";
                    break;
                }
                lines.Add(new DepositLine { Label = label, Amount = amt });
            }
            string method = Ui.Clip(GetDataValue("dmethod"), 20);
            if (err.Length == 0 && method is not ("Check" or "Bank transfer"))
            {
                err = "Choose how the refund is paid.";
            }
            response.SetElementContents("ls-e-dlines", err);
            if (err.Length > 0)
            {
                return response;
            }
            DepositRec rec;
            using (await Store.Hold())
            {
                RentData f = await Rent.Read();
                LeaseRec l = f.Leases.First(x => x.Id == found.Id);
                decimal held = Held(f, l);
                decimal bal = Rent.Balance(f, l.Id);
                decimal refund = held - Math.Max(0, bal) - lines.Sum(x => x.Amount) + Math.Max(0, -bal);
                DateTime now = DateTime.Now;
                foreach (DepositLine x in lines)
                {
                    f.Ledger.Add(new LedgerRec { Id = Fmt.NewId("g-"), LeaseId = l.Id, When = now, Kind = "Deduction", Label = "Deposit deduction: " + x.Label, Amount = x.Amount, By = c.U.Id });
                }
                if (held > 0)
                {
                    f.Ledger.Add(new LedgerRec { Id = Fmt.NewId("g-"), LeaseId = l.Id, When = now.AddSeconds(1), Kind = "Deposit applied", Label = "Security deposit applied", Amount = -held, By = c.U.Id });
                }
                if (refund > 0.004m)
                {
                    f.Ledger.Add(new LedgerRec { Id = Fmt.NewId("g-"), LeaseId = l.Id, When = now.AddSeconds(2), Kind = "Refund", Label = "Deposit refund", Amount = refund, Method = method, Reference = Ui.Clip(GetDataValue("dref"), 40), By = c.U.Id });
                }
                rec = new DepositRec { Id = Fmt.NewId("d-"), LeaseId = l.Id, Held = held, Owed = Math.Max(0, bal), Lines = lines, Refund = Math.Round(refund, 2), Status = "Final", Method = refund > 0.004m ? method : string.Empty, Reference = refund > 0.004m ? Ui.Clip(GetDataValue("dref"), 40) : string.Empty, Note = Ui.Clip(GetDataValue("dnote"), 200), Created = now, Finished = now, By = c.U.Id };
                f.Deposits.Add(rec);
                await Store.SaveRows("ledger", f.Ledger);
                await Store.SaveRows("deposits", f.Deposits);
            }
            await Store.Log(c.U.Id, "deposit statement", found.Code + " · " + (rec.Refund >= 0 ? "refund " : "owed ") + Fmt.Money(Math.Abs(rec.Refund)), true);
            await Reopen(response, c, found.Id, rec.Refund >= 0 ? "Deposit statement finished. Refund " + Fmt.Money(rec.Refund) + " by " + rec.Method.ToLowerInvariant() + "." : "Deposit statement finished. The resident still owes " + Fmt.Money(-rec.Refund) + ".");
            return response;
        }

        private static decimal Money(string? v)
        {
            return Dec(v);
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
                return await Refuse(response, c, "add lease", string.Empty, "You do not have the right to add leases.");
            }
            RentData d = await Rent.Load();
            string unit = Ui.Clip(GetDataValue("cunit"), 60);
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == unit && Access.PropertyOk(c.U, x.PropertyId));
            List<string> res = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                string r = Ui.Clip(GetDataValue("cr" + i), 60);
                if (r.Length > 0 && d.Residents.Any(x => x.Id == r) && !res.Contains(r))
                {
                    res.Add(r);
                }
            }
            bool sOk = Fmt.ParseDate(GetDataValue("cstart"), out DateTime start);
            bool eOk = Fmt.ParseDate(GetDataValue("cend"), out DateTime end);
            string rentText = Ui.Clip(GetDataValue("crent"), 12);
            string depText = Ui.Clip(GetDataValue("cdep"), 12);
            decimal rent = rentText.Length == 0 && u != null ? u.Rent : Money(rentText);
            decimal dep = depText.Length == 0 && u != null ? u.Deposit : Money(depText);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["cunit"] = u == null ? "Choose a home." : string.Empty,
                ["cr0"] = Ui.Clip(GetDataValue("cr0"), 60).Length == 0 || res.Count == 0 ? "Choose the main resident." : string.Empty,
                ["cr1"] = string.Empty,
                ["cr2"] = string.Empty,
                ["cr3"] = string.Empty,
                ["cstart"] = !sOk ? "Enter the start date." : start < DateTime.Today.AddDays(-60) ? "The start date is too far in the past." : string.Empty,
                ["cend"] = !eOk ? "Enter the end date." : sOk && end <= start.AddDays(27) ? "The lease must run at least four weeks." : sOk && end > start.AddYears(3) ? "Keep the lease to three years or less." : string.Empty,
                ["crent"] = rent <= 0 || rent > 100000 ? "Enter the monthly rent." : string.Empty,
                ["cdep"] = dep < 0 || dep > 100000 ? "Enter the deposit (0 if none)." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("ls-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0) || u == null)
            {
                return response;
            }
            (string err, LeaseRec? lease, List<ResidentRec> invited) = await Leasing.Create(new NewLease { UnitId = u.Id, Start = start, End = end, Rent = rent, Deposit = dep, Pets = Ui.Clip(GetDataValue("cpets"), 120), Note = Ui.Clip(GetDataValue("cnote"), 300), ResidentIds = res, Charges = (GetDataValue("ccharge") ?? string.Empty) == "1", By = c.U.Id });
            if (err.Length > 0 || lease == null)
            {
                response.SetElementContents("ls-e-cstart", Ui.Esc(err));
                return response;
            }
            await Store.Log(c.U.Id, "create lease", lease.Code + " · " + Rent.UnitLabel(d, u.Id), true);
            await Refresh(response, c);
            RentData d2 = await Rent.Load();
            LeaseRec? fresh = d2.Leases.FirstOrDefault(x => x.Id == lease.Id);
            if (fresh != null)
            {
                response.SetElementContents("ls-mbody", FormHtml(c, d2, fresh, "Lease " + lease.Code + " is created."));
                response.ExecuteScript("AdminLeasesJs.opened('" + Ui.Js(lease.Code + " · " + Rent.UnitLabel(d2, lease.UnitId)) + "');AdminLeasesJs.clean();");
            }
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
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "edit lease", string.Empty, "You do not have the right to change leases.");
            }
            (RentData d, LeaseRec? found) = await Find(c);
            if (found == null || found.Status is "Ended" or "Cancelled")
            {
                return await Refuse(response, c, "edit lease", Ui.Clip(GetDataValue("id"), 60), found == null ? "That lease was not found." : "An ended lease cannot be changed.");
            }
            bool early = found.Status == "Upcoming";
            bool sOk = Fmt.ParseDate(GetDataValue("lstart"), out DateTime start);
            bool eOk = Fmt.ParseDate(GetDataValue("lend"), out DateTime end);
            if (!early)
            {
                start = found.Start;
                sOk = true;
            }
            decimal rent = Money(GetDataValue("lrent"));
            decimal dep = early ? Money(GetDataValue("ldep")) : found.Deposit;
            List<string> res = found.Residents.Where(id => (GetDataValue("rs_" + id) ?? string.Empty) == "1").ToList();
            string add = Ui.Clip(GetDataValue("radd"), 60);
            if (add.Length > 0 && d.Residents.Any(r => r.Id == add) && !res.Contains(add))
            {
                res.Add(add);
            }
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["lstart"] = !sOk ? "Enter the start date." : string.Empty,
                ["lend"] = !eOk ? "Enter the end date." : end <= start.AddDays(27) ? "The lease must run at least four weeks." : end > start.AddYears(3) ? "Keep the lease to three years or less." : found.Status == "Notice" && found.MoveOut != null && end < found.MoveOut.Value ? "The end cannot be before the move-out date." : string.Empty,
                ["lrent"] = rent <= 0 || rent > 100000 ? "Enter the monthly rent." : string.Empty,
                ["ldep"] = dep < 0 || dep > 100000 ? "Enter the deposit (0 if none)." : string.Empty,
                ["res"] = res.Count == 0 ? "A lease needs at least one resident." : string.Empty
            };
            if (e.Values.All(v => v.Length == 0) && Rent.Overlaps(d, found.UnitId, start, found.MoveOut ?? end, found.Id))
            {
                e["lend"] = "Another lease on this home covers some of these dates.";
            }
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("ls-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<LeaseRec> all = await Store.Rows<LeaseRec>("leases");
                LeaseRec? l = all.FirstOrDefault(x => x.Id == found.Id);
                if (l != null)
                {
                    l.Start = start.Date;
                    l.End = end.Date;
                    l.Rent = rent;
                    l.Deposit = dep;
                    l.Residents = res;
                    l.Pets = Ui.Clip(GetDataValue("lpets"), 120);
                    l.Note = Ui.Clip(GetDataValue("lnote"), 300);
                    await Store.SaveRows("leases", all);
                }
            }
            await Store.Log(c.U.Id, "edit lease", found.Code, true);
            await Reopen(response, c, found.Id, "Saved.");
            return response;
        }

        private async Task Reopen(ApiResponse response, Ctx c, string id, string note)
        {
            await Refresh(response, c);
            RentData d = await Rent.Load();
            LeaseRec? l = d.Leases.FirstOrDefault(x => x.Id == id);
            if (l != null)
            {
                response.SetElementContents("ls-mbody", FormHtml(c, d, l, note));
                response.ExecuteScript("AdminLeasesJs.opened('" + Ui.Js(l.Code + " · " + Rent.UnitLabel(d, l.UnitId)) + "');AdminLeasesJs.clean();");
            }
        }

        public async Task<ApiResponse> StepForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, LeaseRec? l) = await Find(c);
            string act = Ui.Clip(GetDataValue("act"), 12);
            if (l == null || !Can(c, 'E'))
            {
                return await Refuse(response, c, "lease step", act, l == null ? "That lease was not found." : "You do not have the right to change leases.");
            }
            DateTime today = DateTime.Today;
            string inner = act switch
            {
                "movein" => "<h3>Move in</h3><p class=\"ls-mini\">The unit becomes occupied. Hand over the keys and the portal codes.</p>" + Field("adate", "Move-in date", Text("adate", Fmt.IsoDay(l.Start > today ? today : l.Start), 10, "date")),
                "notice" => "<h3>Record notice to move out</h3><p class=\"ls-mini\">The unit is offered on the website from the day after move-out, if it is listed.</p>" + Field("adate", "Move-out date", Text("adate", Fmt.IsoDay(l.End < today.AddDays(30) ? today.AddDays(30) : l.End), 10, "date")),
                "moveout" => "<h3>Move out</h3><p class=\"ls-mini\">The lease ends and the unit is set to make-ready. A make-ready list and a move-out inspection are started for it.</p>" + Field("adate", "Moved out on", Text("adate", Fmt.IsoDay(l.MoveOut != null && l.MoveOut.Value < today ? l.MoveOut.Value : today), 10, "date")),
                "renew" => "<h3>Renew the lease</h3><p class=\"ls-mini\">A new lease starts the day after this one ends, with the same residents. It becomes active by itself on its start date.</p><div class=\"ls-grid2\">" + Field("aend", "New end date", Text("aend", Fmt.IsoDay(l.End.AddYears(1)), 10, "date")) + Field("arent", "New rent a month ($)", Text("arent", Num(Math.Round(l.Rent * 1.03m)), 10, "text", " inputmode=\"decimal\"")) + "</div>",
                "unnotice" => "<h3>Cancel the notice</h3><p class=\"ls-mini\">The resident stays. The lease is active again and the unit comes off the website.</p>",
                "cancel" => "<h3>Cancel this lease</h3><p class=\"ls-mini\">Use this when the move-in will not happen. Charges on the lease are removed. A lease with payments must be settled first.</p>",
                _ => string.Empty
            };
            if (inner.Length == 0)
            {
                return response;
            }
            response.SetElementContents("ls-stepbox", "<div class=\"ls-panel ls-stack\">" + inner + "<div class=\"ls-acts\"><button type=\"button\" class=\"ls-btn" + (act is "cancel" ? " ls-btn-danger" : string.Empty) + "\" onclick=\"AdminLeasesJs.stepGo('" + act + "')\">" + (act switch { "movein" => "Move in", "notice" => "Record notice", "moveout" => "Move out", "renew" => "Create renewal", "unnotice" => "Cancel notice", _ => "Cancel lease" }) + "</button><button type=\"button\" class=\"ls-btn ls-btn-ghost\" onclick=\"AdminLeasesJs.stepClose()\">Back</button></div></div>");
            response.ExecuteScript("AdminLeasesJs.stepShown();");
            return response;
        }

        public async Task<ApiResponse> Step()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, LeaseRec? found) = await Find(c);
            string act = Ui.Clip(GetDataValue("act"), 12);
            if (found == null || !Can(c, 'E'))
            {
                return await Refuse(response, c, "lease step", act, found == null ? "That lease was not found." : "You do not have the right to change leases.");
            }
            bool dOk = Fmt.ParseDate(GetDataValue("adate"), out DateTime day);
            DateTime today = DateTime.Today;
            string err = act switch
            {
                "movein" => found.Status != "Upcoming" || found.RenewalOf.Length > 0 ? "Only an upcoming lease can be moved in." : !dOk ? "Enter the move-in date." : day > today ? "The move-in date cannot be in the future." : day < found.Start.AddDays(-14) ? "That is more than two weeks before the lease starts. Change the start date first." : d.Leases.Any(x => x.Id != found.Id && x.UnitId == found.UnitId && Rent.Live.Contains(x.Status)) ? "The current resident has not moved out yet." : string.Empty,
                "notice" => found.Status != "Active" ? "Only an active lease can take notice." : !dOk ? "Enter the move-out date." : day < today ? "The move-out date cannot be in the past." : string.Empty,
                "unnotice" => found.Status != "Notice" ? "This lease has no notice to cancel." : d.Leases.Any(x => x.UnitId == found.UnitId && x.Status == "Upcoming" && x.RenewalOf.Length == 0) ? "A new lease is already signed for this home. Cancel it first." : string.Empty,
                "moveout" => !(found.Status == "Notice" || (found.Status == "Active" && Rent.Holdover(found))) ? "Record the notice first." : !dOk ? "Enter the move-out date." : day > today ? "The move-out date cannot be in the future." : day < (found.MovedIn ?? found.Start) ? "That is before the move-in date." : string.Empty,
                "renew" => found.Status != "Active" || found.RenewedBy.Length > 0 ? "This lease cannot be renewed." : string.Empty,
                "cancel" => found.Status != "Upcoming" ? "Only an upcoming lease can be cancelled." : d.Ledger.Any(x => x.LeaseId == found.Id && x.Amount < 0) ? "Payments were taken on this lease. Settle them before cancelling." : string.Empty,
                _ => "Unknown step."
            };
            if (err.Length > 0)
            {
                response.SetElementContents("ls-e-adate", Ui.Esc(err));
                if (act is "unnotice" or "cancel" or "renew")
                {
                    response.ExecuteScript("AdminLeasesJs.toast('" + Ui.Js(err) + "');");
                }
                return response;
            }
            string note;
            if (act == "renew")
            {
                bool eOk = Fmt.ParseDate(GetDataValue("aend"), out DateTime newEnd);
                decimal rent = Money(GetDataValue("arent"));
                DateTime start = found.End.AddDays(1);
                string e2 = !eOk ? "Enter the new end date." : newEnd <= start.AddDays(27) ? "The renewal must run at least four weeks." : newEnd > start.AddYears(3) ? "Keep it to three years or less." : string.Empty;
                response.SetElementContents("ls-e-aend", e2);
                response.SetElementContents("ls-e-arent", rent <= 0 || rent > 100000 ? "Enter the new monthly rent." : string.Empty);
                if (e2.Length > 0 || rent <= 0 || rent > 100000)
                {
                    return response;
                }
                (string cerr, LeaseRec? fresh, List<ResidentRec> _) = await Leasing.Create(new NewLease { UnitId = found.UnitId, Start = start, End = newEnd, Rent = rent, Deposit = found.Deposit, Pets = found.Pets, ResidentIds = found.Residents, RenewalOf = found.Id, Charges = false, By = c.U.Id });
                if (cerr.Length > 0 || fresh == null)
                {
                    response.SetElementContents("ls-e-aend", Ui.Esc(cerr));
                    return response;
                }
                await Store.Log(c.U.Id, "renew lease", found.Code + " to " + fresh.Code, true);
                note = "Renewal " + fresh.Code + " created: " + Fmt.Day(fresh.Start) + " to " + Fmt.Day(fresh.End) + " at " + Fmt.Whole(fresh.Rent) + ".";
            }
            else
            {
                using (await Store.Hold())
                {
                    List<LeaseRec> leases = await Store.Rows<LeaseRec>("leases");
                    List<UnitRec> units = await Store.Rows<UnitRec>("units");
                    LeaseRec? l = leases.FirstOrDefault(x => x.Id == found.Id);
                    UnitRec? u = units.FirstOrDefault(x => x.Id == found.UnitId);
                    if (l == null)
                    {
                        return await Refuse(response, c, "lease step", act, "That lease was not found.");
                    }
                    switch (act)
                    {
                        case "movein":
                            l.Status = "Active";
                            l.MovedIn = day;
                            if (u != null)
                            {
                                u.Status = "Occupied";
                                u.StatusSince = DateTime.Now;
                            }
                            break;
                        case "notice":
                            l.Status = "Notice";
                            l.NoticeOn = today;
                            l.MoveOut = day;
                            if (u != null)
                            {
                                u.Status = "Notice";
                                u.StatusSince = DateTime.Now;
                                u.Available = day.AddDays(1);
                            }
                            break;
                        case "unnotice":
                            l.Status = "Active";
                            l.NoticeOn = null;
                            l.MoveOut = null;
                            if (u != null)
                            {
                                u.Status = "Occupied";
                                u.StatusSince = DateTime.Now;
                            }
                            break;
                        case "moveout":
                            l.Status = "Ended";
                            l.MovedOut = day;
                            l.MoveOut ??= day;
                            if (u != null)
                            {
                                u.Status = "Make-ready";
                                u.StatusSince = DateTime.Now;
                                u.Available = null;
                            }
                            break;
                        case "cancel":
                            l.Status = "Cancelled";
                            List<LedgerRec> ledger = await Store.Rows<LedgerRec>("ledger");
                            ledger.RemoveAll(x => x.LeaseId == l.Id);
                            await Store.SaveRows("ledger", ledger);
                            if (l.RenewalOf.Length > 0)
                            {
                                LeaseRec? old = leases.FirstOrDefault(x => x.Id == l.RenewalOf);
                                if (old != null)
                                {
                                    old.RenewedBy = string.Empty;
                                }
                            }
                            break;
                    }
                    await Store.SaveRows("leases", leases);
                    await Store.SaveRows("units", units);
                    if (act == "moveout" && u != null)
                    {
                        List<TurnRec> turns = await Store.Rows<TurnRec>("turns");
                        if (!turns.Any(t => t.UnitId == u.Id && t.Status == "Open"))
                        {
                            turns.Add(Maint.NewTurn(u, l.Id, day, c.U.Id));
                            await Store.SaveRows("turns", turns);
                            List<InspectionRec> ins = await Store.Rows<InspectionRec>("inspections");
                            if (!ins.Any(i => i.LeaseId == l.Id && i.Kind == "Move-out" && i.Status != "Cancelled"))
                            {
                                ins.Add(Maint.NewInspection(u, l.Id, "Move-out", day.Date.AddHours(DateTime.Now.Hour >= 16 ? 34 : Math.Max(DateTime.Now.Hour + 1, 9)), c.U.Id, c.U.Id));
                                await Store.SaveRows("inspections", ins);
                            }
                        }
                    }
                }
                await Store.Log(c.U.Id, act switch { "movein" => "move in", "notice" => "record notice", "unnotice" => "cancel notice", "moveout" => "move out", _ => "cancel lease" }, found.Code + " · " + Rent.UnitLabel(d, found.UnitId), true);
                note = act switch { "movein" => "Moved in. The unit is occupied.", "notice" => "Notice recorded. Move-out on " + Fmt.Day(day) + ".", "unnotice" => "Notice cancelled. The lease is active.", "moveout" => "Moved out. The unit is set to make-ready, with a make-ready list and a move-out inspection.", _ => "Lease cancelled." };
            }
            await Reopen(response, c, found.Id, note);
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
                return await Refuse(response, c, "delete lease", string.Empty, "You do not have the right to delete leases.");
            }
            (RentData d, LeaseRec? l) = await Find(c);
            if (l == null || !(l.Status == "Cancelled" || (l.Status == "Upcoming" && !d.Ledger.Any(x => x.LeaseId == l.Id && x.Amount < 0))))
            {
                return await Refuse(response, c, "delete lease", Ui.Clip(GetDataValue("id"), 60), l == null ? "That lease was not found." : "Only an upcoming or cancelled lease without payments can be deleted.");
            }
            using (await Store.Hold())
            {
                List<LeaseRec> leases = await Store.Rows<LeaseRec>("leases");
                leases.RemoveAll(x => x.Id == l.Id);
                foreach (LeaseRec x in leases.Where(x => x.RenewedBy == l.Id))
                {
                    x.RenewedBy = string.Empty;
                }
                await Store.SaveRows("leases", leases);
                List<LedgerRec> ledger = await Store.Rows<LedgerRec>("ledger");
                ledger.RemoveAll(x => x.LeaseId == l.Id);
                await Store.SaveRows("ledger", ledger);
                List<ApplicationRec> apps = await Store.Rows<ApplicationRec>("applications");
                foreach (ApplicationRec a in apps.Where(a => a.LeaseId == l.Id))
                {
                    a.LeaseId = string.Empty;
                    a.Status = "Approved";
                }
                await Store.SaveRows("applications", apps);
            }
            await Store.Log(c.U.Id, "delete lease", l.Code, true);
            await Refresh(response, c);
            response.ExecuteScript("AdminLeasesJs.deleted();");
            return response;
        }
    }
}
