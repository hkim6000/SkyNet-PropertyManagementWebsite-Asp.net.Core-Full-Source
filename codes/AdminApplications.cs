using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminApplications : WebPage
    {
        private const string AreaKey = "applications";
        private const int Size = 12;
        private static readonly string[] CheckNames = { "Photo ID seen", "Income proof received", "Credit check done", "Background check done", "Landlord reference taken", "Application fee paid" };
        private static readonly (string Key, string Name)[] Tabs = { ("New", "New"), ("Reviewing", "Reviewing"), ("Approved", "Approved"), ("Leased", "Leased"), ("closed", "Declined or withdrawn"), ("all", "All") };

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Applications | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminApplicationsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminApplicationsJs.denied();");
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
            response.SetElementContents("aa-brand", Ui.Esc(site.Brand));
            response.SetElementContents("aa-menu", await Auth.Menu("aa", "AdminApplications", c));
            response.SetElementContents("aa-who", Auth.Who("aa", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminApplicationsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"aa-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"aa-btn aa-btn-ghost aa-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminApplicationsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"aa-btn aa-btn-ghost aa-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminApplicationsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"aa-pill aa-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"aa-err\" id=\"aa-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"aa-field\" for=\"aa-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"aa-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static IEnumerable<ApplicationRec> InTab(IEnumerable<ApplicationRec> q, string tab)
        {
            return tab switch
            {
                "closed" => q.Where(a => a.Status is "Declined" or "Withdrawn"),
                "all" => q,
                _ => q.Where(a => a.Status == tab)
            };
        }

        private static IEnumerable<ApplicationRec> Mine(Ctx c, RentData d, string prop)
        {
            return d.Applications.Where(a => Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, a.UnitId)) && (prop.Length == 0 || Rent.PropertyIdOfUnit(d, a.UnitId) == prop));
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
            string tab = Ui.Clip(GetDataValue("tab"), 12);
            tab = Tabs.Any(t => t.Key == tab) ? tab : "New";
            string code = Ui.Clip(GetDataValue("acode"), 12).ToUpperInvariant();
            List<PropertyRec> mine = d.Properties.Where(p => Access.PropertyOk(c.U, p.Id)).ToList();
            response.SetElementContents("aa-main", "<div class=\"aa-ph\"><div><h1>Applications</h1><p>Rental applications from the website. Review each one, tick the checks as you go, then approve or decline. An approved application becomes a lease in one step.</p></div></div><div id=\"aa-tabs\">" + TabsHtml(c, d, code.Length > 0 ? "all" : tab, string.Empty) + "</div><div class=\"aa-tools\"><input type=\"search\" id=\"aa-q\" placeholder=\"Search by name, email or APP code\" maxlength=\"40\" value=\"" + Ui.Esc(code) + "\" oninput=\"AdminApplicationsJs.type()\" aria-label=\"Search applications\"><select id=\"aa-fp\" onchange=\"AdminApplicationsJs.list(true)\" aria-label=\"Property\">" + Opt(string.Empty, "All properties", string.Empty) + string.Concat(mine.Select(p => Opt(p.Id, p.Name, string.Empty))) + "</select></div><div id=\"aa-list\">" + ListHtml(c, d, code, code.Length > 0 ? "all" : tab, string.Empty, 1) + "</div>");
            if (code.Length > 0)
            {
                ApplicationRec? a = Mine(c, d, string.Empty).FirstOrDefault(x => x.Code == code);
                response.ExecuteScript("AdminApplicationsJs.setTab('all');" + (a != null ? "AdminApplicationsJs.edit('" + Ui.Js(a.Id) + "');" : string.Empty));
            }
            return response;
        }

        private static string TabsHtml(Ctx c, RentData d, string tab, string prop)
        {
            List<ApplicationRec> all = Mine(c, d, prop).ToList();
            return "<div class=\"aa-tabs\" role=\"tablist\">" + string.Concat(Tabs.Select(t => "<button type=\"button\" role=\"tab\" class=\"aa-tab" + (t.Key == tab ? " aa-act" : string.Empty) + "\" aria-selected=\"" + (t.Key == tab ? "true" : "false") + "\" onclick=\"AdminApplicationsJs.tab('" + t.Key + "')\">" + t.Name + (t.Key is "New" or "Reviewing" or "Approved" ? "<i>" + InTab(all, t.Key).Count() + "</i>" : string.Empty) + "</button>")) + "</div>";
        }

        private static decimal Income(ApplicationRec a)
        {
            return a.People.Sum(p => p.Income);
        }

        private static string ListHtml(Ctx c, RentData d, string term, string tab, string prop, int page)
        {
            List<ApplicationRec> rows = InTab(Mine(c, d, prop).Where(a => term.Length == 0 || a.Code.Contains(term, StringComparison.OrdinalIgnoreCase) || a.People.Any(p => Ui.Has(p.First + " " + p.Last, term) || Ui.Has(p.Email, term))), tab).OrderByDescending(a => a.Created).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"aa-panel aa-empty\">No applications here.</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"aa-wrapt\"><table class=\"aa-table\"><thead><tr><th>Application</th><th>Applicants</th><th>Home</th><th>Move in</th><th class=\"aa-num\">Income</th><th>Status</th></tr></thead><tbody>");
            foreach (ApplicationRec a in rows.Skip((page - 1) * Size).Take(Size))
            {
                UnitRec? u = d.Units.FirstOrDefault(x => x.Id == a.UnitId);
                decimal inc = Income(a);
                decimal times = u == null || u.Rent <= 0 ? 0 : inc / u.Rent;
                bool low = u != null && inc > 0 && times < d.Site.IncomeTimes;
                sb.Append("<tr class=\"aa-row\" onclick=\"AdminApplicationsJs.edit('" + Ui.Js(a.Id) + "')\"><td class=\"aa-first\"><b>" + Ui.Esc(a.Code) + "</b><small>" + Ui.Esc(Fmt.Day(a.Created)) + "</small></td><td data-label=\"Applicants\">" + Ui.Esc(Rent.ApplicantName(a)) + "<small class=\"aa-mini\"><br>" + Ui.Esc(a.People.FirstOrDefault()?.Email ?? string.Empty) + "</small></td><td data-label=\"Home\">" + Ui.Esc(Rent.UnitLabel(d, a.UnitId)) + "</td><td data-label=\"Move in\">" + Ui.Esc(Fmt.Day(a.MoveIn)) + "</td><td class=\"aa-num\" data-label=\"Income\">" + (inc > 0 ? Fmt.Whole(inc) + "<small class=\"aa-mini\"><br>" + times.ToString("0.0", Fmt.Us) + "x rent</small>" : "-") + "</td><td data-label=\"Status\">" + Pill(a.Status) + (low ? "<div class=\"aa-flags\"><span class=\"aa-flag\">Low income</span></div>" : string.Empty) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "application", "applications"));
            return sb.ToString();
        }

        private async Task<string> ListNow(Ctx c)
        {
            RentData d = await Rent.Load();
            return ListHtml(c, d, Term(GetDataValue("term")), Ui.Clip(GetDataValue("tab"), 12), Ui.Clip(GetDataValue("prop"), 60), PageNo(GetDataValue("pg")));
        }

        private async Task Refresh(ApiResponse response, Ctx c)
        {
            response.SetElementContents("aa-list", await ListNow(c));
            RentData d = await Rent.Load();
            response.SetElementContents("aa-tabs", TabsHtml(c, d, Ui.Clip(GetDataValue("tab"), 12), Ui.Clip(GetDataValue("prop"), 60)));
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

        private async Task<(RentData D, ApplicationRec? A)> Find(Ctx c)
        {
            string id = Ui.Clip(GetDataValue("id"), 60);
            RentData d = await Rent.Load();
            ApplicationRec? a = d.Applications.FirstOrDefault(x => x.Id == id);
            return a == null || !Access.PropertyOk(c.U, Rent.PropertyIdOfUnit(d, a.UnitId)) ? (d, null) : (d, a);
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            (RentData d, ApplicationRec? a) = await Find(c);
            if (a == null)
            {
                return await Refuse(response, c, "open application", Ui.Clip(GetDataValue("id"), 60), "That application was not found.");
            }
            response.SetElementContents("aa-mbody", FormHtml(c, d, a, await Store.LoadStaff()));
            response.ExecuteScript("AdminApplicationsJs.opened('" + Ui.Js(a.Code + " · " + Rent.ApplicantName(a)) + "');");
            return response;
        }

        private static string FormHtml(Ctx c, RentData d, ApplicationRec a, List<AdminUser> staff)
        {
            bool edit = Can(c, 'E');
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == a.UnitId);
            decimal inc = Income(a);
            decimal need = u == null ? 0 : u.Rent * d.Site.IncomeTimes;
            bool open = a.Status is "New" or "Reviewing" or "Approved";
            string dis = edit && a.Status != "Leased" ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<div class=\"aa-stack\" id=\"aa-form\"><input type=\"hidden\" id=\"aa-f-id\" value=\"" + Ui.Esc(a.Id) + "\">");
            sb.Append("<div class=\"aa-bar\"><span>" + Pill(a.Status) + " Sent " + Ui.Esc(Fmt.When(a.Created)) + (a.DecidedAt != null ? " &middot; " + Ui.Esc(a.Status == "Leased" ? "approved" : a.Status.ToLowerInvariant()) + " by " + Ui.Esc(staff.FirstOrDefault(s => s.Id == a.DecidedBy)?.Name ?? "staff") + " " + Ui.Esc(Fmt.Day(a.DecidedAt.Value)) : string.Empty) + "</span><span class=\"aa-acts-row\">");
            if (edit)
            {
                if (a.Status == "New")
                {
                    sb.Append("<button type=\"button\" class=\"aa-btn aa-btn-ghost aa-btn-sm\" onclick=\"AdminApplicationsJs.decide('Reviewing')\">Start review</button>");
                }
                if (a.Status is "New" or "Reviewing")
                {
                    sb.Append("<button type=\"button\" class=\"aa-btn aa-btn-sm\" onclick=\"AdminApplicationsJs.decide('Approved')\">Approve</button><button type=\"button\" class=\"aa-btn aa-btn-ghost aa-btn-sm\" onclick=\"AdminApplicationsJs.decide('Declined')\">Decline</button>");
                }
                if (open)
                {
                    sb.Append("<button type=\"button\" class=\"aa-btn aa-btn-ghost aa-btn-sm\" onclick=\"AdminApplicationsJs.decide('Withdrawn')\">Withdrawn</button>");
                }
                if (a.Status is "Declined" or "Withdrawn")
                {
                    sb.Append("<button type=\"button\" class=\"aa-btn aa-btn-ghost aa-btn-sm\" onclick=\"AdminApplicationsJs.decide('Reviewing')\">Reopen</button>");
                }
            }
            if (a.Status == "Leased" && Access.Can(c.U, "leases", 'V'))
            {
                LeaseRec? l = d.Leases.FirstOrDefault(x => x.Id == a.LeaseId);
                if (l != null)
                {
                    sb.Append("<a class=\"aa-btn aa-btn-ghost aa-btn-sm\" href=\"AdminLeases?code=" + Ui.Esc(l.Code) + "\">Open lease " + Ui.Esc(l.Code) + "</a>");
                }
            }
            sb.Append("</span></div>");
            if (a.Status == "Approved" && Access.Can(c.U, "leases", 'A') && u != null)
            {
                DateTime end = a.MoveIn.AddMonths(a.Months).AddDays(-1);
                bool clash = Rent.Overlaps(d, u.Id, a.MoveIn, end, string.Empty);
                sb.Append("<div class=\"aa-panel aa-lease\" id=\"aa-mk\"><h3>Create the lease</h3><p class=\"aa-mini\">Everyone on the application becomes a resident with a portal activation code. The deposit and the first month's rent are charged on the start date." + (clash ? " <b>Another lease covers some of these dates; change them before creating.</b>" : string.Empty) + "</p><div class=\"aa-grid2\">" + Field("lstart", "Start", Text("lstart", Fmt.IsoDay(a.MoveIn), 10, "date")) + Field("lend", "End", Text("lend", Fmt.IsoDay(end), 10, "date")) + "</div><div class=\"aa-grid2\">" + Field("lrent", "Rent a month ($)", Text("lrent", Num(u.Rent), 10, "text", " inputmode=\"decimal\"")) + Field("ldep", "Deposit ($)", Text("ldep", Num(u.Deposit), 10, "text", " inputmode=\"decimal\"")) + "</div><div class=\"aa-acts\"><button type=\"button\" class=\"aa-btn\" onclick=\"AdminApplicationsJs.makeLease()\">Create lease</button></div></div>");
            }
            sb.Append("<div class=\"aa-grid2 aa-cols\"><div class=\"aa-sum\"><div><span>Home</span><b>" + Ui.Esc(Rent.UnitLabel(d, a.UnitId)) + "</b></div>" + (u != null ? "<div><span>Unit status</span><span>" + Ui.Esc(u.Status) + "</span></div><div><span>Rent and deposit</span><span>" + Fmt.Whole(u.Rent) + " / " + Fmt.Whole(u.Deposit) + "</span></div>" : string.Empty) + "<div><span>Move in</span><span>" + Ui.Esc(Fmt.Day(a.MoveIn)) + "</span></div><div><span>Lease length</span><span>" + a.Months + " months</span></div><div><span>People living there</span><span>" + a.Occupants + "</span></div><div><span>Pets</span><span>" + Ui.Esc(a.Pets.Length > 0 ? a.Pets : "None") + "</span></div></div>");
            sb.Append("<div class=\"aa-sum\"><div><span>Household income</span><b>" + (inc > 0 ? Fmt.Whole(inc) + " a month" : "Not given") + "</b></div><div><span>Wanted (" + d.Site.IncomeTimes.ToString("0.#", Fmt.Us) + "x rent)</span><span>" + Fmt.Whole(need) + "</span></div><div><span>Result</span>" + (inc <= 0 ? "<span>Ask for proof</span>" : inc >= need ? "<span class=\"aa-goodtxt\">Meets the rule</span>" : "<span class=\"aa-badtxt\">Below by " + Fmt.Whole(need - inc) + "</span>") + "</div><div><span>Lives at</span><span>" + Ui.Esc(a.Address) + "</span></div><div><span>Landlord</span><span>" + Ui.Esc(a.Landlord.Length > 0 ? a.Landlord + (a.LandlordPhone.Length > 0 ? ", " + a.LandlordPhone : string.Empty) : "Not given") + "</span></div><div><span>Years there</span><span>" + (a.YearsAt <= 0 ? "Less than 1" : a.YearsAt >= 5 ? "5 or more" : a.YearsAt.ToString(CultureInfo.InvariantCulture)) + "</span></div></div></div>");
            sb.Append("<div class=\"aa-wrapt\"><table class=\"aa-table aa-ptable2\"><thead><tr><th>Applicant</th><th>Contact</th><th class=\"aa-num\">Age</th><th>Employer or school</th><th class=\"aa-num\">Income</th></tr></thead><tbody>");
            foreach (Applicant p in a.People)
            {
                sb.Append("<tr><td class=\"aa-first\"><b>" + Ui.Esc(p.First + " " + p.Last) + "</b></td><td data-label=\"Contact\"><a href=\"mailto:" + Ui.Esc(p.Email) + "\">" + Ui.Esc(p.Email) + "</a>" + (p.Phone.Length > 0 ? "<small class=\"aa-mini\"><br>" + Ui.Esc(p.Phone) + "</small>" : string.Empty) + "</td><td class=\"aa-num\" data-label=\"Age\">" + (p.Birth != null ? Fmt.Age(p.Birth.Value, DateTime.Today).ToString(CultureInfo.InvariantCulture) : "-") + "</td><td data-label=\"Employer\">" + Ui.Esc(p.Employer.Length > 0 ? p.Employer : "-") + "</td><td class=\"aa-num\" data-label=\"Income\">" + (p.Income > 0 ? Fmt.Whole(p.Income) : "-") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            if (a.Message.Length > 0)
            {
                sb.Append("<div class=\"aa-field\"><span>Message from the applicant</span><p class=\"aa-quote\">" + Ui.Esc(a.Message).Replace("\n", "<br>") + "</p></div>");
            }
            sb.Append("<div class=\"aa-field\"><span>Checks (" + a.Checks.Count(x => CheckNames.Contains(x)) + " of " + CheckNames.Length + ")</span><div class=\"aa-checks\">" + string.Concat(CheckNames.Select((n, i) => "<label class=\"aa-check\"><input type=\"checkbox\" data-k=\"c" + i + "\"" + (a.Checks.Contains(n) ? " checked" : string.Empty) + dis + ">" + Ui.Esc(n) + "</label>")) + "</div></div>");
            sb.Append("<label class=\"aa-field\" for=\"aa-f-dnote\"><span>Decision note (the reason is required to decline)</span><textarea id=\"aa-f-dnote\" data-k=\"dnote\" rows=\"3\" maxlength=\"800\"" + (dis.Length > 0 ? " readonly" : string.Empty) + ">" + Ui.Esc(a.Decision) + "</textarea>" + Err("dnote") + "</label>");
            sb.Append("<div class=\"aa-acts\">" + (dis.Length == 0 ? "<button type=\"button\" class=\"aa-btn\" onclick=\"AdminApplicationsJs.save()\">Save checks and note</button>" : string.Empty) + "<button type=\"button\" class=\"aa-btn aa-btn-ghost\" onclick=\"AdminApplicationsJs.closeModal()\">Close</button>" + (Can(c, 'D') && a.Status != "Leased" ? "<button type=\"button\" class=\"aa-btn aa-btn-danger aa-push\" onclick=\"AdminApplicationsJs.del('" + Ui.Js(a.Id) + "','" + Ui.Js(a.Code) + "')\">Delete</button>" : string.Empty) + "</div></div>");
            return sb.ToString();
        }

        private List<string> ChecksIn()
        {
            List<string> list = new List<string>();
            for (int i = 0; i < CheckNames.Length; i++)
            {
                if ((GetDataValue("c" + i) ?? string.Empty) == "1")
                {
                    list.Add(CheckNames[i]);
                }
            }
            return list;
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
                return await Refuse(response, c, "edit application", string.Empty, "You do not have the right to change applications.");
            }
            (RentData d0, ApplicationRec? found) = await Find(c);
            if (found == null || found.Status == "Leased")
            {
                return await Refuse(response, c, "edit application", Ui.Clip(GetDataValue("id"), 60), found == null ? "That application was not found." : "This application is already a lease.");
            }
            string note = Ui.Clip((GetDataValue("dnote") ?? string.Empty).Replace("\r", string.Empty), 800);
            List<string> checks = ChecksIn();
            using (await Store.Hold())
            {
                List<ApplicationRec> all = await Store.Rows<ApplicationRec>("applications");
                ApplicationRec? a = all.FirstOrDefault(x => x.Id == found.Id);
                if (a != null)
                {
                    a.Checks = checks;
                    a.Decision = note;
                    if (a.Status == "New" && checks.Count > 0)
                    {
                        a.Status = "Reviewing";
                    }
                    await Store.SaveRows("applications", all);
                }
            }
            await Store.Log(c.U.Id, "edit application", found.Code, true);
            await Refresh(response, c);
            (RentData d, ApplicationRec? again) = await Find(c);
            if (again != null)
            {
                response.SetElementContents("aa-mbody", FormHtml(c, d, again, await Store.LoadStaff()));
            }
            response.ExecuteScript("AdminApplicationsJs.clean();AdminApplicationsJs.toast('Saved');");
            return response;
        }

        public async Task<ApiResponse> Decide()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Can(c, 'E'))
            {
                return await Refuse(response, c, "decide application", string.Empty, "You do not have the right to change applications.");
            }
            (RentData d0, ApplicationRec? found) = await Find(c);
            if (found == null)
            {
                return await Refuse(response, c, "decide application", Ui.Clip(GetDataValue("id"), 60), "That application was not found.");
            }
            string act = Ui.Clip(GetDataValue("act"), 20);
            string note = Ui.Clip((GetDataValue("dnote") ?? string.Empty).Replace("\r", string.Empty), 800);
            bool ok = act switch
            {
                "Reviewing" => found.Status is "New" or "Declined" or "Withdrawn",
                "Approved" or "Declined" => found.Status is "New" or "Reviewing",
                "Withdrawn" => found.Status is "New" or "Reviewing" or "Approved",
                _ => false
            };
            if (!ok)
            {
                return await Refuse(response, c, "decide application", found.Code, "That step is not possible for an application that is " + found.Status.ToLowerInvariant() + ".");
            }
            if (act == "Declined" && note.Length < 5)
            {
                response.SetElementContents("aa-e-dnote", "Write the reason for declining first. Keep it factual.");
                return response;
            }
            if (act == "Approved")
            {
                UnitRec? u = d0.Units.FirstOrDefault(x => x.Id == found.UnitId);
                ApplicationRec? other = d0.Applications.FirstOrDefault(x => x.Id != found.Id && x.UnitId == found.UnitId && x.Status == "Approved");
                if (other != null)
                {
                    return await Refuse(response, c, "approve application", found.Code, other.Code + " is already approved for this home. Withdraw it first, or create its lease.");
                }
                if (u == null)
                {
                    return await Refuse(response, c, "approve application", found.Code, "The home on this application no longer exists.");
                }
            }
            List<string> checks = ChecksIn();
            using (await Store.Hold())
            {
                List<ApplicationRec> all = await Store.Rows<ApplicationRec>("applications");
                ApplicationRec? a = all.FirstOrDefault(x => x.Id == found.Id);
                if (a != null)
                {
                    a.Status = act;
                    a.Checks = checks;
                    a.Decision = note;
                    if (act is "Approved" or "Declined" or "Withdrawn")
                    {
                        a.DecidedBy = c.U.Id;
                        a.DecidedAt = DateTime.Now;
                    }
                    else
                    {
                        a.DecidedBy = string.Empty;
                        a.DecidedAt = null;
                    }
                    await Store.SaveRows("applications", all);
                }
            }
            await Store.Log(c.U.Id, act switch { "Reviewing" => "review application", "Approved" => "approve application", "Declined" => "decline application", _ => "withdraw application" }, found.Code, true);
            await Refresh(response, c);
            (RentData d, ApplicationRec? again) = await Find(c);
            if (again != null)
            {
                response.SetElementContents("aa-mbody", FormHtml(c, d, again, await Store.LoadStaff()));
            }
            response.ExecuteScript("AdminApplicationsJs.clean();AdminApplicationsJs.toast('" + Ui.Js(found.Code + (act == "Reviewing" ? " is under review." : " is " + act.ToLowerInvariant() + ".")) + "');");
            return response;
        }

        public async Task<ApiResponse> MakeLease()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, "leases", 'A'))
            {
                return await Refuse(response, c, "create lease", string.Empty, "You do not have the right to create leases.");
            }
            (RentData d0, ApplicationRec? a) = await Find(c);
            if (a == null || a.Status != "Approved")
            {
                return await Refuse(response, c, "create lease", Ui.Clip(GetDataValue("id"), 60), a == null ? "That application was not found." : "Only an approved application can become a lease.");
            }
            bool sOk = Fmt.ParseDate(GetDataValue("lstart"), out DateTime start);
            bool eOk = Fmt.ParseDate(GetDataValue("lend"), out DateTime end);
            decimal rent = Dec(GetDataValue("lrent"));
            decimal dep = Dec(GetDataValue("ldep"));
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["lstart"] = !sOk ? "Enter the start date." : start < DateTime.Today.AddDays(-30) ? "The start date is too far in the past." : string.Empty,
                ["lend"] = !eOk ? "Enter the end date." : sOk && end <= start.AddDays(27) ? "The lease must run at least four weeks." : sOk && end > start.AddYears(3) ? "Keep the lease to three years or less." : string.Empty,
                ["lrent"] = rent <= 0 || rent > 100000 ? "Enter the monthly rent." : string.Empty,
                ["ldep"] = dep < 0 || dep > 100000 ? "Enter the deposit (0 if none)." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("aa-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            (string err, LeaseRec? lease, List<ResidentRec> invited) = await Leasing.Create(new NewLease { UnitId = a.UnitId, Start = start, End = end, Rent = rent, Deposit = dep, Pets = a.Pets, People = a.People, ApplicationId = a.Id, By = c.U.Id });
            if (err.Length > 0 || lease == null)
            {
                response.SetElementContents("aa-e-lstart", Ui.Esc(err));
                return response;
            }
            await Store.Log(c.U.Id, "create lease", lease.Code + " from " + a.Code, true);
            await Refresh(response, c);
            StringBuilder sb = new StringBuilder("<div class=\"aa-stack\"><p class=\"aa-ok\">Lease " + Ui.Esc(lease.Code) + " is created for " + Ui.Esc(Rent.UnitLabel(d0, lease.UnitId)) + ", " + Ui.Esc(Fmt.Day(lease.Start) + " to " + Fmt.Day(lease.End)) + ".</p>");
            if (invited.Count > 0)
            {
                sb.Append("<p>Give each new resident their portal activation code. They use it with their email on the Resident portal page.</p><div class=\"aa-wrapt\"><table class=\"aa-table\"><thead><tr><th>Resident</th><th>Email</th><th>Activation code</th></tr></thead><tbody>" + string.Concat(invited.Select(r => "<tr><td class=\"aa-first\"><b>" + Ui.Esc(Rent.Name(r)) + "</b></td><td data-label=\"Email\">" + Ui.Esc(r.Email) + "</td><td data-label=\"Code\"><b class=\"aa-codebig\">" + Ui.Esc(r.Invite) + "</b></td></tr>")) + "</tbody></table></div>");
            }
            sb.Append("<div class=\"aa-acts\">" + (Access.Can(c.U, "leases", 'V') ? "<a class=\"aa-btn\" href=\"AdminLeases?code=" + Ui.Esc(lease.Code) + "\">Open the lease</a>" : string.Empty) + "<button type=\"button\" class=\"aa-btn aa-btn-ghost\" onclick=\"AdminApplicationsJs.closeModal()\">Close</button></div></div>");
            response.SetElementContents("aa-mbody", sb.ToString());
            response.ExecuteScript("AdminApplicationsJs.clean();AdminApplicationsJs.toast('Lease created');");
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
                return await Refuse(response, c, "delete application", string.Empty, "You do not have the right to delete applications.");
            }
            (RentData d, ApplicationRec? a) = await Find(c);
            if (a == null || a.Status == "Leased")
            {
                return await Refuse(response, c, "delete application", Ui.Clip(GetDataValue("id"), 60), a == null ? "That application was not found." : "This application became a lease, so it stays in the records.");
            }
            using (await Store.Hold())
            {
                List<ApplicationRec> all = await Store.Rows<ApplicationRec>("applications");
                all.RemoveAll(x => x.Id == a.Id);
                await Store.SaveRows("applications", all);
            }
            await Store.Log(c.U.Id, "delete application", a.Code, true);
            await Refresh(response, c);
            response.ExecuteScript("AdminApplicationsJs.deleted();");
            return response;
        }
    }
}
