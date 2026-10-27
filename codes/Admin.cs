using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class Admin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Dashboard | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
            }
            return c;
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("db-brand", Ui.Esc(site.Brand));
            response.SetElementContents("db-menu", await Auth.Menu("db", "Admin", c));
            response.SetElementContents("db-who", Auth.Who("db", c));
        }

        private static string Tile(string href, int number, string label, string note, bool warn)
        {
            return "<a class=\"db-tile" + (warn && number > 0 ? " db-warn" : string.Empty) + "\" href=\"" + href + "\"><b>" + number.ToString(CultureInfo.InvariantCulture) + "</b><span>" + Ui.Esc(label) + "</span><small>" + Ui.Esc(note) + "</small></a>";
        }

        private static string Pill(string status)
        {
            string k = status switch
            {
                "Confirmed" or "Active" => "go",
                "Requested" or "Move-in due" => "wait",
                "Moving in" => "new",
                _ => "off"
            };
            return "<span class=\"db-pill db-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string WoPill(WorkOrderRec w)
        {
            string label = Maint.Overdue(w) ? "Overdue" : w.Priority == "Emergency" ? "Emergency" : w.Status == "New" ? "Not assigned" : w.Priority;
            string k = label is "Overdue" or "Emergency" ? "bad" : label == "High" ? "wait" : "new";
            return "<span class=\"db-pill db-pill-" + k + "\">" + Ui.Esc(label) + "</span>";
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
            DateTime today = DateTime.Today;
            DateTime now = DateTime.Now;
            List<UnitRec> units = d.Units.Where(u => Access.PropertyOk(c.U, u.PropertyId)).ToList();
            HashSet<string> mineU = new HashSet<string>(units.Select(u => u.Id));
            List<LeaseRec> leases = d.Leases.Where(l => mineU.Contains(l.UnitId)).ToList();
            List<ShowingRec> shows = d.Showings.Where(s => mineU.Contains(s.UnitId)).ToList();
            List<ApplicationRec> apps = d.Applications.Where(a => mineU.Contains(a.UnitId)).ToList();
            bool unitsV = Access.Can(c.U, "units", 'V');
            bool showV = Access.Can(c.U, "showings", 'V');
            bool appV = Access.Can(c.U, "applications", 'V');
            bool leaseV = Access.Can(c.U, "leases", 'V');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"db-ph\"><div><h1>Dashboard</h1><p>Welcome, " + Ui.Esc(c.U.Name) + ". " + Ui.Esc(Access.Describe(c.U, d.Properties)) + " &middot; " + Ui.Esc(Fmt.Day(today)) + "</p></div><a class=\"db-btn db-btn-ghost\" href=\"Home\" target=\"_blank\" rel=\"noopener\">View the website</a></div>");
            StringBuilder tiles = new StringBuilder();
            if (unitsV)
            {
                int occ = units.Count(u => u.Status is "Occupied" or "Notice");
                int pct = units.Count == 0 ? 0 : (int)Math.Round(occ * 100.0 / units.Count);
                tiles.Append("<a class=\"db-tile\" href=\"AdminUnits\"><b>" + pct + "%</b><span>occupied</span><small>" + occ + " of " + Ui.Count(units.Count, "unit", "units") + "</small></a>");
                tiles.Append(Tile("AdminUnits?st=Vacant", units.Count(u => u.Status == "Vacant"), "vacant", units.Count(u => u.Status == "Vacant" && !u.Listed) + " not on the website", true));
                tiles.Append(Tile("AdminUnits?st=Make-ready", units.Count(u => u.Status == "Make-ready"), "make-ready", "Being cleaned or repaired", true));
                tiles.Append(Tile("AdminUnits?st=Notice", units.Count(u => u.Status == "Notice"), "on notice", "Residents moving out", false));
            }
            if (showV)
            {
                tiles.Append(Tile("AdminShowings", shows.Count(s => s.When.Date == today && s.Status is "Requested" or "Confirmed"), "showings today", Ui.Count(shows.Count(s => s.When.Date > today && s.When.Date <= today.AddDays(7) && s.Status is "Requested" or "Confirmed"), "more this week", "more this week"), false));
                tiles.Append(Tile("AdminShowings", shows.Count(s => s.Status == "Requested"), "tour requests", "Waiting to be confirmed", true));
            }
            if (appV)
            {
                tiles.Append(Tile("AdminApplications", apps.Count(a => a.Status == "New"), apps.Count(a => a.Status == "New") == 1 ? "new application" : "new applications", apps.Count(a => a.Status == "Reviewing") + " in review, " + apps.Count(a => a.Status == "Approved") + " approved", true));
            }
            if (leaseV)
            {
                int e30 = leases.Count(l => Rent.Expiring(l, 30));
                int e60 = leases.Count(l => Rent.Expiring(l, 60));
                int e90 = leases.Count(l => Rent.Expiring(l, 90));
                tiles.Append(Tile("AdminLeases", e90, e90 == 1 ? "lease ends in 90 days" : "leases end in 90 days", e30 + " in 30 days, " + e60 + " in 60 days", e30 > 0));
                tiles.Append(Tile("AdminLeases", leases.Count(Rent.Holdover), "month to month", "Past the end date, not renewed", true));
                tiles.Append(Tile("AdminLeases", leases.Count(l => l.Status == "Upcoming" && l.RenewalOf.Length == 0 && l.Start.Date <= today.AddDays(14)), "move-ins in 14 days", leases.Count(l => l.Status == "Upcoming" && l.RenewalOf.Length == 0 && l.Start.Date < today) + " overdue", leases.Any(l => l.Status == "Upcoming" && l.RenewalOf.Length == 0 && l.Start.Date < today)));
                List<LeaseRec> owing = leases.Where(l => Rent.Live.Contains(l.Status) && Rent.Balance(d, l.Id) > 0.004m).ToList();
                decimal owed = owing.Sum(l => Rent.Balance(d, l.Id));
                tiles.Append("<a class=\"db-tile" + (owed > 0 ? " db-warn" : string.Empty) + "\" href=\"" + (Access.Can(c.U, "payments", 'V') ? "AdminPayments" : "AdminLeases") + "\"><b>" + Fmt.Whole(owed) + "</b><span>owed by residents</span><small>" + Ui.Count(owing.Count, "lease", "leases") + " with a balance</small></a>");
            }
            if (Access.Can(c.U, "payments", 'V'))
            {
                HashSet<string> lids = new HashSet<string>(leases.Select(l => l.Id));
                DateTime m0 = new DateTime(today.Year, today.Month, 1);
                decimal billed = d.Ledger.Where(x => lids.Contains(x.LeaseId) && x.Kind == "Rent" && x.When >= m0).Sum(x => x.Amount);
                decimal got = -d.Ledger.Where(x => lids.Contains(x.LeaseId) && x.Kind == "Payment" && x.When >= m0).Sum(x => x.Amount);
                int pct = billed <= 0 ? 0 : (int)Math.Round(Math.Min(got, billed) * 100m / billed);
                tiles.Append("<a class=\"db-tile\" href=\"AdminPayments\"><b>" + Fmt.Whole(got) + "</b><span>collected this month</span><small>" + pct + "% of " + Fmt.Money(billed) + " rent charged</small></a>");
            }
            if (Access.Can(c.U, "payouts", 'V') || Access.Can(c.U, "owners", 'V'))
            {
                DateTime last = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                List<Statement> st = d.Owners.Where(o => Books.HadStake(d, o, last, last.AddMonths(1).AddDays(-1))).Select(o => Books.For(d, o, last)).ToList();
                int toPay = st.Count(x => x.Due > 0.004m);
                tiles.Append(Tile(Access.Can(c.U, "payouts", 'V') ? "AdminPayouts" : "AdminOwners", toPay, toPay == 1 ? "owner payout to send" : "owner payouts to send", last.ToString("MMMM", Fmt.Us) + ": " + Fmt.Money(st.Where(x => x.Due > 0).Sum(x => x.Due)) + " net", toPay > 0));
            }
            if (Access.Can(c.U, "workorders", 'V'))
            {
                List<WorkOrderRec> open = d.WorkOrders.Where(w => Access.PropertyOk(c.U, w.PropertyId) && Maint.Open(w)).ToList();
                int emerg = open.Count(w => w.Priority == "Emergency");
                tiles.Append(Tile("AdminWorkOrders", open.Count, open.Count == 1 ? "open work order" : "open work orders", emerg + " emergency, " + open.Count(w => w.Status == "New") + " not assigned yet", emerg > 0));
                int late = open.Count(Maint.Overdue);
                tiles.Append(Tile("AdminWorkOrders?st=overdue", late, "overdue", "Work orders past their due time", true));
            }
            if (Access.Can(c.U, "turns", 'V'))
            {
                List<TurnRec> turns = d.Turns.Where(t => t.Status == "Open" && mineU.Contains(t.UnitId)).ToList();
                tiles.Append(Tile("AdminTurns", turns.Count, turns.Count == 1 ? "home being made ready" : "homes being made ready", turns.Count(t => t.Target.Date < today) + " past the target date", turns.Any(t => t.Target.Date < today)));
            }
            if (Access.Can(c.U, "inspections", 'V'))
            {
                List<InspectionRec> ins = d.Inspections.Where(i => mineU.Contains(i.UnitId) && i.Status == "Scheduled").ToList();
                tiles.Append(Tile("AdminInspections", ins.Count(i => i.Date.Date >= today && i.Date.Date < today.AddDays(7)), "inspections this week", ins.Count(i => i.Date.Date < today) + " late", ins.Any(i => i.Date.Date < today)));
            }
            if (Access.Can(c.U, "messages", 'V'))
            {
                List<InquiryRec> m = await Store.Rows<InquiryRec>("messages");
                int fresh = m.Count(x => x.Status == "New");
                tiles.Append(Tile("AdminMessages", fresh, fresh == 1 ? "new message" : "new messages", m.Count(x => x.Status == "New" && x.Topic == "Owner inquiry") + " from owners", false));
            }
            if (tiles.Length > 0)
            {
                sb.Append("<div class=\"db-tiles\">" + tiles + "</div>");
            }
            if (showV || leaseV)
            {
                sb.Append("<div class=\"db-cols\">");
                if (showV)
                {
                    List<ShowingRec> next = shows.Where(s => s.When >= today && s.When < today.AddDays(3) && s.Status is "Requested" or "Confirmed").OrderBy(s => s.When).ToList();
                    sb.Append("<section class=\"db-panel\"><div class=\"db-sh\"><h2>Showings, next 3 days</h2><a class=\"db-link\" href=\"AdminShowings\">Showings</a></div>" + (next.Count == 0 ? "<p class=\"db-hint\">No showings booked.</p>" : "<ul class=\"db-feed\">" + string.Concat(next.Take(8).Select(s => "<li><a href=\"AdminShowings\"><b>" + Ui.Esc(Fmt.When(s.When)) + "</b>" + Pill(s.Status) + "<small>" + Ui.Esc(s.Name + " · " + Rent.UnitLabel(d, s.UnitId)) + "</small></a></li>")) + (next.Count > 8 ? "<li><small>And " + (next.Count - 8) + " more</small></li>" : string.Empty) + "</ul>") + "</section>");
                }
                if (leaseV)
                {
                    List<(DateTime When, string Kind, LeaseRec L)> moves = leases.Where(l => l.Status == "Upcoming" && l.RenewalOf.Length == 0 && l.Start.Date <= today.AddDays(14)).Select(l => (l.Start, l.Start.Date < today ? "Move-in due" : "Moving in", l)).Concat(leases.Where(l => l.Status == "Notice" && l.MoveOut != null && l.MoveOut.Value.Date <= today.AddDays(14)).Select(l => (l.MoveOut!.Value, "Moving out", l))).OrderBy(x => x.Item1).ToList();
                    sb.Append("<section class=\"db-panel\"><div class=\"db-sh\"><h2>Moves, next 14 days</h2><a class=\"db-link\" href=\"AdminLeases\">Leases</a></div>" + (moves.Count == 0 ? "<p class=\"db-hint\">No move-ins or move-outs coming up.</p>" : "<ul class=\"db-feed\">" + string.Concat(moves.Take(8).Select(x => "<li><a href=\"AdminLeases?code=" + Ui.Esc(x.L.Code) + "\"><b>" + Ui.Esc(Fmt.Day(x.When) + " · " + x.L.Code) + "</b>" + Pill(x.Kind) + "<small>" + Ui.Esc(Rent.Names(d, x.L) + " · " + Rent.UnitLabel(d, x.L.UnitId)) + "</small></a></li>")) + "</ul>") + "</section>");
                }
                sb.Append("</div>");
            }
            if (Access.Can(c.U, "workorders", 'V'))
            {
                List<WorkOrderRec> hot = d.WorkOrders.Where(w => Access.PropertyOk(c.U, w.PropertyId) && Maint.Open(w) && (w.Priority is "Emergency" or "High" || Maint.Overdue(w) || w.Status == "New")).OrderBy(w => Array.IndexOf(Maint.Priorities, w.Priority)).ThenBy(w => w.Due).ToList();
                sb.Append("<section class=\"db-panel\"><div class=\"db-sh\"><h2>Work orders that need attention</h2><a class=\"db-link\" href=\"AdminWorkOrders\">Work orders</a></div>" + (hot.Count == 0 ? "<p class=\"db-hint\">Nothing urgent. Every request is assigned and on time.</p>" : "<ul class=\"db-feed\">" + string.Concat(hot.Take(6).Select(w => "<li><a href=\"AdminWorkOrders?code=" + Ui.Esc(w.Code) + "\"><b>" + Ui.Esc(w.Code + " · " + w.Title) + "</b>" + WoPill(w) + "<small>" + Ui.Esc(Maint.Place(d, w) + " · due " + Fmt.When(w.Due)) + "</small></a></li>")) + (hot.Count > 6 ? "<li><small>And " + (hot.Count - 6) + " more</small></li>" : string.Empty) + "</ul>") + "</section>");
            }
            if (unitsV)
            {
                string[] cols = { "Occupied", "Notice", "Vacant", "Make-ready", "Offline" };
                sb.Append("<section class=\"db-panel\"><div class=\"db-sh\"><h2>Units by property</h2><a class=\"db-link\" href=\"AdminUnits\">Units</a></div><div class=\"db-wrapt\"><table class=\"db-table\"><thead><tr><th>Property</th>" + string.Concat(cols.Select(x => "<th class=\"db-num\">" + x + "</th>")) + "<th class=\"db-num\">Rent roll</th></tr></thead><tbody>");
                foreach (PropertyRec p in d.Properties.Where(x => Access.PropertyOk(c.U, x.Id)))
                {
                    List<UnitRec> pu = units.Where(u => u.PropertyId == p.Id).ToList();
                    decimal roll = leases.Where(l => Rent.Live.Contains(l.Status) && pu.Any(u => u.Id == l.UnitId)).Sum(l => l.Rent);
                    sb.Append("<tr><td class=\"db-first\"><b>" + Ui.Esc(p.Name) + "</b><small>" + Ui.Count(pu.Count, "unit", "units") + (p.Show ? string.Empty : " · hidden") + "</small></td>" + string.Concat(cols.Select(x => "<td class=\"db-num\" data-label=\"" + x + "\">" + pu.Count(u => u.Status == x) + "</td>")) + "<td class=\"db-num\" data-label=\"Rent roll\">" + Fmt.Whole(roll) + "</td></tr>");
                }
                sb.Append("</tbody></table></div></section>");
            }
            if (Access.Can(c.U, "activity", 'V'))
            {
                List<ActivityRec> log = await Store.Rows<ActivityRec>("activity");
                StringBuilder feed = new StringBuilder("<ul class=\"db-feed\">");
                foreach (ActivityRec a in log.Take(6))
                {
                    feed.Append("<li><div><b>" + Ui.Esc(a.Action) + "</b><span class=\"db-pill " + (a.Ok ? "db-pill-go" : "db-pill-bad") + "\">" + (a.Ok ? "Done" : "Refused") + "</span><small>" + Ui.Esc(a.Target) + " &middot; " + Ui.Esc(await Store.Who(a.UserId)) + " &middot; " + Ui.Esc(Ui.When(a.When)) + "</small></div></li>");
                }
                sb.Append("<section class=\"db-panel\"><div class=\"db-sh\"><h2>Recent activity</h2><a class=\"db-link\" href=\"AdminActivity\">Activity log</a></div>" + (log.Count == 0 ? "<p class=\"db-hint\">Nothing has been logged yet.</p>" : feed + "</ul>") + "</section>");
            }
            StringBuilder quick = new StringBuilder();
            if (Access.Can(c.U, "showings", 'A'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminShowings\">Book a showing</a>");
            }
            if (Access.Can(c.U, "leases", 'A'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminLeases\">New lease</a>");
            }
            if (Access.Can(c.U, "workorders", 'A'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminWorkOrders\">New work order</a>");
            }
            if (Access.Can(c.U, "notices", 'A'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminNotices\">Post a notice</a>");
            }
            if (Access.Can(c.U, "units", 'E'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminUnits?st=rent\">Units on the website</a>");
            }
            if (Access.Can(c.U, "payments", 'A'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminPayments\">Record a payment</a>");
            }
            if (Access.Can(c.U, "reports", 'V'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminReports\">Reports</a>");
            }
            if (Access.Can(c.U, "pages", 'E'))
            {
                quick.Append("<a class=\"db-btn db-btn-ghost\" href=\"AdminPages\">Edit the website</a>");
            }
            if (quick.Length > 0)
            {
                sb.Append("<section class=\"db-panel\"><h2>Quick actions</h2><div class=\"db-quick\">" + quick + "</div></section>");
            }
            if (tiles.Length == 0 && quick.Length == 0)
            {
                sb.Append("<section class=\"db-panel\"><p class=\"db-hint\">Your account has no areas yet. Ask a manager to give you rights on Staff.</p></section>");
            }
            response.SetElementContents("db-main", sb.ToString());
            return response;
        }
    }
}
