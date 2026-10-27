using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class Portal : WebPage
    {
        public override async Task OnInitialized()
        {
            await Seed.Keep();
            PublicData d = await Store.LoadPublic();
            HtmlDoc.SetTitle("Resident portal | " + d.Site.Legal);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, follow");
            string token = Ui.Clip(QueryValue("reset"), 80);
            string inner;
            if (token.Length > 0)
            {
                inner = await ResidentAuth.ResetValid(token) ? ResetHtml(token) : GateHtml("in", "This reset link has expired or was already used. Ask for a new one below.", await DemoResidents());
            }
            else
            {
                inner = "<div class=\"po-panel po-wait\" id=\"po-wait\">Opening the portal&hellip;</div>";
            }
            string main = "<div class=\"po-wrap po-ph\"><div class=\"po-crumbs\"><a href=\"Home\">Home</a> / <a href=\"Residents\">Residents</a> / Portal</div><h1>Resident portal</h1></div><section class=\"po-wrap po-sec\"><div id=\"po-portal\"" + (token.Length > 0 ? " data-reset=\"1\"" : string.Empty) + ">" + inner + "</div></section>";
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_head}", HeadHtml(d, "Portal"))
                .Replace("{plhd_main}", main)
                .Replace("{plhd_foot}", FootHtml(d));
        }

        private static string Input(string id, string label, string type, string auto, int max, string value = "")
        {
            return "<label class=\"po-field\" for=\"po-" + id + "\"><span>" + label + "</span><input type=\"" + type + "\" id=\"po-" + id + "\" maxlength=\"" + max + "\" autocomplete=\"" + auto + "\"" + (value.Length > 0 ? " value=\"" + Ui.Esc(value) + "\"" : string.Empty) + "></label>";
        }

        private static async Task<List<ResidentRec>> DemoResidents()
        {
            if (!Store.Demo())
            {
                return new List<ResidentRec>();
            }
            List<LeaseRec> leases = await Store.Rows<LeaseRec>("leases");
            List<ResidentRec> rs = await Store.Rows<ResidentRec>("residents");
            List<string> ids = leases.Where(l => l.Status == "Active").OrderBy(l => l.Start).SelectMany(l => l.Residents).Distinct().ToList();
            return ids.Select(id => rs.FirstOrDefault(r => r.Id == id)).Where(r => r != null && Ui.IsEmail(r.Email)).Select(r => r!).Take(4).ToList();
        }

        private static string DemoHtml(List<ResidentRec> demo)
        {
            if (demo.Count == 0)
            {
                return string.Empty;
            }
            return "<div class=\"po-demo\"><b>Demo</b><p>A sample resident is filled in. In demo any password signs in with the email of any resident on a lease, for example:</p><ul>" + string.Concat(demo.Select(r => "<li><code>" + Ui.Esc(r.Email) + "</code> " + Ui.Esc(Rent.Name(r)) + "</li>")) + "</ul></div>";
        }

        private static string GateHtml(string tab, string note, List<ResidentRec> demo)
        {
            string demoEmail = demo.Count > 0 ? demo[0].Email : string.Empty;
            string demoPass = demo.Count > 0 ? "demo1234" : string.Empty;
            StringBuilder sb = new StringBuilder("<div class=\"po-gwrap\"><div class=\"po-gate\"><div class=\"po-tabs\" role=\"tablist\" aria-label=\"Portal access\">");
            foreach ((string k, string n) in new[] { ("in", "Sign in"), ("act", "Activate account"), ("fgt", "Forgot password") })
            {
                sb.Append("<button type=\"button\" role=\"tab\" class=\"po-tab" + (k == tab ? " po-on" : string.Empty) + "\" id=\"po-tb-" + k + "\" aria-selected=\"" + (k == tab ? "true" : "false") + "\" aria-controls=\"po-t-" + k + "\" onclick=\"PortalJs.tab('" + k + "')\">" + n + "</button>");
            }
            sb.Append("</div>" + (note.Length > 0 ? "<p class=\"po-note\">" + Ui.Esc(note) + "</p>" : string.Empty));
            sb.Append("<form class=\"po-panel po-form\" id=\"po-t-in\" role=\"tabpanel\" aria-labelledby=\"po-tb-in\"" + (tab == "in" ? string.Empty : " hidden") + " onsubmit=\"return PortalJs.signIn()\" novalidate><h2>Sign in</h2>" + Input("in-email", "Email on your lease", "email", "username", 80, demoEmail) + Input("in-pass", "Password", "password", "current-password", 100, demoPass) + "<p class=\"po-serr2\" id=\"po-e-in\" role=\"alert\"></p><button type=\"submit\" class=\"po-btn po-wide\">Sign in</button>" + DemoHtml(demo) + "</form>");
            sb.Append("<form class=\"po-panel po-form\" id=\"po-t-act\" role=\"tabpanel\" aria-labelledby=\"po-tb-act\"" + (tab == "act" ? string.Empty : " hidden") + " onsubmit=\"return PortalJs.activate()\" novalidate><h2>Activate your account</h2><p class=\"po-hint\">Use the email on your lease and the activation code from the office, then choose a password of at least 8 characters.</p>" + Input("ac-email", "Email on your lease", "email", "username", 80) + Input("ac-code", "Activation code", "text", "one-time-code", 20) + "<div class=\"po-grid2\">" + Input("ac-pass", "New password", "password", "new-password", 100) + Input("ac-again", "Type it again", "password", "new-password", 100) + "</div><p class=\"po-serr2\" id=\"po-e-ac\" role=\"alert\"></p><button type=\"submit\" class=\"po-btn po-wide\">Activate and sign in</button></form>");
            sb.Append("<form class=\"po-panel po-form\" id=\"po-t-fgt\" role=\"tabpanel\" aria-labelledby=\"po-tb-fgt\"" + (tab == "fgt" ? string.Empty : " hidden") + " onsubmit=\"return PortalJs.forgot()\" novalidate><h2>Forgot your password?</h2><p class=\"po-hint\">Enter the email on your lease. If it has an account, we send a link to choose a new password. The link works for one hour.</p>" + Input("fg-email", "Email on your lease", "email", "username", 80) + "<p class=\"po-serr2\" id=\"po-e-fg\" role=\"alert\"></p><div class=\"po-sent\" id=\"po-fg-ok\" role=\"status\"></div><button type=\"submit\" class=\"po-btn po-wide\" id=\"po-fg-send\">Send the link</button></form></div>" + AsideHtml() + "</div>");
            return sb.ToString();
        }

        private static string AsideHtml()
        {
            return "<aside class=\"po-gside\"><h2>In the portal</h2><ul class=\"po-feat\"><li>" + Icon("doc") + "<span>Your lease, its dates and who is on it</span></li><li>" + Icon("card") + "<span>Your balance and every charge and payment</span></li><li>" + Icon("wrench") + "<span>Repair requests with photos, and updates until the job is done</span></li><li>" + Icon("calendar") + "<span>Notices about your building, like water shut-offs and inspections</span></li><li>" + Icon("people") + "<span>Your phone, emergency contact and vehicle</span></li></ul><p class=\"po-hint\">No code yet? The office gives one to every adult on a new lease. <a href=\"Contact\">Contact us</a> if yours is missing.</p></aside>";
        }

        private static string ResetHtml(string token)
        {
            return "<div class=\"po-gate\"><form class=\"po-panel po-form\" id=\"po-t-rs\" onsubmit=\"return PortalJs.reset()\" novalidate><h2>Choose a new password</h2><p class=\"po-hint\">At least 8 characters. You will be signed in afterwards, and other devices are signed out.</p><input type=\"hidden\" id=\"po-rs-tok\" value=\"" + Ui.Esc(token) + "\">" + "<div class=\"po-grid2\">" + Input("rs-pass", "New password", "password", "new-password", 100) + Input("rs-again", "Type it again", "password", "new-password", 100) + "</div><p class=\"po-serr2\" id=\"po-e-rs\" role=\"alert\"></p><button type=\"submit\" class=\"po-btn po-wide\">Save and sign in</button></form></div>";
        }

        private static string Nth(int n)
        {
            string end = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return n + end;
        }

        private static readonly string[] Kinds = { "Plumbing", "Electrical", "Heating and cooling", "Appliance", "Doors and locks", "Pests", "Walls and floors", "Windows", "Outside", "Other" };

        private static string Stage(WorkOrderRec w)
        {
            return w.Status switch
            {
                "New" => "Received",
                "Scheduled" => "Visit booked",
                "In progress" => "Being fixed",
                "On hold" => "Waiting for parts",
                "Done" => "Fixed",
                _ => "Closed"
            };
        }

        private static string RequestsHtml(RentData d, ResidentRec r, LeaseRec l, UnitRec u)
        {
            SiteData s = d.Site;
            DateTime since = (l.MovedIn ?? l.Start).AddDays(-14);
            List<WorkOrderRec> mine = d.WorkOrders.Where(w => w.UnitId == u.Id && w.Created >= since && (w.Source == "portal" || w.Notes.Any(n => n.Shared))).OrderByDescending(w => Maint.Open(w)).ThenByDescending(w => w.Created).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"po-panel po-stack\" id=\"po-req\"><div class=\"po-lhead\"><h3>Repair requests</h3><button type=\"button\" class=\"po-btn po-btn-sm\" id=\"po-rq-open\" onclick=\"PortalJs.reqForm(true)\">Report a problem</button></div>");
            sb.Append("<form class=\"po-form po-rqform\" id=\"po-rqform\" hidden onsubmit=\"return PortalJs.request()\" novalidate><p class=\"po-urgent\"><b>Emergency?</b> For a flood, a gas smell, a fire, no heat in freezing weather or a lockout, call " + Ui.Esc(s.Phone) + " now and choose option 1, then send this form.</p>");
            sb.Append("<div class=\"po-grid2\"><label class=\"po-field\" for=\"po-rq-cat\"><span>What kind of problem?</span><select id=\"po-rq-cat\">" + string.Concat(Kinds.Select(k => "<option>" + Ui.Esc(k) + "</option>")) + "</select></label><label class=\"po-field\" for=\"po-rq-title\"><span>In a few words</span><input type=\"text\" id=\"po-rq-title\" maxlength=\"90\" placeholder=\"For example: kitchen tap drips\"></label></div>");
            sb.Append("<label class=\"po-field\" for=\"po-rq-detail\"><span>Tell us more: where it is, since when, what you tried</span><textarea id=\"po-rq-detail\" rows=\"4\" maxlength=\"1500\"></textarea></label>");
            sb.Append("<div class=\"po-grid2\"><label class=\"po-field\" for=\"po-rq-pets\"><span>Pets we should know about (optional)</span><input type=\"text\" id=\"po-rq-pets\" maxlength=\"80\"></label><label class=\"po-field\" for=\"po-rq-photos\"><span>Photos (up to 3, optional)</span><input type=\"file\" id=\"po-rq-photos\" accept=\"image/jpeg,image/png,image/webp,image/gif\" multiple></label></div>");
            sb.Append("<label class=\"po-check\" for=\"po-rq-entry\"><input type=\"checkbox\" id=\"po-rq-entry\">You may enter to fix it when nobody is home</label><label class=\"po-check\" for=\"po-rq-urgent\"><input type=\"checkbox\" id=\"po-rq-urgent\">This is urgent: it is causing damage or makes the home unsafe</label>");
            sb.Append("<p class=\"po-serr2\" id=\"po-e-rq\" role=\"alert\"></p><div class=\"po-acts2\"><button type=\"submit\" class=\"po-btn\" id=\"po-rq-send\">Send request</button><button type=\"button\" class=\"po-btn po-btn-ghost\" onclick=\"PortalJs.reqForm(false)\">Cancel</button></div></form>");
            if (mine.Count == 0)
            {
                sb.Append("<p class=\"po-hint\">No requests yet. If something in your home is broken, report it here and follow the repair.</p>");
            }
            else
            {
                sb.Append("<ul class=\"po-reqs\">");
                foreach (WorkOrderRec w in mine.Take(8))
                {
                    List<WoNote> shared = w.Notes.Where(n => n.Shared).OrderByDescending(n => n.When).ToList();
                    sb.Append("<li><div class=\"po-rqh\"><b>" + Ui.Esc(w.Title) + "</b><span class=\"po-st po-st-" + (w.Status == "Done" ? "on" : w.Status == "Cancelled" ? "off" : "warn") + "\">" + Ui.Esc(Stage(w)) + "</span></div><small>" + Ui.Esc(w.Code + " · " + w.Category + " · sent " + Fmt.Day(w.Created)) + (w.Scheduled != null && Maint.Open(w) ? " · visit " + Ui.Esc(Fmt.When(w.Scheduled.Value)) : string.Empty) + (w.Done != null ? " · fixed " + Ui.Esc(Fmt.Day(w.Done.Value)) : string.Empty) + "</small>");
                    if (shared.Count > 0)
                    {
                        sb.Append("<div class=\"po-rqnotes\">" + string.Concat(shared.Take(3).Select(n => "<p><small>" + Ui.Esc(Fmt.Short(n.When)) + "</small> " + Ui.Esc(n.Text) + "</p>")) + "</div>");
                    }
                    if (w.Status == "Done" && w.BillTo == "Resident" && w.Cost > 0)
                    {
                        sb.Append("<p class=\"po-hint\">" + Fmt.Money(w.Cost) + " was added to your balance for this repair.</p>");
                    }
                    sb.Append("</li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string PayHtml(decimal bal, LeaseRec l)
        {
            decimal suggest = bal > 0.004m ? bal : l.Rent;
            return "<div class=\"po-paybox\"><button type=\"button\" class=\"po-btn po-wide\" id=\"po-pay-open\" onclick=\"PortalJs.payForm(true)\">Pay online</button><form class=\"po-form po-payform\" id=\"po-payform\" hidden onsubmit=\"return PortalJs.pay()\" novalidate><label class=\"po-field\" for=\"po-pay-amt\"><span>Amount to pay ($)</span><input type=\"text\" id=\"po-pay-amt\" inputmode=\"decimal\" maxlength=\"10\" value=\"" + suggest.ToString("0.00", CultureInfo.InvariantCulture) + "\"></label><p class=\"po-serr2\" id=\"po-e-pay\" role=\"alert\"></p><div class=\"po-acts2\"><button type=\"submit\" class=\"po-btn\" id=\"po-pay-go\">Continue to card payment</button><button type=\"button\" class=\"po-btn po-btn-ghost\" onclick=\"PortalJs.payForm(false)\">Cancel</button></div><p class=\"po-hint\">You pay on a secure card page. The payment shows on your account as soon as it is confirmed.</p></form></div>";
        }

        private static async Task<RentData> FreshData()
        {
            RentData d = await Rent.Load();
            d.CanPay = await Pay.Ready() || Store.Demo();
            return d;
        }

        public async Task<ApiResponse> PayStart()
        {
            (ResidentRec? who, ApiResponse response) = await Who();
            if (who == null)
            {
                return response;
            }
            RentData d = await FreshData();
            LeaseRec? l = Rent.LeaseOf(d, who.Id);
            decimal amt = Fmt.Dec(GetDataValue("pamt"));
            amt = Math.Round(amt, 2);
            decimal bal = l == null ? 0 : Rent.Balance(d, l.Id);
            string err = !d.CanPay ? "Online payment is not available. Pay at the office." : l == null || !Rent.Live.Contains(l.Status) ? "Online payment is for the home you live in now." : amt < 1 ? "Enter an amount of at least $1.00." : amt > Math.Max(bal, 0) + l.Rent * 2 ? "That is more than your balance plus two months of rent." : string.Empty;
            if (err.Length > 0 || l == null)
            {
                response.SetElementContents("po-e-pay", Ui.Esc(err));
                response.ExecuteScript("PortalJs.payFailed();");
                return response;
            }
            if (await Pay.Ready())
            {
                (string url, string perr) = await Pay.Checkout(d, l, who, amt, Local.Base());
                if (perr.Length > 0 || url.Length == 0)
                {
                    response.SetElementContents("po-e-pay", Ui.Esc(perr.Length > 0 ? perr : "The payment page could not be opened. Try again in a moment."));
                    response.ExecuteScript("PortalJs.payFailed();");
                    return response;
                }
                response.ExecuteScript("PortalJs.go(" + System.Text.Json.JsonSerializer.Serialize(url) + ");");
                return response;
            }
            PayAttempt a = new PayAttempt { Id = Fmt.NewId("p-"), LeaseId = l.Id, ResidentId = who.Id, Amount = amt, Status = "Open", Source = "test", Created = DateTime.Now };
            using (await Store.Hold())
            {
                List<PayAttempt> all = await Store.Rows<PayAttempt>("payattempts");
                all.Add(a);
                await Store.SaveRows("payattempts", all);
            }
            await Pay.Record(a.Id, string.Empty, amt, "TEST-" + a.Id.Substring(2).ToUpperInvariant(), "Card (test payment)");
            RentData fresh = await FreshData();
            response.SetElementContents("po-portal", BoardHtml(fresh, who, "Thank you. Your payment of " + Fmt.Money(amt) + " is received. This was a test payment: no card was charged."));
            response.ExecuteScript("PortalJs.ready(true);");
            return response;
        }

        private static string StatusText(LeaseRec l)
        {
            return l.Status switch
            {
                "Upcoming" => "Starts " + Fmt.Day(l.Start),
                "Active" => Rent.Holdover(l) ? "Month to month" : "Active",
                "Notice" => "Moving out" + (l.MoveOut != null ? " " + Fmt.Day(l.MoveOut.Value) : string.Empty),
                "Ended" => "Ended",
                _ => l.Status
            };
        }

        private static string BoardHtml(RentData d, ResidentRec r, string note)
        {
            SiteData s = d.Site;
            LeaseRec? l = Rent.LeaseOf(d, r.Id);
            UnitRec? u = l == null ? null : d.Units.FirstOrDefault(x => x.Id == l.UnitId);
            PropertyRec? p = u == null ? null : d.Properties.FirstOrDefault(x => x.Id == u.PropertyId);
            StringBuilder sb = new StringBuilder("<div class=\"po-board\"><div class=\"po-bhello\"><div><h2>Hello, " + Ui.Esc(r.First) + "</h2><p>" + (u != null && p != null ? Ui.Esc(p.Name + " · " + u.Number + ", " + p.Street + ", " + p.City) : "Your account") + "</p></div><button type=\"button\" class=\"po-btn po-btn-ghost po-btn-sm\" onclick=\"PortalJs.signOut()\">Sign out</button></div>");
            if (note.Length > 0)
            {
                sb.Append("<p class=\"po-sent\">" + Ui.Esc(note) + "</p>");
            }
            sb.Append("<div class=\"po-cards2\">");
            if (l == null || u == null || p == null)
            {
                sb.Append("<div class=\"po-panel po-stack\"><h3>No lease yet</h3><p class=\"po-hint\">Your account is not linked to a lease. Call the office on <a href=\"tel:" + Ui.Esc(s.Phone) + "\">" + Ui.Esc(s.Phone) + "</a> and we will sort it out.</p></div>");
            }
            else
            {
                sb.Append("<div class=\"po-panel po-stack\"><div class=\"po-lhead\"><h3>Your lease</h3><span class=\"po-st po-st-" + (l.Status == "Ended" ? "off" : l.Status == "Notice" || Rent.Holdover(l) ? "warn" : "on") + "\">" + Ui.Esc(StatusText(l)) + "</span></div><dl class=\"po-tripdl\"><dt>Lease</dt><dd>" + Ui.Esc(l.Code) + "</dd><dt>Home</dt><dd>" + Ui.Esc(p.Name + " · " + u.Number) + "<br><small>" + Ui.Esc(Fmt.Beds(u.Beds) + ", " + Fmt.Baths(u.Baths)) + "</small></dd><dt>Term</dt><dd>" + Ui.Esc(Fmt.Day(l.Start) + " to " + Fmt.Day(l.End)) + "</dd><dt>Rent</dt><dd>" + Fmt.Money(l.Rent) + " a month</dd><dt>Deposit</dt><dd>" + Fmt.Money(l.Deposit) + "</dd><dt>On the lease</dt><dd>" + Ui.Esc(Rent.Names(d, l)) + "</dd>" + (l.Pets.Length > 0 ? "<dt>Pets</dt><dd>" + Ui.Esc(l.Pets) + "</dd>" : string.Empty) + "</dl>");
                if (Rent.Expiring(l, 90))
                {
                    sb.Append("<p class=\"po-hint\">Your lease ends on " + Ui.Esc(Fmt.Day(l.End)) + ". We will contact you about renewing. If you plan to move, give 60 days written notice.</p>");
                }
                sb.Append("</div>");
                decimal bal = Rent.Balance(d, l.Id);
                sb.Append("<div class=\"po-panel po-stack\"><h3>Balance</h3><div class=\"po-bal" + (bal > 0 ? " po-owe" : string.Empty) + "\"><small>" + (bal > 0 ? "You owe" : bal < 0 ? "Credit on your account" : "All paid") + "</small><b>" + Fmt.Money(Math.Abs(bal)) + "</b></div><p class=\"po-hint\">Rent is due on the " + Nth(s.RentDueDay) + " of each month. A " + Fmt.Whole(s.LateFee) + " late fee is added if rent is still owed after the " + Nth(s.RentDueDay + s.GraceDays - 1) + ".</p>" + (d.CanPay && Rent.Live.Contains(l.Status) ? PayHtml(bal, l) : string.Empty) + "<p class=\"po-hint\">" + (d.CanPay ? "You can also pay" : "Pay") + " by check or card at the office, or by bank transfer with your lease code <b>" + Ui.Esc(l.Code) + "</b> as the reference.</p></div>");
            }
            sb.Append("</div>");
            string pid = p?.Id ?? string.Empty;
            List<NoticeRec> notes = d.Notices.Where(n => (n.Properties.Count == 0 || n.Properties.Contains(pid)) && n.From <= DateTime.Now && (n.Until == null || n.Until.Value.Date >= DateTime.Today)).OrderByDescending(n => n.Pinned).ThenByDescending(n => n.From).ToList();
            sb.Append("<div class=\"po-panel po-stack\"><h3>Notices from management</h3>");
            sb.Append(notes.Count == 0 ? "<p class=\"po-hint\">No notices right now.</p>" : "<ul class=\"po-notes\">" + string.Concat(notes.Select(n => "<li" + (n.Pinned ? " class=\"po-pin\"" : string.Empty) + "><div><b>" + Ui.Esc(n.Title) + "</b><small>" + Ui.Esc(Fmt.Day(n.From)) + (n.Pinned ? " &middot; Important" : string.Empty) + "</small></div>" + Ui.Paras(n.Text) + "</li>")) + "</ul>");
            sb.Append("</div>");
            if (l != null && u != null && Rent.Live.Contains(l.Status))
            {
                sb.Append(RequestsHtml(d, r, l, u));
            }
            DepositRec? dep = l == null ? null : d.Deposits.FirstOrDefault(x => x.LeaseId == l.Id && x.Status == "Final");
            if (l != null && dep != null)
            {
                sb.Append("<div class=\"po-panel po-stack\"><h3>Your deposit statement</h3><table class=\"po-dt\"><tbody><tr><td>Deposit held</td><td class=\"po-num\">" + Fmt.Money(dep.Held) + "</td></tr>" + (dep.Owed > 0.004m ? "<tr><td>Unpaid balance at move-out</td><td class=\"po-num\">-" + Fmt.Money(dep.Owed) + "</td></tr>" : string.Empty) + string.Concat(dep.Lines.Select(x => "<tr><td>" + Ui.Esc(x.Label) + "</td><td class=\"po-num\">-" + Fmt.Money(x.Amount) + "</td></tr>")) + "<tr class=\"po-dt-t\"><td>" + (dep.Refund >= 0 ? "Refunded to you" : "Still owed") + "</td><td class=\"po-num\">" + Fmt.Money(Math.Abs(dep.Refund)) + "</td></tr></tbody></table>" + (dep.Refund > 0 ? "<p class=\"po-hint\">Sent by " + Ui.Esc(dep.Method.ToLowerInvariant()) + (dep.Finished != null ? " on " + Ui.Esc(Fmt.Day(dep.Finished.Value)) : string.Empty) + ".</p>" : string.Empty) + (dep.Note.Length > 0 ? "<p class=\"po-hint\">" + Ui.Esc(dep.Note) + "</p>" : string.Empty) + "</div>");
            }
            if (l != null)
            {
                List<LedgerRec> rows = d.Ledger.Where(x => x.LeaseId == l.Id).OrderBy(x => x.When.Date).ThenBy(x => x.Amount < 0 ? 1 : 0).ThenBy(x => x.When).ToList();
                List<(LedgerRec Row, decimal Run)> run = new List<(LedgerRec, decimal)>();
                decimal t = 0;
                foreach (LedgerRec x in rows)
                {
                    t += x.Amount;
                    run.Add((x, t));
                }
                run.Reverse();
                sb.Append("<div class=\"po-panel po-stack\"><h3>Charges and payments</h3>");
                if (run.Count == 0)
                {
                    sb.Append("<p class=\"po-hint\">Nothing yet.</p>");
                }
                else
                {
                    sb.Append("<div class=\"po-tblwrap\"><table class=\"po-ledger\"><thead><tr><th scope=\"col\">Date</th><th scope=\"col\">Item</th><th scope=\"col\" class=\"po-num\">Charge</th><th scope=\"col\" class=\"po-num\">Payment</th><th scope=\"col\" class=\"po-num\">Balance</th></tr></thead><tbody>");
                    foreach ((LedgerRec x, decimal bal) in run.Take(24))
                    {
                        sb.Append("<tr><td>" + Ui.Esc(Fmt.Day(x.When)) + "</td><td>" + Ui.Esc(x.Label.Length > 0 ? x.Label : x.Kind) + (x.Method.Length > 0 ? "<small>" + Ui.Esc(x.Method + (x.Reference.Length > 0 ? " " + x.Reference : string.Empty)) + "</small>" : string.Empty) + "</td><td class=\"po-num\">" + (x.Amount > 0 ? Fmt.Money(x.Amount) : string.Empty) + "</td><td class=\"po-num\">" + (x.Amount < 0 ? Fmt.Money(-x.Amount) : string.Empty) + "</td><td class=\"po-num\">" + Fmt.Money(bal) + "</td></tr>");
                    }
                    sb.Append("</tbody></table></div>" + (run.Count > 24 ? "<p class=\"po-hint\">Showing the latest 24 of " + run.Count + " lines. Ask the office for a full statement.</p>" : string.Empty));
                }
                sb.Append("</div>");
            }
            sb.Append("<div class=\"po-cards2\"><form class=\"po-panel po-form\" id=\"po-prof\" onsubmit=\"return PortalJs.profile()\" novalidate><h3>Your details</h3><label class=\"po-field\" for=\"po-pf-email\"><span>Email (ask the office to change it)</span><input type=\"email\" id=\"po-pf-email\" value=\"" + Ui.Esc(r.Email) + "\" disabled></label><label class=\"po-field\" for=\"po-pf-phone\"><span>Phone</span><input type=\"tel\" id=\"po-pf-phone\" maxlength=\"30\" autocomplete=\"tel\" value=\"" + Ui.Esc(r.Phone) + "\"></label><label class=\"po-field\" for=\"po-pf-emerg\"><span>Emergency contact (name and phone)</span><input type=\"text\" id=\"po-pf-emerg\" maxlength=\"120\" value=\"" + Ui.Esc(r.Emergency) + "\"></label><label class=\"po-field\" for=\"po-pf-car\"><span>Vehicle (make, color, plate)</span><input type=\"text\" id=\"po-pf-car\" maxlength=\"80\" value=\"" + Ui.Esc(r.Vehicle) + "\"></label><p class=\"po-serr2\" id=\"po-e-pf\" role=\"alert\"></p><button type=\"submit\" class=\"po-btn\">Save details</button></form>");
            sb.Append("<form class=\"po-panel po-form\" id=\"po-pw\" onsubmit=\"return PortalJs.password()\" novalidate><h3>Change password</h3><input type=\"text\" class=\"po-hp\" autocomplete=\"username\" value=\"" + Ui.Esc(r.Email) + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Input("pw-cur", "Current password", "password", "current-password", 100) + Input("pw-new", "New password", "password", "new-password", 100) + Input("pw-again", "Type it again", "password", "new-password", 100) + "<p class=\"po-serr2\" id=\"po-e-pw\" role=\"alert\"></p><button type=\"submit\" class=\"po-btn\">Change password</button></form></div></div>");
            return sb.ToString();
        }

        private async Task<(ResidentRec? R, ApiResponse Response)> Who()
        {
            ApiResponse response = new ApiResponse();
            ResidentRec? r = await ResidentAuth.Check(GetDataValue("tok"));
            if (r == null)
            {
                response.SetElementContents("po-portal", GateHtml("in", "Please sign in again.", await DemoResidents()));
                response.ExecuteScript("PortalJs.clear();");
            }
            return (r, response);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            string tok = (GetDataValue("tok") ?? string.Empty).Trim();
            ResidentRec? r = await ResidentAuth.Check(tok);
            if (r == null)
            {
                response.SetElementContents("po-portal", GateHtml((GetDataValue("tab") ?? string.Empty).Trim() == "act" ? "act" : "in", string.Empty, await DemoResidents()));
                if (tok.Length > 0)
                {
                    response.ExecuteScript("PortalJs.clear();");
                }
                response.ExecuteScript("PortalJs.ready();");
                return response;
            }
            string session = Ui.Clip(GetDataValue("psess"), 200);
            string note = string.Empty;
            if (session.Length > 0)
            {
                string st = await Pay.Ready() ? await Pay.Confirm(session) : "unknown";
                note = st == "paid" ? "Thank you. Your card payment is confirmed and shows on your account." : st == "pending" ? "Thank you. We are waiting for the card company to confirm your payment. It shows here within a few minutes." : string.Empty;
            }
            RentData d = await FreshData();
            response.SetElementContents("po-portal", BoardHtml(d, r, note));
            response.ExecuteScript("PortalJs.ready(" + (note.Length > 0 ? "true" : "false") + ");");
            return response;
        }

        public async Task<ApiResponse> SignIn()
        {
            ApiResponse response = new ApiResponse();
            string email = Ui.Clip(GetDataValue("email"), 80);
            string pass = GetDataValue("pass") ?? string.Empty;
            if (!Ui.IsEmail(email) || pass.Length == 0)
            {
                response.SetElementContents("po-e-in", "Enter your email and password.");
                return response;
            }
            (string err, string token) = await ResidentAuth.SignIn(email, pass);
            if (err.Length > 0)
            {
                response.SetElementContents("po-e-in", Ui.Esc(err));
                return response;
            }
            await Store.Log("resident", "portal sign in", email, true);
            response.ExecuteScript("PortalJs.signed('" + token + "');");
            return response;
        }

        public async Task<ApiResponse> Activate()
        {
            ApiResponse response = new ApiResponse();
            string email = Ui.Clip(GetDataValue("email"), 80);
            string code = Ui.Clip(GetDataValue("code"), 20);
            if (!Ui.IsEmail(email) || code.Length < 4)
            {
                response.SetElementContents("po-e-ac", "Enter the email on your lease and the activation code.");
                return response;
            }
            (string err, string token) = await ResidentAuth.Activate(email, code, GetDataValue("pass") ?? string.Empty, GetDataValue("again") ?? string.Empty);
            if (err.Length > 0)
            {
                response.SetElementContents("po-e-ac", Ui.Esc(err));
                return response;
            }
            await Store.Log("resident", "portal activated", email, true);
            response.ExecuteScript("PortalJs.signed('" + token + "');");
            return response;
        }

        public async Task<ApiResponse> Forgot()
        {
            ApiResponse response = new ApiResponse();
            string email = Ui.Clip(GetDataValue("email"), 80);
            response.SetElementContents("po-e-fg", string.Empty);
            if (!Ui.IsEmail(email))
            {
                response.SetElementContents("po-e-fg", "Enter a valid email address.");
                return response;
            }
            if (Auth.Paused("rf:" + Local.Visitor()))
            {
                response.SetElementContents("po-e-fg", "Too many requests. Wait a few minutes and try again.");
                return response;
            }
            Auth.Failed("rf:" + Local.Visitor());
            SiteData s = await Store.LoadSite();
            if (!Mailer.Ready && !Store.Demo())
            {
                response.SetElementContents("po-fg-ok", "Email is not set up for this site yet. Call the office on " + Ui.Esc(s.Phone) + " and we will give you a new activation code.");
                return response;
            }
            (ResidentRec? r, string token) = await ResidentAuth.StartReset(email);
            string link = Local.Base() + "Portal?reset=" + token;
            if (r != null && Mailer.Ready)
            {
                string result = await Mailer.Send(r.Email, "Choose a new password for the " + s.Brand + " resident portal", "Hello " + r.First + ",\n\nOpen this link to choose a new password. It works for one hour.\n\n" + link + "\n\nIf you did not ask for this, you can ignore this email.\n\n" + s.Legal + "\n" + s.Phone + "\n");
                await Store.Log("resident", "password reset link", r.Email, result.Length == 0);
            }
            string ok = "If an account uses that email, we have sent a link to choose a new password. It works for one hour.";
            if (r != null && !Mailer.Ready)
            {
                ok += " <a href=\"Portal?reset=" + Ui.Esc(token) + "\">Open the link</a>";
            }
            response.SetElementContents("po-fg-ok", ok);
            return response;
        }

        public async Task<ApiResponse> Reset()
        {
            ApiResponse response = new ApiResponse();
            string token = Ui.Clip(GetDataValue("rtoken"), 80);
            (string err, string tok) = await ResidentAuth.Reset(token, GetDataValue("pass") ?? string.Empty, GetDataValue("again") ?? string.Empty);
            if (err.Length > 0)
            {
                response.SetElementContents("po-e-rs", Ui.Esc(err));
                return response;
            }
            await Store.Log("resident", "password reset", "portal", true);
            response.ExecuteScript("PortalJs.signed('" + tok + "', true);");
            return response;
        }

        public async Task<ApiResponse> SaveProfile()
        {
            (ResidentRec? who, ApiResponse response) = await Who();
            if (who == null)
            {
                return response;
            }
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string emerg = Ui.Clip(GetDataValue("emerg"), 120);
            string car = Ui.Clip(GetDataValue("car"), 80);
            if (phone.Length > 0 && !Ui.IsPhone(phone))
            {
                response.SetElementContents("po-e-pf", "Enter a valid phone number, or leave it empty.");
                return response;
            }
            using (await Store.Hold())
            {
                List<ResidentRec> rs = await Store.Rows<ResidentRec>("residents");
                ResidentRec? r = rs.FirstOrDefault(x => x.Id == who.Id);
                if (r != null)
                {
                    r.Phone = phone;
                    r.Emergency = emerg;
                    r.Vehicle = car;
                    await Store.SaveRows("residents", rs);
                    who = r;
                }
            }
            await Store.Log("resident", "portal details", Rent.Name(who), true);
            RentData d = await FreshData();
            response.SetElementContents("po-portal", BoardHtml(d, who, "Your details are saved."));
            response.ExecuteScript("PortalJs.ready(true);");
            return response;
        }

        public async Task<ApiResponse> Password()
        {
            (ResidentRec? who, ApiResponse response) = await Who();
            if (who == null)
            {
                return response;
            }
            string err = await ResidentAuth.ChangeOwn(who.Id, GetDataValue("cur") ?? string.Empty, GetDataValue("next") ?? string.Empty, GetDataValue("again") ?? string.Empty);
            if (err.Length > 0)
            {
                response.SetElementContents("po-e-pw", Ui.Esc(err));
                return response;
            }
            await Store.Log("resident", "portal password", Rent.Name(who), true);
            RentData d = await FreshData();
            response.SetElementContents("po-portal", BoardHtml(d, who, "Your password is changed."));
            response.ExecuteScript("PortalJs.ready(true);");
            return response;
        }

        public async Task<ApiResponse> SendRequest()
        {
            (ResidentRec? who, ApiResponse response) = await Who();
            if (who == null)
            {
                return response;
            }
            RentData d = await FreshData();
            LeaseRec? l = Rent.LeaseOf(d, who.Id);
            UnitRec? u = l == null ? null : d.Units.FirstOrDefault(x => x.Id == l.UnitId);
            if (l == null || u == null || !Rent.Live.Contains(l.Status))
            {
                response.SetElementContents("po-e-rq", "Requests are for the home you live in now. Call the office for anything else.");
                response.ExecuteScript("PortalJs.reqFailed();");
                return response;
            }
            string cat = Ui.Clip(GetDataValue("rcat"), 30);
            cat = Kinds.Contains(cat) ? cat : "Other";
            string title = Ui.Clip(GetDataValue("rtitle"), 90);
            string detail = Ui.Clip((GetDataValue("rdetail") ?? string.Empty).Replace("\r", string.Empty), 1500);
            string err = title.Length < 3 ? "Describe the problem in a few words." : detail.Length < 10 ? "Tell us a little more so we bring the right tools." : string.Empty;
            if (err.Length == 0 && d.WorkOrders.Count(w => w.ResidentId == who.Id && w.Created > DateTime.Now.AddHours(-1)) >= 5)
            {
                err = "You have sent several requests in the last hour. Call the office on " + d.Site.Phone + " if it cannot wait.";
            }
            if (err.Length > 0)
            {
                response.SetElementContents("po-e-rq", Ui.Esc(err));
                response.ExecuteScript("PortalJs.reqFailed();");
                return response;
            }
            List<Incoming> files = await Media.Received();
            List<string> photos = new List<string>();
            List<string> problems = new List<string>();
            foreach (Incoming f in files.Take(3))
            {
                (string path, string perr) = await Media.SaveRequestPhoto(f);
                if (path.Length > 0)
                {
                    photos.Add(path);
                }
                else
                {
                    problems.Add(perr);
                }
            }
            bool urgent = (GetDataValue("rurgent") ?? string.Empty) == "1";
            DateTime now = DateTime.Now;
            string code;
            using (await Store.Hold())
            {
                List<WorkOrderRec> all = await Store.Rows<WorkOrderRec>("workorders");
                string pri = urgent ? "High" : "Normal";
                WorkOrderRec w = new WorkOrderRec { Id = Fmt.NewId("w-"), Code = Fmt.Code("WO-", all.Select(x => x.Code)), PropertyId = u.PropertyId, UnitId = u.Id, Category = cat, Title = title, Detail = detail, Priority = pri, Status = "New", Source = "portal", ResidentId = who.Id, EntryOk = (GetDataValue("rentry") ?? string.Empty) == "1", Pets = Ui.Clip(GetDataValue("rpets"), 80), Photos = photos, Due = Maint.DueFor(pri, now), BillTo = "Owner", Created = now, By = who.Id };
                all.Add(w);
                await Store.SaveRows("workorders", all);
                code = w.Code;
            }
            await Store.Log("resident", "repair request", code + " · " + Rent.UnitLabel(d, u.Id), true);
            if (Mailer.Ready && Ui.IsEmail(d.Site.NotifyEmail))
            {
                string result = await Mailer.Send(d.Site.NotifyEmail, (urgent ? "Urgent repair request " : "Repair request ") + code + ": " + Rent.UnitLabel(d, u.Id), "From: " + Rent.Name(who) + " (" + (who.Phone.Length > 0 ? who.Phone : who.Email) + ")\nHome: " + Rent.UnitLabel(d, u.Id) + "\nKind: " + cat + "\nIssue: " + title + "\n\n" + detail + "\n");
                if (result.Length > 0)
                {
                    await Store.Log("system", "notification email", result, false);
                }
            }
            RentData fresh = await FreshData();
            response.SetElementContents("po-portal", BoardHtml(fresh, who, "Request " + code + " is sent. We answer within one working day" + (urgent ? ", sooner for urgent problems" : string.Empty) + "." + (problems.Count > 0 ? " " + string.Join(" ", problems) : string.Empty)));
            response.ExecuteScript("PortalJs.ready(true);");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await ResidentAuth.SignOut(GetDataValue("tok"));
            response.SetElementContents("po-portal", GateHtml("in", "You are signed out.", await DemoResidents()));
            response.ExecuteScript("PortalJs.clear();");
            return response;
        }

        private string DataPart(PublicData d, Block b, bool first)
        {
            return string.Empty;
        }

        private static readonly (string Page, string Name)[] NavItems =
        {
            ("Home", "Home"),
            ("Units", "Available units"),
            ("Properties", "Properties"),
            ("Owners", "Owners"),
            ("Residents", "Residents"),
            ("Contact", "Contact")
        };

        private static string Tok(SiteData site, string v)
        {
            return v.Replace("{name}", site.Brand);
        }

        private static string TokH(SiteData site, string v)
        {
            return v.Replace("{name}", Ui.Esc(site.Brand));
        }

        private static string Lines(string v)
        {
            return Ui.Esc(v).Replace("\n", "<br>");
        }

        private static string Icon(string k)
        {
            string a = "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">";
            return k switch
            {
                "home" => a + "<path d=\"M4 11l8-6.5 8 6.5\"/><path d=\"M6 10v9.5h12V10\"/><path d=\"M10 19.5v-5h4v5\"/></svg>",
                "wrench" => a + "<path d=\"M14.5 6.5a4 4 0 0 0-5 5L4 17l3 3 5.5-5.5a4 4 0 0 0 5-5l-2.5 2.5-2.5-.5-.5-2.5z\"/></svg>",
                "chart" => a + "<path d=\"M4 20h16\"/><path d=\"M7 16v-5M12 16V7M17 16v-8\"/></svg>",
                "doc" => a + "<path d=\"M7 3.5h7l4 4V20a.5.5 0 0 1-.5.5h-10.5A.5.5 0 0 1 6.5 20V4a.5.5 0 0 1 .5-.5z\"/><path d=\"M14 3.5V8h4M9.5 12h5M9.5 15.5h5\"/></svg>",
                "paw" => a + "<circle cx=\"7\" cy=\"10\" r=\"1.8\"/><circle cx=\"10.5\" cy=\"6.5\" r=\"1.8\"/><circle cx=\"14.5\" cy=\"6.5\" r=\"1.8\"/><circle cx=\"18\" cy=\"10\" r=\"1.8\"/><path d=\"M12.5 11c-3 0-5.5 4-5.5 6.5 0 1.7 1.6 2 3 1.5 1.5-.6 3.5-.6 5 0 1.4.5 3-.1 3-1.5 0-2.5-2.5-6.5-5.5-6.5z\"/></svg>",
                "leaf" => a + "<path d=\"M5 19c0-9 6-14 15-14 0 9-5 15-14 15\"/><path d=\"M5 19l8-8\"/></svg>",
                "bed" => a + "<path d=\"M3.5 18.5V8M3.5 14.5h17v4M20.5 14.5V12a2 2 0 0 0-2-2H11v4.5\"/><circle cx=\"7\" cy=\"11.5\" r=\"1.6\"/></svg>",
                "bath" => a + "<path d=\"M4 12h16v2.5a4 4 0 0 1-4 4H8a4 4 0 0 1-4-4z\"/><path d=\"M6.5 12V6a2 2 0 0 1 3.8-.8M7 18.5l-1 2M17 18.5l1 2\"/></svg>",
                "area" => a + "<rect x=\"4\" y=\"4\" width=\"16\" height=\"16\" rx=\"1.5\"/><path d=\"M4 9h5V4M15 20v-5h5\"/></svg>",
                "car" => a + "<path d=\"M5 16h14M3.5 16v-3.5l2-5A2 2 0 0 1 7.4 6h9.2a2 2 0 0 1 1.9 1.5l2 5V16\"/><path d=\"M3.5 12.5h17\"/><circle cx=\"7.5\" cy=\"16.5\" r=\"1.8\"/><circle cx=\"16.5\" cy=\"16.5\" r=\"1.8\"/></svg>",
                "key" => a + "<circle cx=\"8\" cy=\"15\" r=\"4\"/><path d=\"M11 12l8-8M16 7l2 2M14 9l2 2\"/></svg>",
                "calendar" => a + "<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\"/></svg>",
                "pin" => a + "<path d=\"M12 21s-6.5-6-6.5-11a6.5 6.5 0 0 1 13 0c0 5-6.5 11-6.5 11z\"/><circle cx=\"12\" cy=\"10\" r=\"2.4\"/></svg>",
                "shield" => a + "<path d=\"M12 3l7 3v5c0 5-3 8.5-7 10-4-1.5-7-5-7-10V6z\"/><path d=\"M9 12l2 2 4-4\"/></svg>",
                "fuel" => a + "<path d=\"M5 20V5a1.5 1.5 0 0 1 1.5-1.5h6A1.5 1.5 0 0 1 14 5v15M3.5 20h12M5 10h9\"/><path d=\"M14 8l3 2.5V17a1.5 1.5 0 0 0 3 0V9l-2.5-2.5\"/></svg>",
                "clock" => a + "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg>",
                "phone" => a + "<path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg>",
                "mail" => a + "<rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg>",
                "check" => "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg>",
                "seat" => a + "<path d=\"M8 4h5a2 2 0 0 1 2 2v7H9a2 2 0 0 1-2-2V5a1 1 0 0 1 1-1z\"/><path d=\"M6 13h11a2 2 0 0 1 2 2v2H7a2 2 0 0 1-2-2v-1\"/><path d=\"M8 17v3M17 17v3\"/></svg>",
                "road" => a + "<path d=\"M8 21L10.5 3M16 21L13.5 3\"/><path d=\"M12 6v2M12 11v2M12 16v2\"/></svg>",
                "card" => a + "<rect x=\"3\" y=\"6\" width=\"18\" height=\"12\" rx=\"2\"/><path d=\"M3 10h18M7 15h4\"/></svg>",
                "people" => a + "<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 19c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M16 13.6c2.8 0 5 1.8 5 4.9\"/></svg>",
                "bag" => a + "<rect x=\"4\" y=\"8\" width=\"16\" height=\"12\" rx=\"2\"/><path d=\"M9 8V5.5A1.5 1.5 0 0 1 10.5 4h3A1.5 1.5 0 0 1 15 5.5V8M9 12v4M15 12v4\"/></svg>",
                "gear" => a + "<circle cx=\"6\" cy=\"6\" r=\"1.8\"/><circle cx=\"12\" cy=\"6\" r=\"1.8\"/><circle cx=\"18\" cy=\"6\" r=\"1.8\"/><circle cx=\"6\" cy=\"18\" r=\"1.8\"/><circle cx=\"12\" cy=\"18\" r=\"1.8\"/><path d=\"M6 8v8M12 8v8M18 8v4H6\"/></svg>",
                "door" => a + "<path d=\"M5 20V8l6-4h8v16z\"/><path d=\"M5 12h14M15 15h2\"/></svg>",
                _ => string.Empty
            };
        }

        private static string Logo(int size)
        {
            return "<svg viewBox=\"0 0 40 40\" width=\"" + size + "\" height=\"" + size + "\" aria-hidden=\"true\"><rect width=\"40\" height=\"40\" rx=\"9\" fill=\"#17322A\"/><path d=\"M9 20.5L20 11l11 9.5\" fill=\"none\" stroke=\"#FFFFFF\" stroke-width=\"2.6\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/><path d=\"M13 18.5V30h14V18.5\" fill=\"none\" stroke=\"#FFFFFF\" stroke-width=\"2.6\" stroke-linejoin=\"round\"/><path d=\"M20 21.5l2.6 2.6L20 28l-2.6-3.9z\" fill=\"#B4472B\"/></svg>";
        }

        private static string Pic(List<MediaItem> media, string src, string alt, bool eager, bool thumb)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            string use = thumb && m != null && m.Thumb.Length > 0 ? m.Thumb : src;
            return "<img src=\"" + Ui.Esc(use) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + " decoding=\"async\">";
        }

        private static string Mv(List<MediaItem> media, string src, string alt, bool bg, bool eager)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            if (Blocks.IsVideo(src))
            {
                return "<video src=\"" + Ui.Esc(src) + "\" playsinline " + (bg ? "autoplay muted loop aria-hidden=\"true\"" : "controls preload=\"metadata\" aria-label=\"" + Ui.Esc(alt) + "\"") + "></video>";
            }
            return Pic(media, src, alt, eager, false);
        }

        private static string Btn(SiteData site, string label, string link, bool main)
        {
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"po-btn" + (main ? string.Empty : " po-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"po-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"po-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"po-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"po-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "po-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"po-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"po-wrap po-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            List<BlockItem> more = b.Items.Where(i => i.Media != string.Empty && !Blocks.IsVideo(i.Media) && media.Any(m => m.Path == i.Media)).ToList();
            string text = "<div class=\"po-wrap po-bhero-in\"><div class=\"po-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"po-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "po-blead") + Btns(site, b) + "</div></div>";
            if (mv != string.Empty && more.Count > 0)
            {
                MediaItem? first = media.FirstOrDefault(m => m.Path == b.Media);
                List<(string Html, string Cap)> all = new List<(string, string)> { (mv, first?.Caption ?? string.Empty) };
                foreach (BlockItem it in more)
                {
                    MediaItem? m = media.FirstOrDefault(x => x.Path == it.Media);
                    all.Add(("<img src=\"" + Ui.Esc(it.Media) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : it.Title) + "\" decoding=\"async\">", it.Title));
                }
                StringBuilder sl = new StringBuilder("<div class=\"po-bhero-p po-slides\" id=\"po-slides\" aria-roledescription=\"carousel\" aria-label=\"Pictures\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<figure class=\"po-slide" + (k == 0 ? " po-on" : string.Empty) + "\" aria-roledescription=\"slide\" aria-label=\"" + (k + 1) + " of " + all.Count + "\"" + (k == 0 ? string.Empty : " aria-hidden=\"true\"") + ">" + all[k].Html + (all[k].Cap == string.Empty ? string.Empty : "<figcaption>" + Ui.Esc(all[k].Cap) + "</figcaption>") + "</figure>");
                }
                sl.Append("<div class=\"po-sctl\"><button type=\"button\" class=\"po-sarr\" aria-label=\"Previous picture\" onclick=\"PortalJs.slide(-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 6l-6 6 6 6\"/></svg></button><div class=\"po-sdots\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<button type=\"button\" class=\"po-sdot" + (k == 0 ? " po-on" : string.Empty) + "\" aria-label=\"Show picture " + (k + 1) + "\" aria-pressed=\"" + (k == 0 ? "true" : "false") + "\" onclick=\"PortalJs.slideTo(" + k + ")\"></button>");
                }
                sl.Append("</div><button type=\"button\" class=\"po-sarr\" aria-label=\"Next picture\" onclick=\"PortalJs.slide(1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg></button></div></div>");
                return "<section class=\"po-bhero po-bslides\" id=\"" + Ui.Esc(b.Id) + "\">" + sl + text + "</section>";
            }
            return "<section class=\"po-bhero" + (mv == string.Empty ? " po-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"po-bhero-p\">" + mv + "</div>" : string.Empty) + text + "</section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"po-bsplit" + (b.Flip ? " po-bflip" : string.Empty) + (b.Media == string.Empty ? " po-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"po-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"po-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"po-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"po-bcards po-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"po-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                sb.Append(it.Link != string.Empty ? "<a class=\"po-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"po-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>" : "<div class=\"po-bcard\">" + inner + "</div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"po-bsteps po-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            int i = 1;
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<li><i>" + i++ + "</i><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><p>" + Lines(Tok(site, it.Text)) + "</p></li>");
            }
            sb.Append("</ol>");
            return Sec(b, sb.ToString());
        }

        private static string StatRow(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"po-bstats po-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"po-bpeople po-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                string[] parts = it.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string ini = parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
                sb.Append("<div class=\"po-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"po-bnoface\">" + Ui.Esc(ini) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"po-bfaq\">" + Head(site, b, false) + "<div class=\"po-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"po-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"po-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"po-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "po-blead") + "</div>" + Btns(site, b) + "</div>");
        }

        private string PageHtml(PublicData d, string key)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            if (!d.Site.Pages.TryGetValue(key, out PageDoc? doc))
            {
                return string.Empty;
            }
            foreach (Block b in doc.Blocks.Where(x => !x.Hidden))
            {
                string html = b.Type switch
                {
                    "hero" => Hero(d.Site, d.Media, b, first),
                    "text" => Sec(b, "<div class=\"po-btext\">" + Kick(d.Site, b) + H2(d.Site, b, first ? "h1" : "h2") + Rich(d.Site, b.Text, string.Empty) + "</div>"),
                    "split" => Split(d.Site, d.Media, b),
                    "cards" => Cards(d.Site, b),
                    "steps" => Steps(d.Site, b),
                    "stats" => StatRow(d.Site, b),
                    "people" => People(d.Site, d.Media, b),
                    "faq" => Faq(d.Site, b),
                    "media" => Wide(d.Site, d.Media, b),
                    "cta" => Cta(d.Site, b),
                    "data" => DataPart(d, b, first),
                    _ => string.Empty
                };
                if (html.Length > 0 && (b.Type == "hero" || b.Type == "data" || b.Type == "text"))
                {
                    first = false;
                }
                sb.Append(html);
            }
            return sb.ToString();
        }

        private static string HeadHtml(PublicData d, string active)
        {
            StringBuilder sb = new StringBuilder("<a class=\"po-skip\" href=\"#po-main\">Skip to content</a><header class=\"po-head\" id=\"po-head\"><div class=\"po-wrap po-head-row\"><a class=\"po-logo\" href=\"Home\" aria-label=\"" + Ui.Esc(d.Site.Legal) + " home\">" + Logo(40) + "<span><b>" + Ui.Esc(d.Site.Brand) + "</b><small>Property Management</small></span></a>");
            sb.Append("<nav class=\"po-nav\" id=\"po-nav\" aria-label=\"Main\"><div class=\"po-nav-top\"><b>Menu</b><button type=\"button\" class=\"po-ibtn\" aria-label=\"Close menu\" onclick=\"PortalJs.closeNav()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div><ul class=\"po-menu\">");
            foreach ((string page, string name) in NavItems)
            {
                sb.Append("<li><a class=\"po-ml" + (page == active ? " po-on" : string.Empty) + "\" href=\"" + page + "\"" + (page == active ? " aria-current=\"page\"" : string.Empty) + ">" + name + "</a></li>");
            }
            sb.Append("<li class=\"po-mmanage\"><a class=\"po-ml" + (active == "Portal" ? " po-on" : string.Empty) + "\" href=\"Portal\">Resident portal</a></li></ul></nav><a class=\"po-btn po-btn-ghost po-btn-sm po-head-mg\" href=\"Portal\">Resident portal</a><a class=\"po-btn po-btn-sm po-head-cta\" href=\"Units\">See units</a><button type=\"button\" class=\"po-burger\" aria-label=\"Open menu\" onclick=\"PortalJs.openNav()\"><svg viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M4 12h16M4 17h16\"/></svg></button></div><div class=\"po-scrim\" id=\"po-scrim\" onclick=\"PortalJs.closeNav()\"></div></header>");
            return sb.ToString();
        }

        private static string FootHtml(PublicData d)
        {
            SiteData s = d.Site;
            StringBuilder sb = new StringBuilder("<footer class=\"po-foot\"><div class=\"po-wrap po-foot-grid\"><div class=\"po-fbrand\"><a class=\"po-logo\" href=\"Home\">" + Logo(40) + "<span><b>" + Ui.Esc(s.Brand) + "</b><small>Property Management</small></span></a><p>" + Ui.Esc(s.Tagline) + "</p><p class=\"po-fcontact\"><a href=\"tel:" + Ui.Esc(s.Phone) + "\">" + Ui.Esc(s.Phone) + "</a><br><a href=\"mailto:" + Ui.Esc(s.Email) + "\">" + Ui.Esc(s.Email) + "</a></p></div>");
            sb.Append("<div><h3>Rent</h3><ul><li><a href=\"Units\">Available units</a></li><li><a href=\"Properties\">Properties</a></li><li><a href=\"Policies\">Policies</a></li><li><a href=\"Contact\">Contact</a></li></ul></div>");
            sb.Append("<div><h3>Properties</h3><ul>" + string.Concat(d.Properties.Take(6).Select(p => "<li><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Name) + "</a></li>")) + "</ul></div>");
            sb.Append("<div><h3>Office</h3><ul class=\"po-fbr\"><li><b>" + Ui.Esc(s.Street) + "</b><span>" + Ui.Esc(s.City + ", " + s.Region + " " + s.Zip) + "</span></li><li><span>" + Lines(s.Hours) + "</span></li><li><span><a href=\"Portal\">Resident portal</a> &middot; <a href=\"AdminLogin\">Staff</a></span></li></ul></div></div>");
            sb.Append("<div class=\"po-foot-b\"><div class=\"po-wrap\"><span>&copy; " + DateTime.Now.Year + " " + Ui.Esc(s.Legal) + ". Equal housing opportunity.</span></div></div></footer>");
            return sb.ToString();
        }

        private static string Facts(UnitRec u)
        {
            return "<ul class=\"po-specs\"><li title=\"Bedrooms\">" + Icon("bed") + Ui.Esc(Fmt.Beds(u.Beds)) + "</li><li title=\"Bathrooms\">" + Icon("bath") + Ui.Esc(Fmt.Baths(u.Baths)) + "</li>" + (u.Sqft > 0 ? "<li title=\"Size\">" + Icon("area") + u.Sqft.ToString("N0", Fmt.Us) + " sq ft</li>" : string.Empty) + (u.PetsOk ? "<li title=\"Pets\">" + Icon("paw") + "Pets OK</li>" : string.Empty) + "</ul>";
        }

        private static string Ready(UnitRec u)
        {
            DateTime m = Rent.MoveInDate(u);
            return m <= DateTime.Today ? "Available now" : "Available " + Fmt.Short(m);
        }

        private static string UnitCard(PublicData d, UnitRec u)
        {
            PropertyRec? p = d.Properties.FirstOrDefault(x => x.Id == u.PropertyId);
            string photo = u.Photos.FirstOrDefault() ?? p?.Media ?? string.Empty;
            string href = "Unit?u=" + Ui.Esc(u.Id);
            return "<article class=\"po-ccard\"><a class=\"po-ccard-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, photo, p?.Name ?? u.Number, false, true) + "<span class=\"po-ready" + (Rent.MoveInDate(u) <= DateTime.Today ? " po-now" : string.Empty) + "\">" + Ready(u) + "</span></a><div class=\"po-ccard-b\"><div class=\"po-ccard-h\"><h3><a href=\"" + href + "\">" + Ui.Esc((p?.Name ?? string.Empty) + " · " + u.Number) + "</a></h3><small>" + Ui.Esc(p == null ? string.Empty : p.Kind + " · " + p.City) + "</small></div>" + Facts(u) + "<div class=\"po-ccard-f\"><span class=\"po-from\"><b>" + Fmt.Whole(u.Rent) + "</b><small>a month</small></span><a class=\"po-btn po-btn-sm\" href=\"" + href + "\">View</a></div></div></article>";
        }

        private static string PropertyCard(PublicData d, PropertyRec p)
        {
            int open = d.Units.Count(u => u.PropertyId == p.Id && Rent.Offered(u));
            List<UnitRec> mine = d.Units.Where(u => u.PropertyId == p.Id).ToList();
            string rents = mine.Count == 0 ? string.Empty : "From " + Fmt.Whole(mine.Min(u => u.Rent)) + " a month";
            return "<article class=\"po-pcard\"><a class=\"po-pcard-img\" href=\"Property?p=" + Ui.Esc(p.Id) + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, p.Media, p.Name, false, true) + "</a><div class=\"po-pcard-b\"><small class=\"po-kind\">" + Ui.Esc(p.Kind) + "</small><h3><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Name) + "</a></h3><p class=\"po-addr\">" + Icon("pin") + "<span>" + Ui.Esc(p.Street + ", " + p.City) + "</span></p><p class=\"po-pmeta\"><span>" + Ui.Esc(rents) + "</span><b class=\"po-open" + (open == 0 ? " po-none" : string.Empty) + "\">" + (open == 0 ? "Fully rented" : Ui.Count(open, "unit ready", "units ready")) + "</b></p></div></article>";
        }

        private static string UnitSearch(PublicData d, string title, string prop, int beds, int max, string move, bool pets, bool full)
        {
            StringBuilder sb = new StringBuilder("<form class=\"po-search\" id=\"find\" action=\"Units\" method=\"get\" onsubmit=\"return PortalJs.find(this)\">" + (title.Length > 0 ? "<h2 class=\"po-search-t\">" + Ui.Esc(title) + "</h2>" : string.Empty) + "<div class=\"po-search-row" + (full ? " po-search-full" : string.Empty) + "\">");
            if (full)
            {
                sb.Append("<label class=\"po-sf\"><span>" + Icon("home") + "Property</span><select name=\"p\" aria-label=\"Property\"><option value=\"\">Any property</option>" + string.Concat(d.Properties.Select(p => "<option value=\"" + Ui.Esc(p.Id) + "\"" + (p.Id == prop ? " selected" : string.Empty) + ">" + Ui.Esc(p.Name) + "</option>")) + "</select></label>");
            }
            sb.Append("<label class=\"po-sf\"><span>" + Icon("bed") + "Bedrooms</span><select name=\"b\" aria-label=\"Bedrooms\">" + string.Concat(new[] { (-1, "Any"), (0, "Studio"), (1, "1 or more"), (2, "2 or more"), (3, "3 or more"), (4, "4 or more") }.Select(o => "<option value=\"" + (o.Item1 < 0 ? string.Empty : o.Item1.ToString(CultureInfo.InvariantCulture)) + "\"" + (o.Item1 == beds ? " selected" : string.Empty) + ">" + o.Item2 + "</option>")) + "</select></label>");
            sb.Append("<label class=\"po-sf\"><span>" + Icon("card") + "Most rent</span><select name=\"r\" aria-label=\"Most rent a month\">" + string.Concat(new[] { 0, 1300, 1600, 2000, 2500, 3000 }.Select(v => "<option value=\"" + (v == 0 ? string.Empty : v.ToString(CultureInfo.InvariantCulture)) + "\"" + (v == max ? " selected" : string.Empty) + ">" + (v == 0 ? "No limit" : "Up to " + Fmt.Whole(v)) + "</option>")) + "</select></label>");
            sb.Append("<label class=\"po-sf\"><span>" + Icon("calendar") + "Move in by</span><input type=\"date\" name=\"m\" value=\"" + Ui.Esc(move) + "\" min=\"" + Fmt.IsoDay(DateTime.Today) + "\"></label>");
            sb.Append("<label class=\"po-sf po-sf-chk\"><input type=\"checkbox\" name=\"pets\" value=\"1\"" + (pets ? " checked" : string.Empty) + "><span>" + Icon("paw") + "Pets</span></label>");
            sb.Append("<button type=\"submit\" class=\"po-btn po-sbtn\">Search</button></div></form>");
            return sb.ToString();
        }
    }
}
