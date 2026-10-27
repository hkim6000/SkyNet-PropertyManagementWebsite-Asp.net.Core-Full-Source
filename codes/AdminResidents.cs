using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminResidents : WebPage
    {
        private const string AreaKey = "residents";
        private const int Size = 12;
        private static readonly (string Key, string Name)[] Tabs = { ("now", "Living here"), ("soon", "Moving in"), ("past", "Past"), ("none", "No lease"), ("all", "All") };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Residents | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminResidentsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminResidentsJs.denied();");
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
            response.SetElementContents("rr-brand", Ui.Esc(site.Brand));
            response.SetElementContents("rr-menu", await Auth.Menu("rr", "AdminResidents", c));
            response.SetElementContents("rr-who", Auth.Who("rr", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminResidentsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"rr-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"rr-btn rr-btn-ghost rr-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminResidentsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"rr-btn rr-btn-ghost rr-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminResidentsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"rr-pill rr-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"rr-err\" id=\"rr-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"rr-field\" for=\"rr-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"rr-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static List<LeaseRec> LeasesOf(RentData d, string id)
        {
            return d.Leases.Where(l => l.Residents.Contains(id) && l.Status != "Cancelled").OrderByDescending(l => l.Start).ToList();
        }

        private static bool Visible(Ctx c, RentData d, ResidentRec r)
        {
            List<LeaseRec> ls = LeasesOf(d, r.Id);
            return ls.Count == 0 || ls.Any(l => Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, l.UnitId)));
        }

        private static string Group(RentData d, ResidentRec r)
        {
            List<LeaseRec> ls = LeasesOf(d, r.Id);
            return ls.Any(l => Rent.Live.Contains(l.Status)) ? "now" : ls.Any(l => l.Status == "Upcoming") ? "soon" : ls.Count > 0 ? "past" : "none";
        }

        private static string PortalState(ResidentRec r)
        {
            return r.Hash.Length > 0 ? "Active" : r.Invite.Length > 0 ? "Invited" : "No access";
        }

        private static IEnumerable<ResidentRec> Mine(Ctx c, RentData d, string prop)
        {
            return d.Residents.Where(r => Visible(c, d, r) && (prop.Length == 0 || LeasesOf(d, r.Id).Any(l => Rent.PropertyIdOfUnit(d, l.UnitId) == prop)));
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
            string open = Ui.Clip(GetDataValue("rid"), 60);
            List<PropertyRec> mine = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).ToList();
            response.SetElementContents("rr-main", "<div class=\"rr-ph\"><div><h1>Residents</h1><p>Everyone on a lease, with their contact details and resident portal access. Give new residents their activation code at move-in.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"rr-btn\" onclick=\"AdminResidentsJs.add()\">Add a resident</button>" : string.Empty) + "</div><div id=\"rr-tabs\">" + TabsHtml(c, d, "now", string.Empty) + "</div><div class=\"rr-tools\"><input type=\"search\" id=\"rr-q\" placeholder=\"Search by name, email or phone\" maxlength=\"40\" oninput=\"AdminResidentsJs.type()\" aria-label=\"Search residents\"><select id=\"rr-fp\" onchange=\"AdminResidentsJs.list(true)\" aria-label=\"Property\">" + Opt(string.Empty, "All properties", string.Empty) + string.Concat(mine.Select(p => Opt(p.Id, p.Name, string.Empty))) + "</select></div><div id=\"rr-list\">" + ListHtml(c, d, string.Empty, "now", string.Empty, 1) + "</div>");
            ResidentRec? r = d.Residents.FirstOrDefault(x => x.Id == open);
            if (r != null && Visible(c, d, r))
            {
                response.ExecuteScript("AdminResidentsJs.edit('" + Ui.Js(r.Id) + "');");
            }
            return response;
        }

        private static string TabsHtml(Ctx c, RentData d, string tab, string prop)
        {
            List<ResidentRec> all = Mine(c, d, prop).ToList();
            return "<div class=\"rr-tabs\" role=\"tablist\">" + string.Concat(Tabs.Select(t => "<button type=\"button\" role=\"tab\" class=\"rr-tab" + (t.Key == tab ? " rr-act" : string.Empty) + "\" aria-selected=\"" + (t.Key == tab ? "true" : "false") + "\" onclick=\"AdminResidentsJs.tab('" + t.Key + "')\">" + t.Name + "<i>" + (t.Key == "all" ? all.Count : all.Count(r => Group(d, r) == t.Key)) + "</i></button>")) + "</div>";
        }

        private static string ListHtml(Ctx c, RentData d, string term, string tab, string prop, int page)
        {
            string digits = new string(term.Where(char.IsDigit).ToArray());
            List<ResidentRec> rows = Mine(c, d, prop).Where(r => (tab == "all" || Group(d, r) == tab) && (term.Length == 0 || Ui.Has(Rent.Name(r), term) || Ui.Has(r.Email, term) || (digits.Length >= 3 && new string(r.Phone.Where(char.IsDigit).ToArray()).Contains(digits)))).OrderBy(r => r.Last).ThenBy(r => r.First).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"rr-panel rr-empty\">No residents here.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"rr-wrapt\"><table class=\"rr-table\"><thead><tr><th>Resident</th><th>Phone</th><th>Home</th><th>Lease</th><th>Portal</th></tr></thead><tbody>");
            foreach (ResidentRec r in rows.Skip((page - 1) * Size).Take(Size))
            {
                LeaseRec? l = Rent.LeaseOf(d, r.Id);
                sb.Append("<tr class=\"rr-row\" onclick=\"AdminResidentsJs.edit('" + Ui.Js(r.Id) + "')\"><td class=\"rr-first\"><b>" + Ui.Esc(Rent.Name(r)) + "</b><small>" + Ui.Esc(r.Email) + "</small></td><td data-label=\"Phone\">" + Ui.Esc(r.Phone.Length > 0 ? r.Phone : "-") + "</td><td data-label=\"Home\">" + Ui.Esc(l != null ? Rent.UnitLabel(d, l.UnitId) : "-") + "</td><td data-label=\"Lease\">" + (l != null ? Ui.Esc(l.Code) + " " + Pill(l.Status == "Upcoming" ? "Upcoming" : Rent.Holdover(l) ? "Month to month" : l.Status) : "-") + "</td><td data-label=\"Portal\">" + Pill(PortalState(r)) + (r.LastSeen != null && r.Hash.Length > 0 ? "<small class=\"rr-mini\"><br>Seen " + Ui.Esc(Fmt.Day(r.LastSeen.Value)) + "</small>" : string.Empty) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "resident", "residents"));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            RentData d = await Rent.Load();
            string tab = Ui.Clip(GetDataValue("tab"), 10);
            string prop = Ui.Clip(GetDataValue("prop"), 60);
            response.SetElementContents("rr-list", ListHtml(c, d, Term(GetDataValue("term")), tab, prop, PageNo(GetDataValue("pg"))));
            response.SetElementContents("rr-tabs", TabsHtml(c, d, tab, prop));
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
            ResidentRec? r = d.Residents.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && (r == null || !Visible(c, d, r)))
            {
                return await Refuse(response, c, "open resident", id, "That resident was not found.");
            }
            if (r == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add resident", string.Empty, "You can view residents but not add them.");
            }
            response.SetElementContents("rr-mbody", FormHtml(c, d, r, string.Empty));
            response.ExecuteScript("AdminResidentsJs.opened('" + Ui.Js(r == null ? "New resident" : Rent.Name(r)) + "');");
            return response;
        }

        private static string FormHtml(Ctx c, RentData d, ResidentRec? res, string note)
        {
            bool isNew = res == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            ResidentRec r = res ?? new ResidentRec();
            string ro = edit ? string.Empty : " readonly";
            StringBuilder sb = new StringBuilder("<form class=\"rr-stack\" id=\"rr-form\" onsubmit=\"return AdminResidentsJs.save()\" novalidate><input type=\"hidden\" id=\"rr-f-id\" value=\"" + Ui.Esc(r.Id) + "\">" + (note.Length > 0 ? "<p class=\"rr-ok\">" + note + "</p>" : string.Empty));
            if (!isNew)
            {
                List<LeaseRec> ls = LeasesOf(d, r.Id);
                sb.Append("<div class=\"rr-bar\"><span>Portal " + Pill(PortalState(r)) + (r.Invite.Length > 0 ? " Activation code <b class=\"rr-codebig\">" + Ui.Esc(r.Invite) + "</b>" : string.Empty) + (r.LastSeen != null && r.Hash.Length > 0 ? " &middot; last signed in " + Ui.Esc(Fmt.Day(r.LastSeen.Value)) : string.Empty) + "</span>" + (edit ? "<span class=\"rr-acts-row\"><button type=\"button\" class=\"rr-btn rr-btn-ghost rr-btn-sm\" onclick=\"AdminResidentsJs.portal('code')\">" + (r.Invite.Length > 0 ? "New code" : r.Hash.Length > 0 ? "Code to reset password" : "Create activation code") + "</button>" + (r.Hash.Length > 0 || r.Invite.Length > 0 ? "<button type=\"button\" class=\"rr-btn rr-btn-ghost rr-btn-sm\" onclick=\"AdminResidentsJs.portal('off')\">Remove access</button>" : string.Empty) + "</span>" : string.Empty) + "</div>");
                if (ls.Count > 0)
                {
                    sb.Append("<div class=\"rr-field\"><span>Leases</span><ul class=\"rr-llist\">" + string.Concat(ls.Select(l => "<li>" + (Access.Can(c.U, "leases", 'V') ? "<a href=\"AdminLeases?code=" + Ui.Esc(l.Code) + "\">" + Ui.Esc(l.Code) + "</a>" : Ui.Esc(l.Code)) + " " + Pill(l.Status) + " <span>" + Ui.Esc(Rent.UnitLabel(d, l.UnitId) + " · " + Fmt.Day(l.Start) + " to " + Fmt.Day(l.End)) + "</span>" + (Rent.Live.Contains(l.Status) ? " <b class=\"rr-mini\">Balance " + Fmt.Money(Rent.Balance(d, l.Id)) + "</b>" : string.Empty) + "</li>")) + "</ul></div>");
                }
            }
            sb.Append("<div class=\"rr-grid2\">" + Field("first", "First name", Text("first", r.First, 40, "text", ro)) + Field("last", "Last name", Text("last", r.Last, 40, "text", ro)) + "</div>");
            sb.Append("<div class=\"rr-grid2\">" + Field("email", "Email (used to sign in to the portal)", Text("email", r.Email, 80, "email", ro)) + Field("phone", "Phone", Text("phone", r.Phone, 30, "tel", ro)) + "</div>");
            sb.Append("<div class=\"rr-grid2\">" + Field("emerg", "Emergency contact", Text("emerg", r.Emergency, 120, "text", ro)) + Field("car", "Vehicle", Text("car", r.Vehicle, 80, "text", ro)) + "</div>");
            sb.Append("<label class=\"rr-field\" for=\"rr-f-note\"><span>Staff note (residents do not see it)</span><textarea id=\"rr-f-note\" data-k=\"note\" rows=\"2\" maxlength=\"500\"" + ro + ">" + Ui.Esc(r.Note) + "</textarea>" + Err("note") + "</label>");
            if (isNew)
            {
                sb.Append("<label class=\"rr-check\"><input type=\"checkbox\" id=\"rr-f-invite\" data-k=\"invite\" checked>Create a portal activation code</label>");
            }
            sb.Append("<div class=\"rr-acts\">" + (edit ? "<button type=\"submit\" class=\"rr-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"rr-btn rr-btn-ghost\" onclick=\"AdminResidentsJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Can(c, 'D') && !d.Leases.Any(l => l.Residents.Contains(r.Id)) ? "<button type=\"button\" class=\"rr-btn rr-btn-danger rr-push\" onclick=\"AdminResidentsJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(Rent.Name(r)) + "')\">Delete</button>" : string.Empty) + "</div></form>");
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
                return await Refuse(response, c, isNew ? "add resident" : "edit resident", id, "You do not have the right to " + (isNew ? "add" : "change") + " residents.");
            }
            RentData d = await Rent.Load();
            ResidentRec? old = d.Residents.FirstOrDefault(x => x.Id == id);
            if (!isNew && (old == null || !Visible(c, d, old)))
            {
                return await Refuse(response, c, "edit resident", id, "That resident was not found.");
            }
            string first = Ui.Clip(GetDataValue("first"), 40);
            string last = Ui.Clip(GetDataValue("last"), 40);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["first"] = first.Length < 1 ? "Enter the first name." : string.Empty,
                ["last"] = last.Length < 1 ? "Enter the last name." : string.Empty,
                ["email"] = !Ui.IsEmail(email) ? "Enter a valid email address." : d.Residents.Any(x => x.Id != id && x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)) ? "Another resident already uses this email." : string.Empty,
                ["phone"] = phone.Length > 0 && !Ui.IsPhone(phone) ? "Enter a valid phone number, or leave it empty." : string.Empty,
                ["emerg"] = string.Empty,
                ["car"] = string.Empty,
                ["note"] = string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("rr-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            ResidentRec? r;
            bool emailChanged = false;
            using (await Store.Hold())
            {
                List<ResidentRec> all = await Store.Rows<ResidentRec>("residents");
                r = all.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    r = new ResidentRec { Id = Fmt.NewId("r-"), Created = DateTime.Now, Invite = (GetDataValue("invite") ?? string.Empty) == "1" ? Fmt.Code(string.Empty, all.Select(x => x.Invite)) : string.Empty };
                    all.Add(r);
                }
                if (r == null)
                {
                    return await Refuse(response, c, "edit resident", id, "That resident was not found.");
                }
                emailChanged = !isNew && !r.Email.Equals(email, StringComparison.OrdinalIgnoreCase);
                r.First = first;
                r.Last = last;
                r.Email = email;
                r.Phone = phone;
                r.Emergency = Ui.Clip(GetDataValue("emerg"), 120);
                r.Vehicle = Ui.Clip(GetDataValue("car"), 80);
                r.Note = Ui.Clip((GetDataValue("note") ?? string.Empty).Replace("\r", string.Empty), 500);
                await Store.SaveRows("residents", all);
            }
            if (emailChanged)
            {
                await ResidentAuth.CloseAll(r.Id);
            }
            await Store.Log(c.U.Id, isNew ? "add resident" : "edit resident", first + " " + last, true);
            await Refresh(response, c);
            RentData d2 = await Rent.Load();
            ResidentRec? fresh = d2.Residents.FirstOrDefault(x => x.Id == r.Id);
            if (isNew && fresh != null)
            {
                response.SetElementContents("rr-mbody", FormHtml(c, d2, fresh, "Saved." + (fresh.Invite.Length > 0 ? " Give " + Ui.Esc(fresh.First) + " the activation code <b class=\"rr-codebig\">" + Ui.Esc(fresh.Invite) + "</b> to open the resident portal." : string.Empty)));
                response.ExecuteScript("AdminResidentsJs.opened('" + Ui.Js(Rent.Name(fresh)) + "');AdminResidentsJs.clean();");
                return response;
            }
            response.ExecuteScript("AdminResidentsJs.saved();");
            return response;
        }

        public async Task<ApiResponse> PortalAccess()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("id"), 60);
            string act = Ui.Clip(GetDataValue("act"), 6);
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "resident portal", id, "You do not have the right to change residents.");
            }
            RentData d = await Rent.Load();
            ResidentRec? found = d.Residents.FirstOrDefault(x => x.Id == id);
            if (found == null || !Visible(c, d, found) || act is not ("code" or "off"))
            {
                return await Refuse(response, c, "resident portal", id, "That resident was not found.");
            }
            string note;
            using (await Store.Hold())
            {
                List<ResidentRec> all = await Store.Rows<ResidentRec>("residents");
                ResidentRec? r = all.FirstOrDefault(x => x.Id == id);
                if (r == null)
                {
                    return await Refuse(response, c, "resident portal", id, "That resident was not found.");
                }
                if (act == "code")
                {
                    r.Invite = Fmt.Code(string.Empty, all.Select(x => x.Invite));
                    note = "New activation code <b class=\"rr-codebig\">" + Ui.Esc(r.Invite) + "</b>. " + Ui.Esc(r.First) + " uses it with " + Ui.Esc(r.Email) + " under Activate account on the Resident portal page" + (r.Hash.Length > 0 ? " to choose a new password." : ".");
                }
                else
                {
                    r.Invite = string.Empty;
                    r.Hash = string.Empty;
                    r.ResetToken = string.Empty;
                    r.ResetUntil = null;
                    note = "Portal access removed. " + Ui.Esc(r.First) + " is signed out everywhere.";
                }
                await Store.SaveRows("residents", all);
            }
            if (act == "off")
            {
                await ResidentAuth.CloseAll(id);
            }
            await Store.Log(c.U.Id, act == "code" ? "resident activation code" : "remove portal access", Rent.Name(found), true);
            await Refresh(response, c);
            RentData d2 = await Rent.Load();
            ResidentRec? fresh = d2.Residents.FirstOrDefault(x => x.Id == id);
            if (fresh != null)
            {
                response.SetElementContents("rr-mbody", FormHtml(c, d2, fresh, note));
                response.ExecuteScript("AdminResidentsJs.clean();");
            }
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
                return await Refuse(response, c, "delete resident", id, "You do not have the right to delete residents.");
            }
            string name;
            using (await Store.Hold())
            {
                RentData d = await Rent.Read();
                ResidentRec? r = d.Residents.FirstOrDefault(x => x.Id == id);
                if (r == null || !Visible(c, d, r))
                {
                    return await Refuse(response, c, "delete resident", id, "That resident was not found.");
                }
                name = Rent.Name(r);
                if (d.Leases.Any(l => l.Residents.Contains(id)))
                {
                    return await Refuse(response, c, "delete resident", name, name + " is on a lease, so the record stays. Remove portal access instead.");
                }
                d.Residents.Remove(r);
                await Store.SaveRows("residents", d.Residents);
            }
            await ResidentAuth.CloseAll(id);
            await Store.Log(c.U.Id, "delete resident", name, true);
            await Refresh(response, c);
            response.ExecuteScript("AdminResidentsJs.deleted();");
            return response;
        }
    }
}
