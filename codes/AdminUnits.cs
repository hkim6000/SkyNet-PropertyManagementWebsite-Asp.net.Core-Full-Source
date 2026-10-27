using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminUnits : WebPage
    {
        private const string AreaKey = "units";
        private const int Size = 12;
        private const int Pics = 6;
        private static readonly string[] Manual = { "Vacant", "Make-ready", "Offline" };
        private static readonly (string Key, string Name)[] Views = { ("all", "All statuses"), ("rent", "To rent on the website"), ("Vacant", "Vacant"), ("Notice", "Notice given"), ("Make-ready", "Make-ready"), ("Occupied", "Occupied"), ("Offline", "Offline"), ("unlisted", "Not listed") };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Units | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminUnitsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminUnitsJs.denied();");
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
            response.SetElementContents("uu-brand", Ui.Esc(site.Brand));
            response.SetElementContents("uu-menu", await Auth.Menu("uu", "AdminUnits", c));
            response.SetElementContents("uu-who", Auth.Who("uu", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminUnitsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"uu-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"uu-btn uu-btn-ghost uu-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminUnitsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"uu-btn uu-btn-ghost uu-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminUnitsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"uu-pill uu-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"uu-err\" id=\"uu-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"uu-field\" for=\"uu-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"uu-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static string Shot(MediaItem m)
        {
            return m.Kind == "video" ? "<video src=\"" + Ui.Esc(m.Path) + "#t=0.1\" muted preload=\"metadata\" playsinline></video><i class=\"uu-pkplay\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"14\" height=\"14\" fill=\"currentColor\"><path d=\"M8 5v14l11-7z\"/></svg></i>" : "<img src=\"" + Ui.Esc(m.Thumb.Length > 0 ? m.Thumb : m.Path) + "\" alt=\"\" loading=\"lazy\">";
        }

        private static string PickItem(MediaItem m)
        {
            string thumb = m.Thumb.Length > 0 ? m.Thumb : m.Path;
            return "<button type=\"button\" class=\"uu-pki\" data-src=\"" + Ui.Esc(m.Path) + "\" data-thumb=\"" + Ui.Esc(thumb) + "\" data-kind=\"" + Ui.Esc(m.Kind) + "\" onclick=\"AdminUnitsJs.pkPick(this)\" title=\"" + Ui.Esc(m.Name) + "\">" + Shot(m) + "<span>" + Ui.Esc(m.Name) + "</span></button>";
        }

        private static string PickKind(string? v)
        {
            return v is "video" or "photo" ? v : v == "all" ? string.Empty : "photo";
        }

        private static string PickGrid(List<MediaItem> media, string term, int page, string kind)
        {
            List<MediaItem> rows = media.Where(m => (kind.Length == 0 ? m.Kind is "photo" or "video" : m.Kind == kind) && (term == string.Empty || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term) || Ui.Has(m.Caption, term))).OrderByDescending(m => m.Added).ToList();
            if (rows.Count == 0)
            {
                return "<p class=\"uu-none\">Nothing matches. Upload " + (kind == "video" ? "an MP4 video" : kind == "photo" ? "a photo" : "a photo or an MP4 video") + " above, or add it in the Media library.</p>";
            }
            int pages = (rows.Count + PickSize - 1) / PickSize;
            page = Math.Min(Math.Max(page, 1), pages);
            string pager = pages <= 1 ? string.Empty : "<div class=\"uu-pkpager\"><span>Page " + page + " of " + pages + " &middot; " + (kind == "video" ? Ui.Count(rows.Count, "video", "videos") : kind == "photo" ? Ui.Count(rows.Count, "photo", "photos") : Ui.Count(rows.Count, "item", "items")) + "</span><div><button type=\"button\" class=\"uu-btn uu-btn-ghost uu-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminUnitsJs.pkList(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"uu-btn uu-btn-ghost uu-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminUnitsJs.pkList(" + (page + 1) + ")\">Next</button></div></div>";
            return string.Concat(rows.Skip((page - 1) * PickSize).Take(PickSize).Select(PickItem)) + pager;
        }

        private const int PickSize = 12;

        private static int PickPage(string? v)
        {
            return int.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
        }

        public async Task<ApiResponse> MediaPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("uu-pkgrid", PickGrid(media, Ui.Clip(GetDataValue("pterm"), 40), PickPage(GetDataValue("ppg")), PickKind(GetDataValue("pkind"))));
            return response;
        }

        public async Task<ApiResponse> MediaUp()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E') && !Can(c, 'A'))
            {
                return await Refuse(response, c, "upload media", AreaKey, "You can view this area but not change it.");
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = await Media.Received();
            if (files.Count == 0)
            {
                response.ExecuteScript("AdminUnitsJs.pkUploaded(false,'Choose a file first.');");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files.Take(1).ToList(), site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("uu-pkgrid", PickGrid(media, string.Empty, 1, PickKind(GetDataValue("pkind"))));
            response.ExecuteScript("AdminUnitsJs.pkUploaded(" + (added > 0 ? "true" : "false") + ",'" + Ui.Js(((added > 0 ? "Added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()) + "');");
            return response;
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
            string prop = Ui.Clip(GetDataValue("prop"), 60);
            prop = d.Properties.Any(x => x.Id == prop && Access.PropertyOk(c.U, x.Id)) ? prop : string.Empty;
            string view = Ui.Clip(GetDataValue("view"), 20);
            view = Views.Any(v => v.Key == view) ? view : "all";
            List<PropertyRec> mine = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).ToList();
            response.SetElementContents("uu-main", "<div class=\"uu-ph\"><div><h1>Units</h1><p>Every home you rent out. The status follows the lease: occupied while a lease runs, notice once the resident gives notice. Set make-ready or offline yourself.</p></div>" + (Can(c, 'A') && mine.Count > 0 ? "<button type=\"button\" class=\"uu-btn\" onclick=\"AdminUnitsJs.add()\">Add a unit</button>" : string.Empty) + "</div><div class=\"uu-tools\"><input type=\"search\" id=\"uu-q\" placeholder=\"Search by unit or property\" maxlength=\"40\" oninput=\"AdminUnitsJs.type()\" aria-label=\"Search units\"><select id=\"uu-fp\" onchange=\"AdminUnitsJs.list(true)\" aria-label=\"Property\">" + Opt(string.Empty, "All properties", prop) + string.Concat(mine.Select(p => Opt(p.Id, p.Name, prop))) + "</select><select id=\"uu-fs\" onchange=\"AdminUnitsJs.list(true)\" aria-label=\"Status\">" + string.Concat(Views.Select(v => Opt(v.Key, v.Name, view))) + "</select></div><div id=\"uu-list\">" + ListHtml(c, d, string.Empty, prop, view, 1) + "</div>");
            return response;
        }

        private static string ListHtml(Ctx c, RentData d, string term, string prop, string view, int page)
        {
            IEnumerable<UnitRec> q = d.Units.Where(u => Access.PropertyOk(c.U, u.PropertyId));
            if (prop.Length > 0)
            {
                q = q.Where(u => u.PropertyId == prop);
            }
            q = view switch
            {
                "rent" => q.Where(Rent.Offered),
                "unlisted" => q.Where(u => !u.Listed),
                "all" => q,
                _ => q.Where(u => u.Status == view)
            };
            if (term.Length > 0)
            {
                q = q.Where(u => Ui.Has(u.Number, term) || Ui.Has(Rent.PropertyName(d, u.PropertyId), term) || Ui.Has(Rent.CurrentLease(d, u.Id) is LeaseRec l ? Rent.Names(d, l) : string.Empty, term));
            }
            List<UnitRec> rows = q.OrderBy(u => d.Properties.FindIndex(p => p.Id == u.PropertyId)).ThenBy(u => u.Number, StringComparer.OrdinalIgnoreCase).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"uu-panel uu-empty\">No units match.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"uu-wrapt\"><table class=\"uu-table\"><thead><tr><th>Unit</th><th class=\"uu-num\">Rent</th><th>Status</th><th>Ready</th><th>Resident</th><th>Website</th></tr></thead><tbody>");
            foreach (UnitRec u in rows.Skip((page - 1) * Size).Take(Size))
            {
                LeaseRec? l = Rent.CurrentLease(d, u.Id);
                LeaseRec? next = d.Leases.FirstOrDefault(x => x.UnitId == u.Id && x.Status == "Upcoming");
                string ready = u.Status is "Occupied" ? "-" : next != null ? "Leased from " + Fmt.Short(next.Start) : Rent.MoveInDate(u) <= DateTime.Today ? "Now" : Fmt.Short(Rent.MoveInDate(u));
                sb.Append("<tr class=\"uu-row\" onclick=\"AdminUnitsJs.edit('" + Ui.Js(u.Id) + "')\"><td class=\"uu-first\"><b>" + Ui.Esc(Rent.PropertyName(d, u.PropertyId) + " · " + u.Number) + "</b><small>" + Ui.Esc(Fmt.Beds(u.Beds) + ", " + Fmt.Baths(u.Baths) + (u.Sqft > 0 ? ", " + u.Sqft.ToString("N0", Fmt.Us) + " sq ft" : string.Empty)) + "</small></td>");
                sb.Append("<td class=\"uu-num\" data-label=\"Rent\">" + Fmt.Whole(u.Rent) + "</td><td data-label=\"Status\">" + Pill(u.Status) + "<small class=\"uu-mini\"> " + Ui.Esc(Since(u.StatusSince)) + "</small></td><td data-label=\"Ready\">" + Ui.Esc(ready) + "</td><td data-label=\"Resident\">" + Ui.Esc(l != null ? Rent.Names(d, l) : next != null ? Rent.Names(d, next) + " (upcoming)" : "-") + "</td><td data-label=\"Website\">" + (Rent.Offered(u) && next == null ? "<span class=\"uu-pill uu-pill-go\">Listed</span>" : "<span class=\"uu-mini\">" + (u.Listed ? "Not offered" : "Not listed") + "</span>") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "unit", "units"));
            return sb.ToString();
        }

        private static string Since(DateTime d)
        {
            int days = (DateTime.Today - d.Date).Days;
            return days <= 0 ? "since today" : days == 1 ? "1 day" : days + " days";
        }

        private async Task<string> ListNow(Ctx c)
        {
            RentData d = await Rent.Load();
            return ListHtml(c, d, Term(GetDataValue("term")), Ui.Clip(GetDataValue("prop"), 60), Ui.Clip(GetDataValue("view"), 20), PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("uu-list", await ListNow(c));
            return response;
        }

        private static string PicBox(string key, string src, List<MediaItem> media, bool edit, string ph, bool video)
        {
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            return "<div class=\"uu-pic\" id=\"uu-f-" + key + "\" data-k=\"" + key + "\" data-src=\"" + Ui.Esc(m == null ? string.Empty : src) + "\" data-ph=\"" + Ui.Esc(ph) + "\"" + (video ? " data-any=\"1\"" : string.Empty) + "><button type=\"button\" class=\"uu-picb\"" + (edit ? " onclick=\"AdminUnitsJs.pic(this)\"" : " disabled") + " aria-label=\"" + Ui.Esc(ph) + "\">" + (m != null ? Shot(m) : "<span>" + Ui.Esc(ph) + "</span>") + "</button>" + (edit ? "<button type=\"button\" class=\"uu-picx\"" + (m != null ? string.Empty : " hidden") + " onclick=\"AdminUnitsJs.picClear(this)\">Remove</button>" : string.Empty) + "</div>";
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
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && (u == null || !Access.PropertyOk(c.U, u.PropertyId)))
            {
                return await Refuse(response, c, "open unit", id, "That unit was not found.");
            }
            if (u == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add unit", string.Empty, "You can view units but not add them.");
            }
            response.SetElementContents("uu-mbody", FormHtml(c, d, u, await Media.Load(), Ui.Clip(GetDataValue("prop"), 60)));
            response.ExecuteScript("AdminUnitsJs.opened('" + Ui.Js(u == null ? "New unit" : Rent.UnitLabel(d, u.Id)) + "');");
            return response;
        }

        private static string FormHtml(Ctx c, RentData d, UnitRec? u, List<MediaItem> media, string prop)
        {
            bool isNew = u == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            UnitRec r = u ?? new UnitRec { PropertyId = prop, Status = "Vacant", Listed = false, Available = DateTime.Today };
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            LeaseRec? live = isNew ? null : Rent.CurrentLease(d, r.Id);
            LeaseRec? next = isNew ? null : d.Leases.FirstOrDefault(x => x.UnitId == r.Id && x.Status == "Upcoming");
            StringBuilder sb = new StringBuilder("<form class=\"uu-stack\" id=\"uu-form\" onsubmit=\"return AdminUnitsJs.save()\" novalidate><input type=\"hidden\" id=\"uu-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            if (!isNew)
            {
                sb.Append("<div class=\"uu-bar\"><span>" + Pill(r.Status) + " " + Ui.Esc(live != null ? Rent.Names(d, live) + " · lease " + live.Code + " to " + Fmt.Day(live.MoveOut ?? live.End) : "No current lease") + (next != null ? " &middot; next lease " + Ui.Esc(next.Code) + " from " + Ui.Esc(Fmt.Day(next.Start)) : string.Empty) + "</span><span class=\"uu-acts-row\">" + (Access.Can(c.U, "leases", 'V') ? "<a class=\"uu-btn uu-btn-ghost uu-btn-sm\" href=\"AdminLeases?u=" + Ui.Esc(r.Id) + "\">Leases</a>" : string.Empty) + (Rent.Offered(r) && next == null && d.Properties.Any(p => p.Id == r.PropertyId && p.Show) ? "<a class=\"uu-btn uu-btn-ghost uu-btn-sm\" href=\"Unit?u=" + Ui.Esc(r.Id) + "\" target=\"_blank\" rel=\"noopener\">View on website</a>" : string.Empty) + "</span></div>");
            }
            List<PropertyRec> props = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).ToList();
            sb.Append("<div class=\"uu-grid3\">" + Field("uprop", "Property", "<select id=\"uu-f-uprop\" data-k=\"uprop\"" + (edit && live == null && next == null ? string.Empty : " disabled") + ">" + (isNew && prop.Length == 0 ? Opt(string.Empty, "Choose a property", string.Empty) : string.Empty) + string.Concat(props.Select(p => Opt(p.Id, p.Name, r.PropertyId))) + "</select>") + Field("number", "Unit number", Text("number", r.Number, 12, "text", ro)) + Field("floor", "Floor", Text("floor", r.Floor.ToString(CultureInfo.InvariantCulture), 3, "text", ro + " inputmode=\"numeric\"")) + "</div>");
            sb.Append("<div class=\"uu-grid3\">" + Field("beds", "Bedrooms", "<select id=\"uu-f-beds\" data-k=\"beds\"" + dis + ">" + string.Concat(Enumerable.Range(0, 7).Select(n => Opt(n.ToString(CultureInfo.InvariantCulture), Fmt.Beds(n), r.Beds.ToString(CultureInfo.InvariantCulture)))) + "</select>") + Field("baths", "Bathrooms", "<select id=\"uu-f-baths\" data-k=\"baths\"" + dis + ">" + string.Concat(new[] { 1m, 1.5m, 2m, 2.5m, 3m, 3.5m, 4m }.Select(n => Opt(Num(n), Fmt.Baths(n), Num(r.Baths)))) + "</select>") + Field("sqft", "Size (sq ft)", Text("sqft", r.Sqft > 0 ? r.Sqft.ToString(CultureInfo.InvariantCulture) : string.Empty, 6, "text", ro + " inputmode=\"numeric\"")) + "</div>");
            sb.Append("<div class=\"uu-grid3\">" + Field("rent", "Rent a month ($)", Text("rent", r.Rent > 0 ? Num(r.Rent) : string.Empty, 10, "text", ro + " inputmode=\"decimal\"")) + Field("deposit", "Deposit ($)", Text("deposit", r.Deposit > 0 ? Num(r.Deposit) : string.Empty, 10, "text", ro + " inputmode=\"decimal\"")) + Field("avail", "Ready from", Text("avail", r.Available == null ? string.Empty : Fmt.IsoDay(r.Available.Value), 10, "date", ro), "Leave empty if ready now.") + "</div>");
            if (live != null)
            {
                sb.Append("<div class=\"uu-field\"><span>Status</span><p class=\"uu-mini\">" + Pill(r.Status) + " Set by lease " + Ui.Esc(live.Code) + ". It changes when the resident moves out.</p><input type=\"hidden\" id=\"uu-f-status\" data-k=\"status\" value=\"" + Ui.Esc(r.Status) + "\"></div>");
            }
            else
            {
                sb.Append(Field("status", "Status", "<select id=\"uu-f-status\" data-k=\"status\"" + dis + ">" + string.Concat(Manual.Select(s => Opt(s, s, r.Status))) + "</select>", "Make-ready while you clean or repair it. Offline keeps it off the market."));
            }
            sb.Append("<div class=\"uu-checks\"><label class=\"uu-check\"><input type=\"checkbox\" id=\"uu-f-pets\" data-k=\"pets\"" + (r.PetsOk ? " checked" : string.Empty) + dis + ">Pets allowed</label><label class=\"uu-check\"><input type=\"checkbox\" id=\"uu-f-listed\" data-k=\"listed\"" + (r.Listed ? " checked" : string.Empty) + dis + ">Show on the website when it is free</label></div>");
            sb.Append("<label class=\"uu-field\" for=\"uu-f-feat\"><span>Features (one per line)</span><textarea id=\"uu-f-feat\" data-k=\"feat\" rows=\"4\" maxlength=\"800\"" + ro + ">" + Ui.Esc(string.Join("\n", r.Features)) + "</textarea>" + Err("feat") + "</label>");
            sb.Append("<div class=\"uu-field\"><span>Photos and videos (the first is the main photo; the others can be videos)</span><div class=\"uu-pics\">");
            for (int i = 0; i < Pics; i++)
            {
                sb.Append(PicBox("ph" + i, i < r.Photos.Count ? r.Photos[i] : string.Empty, media, edit, i == 0 ? "Main photo" : "Add", i > 0));
            }
            sb.Append("</div>" + Err("photos") + "</div>");
            sb.Append("<div class=\"uu-field\"><span>Floor plan</span>" + PicBox("plan", r.Plan, media, edit, "Choose a plan", false) + Err("plan") + "</div>");
            sb.Append("<label class=\"uu-field\" for=\"uu-f-note\"><span>Staff note (not on the website)</span><textarea id=\"uu-f-note\" data-k=\"note\" rows=\"2\" maxlength=\"500\"" + ro + ">" + Ui.Esc(r.Note) + "</textarea>" + Err("note") + "</label>");
            sb.Append("<div class=\"uu-acts\">" + (edit ? "<button type=\"submit\" class=\"uu-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"uu-btn uu-btn-ghost\" onclick=\"AdminUnitsJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Can(c, 'D') ? "<button type=\"button\" class=\"uu-btn uu-btn-danger uu-push\" onclick=\"AdminUnitsJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(Rent.UnitLabel(d, r.Id)) + "')\">Delete</button>" : string.Empty) + "</div></form>");
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
                return await Refuse(response, c, isNew ? "add unit" : "edit unit", id, "You do not have the right to " + (isNew ? "add" : "change") + " units.");
            }
            RentData d = await Rent.Load();
            UnitRec? old = d.Units.FirstOrDefault(x => x.Id == id);
            if (!isNew && (old == null || !Access.PropertyOk(c.U, old.PropertyId)))
            {
                return await Refuse(response, c, "edit unit", id, "That unit was not found.");
            }
            LeaseRec? live = old == null ? null : Rent.CurrentLease(d, old.Id);
            bool locked = old != null && (live != null || d.Leases.Any(x => x.UnitId == old.Id && x.Status == "Upcoming"));
            string prop = locked && old != null ? old.PropertyId : Ui.Clip(GetDataValue("uprop"), 60);
            string number = Ui.Clip(GetDataValue("number"), 12);
            int floor = Int(GetDataValue("floor"), -99);
            int beds = Math.Clamp(Int(GetDataValue("beds"), 1), 0, 6);
            decimal baths = Dec(GetDataValue("baths"));
            string sqftText = Ui.Clip(GetDataValue("sqft"), 6);
            int sqft = sqftText.Length == 0 ? 0 : Int(sqftText, -1);
            decimal rent = Dec(GetDataValue("rent"));
            decimal deposit = Dec(GetDataValue("deposit"));
            string availText = Ui.Clip(GetDataValue("avail"), 10);
            bool availOk = Fmt.ParseDate(availText, out DateTime avail);
            string status = Ui.Clip(GetDataValue("status"), 20);
            bool pets = (GetDataValue("pets") ?? string.Empty) == "1";
            bool listed = (GetDataValue("listed") ?? string.Empty) == "1";
            List<string> feat = (GetDataValue("feat") ?? string.Empty).Replace("\r", string.Empty).Split('\n').Select(x => Ui.Clip(x, 60)).Where(x => x.Length > 0).Distinct().Take(16).ToList();
            string plan = Ui.Clip(GetDataValue("plan"), 200);
            string note = Ui.Clip((GetDataValue("note") ?? string.Empty).Replace("\r", string.Empty), 500);
            List<MediaItem> library = await Media.Load();
            List<string> photos = new List<string>();
            bool badPic = false;
            for (int i = 0; i < Pics; i++)
            {
                string g = Ui.Clip(GetDataValue("ph" + i), 200);
                if (g.Length == 0)
                {
                    continue;
                }
                if (!library.Any(m => m.Path == g && m.Kind is "photo" or "video"))
                {
                    badPic = true;
                }
                else if (!photos.Contains(g))
                {
                    photos.Add(g);
                }
            }
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["uprop"] = !d.Properties.Any(p => p.Id == prop && Access.PropertyOk(c.U, p.Id)) ? "Choose a property." : string.Empty,
                ["number"] = number.Length < 1 ? "Enter the unit number." : d.Units.Any(x => x.Id != id && x.PropertyId == prop && x.Number.Equals(number, StringComparison.OrdinalIgnoreCase)) ? "This property already has a unit with that number." : string.Empty,
                ["floor"] = floor < -3 || floor > 200 ? "Enter the floor number (1 for the ground floor)." : string.Empty,
                ["beds"] = string.Empty,
                ["baths"] = baths < 0.5m || baths > 10 ? "Choose the bathrooms." : string.Empty,
                ["sqft"] = sqft < 0 || sqft > 20000 ? "Enter the size in square feet, or leave it empty." : string.Empty,
                ["rent"] = rent <= 0 || rent > 100000 ? "Enter the monthly rent." : string.Empty,
                ["deposit"] = deposit < 0 || deposit > 100000 ? "Enter the deposit (0 if none)." : string.Empty,
                ["avail"] = availText.Length > 0 && !availOk ? "Enter a valid date, or leave it empty." : string.Empty,
                ["status"] = live == null && !Manual.Contains(status) ? "Choose a status." : string.Empty,
                ["feat"] = string.Empty,
                ["photos"] = badPic ? "One photo or video is no longer in the media library. Choose it again." : photos.Count > 0 && !library.Any(m => m.Path == photos[0] && m.Kind == "photo") ? "The first one must be a photo. It is the picture on unit cards." : listed && photos.Count == 0 ? "Add at least one photo before showing the unit on the website." : string.Empty,
                ["plan"] = plan.Length > 0 && !library.Any(m => m.Path == plan && m.Kind == "photo") ? "That plan is not in the media library. Choose it again." : string.Empty,
                ["note"] = string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("uu-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            string label;
            using (await Store.Hold())
            {
                List<UnitRec> all = await Store.Rows<UnitRec>("units");
                UnitRec? u = all.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    u = new UnitRec { Id = Fmt.NewId("u-"), StatusSince = DateTime.Now };
                    all.Add(u);
                }
                if (u == null)
                {
                    return await Refuse(response, c, "edit unit", id, "That unit was not found.");
                }
                u.PropertyId = prop;
                u.Number = number;
                u.Floor = floor;
                u.Beds = beds;
                u.Baths = baths;
                u.Sqft = sqft;
                u.Rent = rent;
                u.Deposit = deposit;
                u.Available = availText.Length == 0 ? null : avail.Date;
                if (live == null && u.Status != status)
                {
                    u.Status = status;
                    u.StatusSince = DateTime.Now;
                }
                u.PetsOk = pets;
                u.Listed = listed;
                u.Features = feat;
                u.Photos = photos;
                u.Plan = plan;
                u.Note = note;
                await Store.SaveRows("units", all);
                label = d.Properties.First(p => p.Id == prop).Name + " · " + number;
            }
            await Store.Log(c.U.Id, isNew ? "add unit" : "edit unit", label, true);
            response.SetElementContents("uu-list", await ListNow(c));
            response.ExecuteScript("AdminUnitsJs.saved();");
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
                return await Refuse(response, c, "delete unit", id, "You do not have the right to delete units.");
            }
            string label;
            using (await Store.Hold())
            {
                RentData d = await Rent.Read();
                UnitRec? u = d.Units.FirstOrDefault(x => x.Id == id);
                if (u == null || !Access.PropertyOk(c.U, u.PropertyId))
                {
                    return await Refuse(response, c, "delete unit", id, "That unit was not found.");
                }
                label = Rent.UnitLabel(d, u.Id);
                if (d.Leases.Any(x => x.UnitId == id) || d.Applications.Any(x => x.UnitId == id) || d.Showings.Any(x => x.UnitId == id))
                {
                    return await Refuse(response, c, "delete unit", label, label + " has leases, applications or showings in its records, so it cannot be deleted. Set it offline instead.");
                }
                List<UnitRec> all = await Store.Rows<UnitRec>("units");
                all.RemoveAll(x => x.Id == id);
                await Store.SaveRows("units", all);
            }
            await Store.Log(c.U.Id, "delete unit", label, true);
            response.SetElementContents("uu-list", await ListNow(c));
            response.ExecuteScript("AdminUnitsJs.deleted();");
            return response;
        }
    }
}
