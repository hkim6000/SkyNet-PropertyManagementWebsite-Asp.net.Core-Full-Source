using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminOwnership : WebPage
    {
        private const string AreaKey = "ownership";
        private const int Size = 12;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Ownership | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminOwnershipJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminOwnershipJs.denied();");
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
            response.SetElementContents("oh-brand", Ui.Esc(site.Brand));
            response.SetElementContents("oh-menu", await Auth.Menu("oh", "AdminOwnership", c));
            response.SetElementContents("oh-who", Auth.Who("oh", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminOwnershipJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"oh-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"oh-btn oh-btn-ghost oh-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminOwnershipJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"oh-btn oh-btn-ghost oh-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminOwnershipJs.go(" + (page + 1) + ")\">Next</button></div></div>";
        }

        private static string Opt(string value, string label, string current)
        {
            return "<option value=\"" + Ui.Esc(value) + "\"" + (value == current ? " selected" : string.Empty) + ">" + Ui.Esc(label) + "</option>";
        }

        private static string Err(string key)
        {
            return "<em class=\"oh-err\" id=\"oh-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"oh-field\" for=\"oh-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"oh-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
        }

        private static string Num(decimal v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static decimal Dec(string? v)
        {
            return decimal.TryParse((v ?? string.Empty).Trim().TrimStart('$'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? Math.Round(d, 2) : -1;
        }

        private static string OwnerName(RentData d, string id)
        {
            return d.Owners.FirstOrDefault(o => o.Id == id)?.Name ?? "Removed owner";
        }

        private static DateTime? LastChange(List<OwnershipRec> rows, string pid)
        {
            List<DateTime> marks = rows.Where(x => x.PropertyId == pid && x.From != null).Select(x => x.From!.Value.Date).Concat(rows.Where(x => x.PropertyId == pid && x.Until != null).Select(x => x.Until!.Value.Date.AddDays(1))).ToList();
            return marks.Count == 0 ? null : marks.Max();
        }

        private static DateTime? Locked(RentData d, string pid)
        {
            HashSet<string> owners = new HashSet<string>(d.Ownership.Where(x => x.PropertyId == pid).Select(x => x.OwnerId));
            List<DateTime> ends = d.Payouts.Where(p => owners.Contains(p.OwnerId)).Select(p => Books.ParseMonth(p.Month, out DateTime m) ? (DateTime?)m.AddMonths(1).AddDays(-1) : null).Where(x => x != null).Select(x => x!.Value).ToList();
            return ends.Count == 0 ? null : ends.Max();
        }

        private static bool Open(OwnershipRec x, DateTime? locked)
        {
            return locked == null || (x.From != null && x.From.Value.Date > locked.Value);
        }

        private static string Day(DateTime? d)
        {
            return d == null ? "-" : Fmt.Day(d.Value);
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
            response.SetElementContents("oh-main", "<div class=\"oh-ph\"><div><h1>Ownership</h1><p>Who owns each property, at what share and since when. Statements and reports split the money by these shares, day by day.</p></div></div><div class=\"oh-tools\"><input type=\"search\" id=\"oh-q\" placeholder=\"Search property or owner\" maxlength=\"40\" oninput=\"AdminOwnershipJs.type()\" aria-label=\"Search\"><select id=\"oh-ff\" onchange=\"AdminOwnershipJs.list(true)\" aria-label=\"Show\">" + Opt(string.Empty, "All properties", string.Empty) + Opt("shared", "Shared by several owners", string.Empty) + Opt("gap", "Part has no owner", string.Empty) + Opt("future", "Change coming up", string.Empty) + "</select></div><div id=\"oh-list\">" + ListHtml(c, d, string.Empty, string.Empty, 1) + "</div>");
            string pid = Ui.Clip(GetDataValue("p"), 60);
            if (pid.Length > 0 && d.Properties.Any(p => p.Id == pid) && Access.PropertyOk(c.U, pid))
            {
                response.ExecuteScript("AdminOwnershipJs.edit('" + Ui.Js(pid) + "');");
            }
            return response;
        }

        private static string ListHtml(Ctx c, RentData d, string term, string filter, int page)
        {
            DateTime today = DateTime.Today;
            List<PropertyRec> rows = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).Where(p =>
            {
                List<(string OwnerId, decimal Share)> h = Books.Holders(d, p.Id, today).Where(x => x.OwnerId.Length > 0).ToList();
                bool match = term.Length == 0 || Ui.Has(p.Name, term) || d.Ownership.Any(x => x.PropertyId == p.Id && Ui.Has(OwnerName(d, x.OwnerId), term));
                bool want = filter switch
                {
                    "shared" => h.Count > 1,
                    "gap" => h.Sum(x => x.Share) < 100m,
                    "future" => d.Ownership.Any(x => x.PropertyId == p.Id && ((x.From != null && x.From.Value.Date > today) || (x.Until != null && x.Until.Value.Date >= today))),
                    _ => true
                };
                return match && want;
            }).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"oh-panel oh-empty\">" + (d.Properties.Count == 0 ? "No properties yet. Add them on Properties first." : "No properties match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"oh-wrapt\"><table class=\"oh-table\"><thead><tr><th>Property</th><th>Owners today</th><th>Since</th><th class=\"oh-num\">Share</th>" + (Can(c, 'E') ? "<th></th>" : string.Empty) + "</tr></thead><tbody>");
            foreach (PropertyRec p in rows.Skip((page - 1) * Size).Take(Size))
            {
                List<(string OwnerId, decimal Share)> h = Books.Holders(d, p.Id, today).Where(x => x.OwnerId.Length > 0).ToList();
                List<string> names = h.Select(x => Ui.Esc(OwnerName(d, x.OwnerId))).ToList();
                List<string> since = h.Select(x => Ui.Esc(Day(d.Ownership.Where(r => r.PropertyId == p.Id && r.OwnerId == x.OwnerId && Books.Covers(r, today)).Select(r => r.From).Max()))).ToList();
                List<string> shares = h.Select(x => Ui.Esc(Books.Pct(x.Share))).ToList();
                decimal free = 100m - h.Sum(x => x.Share);
                if (free > 0)
                {
                    names.Add("<span class=\"oh-gap\">No owner</span>");
                    since.Add("<span class=\"oh-gap\">-</span>");
                    shares.Add("<span class=\"oh-gap\">" + Ui.Esc(Books.Pct(free)) + "</span>");
                }
                DateTime? next = d.Ownership.Where(x => x.PropertyId == p.Id).Select(x => x.From != null && x.From.Value.Date > today ? x.From.Value.Date : x.Until != null && x.Until.Value.Date >= today ? x.Until.Value.Date.AddDays(1) : DateTime.MaxValue).Where(x => x != DateTime.MaxValue).OrderBy(x => x).Cast<DateTime?>().FirstOrDefault();
                DateTime? paid = Locked(d, p.Id);
                sb.Append("<tr class=\"oh-row\" onclick=\"AdminOwnershipJs.edit('" + Ui.Js(p.Id) + "')\"><td class=\"oh-first\"><b>" + Ui.Esc(p.Name) + "</b><small>" + Ui.Count(d.Units.Count(u => u.PropertyId == p.Id), "unit", "units") + (next != null ? " · change on " + Ui.Esc(Fmt.Day(next.Value)) : string.Empty) + (paid != null ? " · paid through " + Ui.Esc(paid.Value.ToString("MMM yyyy", Fmt.Us)) : string.Empty) + "</small></td><td data-label=\"Owners today\"><div class=\"oh-lines\">" + string.Concat(names.Select(x => "<span>" + x + "</span>")) + "</div></td><td data-label=\"Since\"><div class=\"oh-lines\">" + string.Concat(since.Select(x => "<span>" + x + "</span>")) + "</div></td><td class=\"oh-num\" data-label=\"Share\"><div class=\"oh-lines\">" + string.Concat(shares.Select(x => "<span>" + x + "</span>")) + "</div></td>" + (Can(c, 'E') ? "<td class=\"oh-actd\"><button type=\"button\" class=\"oh-btn oh-btn-sm oh-btn-ghost\" onclick=\"event.stopPropagation();AdminOwnershipJs.change('" + Ui.Js(p.Id) + "')\">Change</button></td>" : string.Empty) + "</tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "property", "properties"));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            RentData d = await Rent.Load();
            response.SetElementContents("oh-list", ListHtml(c, d, Term(GetDataValue("term")), Ui.Clip(GetDataValue("flt"), 10), PageNo(GetDataValue("pg"))));
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

        private static string HistoryHtml(Ctx c, RentData d, string pid, string note)
        {
            StringBuilder sb = new StringBuilder("<div class=\"oh-stack\" id=\"oh-own\"><input type=\"hidden\" id=\"oh-f-pid\" value=\"" + Ui.Esc(pid) + "\">" + (note.Length > 0 ? "<p class=\"oh-ok\">" + Ui.Esc(note) + "</p>" : string.Empty));
            List<(string OwnerId, decimal Share)> today = Books.Holders(d, pid, DateTime.Today).Where(x => x.OwnerId.Length > 0).ToList();
            decimal free = 100m - today.Sum(x => x.Share);
            sb.Append("<div class=\"oh-sum\"><span>Today</span><b>" + (today.Count == 0 ? "No owner" : Ui.Esc(string.Join(", ", today.Select(x => OwnerName(d, x.OwnerId) + " " + Books.Pct(x.Share))))) + "</b>" + (free > 0 && today.Count > 0 ? "<span class=\"oh-pill oh-pill-wait\">" + Ui.Esc(Books.Pct(free)) + " has no owner</span>" : string.Empty) + (Can(c, 'E') ? "<button type=\"button\" class=\"oh-btn oh-btn-sm oh-push\" onclick=\"AdminOwnershipJs.change()\">Change ownership</button>" : string.Empty) + "</div>");
            DateTime? locked = Locked(d, pid);
            List<OwnershipRec> rows = d.Ownership.Where(x => x.PropertyId == pid).OrderByDescending(x => x.From ?? DateTime.MinValue).ThenByDescending(x => x.Share).ToList();
            sb.Append("<h3>History</h3>");
            if (rows.Count == 0)
            {
                sb.Append("<p class=\"oh-mini\">No ownership recorded for this property yet." + (Can(c, 'E') ? " Use Change ownership to add the first owner." : string.Empty) + "</p>");
            }
            else
            {
                bool canEdit = Can(c, 'E') || Can(c, 'D');
                sb.Append("<div class=\"oh-wrapt\"><table class=\"oh-table oh-otab\"><thead><tr><th>Owner</th><th class=\"oh-num\">Share</th><th>From</th><th>Until</th><th>Reason</th><th></th></tr></thead><tbody>");
                foreach (OwnershipRec x in rows)
                {
                    bool now = Books.Covers(x, DateTime.Today);
                    bool open = canEdit && Open(x, locked);
                    sb.Append("<tr class=\"" + (open ? "oh-row" : "oh-lock") + (now ? string.Empty : " oh-past") + "\"" + (open ? " onclick=\"AdminOwnershipJs.row('" + Ui.Js(x.Id) + "')\"" : string.Empty) + "><td class=\"oh-first\"><b>" + Ui.Esc(OwnerName(d, x.OwnerId)) + "</b></td><td class=\"oh-num\" data-label=\"Share\">" + Ui.Esc(Books.Pct(x.Share)) + "</td><td data-label=\"From\">" + Ui.Esc(Day(x.From)) + "</td><td data-label=\"Until\">" + Ui.Esc(x.Until == null ? "Now" : Fmt.Day(x.Until.Value)) + "</td><td data-label=\"Reason\">" + Ui.Esc(x.Reason.Length > 0 ? x.Reason : "-") + "</td><td class=\"oh-rstate\" data-label=\"Edit\">" + (open ? "<span class=\"oh-pill oh-pill-new\">Edit</span>" : Open(x, locked) ? string.Empty : "<span class=\"oh-pill oh-pill-off\">Paid</span>") + "</td></tr>");
                }
                sb.Append("</tbody></table></div>");
                if (locked != null)
                {
                    sb.Append("<p class=\"oh-mini\">Payouts are recorded through " + Ui.Esc(locked.Value.ToString("MMMM yyyy", Fmt.Us)) + ", so rows that start on or before " + Ui.Esc(Fmt.Day(locked.Value)) + " are kept as they are. Rows that start later can be changed or deleted.</p>");
                }
            }
            sb.Append("<div class=\"oh-acts\"><button type=\"button\" class=\"oh-btn oh-btn-ghost\" onclick=\"AdminOwnershipJs.closeModal()\">Close</button></div></div>");
            return sb.ToString();
        }

        private static string ChangeHtml(RentData d, string pid, DateTime date, DateTime? locked)
        {
            List<(string OwnerId, decimal Share)> held = Books.Holders(d, pid, date).Where(x => x.OwnerId.Length > 0).ToList();
            StringBuilder sb = new StringBuilder("<form class=\"oh-stack\" id=\"oh-oform\" onsubmit=\"return AdminOwnershipJs.save()\" novalidate><input type=\"hidden\" id=\"oh-f-cpid\" value=\"" + Ui.Esc(pid) + "\"><p class=\"oh-mini\">Set each owner's share from this date. The current shares end the day before, and the new shares start on the date. Use 0 to end an owner's share.</p>" + (locked != null ? "<p class=\"oh-mini\">Payouts are recorded through " + Ui.Esc(locked.Value.ToString("MMMM yyyy", Fmt.Us)) + ", so the change date must be " + Ui.Esc(Fmt.Day(locked.Value.AddDays(1))) + " or later.</p>" : string.Empty));
            sb.Append(Field("odate", "From date", Text("odate", Fmt.IsoDay(date), 10, "date", (locked != null ? " min=\"" + Fmt.IsoDay(locked.Value.AddDays(1)) + "\"" : string.Empty) + " onchange=\"AdminOwnershipJs.changeDate()\"")));
            sb.Append("<div class=\"oh-oshares\">" + string.Concat(held.Select(h => "<label class=\"oh-orow\" for=\"oh-f-sh_" + Ui.Esc(h.OwnerId) + "\"><span>" + Ui.Esc(OwnerName(d, h.OwnerId)) + "</span><span class=\"oh-sharein\"><input type=\"text\" id=\"oh-f-sh_" + Ui.Esc(h.OwnerId) + "\" data-k=\"sh_" + Ui.Esc(h.OwnerId) + "\" data-share=\"1\" maxlength=\"6\" inputmode=\"decimal\" value=\"" + Num(h.Share) + "\" oninput=\"AdminOwnershipJs.total()\"><b>%</b></span></label>")));
            List<OwnerRec> others = d.Owners.Where(o => held.All(h => h.OwnerId != o.Id)).ToList();
            if (others.Count > 0)
            {
                sb.Append("<div class=\"oh-orow\"><select id=\"oh-f-oadd\" data-k=\"oadd\" aria-label=\"Add an owner\" onchange=\"AdminOwnershipJs.total()\"><option value=\"\">" + (held.Count == 0 ? "Choose the owner" : "Add another owner") + "</option>" + string.Concat(others.Select(o => Opt(o.Id, o.Name, string.Empty))) + "</select><span class=\"oh-sharein\"><input type=\"text\" id=\"oh-f-oaddshare\" data-k=\"oaddshare\" data-share=\"1\" maxlength=\"6\" inputmode=\"decimal\" value=\"" + (held.Count == 0 ? "100" : string.Empty) + "\" aria-label=\"Share of the added owner in percent\" oninput=\"AdminOwnershipJs.total()\"><b>%</b></span></div>");
            }
            sb.Append("</div><p class=\"oh-ototal\" id=\"oh-o-total\"></p>");
            sb.Append(Field("oreason", "Reason", Text("oreason", string.Empty, 80, "text", " placeholder=\"For example: sale, purchase, inheritance\"")));
            sb.Append(Err("own") + "<div class=\"oh-acts\"><button type=\"submit\" class=\"oh-btn\">Save the change</button><button type=\"button\" class=\"oh-btn oh-btn-ghost\" onclick=\"AdminOwnershipJs.m2Close()\">Cancel</button></div></form>");
            return sb.ToString();
        }

        private static string RowHtml(Ctx c, RentData d, OwnershipRec x)
        {
            StringBuilder sb = new StringBuilder("<form class=\"oh-stack\" id=\"oh-rform\" onsubmit=\"return AdminOwnershipJs.rowSave()\" novalidate><input type=\"hidden\" id=\"oh-f-rid\" value=\"" + Ui.Esc(x.Id) + "\">");
            bool edit = Can(c, 'E');
            string ro = edit ? string.Empty : " readonly";
            sb.Append("<div class=\"oh-grid2\">" + Field("rowner", "Owner", "<select id=\"oh-f-rowner\" data-k=\"rowner\"" + (edit ? string.Empty : " disabled") + ">" + string.Concat(d.Owners.Select(o => Opt(o.Id, o.Name, x.OwnerId))) + "</select>") + Field("rshare", "Share (%)", Text("rshare", Num(x.Share), 6, "text", ro + " inputmode=\"decimal\"")) + "</div>");
            sb.Append("<div class=\"oh-grid2\">" + Field("rfrom", "From", Text("rfrom", x.From == null ? string.Empty : Fmt.IsoDay(x.From.Value), 10, "date", ro)) + Field("runtil", "Until (leave empty if still owned)", Text("runtil", x.Until == null ? string.Empty : Fmt.IsoDay(x.Until.Value), 10, "date", ro)) + "</div>");
            sb.Append(Field("rreason", "Reason", Text("rreason", x.Reason, 80, "text", ro)));
            sb.Append(Err("row") + "<div class=\"oh-acts\">" + (edit ? "<button type=\"submit\" class=\"oh-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"oh-btn oh-btn-ghost\" onclick=\"AdminOwnershipJs.m2Close()\">Cancel</button>" + (Can(c, 'D') ? "<button type=\"button\" class=\"oh-btn oh-btn-danger oh-push\" onclick=\"AdminOwnershipJs.rowDelete()\">Delete</button>" : string.Empty) + "</div></form>");
            return sb.ToString();
        }

        private async Task<(Ctx? C, RentData D, PropertyRec? P)> Find(ApiResponse response, string action)
        {
            Ctx? c = await Guard(response);
            RentData d = await Rent.Load();
            if (c == null)
            {
                return (null, d, null);
            }
            string pid = Ui.Clip(GetDataValue("pid"), 60);
            PropertyRec? p = d.Properties.FirstOrDefault(x => x.Id == pid);
            if (p == null || !Access.PropertyOk(c.U, pid))
            {
                await Refuse(response, c, action, pid, "That property was not found.");
                return (c, d, null);
            }
            return (c, d, p);
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, RentData d, PropertyRec? p) = await Find(response, "open ownership");
            if (c == null || p == null)
            {
                return response;
            }
            response.SetElementContents("oh-mbody", HistoryHtml(c, d, p.Id, string.Empty));
            response.ExecuteScript("AdminOwnershipJs.opened('" + Ui.Js(p.Name) + "');");
            return response;
        }

        public async Task<ApiResponse> Change()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, RentData d, PropertyRec? p) = await Find(response, "change ownership");
            if (c == null || p == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "change ownership", p.Id, "You do not have the right to change ownership.");
            }
            if (d.Owners.Count == 0)
            {
                return await Refuse(response, c, "change ownership", p.Id, "Add the owners on Owners first.");
            }
            DateTime? locked = Locked(d, p.Id);
            DateTime date = Fmt.ParseDate(GetDataValue("odate"), out DateTime v) ? v : locked != null && locked.Value >= DateTime.Today ? locked.Value.AddDays(1) : DateTime.Today;
            response.SetElementContents("oh-m2body", ChangeHtml(d, p.Id, date, locked));
            response.ExecuteScript("AdminOwnershipJs.m2Opened('" + Ui.Js("Change ownership · " + p.Name) + "');AdminOwnershipJs.total();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, RentData d, PropertyRec? prop) = await Find(response, "change ownership");
            if (c == null || prop == null)
            {
                return response;
            }
            string pid = prop.Id;
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "change ownership", pid, "You do not have the right to change ownership.");
            }
            if (!Fmt.ParseDate(GetDataValue("odate"), out DateTime date) || date.Year < 2000 || date.Year > 2099)
            {
                response.SetElementContents("oh-e-odate", "Enter the date.");
                return response;
            }
            DateTime? locked = Locked(d, pid);
            response.SetElementContents("oh-e-odate", locked != null && date <= locked.Value ? "Payouts are recorded through " + locked.Value.ToString("MMMM yyyy", Fmt.Us) + ". Pick a date after " + Fmt.Day(locked.Value) + "." : string.Empty);
            if (locked != null && date <= locked.Value)
            {
                return response;
            }
            Dictionary<string, decimal> want = new Dictionary<string, decimal>();
            List<string> bad = new List<string>();
            foreach (OwnerRec o in d.Owners)
            {
                string? raw = GetDataValue("sh_" + o.Id);
                if (raw == null)
                {
                    continue;
                }
                string t = Ui.Clip(raw, 6).TrimEnd('%').Trim();
                decimal v = t.Length == 0 ? 0 : Dec(t);
                if (v < 0 || v > 100)
                {
                    bad.Add(o.Name + ": enter a share from 0 to 100.");
                }
                want[o.Id] = v;
            }
            string addId = Ui.Clip(GetDataValue("oadd"), 60);
            string addText = Ui.Clip(GetDataValue("oaddshare"), 6).TrimEnd('%').Trim();
            if (addId.Length > 0 || (addText.Length > 0 && want.Count > 0))
            {
                OwnerRec? added = d.Owners.FirstOrDefault(o => o.Id == addId);
                decimal v = addText.Length == 0 ? -1 : Dec(addText);
                if (added == null)
                {
                    bad.Add("Choose the owner to add.");
                }
                else if (v <= 0 || v > 100)
                {
                    bad.Add(added.Name + ": enter a share from 0.01 to 100.");
                }
                else
                {
                    want[added.Id] = (want.TryGetValue(added.Id, out decimal had) ? had : 0) + v;
                }
            }
            else if (want.Count == 0)
            {
                bad.Add("Choose the owner.");
            }
            decimal total = want.Values.Where(v => v > 0).Sum();
            if (bad.Count == 0 && total > 100m)
            {
                bad.Add("The shares add up to " + Books.Pct(total) + ". They can be 100% at most.");
            }
            DateTime? later = LastChange(d.Ownership, pid);
            if (bad.Count == 0 && later != null && later.Value > date)
            {
                bad.Add("There is a later change on " + Fmt.Day(later.Value) + ". Edit or delete that row first, or pick a later date.");
            }
            Dictionary<string, decimal> now = Books.Holders(d, pid, date).Where(x => x.OwnerId.Length > 0).ToDictionary(x => x.OwnerId, x => x.Share);
            if (bad.Count == 0 && now.Keys.Any(id => !want.ContainsKey(id)))
            {
                bad.Add("The list of owners does not match this date. Pick the date again.");
            }
            List<string> changed = now.Keys.Concat(want.Keys).Distinct().Where(id => (now.TryGetValue(id, out decimal a) ? a : 0) != (want.TryGetValue(id, out decimal b) ? b : 0)).ToList();
            if (bad.Count == 0 && changed.Count == 0)
            {
                bad.Add("Nothing changed on this date.");
            }
            response.SetElementContents("oh-e-own", string.Join(" ", bad));
            if (bad.Count > 0)
            {
                return response;
            }
            string reason = Ui.Clip(GetDataValue("oreason"), 80);
            using (await Store.Hold())
            {
                List<OwnershipRec> rows = await Store.Rows<OwnershipRec>("ownership");
                DateTime? newest = LastChange(rows, pid);
                if (newest != null && newest.Value > date)
                {
                    response.SetElementContents("oh-e-own", "Someone changed this property's ownership just now. Close this window and open it again.");
                    return response;
                }
                foreach (OwnershipRec x in rows.Where(x => x.PropertyId == pid && Books.Covers(x, date)).ToList())
                {
                    if (x.From != null && x.From.Value.Date == date)
                    {
                        rows.Remove(x);
                    }
                    else
                    {
                        x.Until = date.AddDays(-1);
                    }
                }
                foreach (KeyValuePair<string, decimal> kv in want.Where(kv => kv.Value > 0))
                {
                    rows.Add(new OwnershipRec { Id = Fmt.NewId("w-"), PropertyId = pid, OwnerId = kv.Key, Share = kv.Value, From = date, Reason = reason, By = c.U.Id, Created = DateTime.Now });
                }
                await Store.SaveRows("ownership", rows);
            }
            string who = want.Values.Any(v => v > 0) ? string.Join(", ", want.Where(kv => kv.Value > 0).Select(kv => OwnerName(d, kv.Key) + " " + Books.Pct(kv.Value))) : "no owner";
            await Store.Log(c.U.Id, "change ownership", prop.Name + " · " + Fmt.Day(date) + " · " + who, true);
            await After(response, c, pid, "Saved. " + prop.Name + " from " + Fmt.Day(date) + ": " + who + ".");
            return response;
        }

        public async Task<ApiResponse> Row()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, RentData d, PropertyRec? p) = await Find(response, "open ownership row");
            if (c == null || p == null)
            {
                return response;
            }
            OwnershipRec? x = d.Ownership.FirstOrDefault(r => r.Id == Ui.Clip(GetDataValue("rid"), 60) && r.PropertyId == p.Id);
            if (x == null)
            {
                return await Refuse(response, c, "open ownership row", p.Id, "That row was not found.");
            }
            if (!Open(x, Locked(d, p.Id)))
            {
                return await Refuse(response, c, "open ownership row", p.Id, "This row covers months already paid out, so it is kept as it is.");
            }
            response.SetElementContents("oh-m2body", RowHtml(c, d, x));
            response.ExecuteScript("AdminOwnershipJs.m2Opened('" + Ui.Js(OwnerName(d, x.OwnerId) + " · " + p.Name) + "');");
            return response;
        }

        private static string Overlap(List<OwnershipRec> rows, string pid)
        {
            List<OwnershipRec> mine = rows.Where(r => r.PropertyId == pid).ToList();
            foreach (IGrouping<string, OwnershipRec> g in mine.GroupBy(r => r.OwnerId))
            {
                List<OwnershipRec> list = g.ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        if (Books.Overlaps(list[i], list[j].From ?? DateTime.MinValue, list[j].Until ?? DateTime.MaxValue.Date))
                        {
                            return "This owner already has another row on these dates.";
                        }
                    }
                }
            }
            List<DateTime> marks = mine.Where(r => r.From != null).Select(r => r.From!.Value.Date).Concat(mine.Where(r => r.Until != null && r.Until.Value.Date < DateTime.MaxValue.Date).Select(r => r.Until!.Value.Date.AddDays(1))).Distinct().ToList();
            marks.Add(DateTime.MinValue);
            foreach (DateTime day in marks)
            {
                decimal sum = mine.Where(r => Books.Covers(r, day)).Sum(r => r.Share);
                if (sum > 100m)
                {
                    return "From " + (day == DateTime.MinValue ? "the first row" : Fmt.Day(day)) + " the shares would add up to " + Books.Pct(sum) + ". They can be 100% at most.";
                }
            }
            return string.Empty;
        }

        public async Task<ApiResponse> RowSave()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, RentData d, PropertyRec? p) = await Find(response, "edit ownership row");
            if (c == null || p == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "edit ownership row", p.Id, "You do not have the right to change ownership.");
            }
            string rid = Ui.Clip(GetDataValue("rid"), 60);
            DateTime? locked = Locked(d, p.Id);
            OwnerRec? owner = d.Owners.FirstOrDefault(o => o.Id == Ui.Clip(GetDataValue("rowner"), 60));
            decimal share = Dec(Ui.Clip(GetDataValue("rshare"), 6).TrimEnd('%'));
            bool fOk = Fmt.ParseDate(GetDataValue("rfrom"), out DateTime from);
            string untilText = Ui.Clip(GetDataValue("runtil"), 10);
            bool uOk = Fmt.ParseDate(untilText, out DateTime until);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["rowner"] = owner == null ? "Choose the owner." : string.Empty,
                ["rshare"] = share <= 0 || share > 100 ? "Enter a share from 0.01 to 100." : string.Empty,
                ["rfrom"] = !fOk ? "Enter the date." : locked != null && from.Date <= locked.Value ? "Payouts are recorded through " + locked.Value.ToString("MMMM yyyy", Fmt.Us) + ". Pick a date after " + Fmt.Day(locked.Value) + "." : string.Empty,
                ["runtil"] = untilText.Length > 0 && !uOk ? "Enter a valid date, or leave it empty." : uOk && fOk && until.Date < from.Date ? "This must be on or after the From date." : string.Empty,
                ["row"] = string.Empty
            };
            if (e.Values.All(v => v.Length == 0))
            {
                using (await Store.Hold())
                {
                    List<OwnershipRec> rows = await Store.Rows<OwnershipRec>("ownership");
                    OwnershipRec? x = rows.FirstOrDefault(r => r.Id == rid && r.PropertyId == p.Id);
                    if (x == null || !Open(x, locked))
                    {
                        e["row"] = "That row was not found, or it covers months already paid out.";
                    }
                    else
                    {
                        OwnershipRec before = new OwnershipRec { OwnerId = x.OwnerId, Share = x.Share, From = x.From, Until = x.Until, Reason = x.Reason };
                        x.OwnerId = owner!.Id;
                        x.Share = share;
                        x.From = from.Date;
                        x.Until = uOk ? until.Date : null;
                        x.Reason = Ui.Clip(GetDataValue("rreason"), 80);
                        string clash = Overlap(rows, p.Id);
                        if (clash.Length > 0)
                        {
                            x.OwnerId = before.OwnerId;
                            x.Share = before.Share;
                            x.From = before.From;
                            x.Until = before.Until;
                            x.Reason = before.Reason;
                            e["row"] = clash;
                        }
                        else
                        {
                            await Store.SaveRows("ownership", rows);
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("oh-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            await Store.Log(c.U.Id, "edit ownership row", p.Name + " · " + owner!.Name + " " + Books.Pct(share) + " · " + Fmt.Day(from) + (uOk ? " to " + Fmt.Day(until) : string.Empty), true);
            await After(response, c, p.Id, "Saved. " + owner.Name + " " + Books.Pct(share) + " from " + Fmt.Day(from) + (uOk ? " until " + Fmt.Day(until) : string.Empty) + ".");
            return response;
        }

        public async Task<ApiResponse> RowDelete()
        {
            ApiResponse response = new ApiResponse();
            (Ctx? c, RentData d, PropertyRec? p) = await Find(response, "delete ownership row");
            if (c == null || p == null)
            {
                return response;
            }
            if (!Can(c, 'D'))
            {
                return await Refuse(response, c, "delete ownership row", p.Id, "You do not have the right to delete ownership rows.");
            }
            string rid = Ui.Clip(GetDataValue("rid"), 60);
            DateTime? locked = Locked(d, p.Id);
            OwnershipRec? gone;
            using (await Store.Hold())
            {
                List<OwnershipRec> rows = await Store.Rows<OwnershipRec>("ownership");
                gone = rows.FirstOrDefault(r => r.Id == rid && r.PropertyId == p.Id);
                if (gone == null || !Open(gone, locked))
                {
                    return await Refuse(response, c, "delete ownership row", p.Id, "That row was not found, or it covers months already paid out.");
                }
                rows.Remove(gone);
                await Store.SaveRows("ownership", rows);
            }
            await Store.Log(c.U.Id, "delete ownership row", p.Name + " · " + OwnerName(d, gone.OwnerId) + " " + Books.Pct(gone.Share) + " · " + Day(gone.From), true);
            await After(response, c, p.Id, "Deleted: " + OwnerName(d, gone.OwnerId) + " " + Books.Pct(gone.Share) + " from " + Day(gone.From) + ". If another owner should take these days back, edit that owner's row and clear its Until date.");
            return response;
        }

        private async Task After(ApiResponse response, Ctx c, string pid, string note)
        {
            await Refresh(response, c);
            RentData d = await Rent.Load();
            response.SetElementContents("oh-mbody", HistoryHtml(c, d, pid, note));
            response.ExecuteScript("AdminOwnershipJs.m2Done('" + Ui.Js(note) + "');");
        }
    }
}
