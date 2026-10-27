using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminNotices : WebPage
    {
        private const string AreaKey = "notices";
        private const int Size = 12;
        private static readonly (string Key, string Name)[] Tabs = { ("Live", "Showing now"), ("Scheduled", "Scheduled"), ("Expired", "Ended"), ("all", "All") };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Notices | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminNoticesJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminNoticesJs.denied();");
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
            response.SetElementContents("nt-brand", Ui.Esc(site.Brand));
            response.SetElementContents("nt-menu", await Auth.Menu("nt", "AdminNotices", c));
            response.SetElementContents("nt-who", Auth.Who("nt", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminNoticesJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"nt-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"nt-btn nt-btn-ghost nt-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminNoticesJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"nt-btn nt-btn-ghost nt-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminNoticesJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"nt-pill nt-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"nt-err\" id=\"nt-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"nt-field\" for=\"nt-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"nt-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static string State(NoticeRec n)
        {
            return n.From.Date > DateTime.Today ? "Scheduled" : n.Until != null && n.Until.Value.Date < DateTime.Today ? "Expired" : "Live";
        }

        private static string Where(RentData d, NoticeRec n)
        {
            return n.Properties.Count == 0 ? "All properties" : string.Join(", ", n.Properties.Select(id => d.Properties.FirstOrDefault(p => p.Id == id)?.Name).Where(x => x != null));
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
            response.SetElementContents("nt-main", "<div class=\"nt-ph\"><div><h1>Notices</h1><p>Messages residents see in the resident portal: water shut-offs, inspections, office hours. Choose the properties and the days each notice shows.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"nt-btn\" onclick=\"AdminNoticesJs.add()\">Add a notice</button>" : string.Empty) + "</div><div id=\"nt-tabs\">" + TabsHtml(d, "Live") + "</div><div class=\"nt-tools\"><input type=\"search\" id=\"nt-q\" placeholder=\"Search notices\" maxlength=\"40\" oninput=\"AdminNoticesJs.type()\" aria-label=\"Search notices\"></div><div id=\"nt-list\">" + ListHtml(d, await Store.LoadStaff(), string.Empty, "Live", 1) + "</div>");
            return response;
        }

        private static string TabsHtml(RentData d, string tab)
        {
            return "<div class=\"nt-tabs\" role=\"tablist\">" + string.Concat(Tabs.Select(t => "<button type=\"button\" role=\"tab\" class=\"nt-tab" + (t.Key == tab ? " nt-act" : string.Empty) + "\" aria-selected=\"" + (t.Key == tab ? "true" : "false") + "\" onclick=\"AdminNoticesJs.tab('" + t.Key + "')\">" + t.Name + "<i>" + (t.Key == "all" ? d.Notices.Count : d.Notices.Count(n => State(n) == t.Key)) + "</i></button>")) + "</div>";
        }

        private static string ListHtml(RentData d, List<AdminUser> staff, string term, string tab, int page)
        {
            List<NoticeRec> rows = d.Notices.Where(n => (tab == "all" || State(n) == tab) && (term.Length == 0 || Ui.Has(n.Title, term) || Ui.Has(n.Text, term))).OrderByDescending(n => n.Pinned).ThenByDescending(n => n.From).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"nt-panel nt-empty\">No notices here.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"nt-wrapt\"><table class=\"nt-table\"><thead><tr><th>Notice</th><th>Where</th><th>Shows</th><th>Status</th></tr></thead><tbody>");
            foreach (NoticeRec n in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<tr class=\"nt-row\" onclick=\"AdminNoticesJs.edit('" + Ui.Js(n.Id) + "')\"><td class=\"nt-first\"><b>" + Ui.Esc(n.Title) + "</b><small>" + Ui.Esc(n.Text.Length > 90 ? Ui.Clip(n.Text, 88).TrimEnd() + "…" : n.Text) + "</small></td><td data-label=\"Where\">" + Ui.Esc(Where(d, n)) + "</td><td data-label=\"Shows\">" + Ui.Esc(Fmt.Day(n.From)) + "<small class=\"nt-mini\"><br>" + Ui.Esc(n.Until == null ? "until removed" : "to " + Fmt.Day(n.Until.Value)) + "</small></td><td data-label=\"Status\">" + Pill(State(n)) + (n.Pinned ? "<div class=\"nt-flags\"><span class=\"nt-flag\">Important</span></div>" : string.Empty) + "<small class=\"nt-mini\"><br>" + Ui.Esc(staff.FirstOrDefault(u => u.Id == n.By)?.Name ?? string.Empty) + "</small></td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "notice", "notices"));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response)
        {
            RentData d = await Rent.Load();
            string tab = Ui.Clip(GetDataValue("tab"), 10);
            response.SetElementContents("nt-list", ListHtml(d, await Store.LoadStaff(), Term(GetDataValue("term")), tab, PageNo(GetDataValue("pg"))));
            response.SetElementContents("nt-tabs", TabsHtml(d, tab));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            await Refresh(response);
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
            NoticeRec? n = d.Notices.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && n == null)
            {
                return await Refuse(response, c, "open notice", id, "That notice was not found.");
            }
            if (n == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add notice", string.Empty, "You can view notices but not add them.");
            }
            response.SetElementContents("nt-mbody", FormHtml(c, d, n));
            response.ExecuteScript("AdminNoticesJs.opened('" + Ui.Js(n == null ? "New notice" : n.Title) + "');");
            return response;
        }

        private static string FormHtml(Ctx c, RentData d, NoticeRec? n)
        {
            bool isNew = n == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            NoticeRec r = n ?? new NoticeRec { From = DateTime.Today, Until = DateTime.Today.AddDays(14) };
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<form class=\"nt-stack\" id=\"nt-form\" onsubmit=\"return AdminNoticesJs.save()\" novalidate><input type=\"hidden\" id=\"nt-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            if (!isNew)
            {
                sb.Append("<div class=\"nt-bar\"><span>" + Pill(State(r)) + " " + Ui.Esc(Where(d, r)) + "</span></div>");
            }
            sb.Append(Field("title", "Title", Text("title", r.Title, 80, "text", ro)));
            sb.Append("<label class=\"nt-field\" for=\"nt-f-text\"><span>Message (leave an empty line between paragraphs)</span><textarea id=\"nt-f-text\" data-k=\"text\" rows=\"6\" maxlength=\"2000\"" + ro + ">" + Ui.Esc(r.Text) + "</textarea>" + Err("text") + "</label>");
            sb.Append("<div class=\"nt-field\"><span>Show to residents of</span><div class=\"nt-checks\"><label class=\"nt-check\"><input type=\"checkbox\" data-k=\"pall\" id=\"nt-f-pall\" onchange=\"AdminNoticesJs.allProps(this)\"" + (r.Properties.Count == 0 ? " checked" : string.Empty) + dis + ">All properties</label>" + string.Concat(d.Properties.Select(p => "<label class=\"nt-check\"><input type=\"checkbox\" class=\"nt-pp\" data-k=\"pp_" + Ui.Esc(p.Id) + "\"" + (r.Properties.Contains(p.Id) ? " checked" : string.Empty) + (r.Properties.Count == 0 || !edit ? " disabled" : string.Empty) + ">" + Ui.Esc(p.Name) + "</label>")) + "</div>" + Err("props") + "</div>");
            sb.Append("<div class=\"nt-grid2\">" + Field("from", "Show from", Text("from", Fmt.IsoDay(r.From), 10, "date", ro)) + Field("until", "Show until (optional)", Text("until", r.Until == null ? string.Empty : Fmt.IsoDay(r.Until.Value), 10, "date", ro), "Leave empty to show it until you remove it.") + "</div>");
            sb.Append("<label class=\"nt-check\"><input type=\"checkbox\" id=\"nt-f-pin\" data-k=\"pin\"" + (r.Pinned ? " checked" : string.Empty) + dis + ">Important: show it first and highlighted</label>");
            sb.Append("<div class=\"nt-acts\">" + (edit ? "<button type=\"submit\" class=\"nt-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"nt-btn nt-btn-ghost\" onclick=\"AdminNoticesJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Can(c, 'D') ? "<button type=\"button\" class=\"nt-btn nt-btn-danger nt-push\" onclick=\"AdminNoticesJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Title) + "')\">Delete</button>" : string.Empty) + "</div></form>");
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
                return await Refuse(response, c, isNew ? "add notice" : "edit notice", id, "You do not have the right to " + (isNew ? "add" : "change") + " notices.");
            }
            RentData d = await Rent.Load();
            string title = Ui.Clip(GetDataValue("title"), 80);
            string text = Ui.Clip((GetDataValue("text") ?? string.Empty).Replace("\r", string.Empty), 2000);
            bool all = (GetDataValue("pall") ?? string.Empty) == "1";
            List<string> props = all ? new List<string>() : d.Properties.Where(p => (GetDataValue("pp_" + p.Id) ?? string.Empty) == "1").Select(p => p.Id).ToList();
            bool fOk = Fmt.ParseDate(GetDataValue("from"), out DateTime from);
            string untilText = Ui.Clip(GetDataValue("until"), 10);
            bool uOk = Fmt.ParseDate(untilText, out DateTime until);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["title"] = title.Length < 3 ? "Enter a short title." : string.Empty,
                ["text"] = text.Length < 10 ? "Write the message for residents." : string.Empty,
                ["props"] = !all && props.Count == 0 ? "Choose at least one property, or All properties." : string.Empty,
                ["from"] = !fOk ? "Enter the first day to show it." : string.Empty,
                ["until"] = untilText.Length > 0 && !uOk ? "Enter a valid date, or leave it empty." : untilText.Length > 0 && fOk && until < from ? "The last day cannot be before the first day." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("nt-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<NoticeRec> rows = await Store.Rows<NoticeRec>("notices");
                NoticeRec? n = rows.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    n = new NoticeRec { Id = Fmt.NewId("n-"), Created = DateTime.Now, By = c.U.Id };
                    rows.Add(n);
                }
                if (n == null)
                {
                    return await Refuse(response, c, "edit notice", id, "That notice was not found.");
                }
                n.Title = title;
                n.Text = text;
                n.Properties = props;
                n.From = from.Date;
                n.Until = untilText.Length == 0 ? null : until.Date;
                n.Pinned = (GetDataValue("pin") ?? string.Empty) == "1";
                await Store.SaveRows("notices", rows);
            }
            await Store.Log(c.U.Id, isNew ? "add notice" : "edit notice", title, true);
            await Refresh(response);
            response.ExecuteScript("AdminNoticesJs.saved();");
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
                return await Refuse(response, c, "delete notice", id, "You do not have the right to delete notices.");
            }
            string title;
            using (await Store.Hold())
            {
                List<NoticeRec> rows = await Store.Rows<NoticeRec>("notices");
                NoticeRec? n = rows.FirstOrDefault(x => x.Id == id);
                if (n == null)
                {
                    return await Refuse(response, c, "delete notice", id, "That notice was not found.");
                }
                title = n.Title;
                rows.Remove(n);
                await Store.SaveRows("notices", rows);
            }
            await Store.Log(c.U.Id, "delete notice", title, true);
            await Refresh(response);
            response.ExecuteScript("AdminNoticesJs.deleted();");
            return response;
        }
    }
}
