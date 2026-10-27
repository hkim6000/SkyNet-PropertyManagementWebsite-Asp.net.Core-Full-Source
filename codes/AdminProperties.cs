using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminProperties : WebPage
    {
        private const string AreaKey = "properties";
        private const int Size = 10;
        private const int Pics = 6;
        private static readonly string[] Kinds = { "Apartments", "Townhomes", "Houses", "Condos", "Mixed" };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Properties | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPropertiesJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminPropertiesJs.denied();");
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
            response.SetElementContents("pr-brand", Ui.Esc(site.Brand));
            response.SetElementContents("pr-menu", await Auth.Menu("pr", "AdminProperties", c));
            response.SetElementContents("pr-who", Auth.Who("pr", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminPropertiesJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"pr-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"pr-btn pr-btn-ghost pr-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminPropertiesJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"pr-btn pr-btn-ghost pr-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminPropertiesJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"pr-pill pr-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"pr-err\" id=\"pr-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"pr-field\" for=\"pr-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"pr-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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
            return m.Kind == "video" ? "<video src=\"" + Ui.Esc(m.Path) + "#t=0.1\" muted preload=\"metadata\" playsinline></video><i class=\"pr-pkplay\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"14\" height=\"14\" fill=\"currentColor\"><path d=\"M8 5v14l11-7z\"/></svg></i>" : "<img src=\"" + Ui.Esc(m.Thumb.Length > 0 ? m.Thumb : m.Path) + "\" alt=\"\" loading=\"lazy\">";
        }

        private static string PickItem(MediaItem m)
        {
            string thumb = m.Thumb.Length > 0 ? m.Thumb : m.Path;
            return "<button type=\"button\" class=\"pr-pki\" data-src=\"" + Ui.Esc(m.Path) + "\" data-thumb=\"" + Ui.Esc(thumb) + "\" data-kind=\"" + Ui.Esc(m.Kind) + "\" onclick=\"AdminPropertiesJs.pkPick(this)\" title=\"" + Ui.Esc(m.Name) + "\">" + Shot(m) + "<span>" + Ui.Esc(m.Name) + "</span></button>";
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
                return "<p class=\"pr-none\">Nothing matches. Upload " + (kind == "video" ? "an MP4 video" : kind == "photo" ? "a photo" : "a photo or an MP4 video") + " above, or add it in the Media library.</p>";
            }
            int pages = (rows.Count + PickSize - 1) / PickSize;
            page = Math.Min(Math.Max(page, 1), pages);
            string pager = pages <= 1 ? string.Empty : "<div class=\"pr-pkpager\"><span>Page " + page + " of " + pages + " &middot; " + (kind == "video" ? Ui.Count(rows.Count, "video", "videos") : kind == "photo" ? Ui.Count(rows.Count, "photo", "photos") : Ui.Count(rows.Count, "item", "items")) + "</span><div><button type=\"button\" class=\"pr-btn pr-btn-ghost pr-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminPropertiesJs.pkList(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"pr-btn pr-btn-ghost pr-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminPropertiesJs.pkList(" + (page + 1) + ")\">Next</button></div></div>";
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
            response.SetElementContents("pr-pkgrid", PickGrid(media, Ui.Clip(GetDataValue("pterm"), 40), PickPage(GetDataValue("ppg")), PickKind(GetDataValue("pkind"))));
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
                response.ExecuteScript("AdminPropertiesJs.pkUploaded(false,'Choose a file first.');");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files.Take(1).ToList(), site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            List<MediaItem> media = await Media.Load();
            response.SetElementContents("pr-pkgrid", PickGrid(media, string.Empty, 1, PickKind(GetDataValue("pkind"))));
            response.ExecuteScript("AdminPropertiesJs.pkUploaded(" + (added > 0 ? "true" : "false") + ",'" + Ui.Js(((added > 0 ? "Added to the library. " : string.Empty) + string.Join(" ", errors)).Trim()) + "');");
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
            response.SetElementContents("pr-main", "<div class=\"pr-ph\"><div><h1>Properties</h1><p>The buildings and houses you manage. Hidden properties stay in the records but are left off the website.</p></div>" + (Can(c, 'A') && (c.U.Manager || c.U.AllProperties) ? "<button type=\"button\" class=\"pr-btn\" onclick=\"AdminPropertiesJs.add()\">Add a property</button>" : string.Empty) + "</div><div class=\"pr-tools\"><input type=\"search\" id=\"pr-q\" placeholder=\"Search by name or address\" maxlength=\"40\" oninput=\"AdminPropertiesJs.type()\" aria-label=\"Search properties\"></div><div id=\"pr-list\">" + ListHtml(c, d, await Media.Load(), string.Empty, 1) + "</div>");
            return response;
        }

        private static string ListHtml(Ctx c, RentData d, List<MediaItem> media, string term, int page)
        {
            List<PropertyRec> rows = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id) && (term == string.Empty || Ui.Has(p.Name, term) || Ui.Has(p.Street, term) || Ui.Has(p.City, term))).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"pr-panel pr-empty\">" + (d.Properties.Count == 0 ? "No properties yet. Add the first one." : "No properties match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"pr-wrapt\"><table class=\"pr-table\"><thead><tr><th>Property</th><th>Kind</th><th class=\"pr-num\">Units</th><th class=\"pr-num\">Occupied</th><th class=\"pr-num\">To rent</th><th>Website</th></tr></thead><tbody>");
            foreach (PropertyRec p in rows.Skip((page - 1) * Size).Take(Size))
            {
                MediaItem? m = media.FirstOrDefault(x => x.Path == p.Media);
                string thumb = m == null ? string.Empty : m.Thumb.Length > 0 ? m.Thumb : m.Path;
                List<UnitRec> units = d.Units.Where(u => u.PropertyId == p.Id).ToList();
                int occ = units.Count(u => u.Status is "Occupied" or "Notice");
                int pct = units.Count == 0 ? 0 : (int)Math.Round(occ * 100.0 / units.Count);
                sb.Append("<tr class=\"pr-row\" onclick=\"AdminPropertiesJs.edit('" + Ui.Js(p.Id) + "')\"><td class=\"pr-first\"><div class=\"pr-cell-main\">" + (thumb.Length > 0 ? "<img class=\"pr-thumb\" src=\"" + Ui.Esc(thumb) + "\" alt=\"\" loading=\"lazy\">" : "<span class=\"pr-thumb\"></span>") + "<div><b>" + Ui.Esc(p.Name) + "</b><small>" + Ui.Esc(p.Street + ", " + p.City) + "</small></div></div></td>");
                sb.Append("<td data-label=\"Kind\">" + Ui.Esc(p.Kind) + "</td><td class=\"pr-num\" data-label=\"Units\">" + units.Count + "</td><td class=\"pr-num\" data-label=\"Occupied\">" + occ + " <small class=\"pr-mini\">(" + pct + "%)</small></td><td class=\"pr-num\" data-label=\"To rent\">" + units.Count(Rent.Offered) + "</td><td data-label=\"Website\"><span class=\"pr-pill " + (p.Show ? "pr-pill-go\">Shown" : "pr-pill-off\">Hidden") + "</span></td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "property", "properties"));
            return sb.ToString();
        }

        private async Task<string> ListNow(Ctx c)
        {
            RentData d = await Rent.Load();
            return ListHtml(c, d, await Media.Load(), Term(GetDataValue("term")), PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("pr-list", await ListNow(c));
            return response;
        }

        private static string PicBox(string key, string src, List<MediaItem> media, bool edit, string ph, bool video)
        {
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            return "<div class=\"pr-pic\" id=\"pr-f-" + key + "\" data-k=\"" + key + "\" data-src=\"" + Ui.Esc(m == null ? string.Empty : src) + "\" data-ph=\"" + Ui.Esc(ph) + "\"" + (video ? " data-any=\"1\"" : string.Empty) + "><button type=\"button\" class=\"pr-picb\"" + (edit ? " onclick=\"AdminPropertiesJs.pic(this)\"" : " disabled") + " aria-label=\"" + Ui.Esc(ph) + "\">" + (m != null ? Shot(m) : "<span>" + Ui.Esc(ph) + "</span>") + "</button>" + (edit ? "<button type=\"button\" class=\"pr-picx\"" + (m != null ? string.Empty : " hidden") + " onclick=\"AdminPropertiesJs.picClear(this)\">Remove</button>" : string.Empty) + "</div>";
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
            PropertyRec? p = d.Properties.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && (p == null || !Access.PropertyOk(c.U, p.Id)))
            {
                return await Refuse(response, c, "open property", id, "That property was not found.");
            }
            if (p == null && (!Can(c, 'A') || !(c.U.Manager || c.U.AllProperties)))
            {
                return await Refuse(response, c, "add property", string.Empty, "You can view properties but not add them.");
            }
            response.SetElementContents("pr-mbody", FormHtml(c, d, p, await Media.Load()));
            response.ExecuteScript("AdminPropertiesJs.opened('" + Ui.Js(p == null ? "New property" : p.Name) + "');");
            return response;
        }

        private static string FormHtml(Ctx c, RentData d, PropertyRec? p, List<MediaItem> media)
        {
            bool isNew = p == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            PropertyRec r = p ?? new PropertyRec { City = d.Site.City, Region = d.Site.Region, Order = d.Properties.Count + 1 };
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<form class=\"pr-stack\" id=\"pr-form\" onsubmit=\"return AdminPropertiesJs.save()\" novalidate><input type=\"hidden\" id=\"pr-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            if (!isNew)
            {
                List<UnitRec> units = d.Units.Where(u => u.PropertyId == r.Id).ToList();
                sb.Append("<div class=\"pr-bar\"><span>" + Ui.Count(units.Count, "unit", "units") + " &middot; " + units.Count(u => u.Status is "Occupied" or "Notice") + " occupied &middot; " + units.Count(Rent.Offered) + " to rent</span><span class=\"pr-acts-row\"><a class=\"pr-btn pr-btn-ghost pr-btn-sm\" href=\"AdminUnits?p=" + Ui.Esc(r.Id) + "\">Units</a>" + (r.Show ? "<a class=\"pr-btn pr-btn-ghost pr-btn-sm\" href=\"Property?p=" + Ui.Esc(r.Id) + "\" target=\"_blank\" rel=\"noopener\">View on website</a>" : string.Empty) + "</span></div>");
            }
            sb.Append("<div class=\"pr-grid2\">" + Field("name", "Name", Text("name", r.Name, 60, "text", ro)) + Field("kind", "Kind", "<select id=\"pr-f-kind\" data-k=\"kind\"" + dis + ">" + string.Concat(Kinds.Select(k => Opt(k, k, r.Kind))) + "</select>") + "</div>");
            sb.Append(Field("street", "Street address", Text("street", r.Street, 80, "text", ro)));
            sb.Append("<div class=\"pr-grid3\">" + Field("city", "City", Text("city", r.City, 40, "text", ro)) + Field("region", "State", Text("region", r.Region, 30, "text", ro)) + Field("zip", "ZIP code", Text("zip", r.Zip, 12, "text", ro)) + "</div>");
            sb.Append("<div class=\"pr-grid3\">" + Field("phone", "Leasing phone", Text("phone", r.Phone, 30, "tel", ro)) + Field("email", "Leasing email", Text("email", r.Email, 80, "email", ro)) + Field("year", "Year built", Text("year", r.YearBuilt > 0 ? r.YearBuilt.ToString(CultureInfo.InvariantCulture) : string.Empty, 4, "text", ro + " inputmode=\"numeric\"")) + "</div>");
            sb.Append("<label class=\"pr-field\" for=\"pr-f-summary\"><span>Summary for the website</span><textarea id=\"pr-f-summary\" data-k=\"summary\" rows=\"3\" maxlength=\"500\"" + ro + ">" + Ui.Esc(r.Summary) + "</textarea>" + Err("summary") + "</label>");
            sb.Append("<label class=\"pr-field\" for=\"pr-f-amen\"><span>Amenities (one per line)</span><textarea id=\"pr-f-amen\" data-k=\"amen\" rows=\"4\" maxlength=\"800\"" + ro + ">" + Ui.Esc(string.Join("\n", r.Amenities)) + "</textarea>" + Err("amen") + "</label>");
            sb.Append("<div class=\"pr-grid2\">" + Field("pets", "Pet policy", Text("pets", r.Pets, 160, "text", ro)) + Field("parking", "Parking", Text("parking", r.Parking, 160, "text", ro)) + "</div>");
            sb.Append("<div class=\"pr-field\"><span>Main picture</span>" + PicBox("media", r.Media, media, edit, "Choose a picture", false) + Err("media") + "</div>");
            sb.Append("<div class=\"pr-field\"><span>More photos or videos (shown in the gallery and the slide show)</span><div class=\"pr-pics\">");
            for (int i = 0; i < Pics; i++)
            {
                sb.Append(PicBox("g" + i, i < r.Gallery.Count ? r.Gallery[i] : string.Empty, media, edit, "Add", true));
            }
            sb.Append("</div>" + Err("gallery") + "</div>");
            sb.Append("<div class=\"pr-grid2\">" + Field("order", "Order on the website", Text("order", r.Order.ToString(CultureInfo.InvariantCulture), 3, "text", ro + " inputmode=\"numeric\"")) + "<div class=\"pr-field\"><span>Website</span><label class=\"pr-check\"><input type=\"checkbox\" id=\"pr-f-show\" data-k=\"show\"" + (r.Show ? " checked" : string.Empty) + dis + ">Show this property and its units on the website</label></div></div>");
            sb.Append("<label class=\"pr-field\" for=\"pr-f-note\"><span>Staff note (not on the website)</span><textarea id=\"pr-f-note\" data-k=\"note\" rows=\"2\" maxlength=\"500\"" + ro + ">" + Ui.Esc(r.Note) + "</textarea>" + Err("note") + "</label>");
            sb.Append("<div class=\"pr-acts\">" + (edit ? "<button type=\"submit\" class=\"pr-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"pr-btn pr-btn-ghost\" onclick=\"AdminPropertiesJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Can(c, 'D') ? "<button type=\"button\" class=\"pr-btn pr-btn-danger pr-push\" onclick=\"AdminPropertiesJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Name) + "')\">Delete</button>" : string.Empty) + "</div></form>");
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
            if (!Can(c, isNew ? 'A' : 'E') || (isNew && !(c.U.Manager || c.U.AllProperties)) || (!isNew && !Access.PropertyOk(c.U, id)))
            {
                return await Refuse(response, c, isNew ? "add property" : "edit property", id, "You do not have the right to " + (isNew ? "add" : "change") + " this property.");
            }
            string name = Ui.Clip(GetDataValue("name"), 60);
            string kind = Ui.Clip(GetDataValue("kind"), 20);
            kind = Kinds.Contains(kind) ? kind : Kinds[0];
            string street = Ui.Clip(GetDataValue("street"), 80);
            string city = Ui.Clip(GetDataValue("city"), 40);
            string region = Ui.Clip(GetDataValue("region"), 30);
            string zip = Ui.Clip(GetDataValue("zip"), 12);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string yearText = Ui.Clip(GetDataValue("year"), 4);
            int year = yearText.Length == 0 ? 0 : Int(yearText, -1);
            string summary = Ui.Clip((GetDataValue("summary") ?? string.Empty).Replace("\r", string.Empty), 500);
            List<string> amen = (GetDataValue("amen") ?? string.Empty).Replace("\r", string.Empty).Split('\n').Select(x => Ui.Clip(x, 60)).Where(x => x.Length > 0).Distinct().Take(16).ToList();
            string pets = Ui.Clip(GetDataValue("pets"), 160);
            string parking = Ui.Clip(GetDataValue("parking"), 160);
            string media = Ui.Clip(GetDataValue("media"), 200);
            List<MediaItem> library = await Media.Load();
            List<string> gallery = new List<string>();
            bool badPic = false;
            for (int i = 0; i < Pics; i++)
            {
                string g = Ui.Clip(GetDataValue("g" + i), 200);
                if (g.Length == 0)
                {
                    continue;
                }
                if (!library.Any(m => m.Path == g && m.Kind is "photo" or "video"))
                {
                    badPic = true;
                    continue;
                }
                if (!gallery.Contains(g) && g != media)
                {
                    gallery.Add(g);
                }
            }
            int order = Int(GetDataValue("order"), 0);
            bool show = (GetDataValue("show") ?? string.Empty) == "1";
            string note = Ui.Clip((GetDataValue("note") ?? string.Empty).Replace("\r", string.Empty), 500);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 ? "Please enter the property name." : string.Empty,
                ["street"] = street.Length < 3 ? "Please enter the street address." : string.Empty,
                ["city"] = city.Length < 2 ? "Please enter the city." : string.Empty,
                ["region"] = region.Length < 2 ? "Please enter the state." : string.Empty,
                ["zip"] = zip.Length < 3 ? "Please enter the ZIP code." : string.Empty,
                ["phone"] = phone.Length > 0 && !Ui.IsPhone(phone) ? "Please enter a valid phone number, or leave it empty." : string.Empty,
                ["email"] = email.Length > 0 && !Ui.IsEmail(email) ? "Please enter a valid email address, or leave it empty." : string.Empty,
                ["year"] = yearText.Length > 0 && (year < 1700 || year > DateTime.Today.Year + 2) ? "Enter a year like 1968, or leave it empty." : string.Empty,
                ["summary"] = show && summary.Length < 20 ? "Write a short summary of at least 20 characters for the website." : string.Empty,
                ["amen"] = string.Empty,
                ["media"] = media.Length > 0 && !library.Any(m => m.Path == media && m.Kind == "photo") ? "That picture is not in the media library. Choose it again." : show && media.Length == 0 ? "Choose a main picture for the website." : string.Empty,
                ["gallery"] = badPic ? "One photo or video is no longer in the media library. Choose it again." : string.Empty,
                ["order"] = order < 0 || order > 999 ? "Enter a number from 0 to 999." : string.Empty,
                ["note"] = string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("pr-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<PropertyRec> all = await Store.Rows<PropertyRec>("properties");
                if (all.Any(x => x.Id != id && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    response.SetElementContents("pr-e-name", "Another property already has this name.");
                    return response;
                }
                PropertyRec? p = all.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    string key = "p-" + Ui.Slug(name);
                    while (all.Any(x => x.Id == key))
                    {
                        key += "-2";
                    }
                    p = new PropertyRec { Id = key };
                    all.Add(p);
                }
                if (p == null)
                {
                    return await Refuse(response, c, "edit property", id, "That property was not found.");
                }
                p.Name = name;
                p.Kind = kind;
                p.Street = street;
                p.City = city;
                p.Region = region;
                p.Zip = zip;
                p.Phone = phone;
                p.Email = email;
                p.YearBuilt = year;
                p.Summary = summary;
                p.Amenities = amen;
                p.Pets = pets;
                p.Parking = parking;
                p.Media = media;
                p.Gallery = gallery;
                p.Order = order;
                p.Show = show;
                p.Note = note;
                await Store.SaveRows("properties", all);
            }
            await Store.Log(c.U.Id, isNew ? "add property" : "edit property", name, true);
            response.SetElementContents("pr-list", await ListNow(c));
            response.ExecuteScript("AdminPropertiesJs.saved();");
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
            if (!Can(c, 'D') || !Access.PropertyOk(c.U, id))
            {
                return await Refuse(response, c, "delete property", id, "You do not have the right to delete properties.");
            }
            string name;
            using (await Store.Hold())
            {
                RentData d = await Rent.Read();
                PropertyRec? p = d.Properties.FirstOrDefault(x => x.Id == id);
                if (p == null)
                {
                    return await Refuse(response, c, "delete property", id, "That property was not found.");
                }
                if (d.Units.Any(u => u.PropertyId == id))
                {
                    return await Refuse(response, c, "delete property", p.Name, p.Name + " still has units. Delete or move them first, or hide the property instead.");
                }
                name = p.Name;
                List<PropertyRec> all = await Store.Rows<PropertyRec>("properties");
                all.RemoveAll(x => x.Id == id);
                await Store.SaveRows("properties", all);
                List<NoticeRec> notes = await Store.Rows<NoticeRec>("notices");
                foreach (NoticeRec n in notes)
                {
                    n.Properties.Remove(id);
                }
                await Store.SaveRows("notices", notes);
                List<AdminUser> staff = await Store.LoadStaff();
                foreach (AdminUser u in staff)
                {
                    u.Properties.Remove(id);
                }
                await Store.SaveStaff(staff);
            }
            await Store.Log(c.U.Id, "delete property", name, true);
            response.SetElementContents("pr-list", await ListNow(c));
            response.ExecuteScript("AdminPropertiesJs.deleted();");
            return response;
        }
    }
}
