using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminSettings : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Settings | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private const string AreaKey = "settings";

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminSettingsJs.denied();");
                return null;
            }
            return c;
        }

        private static bool Can(Ctx c, char letter)
        {
            return Access.Can(c.U, AreaKey, letter);
        }

        private static async Task<ApiResponse> Deny(ApiResponse response, Ctx c, string action, string message)
        {
            await Store.Log(c.U.Id, action, "no right", false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("se-brand", Ui.Esc(site.Brand));
            response.SetElementContents("se-menu", await Auth.Menu("se", "AdminSettings", c));
            response.SetElementContents("se-who", Auth.Who("se", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
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
            SiteData site = await Store.LoadSite();
            await Shell(response, c, site);
            response.SetElementContents("se-main", FormHtml(site, Can(c, 'E'), c.U.Manager, await Pay.Settings()));
            return response;
        }

        private static string Field(string id, string label, string value, int max, string hint = "", string type = "text")
        {
            return "<label class=\"se-field\" for=\"se-f-" + id + "\"><span>" + label + "</span><input type=\"" + type + "\" id=\"se-f-" + id + "\" data-k=\"" + id + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\">" + (hint.Length > 0 ? "<small>" + hint + "</small>" : string.Empty) + "<em class=\"se-err\" id=\"se-e-" + id + "\"></em></label>";
        }

        private static string Area(string id, string label, string value, int max, int rows, string hint = "")
        {
            return "<label class=\"se-field\" for=\"se-f-" + id + "\"><span>" + label + "</span><textarea id=\"se-f-" + id + "\" data-k=\"" + id + "\" rows=\"" + rows + "\" maxlength=\"" + max + "\">" + Ui.Esc(value) + "</textarea>" + (hint.Length > 0 ? "<small>" + hint + "</small>" : string.Empty) + "<em class=\"se-err\" id=\"se-e-" + id + "\"></em></label>";
        }

        private static string Inv(decimal v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormHtml(SiteData s, bool edit, bool manager, PaySettings pay)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"se-ph\"><div><h1>Settings</h1><p>Company facts shown on the website and in emails, and the rules every application, lease and rent charge follows.</p></div></div>");
            sb.Append("<form class=\"se-panel se-stack\" id=\"se-form\" onsubmit=\"return AdminSettingsJs.save()\" novalidate>");
            sb.Append("<h2>The company</h2><div class=\"se-grid2\">" + Field("brand", "Short name", s.Brand, 40, "Used in the header and in titles.") + Field("legal", "Full name", s.Legal, 80, "Used in the footer and in emails.") + "</div>");
            sb.Append(Field("tagline", "Tagline", s.Tagline, 140));
            sb.Append("<div class=\"se-grid2\">" + Field("phone", "Main phone", s.Phone, 30) + Field("email", "Main email", s.Email, 80, string.Empty, "email") + "</div>");
            sb.Append("<div class=\"se-grid2\">" + Field("street", "Office street address", s.Street, 80) + Field("city", "City", s.City, 40) + "</div>");
            sb.Append("<div class=\"se-grid2\">" + Field("region", "State", s.Region, 20) + Field("zip", "ZIP code", s.Zip, 10) + "</div>");
            sb.Append(Area("hours", "Office hours (one line per row)", s.Hours, 300, 3));
            sb.Append("<h2 class=\"se-sec\">Leasing rules</h2><div class=\"se-grid3\">" + Field("appfee", "Application fee (US dollars)", Inv(s.ApplicationFee), 8, "Per adult applicant. Shown on the Apply page.") + Field("times", "Income needed (times the rent)", Inv(s.IncomeTimes), 4, "Applications below this are flagged for review.") + Field("video", "Largest video upload (MB)", s.MaxVideoMb.ToString(CultureInfo.InvariantCulture), 3, string.Empty, "number") + "</div>");
            sb.Append("<h2 class=\"se-sec\">Rent</h2><div class=\"se-grid3\">" + Field("dueday", "Rent due on day", s.RentDueDay.ToString(CultureInfo.InvariantCulture), 2, "Day of the month rent is posted.", "number") + Field("grace", "Grace days", s.GraceDays.ToString(CultureInfo.InvariantCulture), 2, "A late fee is added when rent is still owed after these days.", "number") + Field("late", "Late fee (US dollars)", Inv(s.LateFee), 8) + "</div>");
            sb.Append("<div class=\"se-grid3\">" + Field("mgmt", "Management fee (% of rent collected)", Inv(s.ManagementFee), 5, "Used on owner statements.") + "</div>");
            sb.Append("<h2 class=\"se-sec\">Messages</h2>" + Field("notify", "Send a copy of each new application and message to", s.NotifyEmail, 80, Mailer.Ready ? "Mail is set up, so a copy is emailed here." : "Mail is not set up yet (see below), so nothing is emailed.", "email"));
            sb.Append(edit ? "<div class=\"se-acts\"><button type=\"submit\" class=\"se-btn\">Save settings</button></div>" : "<p class=\"se-hint\">You can view the settings but not change them.</p>");
            sb.Append("</form>");
            sb.Append("<section class=\"se-panel se-stack\"><h2>Email</h2><dl class=\"se-kv\"><dt>Mail server</dt><dd>" + Ui.Esc(Mailer.Server.Length > 0 ? Mailer.Server : "Not set") + "</dd><dt>Sender</dt><dd>" + Ui.Esc(Mailer.From.Length > 0 ? Mailer.From : "Not set") + "</dd><dt>Status</dt><dd>" + (Mailer.Ready ? "Ready. Residents and staff get emails." : "Not set up. The sample values in appConfig/application.cfg are placeholders.") + "</dd></dl><p class=\"se-hint\">The mail server, sender and password live in the app.mail lines of appConfig/application.cfg. Edit that file and restart the site. The password is never shown here.</p></section>");
            bool ready = pay.Enabled && (pay.SecretKey.StartsWith("sk_", StringComparison.Ordinal) || pay.SecretKey.StartsWith("rk_", StringComparison.Ordinal)) && pay.WebhookSecret.StartsWith("whsec_", StringComparison.Ordinal);
            sb.Append("<section class=\"se-panel se-stack\" id=\"se-pay\"><h2>Online rent payment</h2><p class=\"se-hint\">Residents pay by card from the resident portal on a Stripe payment page. The money goes to your Stripe account, and the payment is added to the lease when Stripe confirms it.</p><dl class=\"se-kv\"><dt>Status</dt><dd>" + (ready ? "On. Residents see Pay online in the portal." : pay.Enabled ? "Waiting for both keys." : "Off." + (Store.Demo() ? " In this demo, residents can try a test payment without Stripe." : string.Empty)) + "</dd><dt>Secret key</dt><dd>" + Ui.Esc(pay.SecretKey.Length > 0 ? Pay.Mask(pay.SecretKey) : "Not set") + "</dd><dt>Webhook secret</dt><dd>" + Ui.Esc(pay.WebhookSecret.Length > 0 ? Pay.Mask(pay.WebhookSecret) : "Not set") + "</dd><dt>Webhook address</dt><dd><code>" + Ui.Esc(Local.Base() + "stripe-webhook") + "</code></dd></dl>");
            if (manager)
            {
                sb.Append("<form class=\"se-stack\" id=\"se-payform\" onsubmit=\"return AdminSettingsJs.savePay()\" novalidate><div class=\"se-grid2\"><label class=\"se-field\" for=\"se-f-sk\"><span>Stripe secret key</span><input type=\"password\" id=\"se-f-sk\" data-k=\"sk\" maxlength=\"200\" autocomplete=\"off\" placeholder=\"" + (pay.SecretKey.Length > 0 ? "Leave empty to keep the saved key" : "sk_live_...") + "\"><small>Stripe dashboard, Developers, API keys.</small><em class=\"se-err\" id=\"se-e-sk\"></em></label><label class=\"se-field\" for=\"se-f-wh\"><span>Webhook signing secret</span><input type=\"password\" id=\"se-f-wh\" data-k=\"wh\" maxlength=\"200\" autocomplete=\"off\" placeholder=\"" + (pay.WebhookSecret.Length > 0 ? "Leave empty to keep the saved secret" : "whsec_...") + "\"><small>Add the webhook address above in Stripe for the checkout.session.completed event, then copy its signing secret.</small><em class=\"se-err\" id=\"se-e-wh\"></em></label></div><label class=\"se-check\"><input type=\"checkbox\" id=\"se-f-payon\" data-k=\"payon\"" + (pay.Enabled ? " checked" : string.Empty) + "> Turn on online payment</label><div class=\"se-acts\"><button type=\"submit\" class=\"se-btn\">Save payment settings</button></div></form>");
            }
            else
            {
                sb.Append("<p class=\"se-hint\">Only a manager can change the payment keys.</p>");
            }
            sb.Append("</section>");
            sb.Append("<section class=\"se-panel se-stack\"><h2>Run mode</h2><p class=\"se-hint\">Run mode is set in appConfig/application.cfg.</p></section>");
            if (Store.Demo() && manager)
            {
                sb.Append("<section class=\"se-panel se-stack se-danger\"><h2>Initialize data</h2><p class=\"se-hint\">Removes every unit, showing, application, lease, resident, rent charge and payment, online payment, deposit statement, expense, owner payout, work order, inspection, make-ready, notice, message and the activity log. Properties, owners, vendors, pages, settings, staff and the media library stay.</p><div><button type=\"button\" class=\"se-btn se-btn-danger\" onclick=\"AdminSettingsJs.initialize()\">Initialize data</button></div></section>");
            }
            return sb.ToString();
        }

        private static int Whole(string? v)
        {
            return int.TryParse((v ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : -1;
        }

        private static decimal Money(string? v)
        {
            return decimal.TryParse((v ?? string.Empty).Trim().TrimStart('$'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? Math.Round(d, 2) : -1;
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
                return await Deny(response, c, "save settings", "You can view the settings but not change them.");
            }
            string brand = Ui.Clip(GetDataValue("brand"), 40);
            string legal = Ui.Clip(GetDataValue("legal"), 80);
            string tagline = Ui.Clip(GetDataValue("tagline"), 140);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string notify = Ui.Clip(GetDataValue("notify"), 80);
            string street = Ui.Clip(GetDataValue("street"), 80);
            string city = Ui.Clip(GetDataValue("city"), 40);
            string region = Ui.Clip(GetDataValue("region"), 20);
            string zip = Ui.Clip(GetDataValue("zip"), 10);
            string hours = Ui.Clip(GetDataValue("hours"), 300);
            int video = Whole(GetDataValue("video"));
            int dueDay = Whole(GetDataValue("dueday"));
            int grace = Whole(GetDataValue("grace"));
            decimal appFee = Money(GetDataValue("appfee"));
            decimal times = Money(GetDataValue("times"));
            decimal late = Money(GetDataValue("late"));
            decimal mgmt = Money(GetDataValue("mgmt"));
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                { "brand", brand.Length < 2 ? "Please enter the short name." : string.Empty },
                { "legal", legal.Length < 2 ? "Please enter the full name." : string.Empty },
                { "tagline", string.Empty },
                { "phone", !Ui.IsPhone(phone) ? "Please enter a valid phone number." : string.Empty },
                { "email", !Ui.IsEmail(email) ? "Please enter a valid email address." : string.Empty },
                { "street", string.Empty },
                { "city", string.Empty },
                { "region", string.Empty },
                { "zip", string.Empty },
                { "hours", string.Empty },
                { "notify", notify.Length > 0 && !Ui.IsEmail(notify) ? "Please enter a valid email address, or leave it empty." : string.Empty },
                { "video", video < 1 || video > 100 ? "Enter a whole number from 1 to 100." : string.Empty },
                { "appfee", appFee < 0 || appFee > 500 ? "From 0 to 500." : string.Empty },
                { "times", times < 0 || times > 10 ? "From 0 to 10." : string.Empty },
                { "dueday", dueDay < 1 || dueDay > 28 ? "From 1 to 28." : string.Empty },
                { "grace", grace < 0 || grace > 20 ? "From 0 to 20 days." : string.Empty },
                { "late", late < 0 || late > 1000 ? "From 0 to 1,000." : string.Empty },
                { "mgmt", mgmt < 0 || mgmt > 30 ? "From 0 to 30." : string.Empty }
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("se-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                site.Brand = brand;
                site.Legal = legal;
                site.Tagline = tagline;
                site.Phone = phone;
                site.Email = email;
                site.NotifyEmail = notify;
                site.Street = street;
                site.City = city;
                site.Region = region;
                site.Zip = zip;
                site.Hours = hours;
                site.MaxVideoMb = video;
                site.ApplicationFee = appFee;
                site.IncomeTimes = times;
                site.RentDueDay = dueDay;
                site.GraceDays = grace;
                site.LateFee = late;
                site.ManagementFee = mgmt;
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "save settings", brand, true);
            SiteData fresh = await Store.LoadSite();
            response.SetElementContents("se-brand", Ui.Esc(fresh.Brand));
            response.ExecuteScript("AdminSettingsJs.saved();");
            return response;
        }

        public async Task<ApiResponse> SavePay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.U.Manager)
            {
                await Store.Log(c.U.Id, "payment settings", "not a manager", false);
                response.ExecuteScript("AdminSettingsJs.toast('Only a manager can change the payment keys.');");
                return response;
            }
            string sk = (GetDataValue("sk") ?? string.Empty).Trim();
            string wh = (GetDataValue("wh") ?? string.Empty).Trim();
            bool on = (GetDataValue("payon") ?? string.Empty) == "1";
            PaySettings p = await Pay.Settings();
            string e1 = sk.Length > 0 && !(sk.StartsWith("sk_live_", StringComparison.Ordinal) || sk.StartsWith("sk_test_", StringComparison.Ordinal) || sk.StartsWith("rk_", StringComparison.Ordinal)) ? "A Stripe secret key starts with sk_live_, sk_test_ or rk_." : on && sk.Length == 0 && p.SecretKey.Length == 0 ? "Enter the secret key to turn payment on." : string.Empty;
            string e2 = wh.Length > 0 && !wh.StartsWith("whsec_", StringComparison.Ordinal) ? "A webhook signing secret starts with whsec_." : on && wh.Length == 0 && p.WebhookSecret.Length == 0 ? "Enter the webhook signing secret to turn payment on." : string.Empty;
            response.SetElementContents("se-e-sk", e1);
            response.SetElementContents("se-e-wh", e2);
            if (e1.Length > 0 || e2.Length > 0)
            {
                return response;
            }
            if (sk.Length > 0)
            {
                p.SecretKey = sk;
            }
            if (wh.Length > 0)
            {
                p.WebhookSecret = wh;
            }
            p.Enabled = on;
            p.Updated = DateTime.Now;
            p.By = c.U.Id;
            using (await Store.Hold())
            {
                await Pay.SaveSettings(p);
            }
            await Store.Log(c.U.Id, "payment settings", on ? "online payment on" : "online payment off", true);
            SiteData site = await Store.LoadSite();
            response.SetElementContents("se-main", FormHtml(site, Can(c, 'E'), c.U.Manager, p));
            response.ExecuteScript("AdminSettingsJs.toast('Payment settings saved');");
            return response;
        }

        public async Task<ApiResponse> Initialize()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.U.Manager)
            {
                return await Deny(response, c, "initialize data", "Only a manager can initialize the data.");
            }
            if (!Store.Demo())
            {
                return await Refuse(response, c, "initialize data", "settings", "In Production the data is initialized once, on the first start. Edit records in their own pages instead.");
            }
            if ((GetDataValue("word") ?? string.Empty).Trim() != "INITIALIZE")
            {
                response.ExecuteScript("AdminSettingsJs.toast('Type INITIALIZE to confirm.');");
                return response;
            }
            await Store.Log(c.U.Id, "initialize data", "requested from Settings", true);
            int removed = await Init.Run("Settings, by " + c.U.Name, false, true);
            response.ExecuteScript("AdminSettingsJs.initialized(" + removed + ");");
            return response;
        }
    }
}
