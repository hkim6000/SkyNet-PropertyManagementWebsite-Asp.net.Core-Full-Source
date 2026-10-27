using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class Unit : WebPage
    {
        private static readonly Dictionary<string, List<DateTime>> Recent = new Dictionary<string, List<DateTime>>();
        private static readonly int[] Weekday = { 10, 11, 13, 14, 15, 16, 17 };
        private static readonly int[] Saturday = { 10, 11, 12 };

        public override async Task OnInitialized()
        {
            await Seed.Keep();
            PublicData d = await Store.LoadPublic();
            string id = Ui.Clip(QueryValue("u"), 60);
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == id);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            string main;
            if (u == null || !Rent.Offered(u))
            {
                HtmlDoc.SetTitle("Unit not available | " + d.Site.Legal);
                main = "<div class=\"un-wrap un-ph\"><div class=\"un-crumbs\"><a href=\"Home\">Home</a> / <a href=\"Units\">Available units</a></div><h1>This home is not available</h1></div><section class=\"un-wrap un-sec\"><p class=\"un-hint\">It has been rented or taken off the market. These homes are ready now or soon.</p><div class=\"un-bbtns\"><a class=\"un-btn\" href=\"Units\">See available units</a></div></section>";
            }
            else
            {
                PropertyRec p = d.Properties.First(x => x.Id == u.PropertyId);
                HtmlDoc.SetTitle(Fmt.Beds(u.Beds) + " at " + p.Name + ", unit " + u.Number + " | " + d.Site.Legal);
                HtmlDoc.AddMetaElement("description", Fmt.Beds(u.Beds) + ", " + Fmt.Baths(u.Baths) + " for " + Fmt.Whole(u.Rent) + " a month at " + p.Name + ", " + p.City + ".");
                main = Detail(d, p, u);
            }
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_head}", HeadHtml(d, "Units"))
                .Replace("{plhd_main}", main)
                .Replace("{plhd_foot}", FootHtml(d));
        }

        private static string Gallery(PublicData d, List<string> pics, string alt)
        {
            List<string> list = pics.Where(x => x != string.Empty && d.Media.Any(m => m.Path == x && m.Kind is "photo" or "video")).Distinct().ToList();
            if (list.Count == 0)
            {
                return string.Empty;
            }
            List<(string Src, bool Video, string Alt)> items = list.Select(x =>
            {
                MediaItem? m = d.Media.First(y => y.Path == x);
                return (x, m.Kind == "video", m.Alt != string.Empty ? m.Alt : m.Caption != string.Empty ? m.Caption : alt);
            }).ToList();
            string json = System.Text.Json.JsonSerializer.Serialize(items.Select(x => new Dictionary<string, string> { ["s"] = x.Src, ["k"] = x.Video ? "video" : "photo", ["a"] = x.Alt }));
            int vids = items.Count(x => x.Video);
            string what = items.Count == 1 ? "View larger" : "Slide show &middot; " + items.Count;
            StringBuilder sb = new StringBuilder("<div class=\"un-gal\" id=\"un-gal\" data-items=\"" + Ui.Esc(json) + "\"><div class=\"un-galmain\"><div class=\"un-galstage\" id=\"un-galmain\">" + Stage(d.Media, items[0].Src, items[0].Video, items[0].Alt) + "</div><button type=\"button\" class=\"un-slidebtn\" onclick=\"UnitJs.show()\" aria-label=\"" + (items.Count == 1 ? "View larger" : "Open the slide show of " + items.Count + (vids > 0 ? " photos and videos" : " photos")) + "\"><svg viewBox=\"0 0 24 24\" width=\"14\" height=\"14\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M8 5v14l11-7z\"/></svg>" + what + "</button></div>");
            if (items.Count > 1)
            {
                sb.Append("<div class=\"un-thumbs\">");
                for (int i = 0; i < items.Count; i++)
                {
                    sb.Append("<button type=\"button\" class=\"un-thumb" + (i == 0 ? " un-on" : string.Empty) + "\" data-i=\"" + i + "\" aria-label=\"Show " + (items[i].Video ? "video " : "picture ") + (i + 1) + "\" aria-pressed=\"" + (i == 0 ? "true" : "false") + "\" onclick=\"UnitJs.pick(this)\">" + (items[i].Video ? "<video src=\"" + Ui.Esc(items[i].Src) + "#t=0.1\" muted preload=\"metadata\" playsinline tabindex=\"-1\" aria-hidden=\"true\"></video><i class=\"un-tplay\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"14\" height=\"14\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M8 5v14l11-7z\"/></svg></i>" : Pic(d.Media, items[i].Src, alt, false, true)) + "</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Stage(List<MediaItem> media, string src, bool video, string alt)
        {
            return video ? "<video src=\"" + Ui.Esc(src) + "\" controls playsinline preload=\"metadata\" aria-label=\"" + Ui.Esc(alt) + "\"></video>" : "<button type=\"button\" class=\"un-galzoom\" onclick=\"UnitJs.show()\" aria-label=\"Open the picture larger\">" + Pic(media, src, alt, true, false) + "</button>";
        }

        private static string Detail(PublicData d, PropertyRec p, UnitRec u)
        {
            SiteData s = d.Site;
            string title = p.Name + " · " + u.Number;
            StringBuilder sb = new StringBuilder("<div class=\"un-wrap un-ph\"><div class=\"un-crumbs\"><a href=\"Home\">Home</a> / <a href=\"Units\">Available units</a> / " + Ui.Esc(title) + "</div></div>");
            List<string> pics = new List<string>(u.Photos) { p.Media };
            sb.Append("<section class=\"un-wrap un-sec un-utop\">" + Gallery(d, pics, title) + "<div class=\"un-uinfo\"><div class=\"un-eyebrow\">" + Ui.Esc(p.Kind) + " &middot; " + (u.Floor <= 1 ? "Ground floor" : "Floor " + u.Floor) + "</div><h1>" + Ui.Esc(title) + "</h1><p class=\"un-uaddr\"><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Street + ", " + p.City + ", " + p.Region + " " + p.Zip) + "</a></p>" + Facts(u));
            sb.Append("<div class=\"un-rents\"><div><small>Rent</small><b>" + Fmt.Whole(u.Rent) + "</b><span>a month</span></div><div><small>Deposit</small><b>" + Fmt.Whole(u.Deposit) + "</b><span>once</span></div><div><small>Move in</small><b>" + (Rent.MoveInDate(u) <= DateTime.Today ? "Now" : Ui.Esc(Fmt.Short(Rent.MoveInDate(u)))) + "</b><span>" + (Rent.MoveInDate(u) <= DateTime.Today ? "ready today" : "or later") + "</span></div></div>");
            sb.Append("<p class=\"un-hint\">The application fee is " + Fmt.Whole(s.ApplicationFee) + " for each adult. We look for a household income of about " + s.IncomeTimes.ToString("0.#", Fmt.Us) + " times the rent (" + Fmt.Whole(u.Rent * s.IncomeTimes) + " a month).</p>");
            sb.Append("<div class=\"un-bbtns\"><a class=\"un-btn\" href=\"Apply?u=" + Ui.Esc(u.Id) + "\">Apply for this home</a><a class=\"un-btn un-btn-ghost\" href=\"#tour\">Book a tour</a></div></div></section>");
            sb.Append("<section class=\"un-wrap un-sec un-two\"><div class=\"un-stack\">");
            if (u.Features.Count > 0)
            {
                sb.Append("<h2 class=\"un-bh2\">In the home</h2><ul class=\"un-feat\">" + string.Concat(u.Features.Select(x => "<li>" + Icon("check") + "<span>" + Ui.Esc(x) + "</span></li>")) + "</ul>");
            }
            sb.Append("<h2 class=\"un-bh2\">At " + Ui.Esc(p.Name) + "</h2>" + (p.Amenities.Count > 0 ? "<ul class=\"un-feat\">" + string.Concat(p.Amenities.Select(x => "<li>" + Icon("check") + "<span>" + Ui.Esc(x) + "</span></li>")) + "</ul>" : string.Empty));
            sb.Append("<dl class=\"un-tripdl\"><dt>Pets</dt><dd>" + (u.PetsOk ? Ui.Esc(p.Pets != string.Empty ? p.Pets : "Pets are welcome.") : "No pets in this home.") + "</dd>" + (p.Parking != string.Empty ? "<dt>Parking</dt><dd>" + Ui.Esc(p.Parking) + "</dd>" : string.Empty) + "<dt>Lease</dt><dd>12 months is usual. Ask about other terms.</dd></dl>");
            if (u.Plan != string.Empty && d.Media.Any(m => m.Path == u.Plan))
            {
                sb.Append("<h2 class=\"un-bh2\">Floor plan</h2><figure class=\"un-plan\"><button type=\"button\" class=\"un-planbtn\" data-src=\"" + Ui.Esc(u.Plan) + "\" data-cap=\"" + Ui.Esc("Floor plan · " + title) + "\" aria-label=\"Enlarge the floor plan\" onclick=\"UnitJs.view(this)\">" + Pic(d.Media, u.Plan, "Floor plan of unit " + u.Number, false, false) + "<span class=\"un-zoom\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\"><circle cx=\"10.5\" cy=\"10.5\" r=\"6.5\"/><path d=\"M15.5 15.5L21 21M10.5 7.5v6M7.5 10.5h6\"/></svg>Click to enlarge</span></button></figure>");
            }
            sb.Append("</div>" + TourForm(u) + "</section>");
            List<UnitRec> more = d.Units.Where(x => x.Id != u.Id && x.PropertyId == p.Id && Rent.Offered(x)).OrderBy(Rent.MoveInDate).Take(4).ToList();
            if (more.Count > 0)
            {
                sb.Append("<section class=\"un-wrap un-sec\"><h2 class=\"un-bh2\">More at " + Ui.Esc(p.Name) + "</h2><div class=\"un-cgrid un-top\">" + string.Concat(more.Select(x => UnitCard(d, x))) + "</div></section>");
            }
            return sb.ToString();
        }

        private static string TourForm(UnitRec u)
        {
            DateTime first = DateTime.Today.AddDays(1);
            StringBuilder sb = new StringBuilder("<form class=\"un-form un-panel\" id=\"tour\" onsubmit=\"return UnitJs.send()\" novalidate><h2>Book a tour</h2><p class=\"un-hint\">Pick a time and a leasing agent will confirm by email. Tours run Monday to Saturday.</p><input type=\"hidden\" id=\"un-unit\" value=\"" + Ui.Esc(u.Id) + "\">");
            sb.Append("<label class=\"un-field\" for=\"un-name\"><span>Your name</span><input type=\"text\" id=\"un-name\" maxlength=\"60\" autocomplete=\"name\"><em class=\"un-err\" id=\"un-e-name\"></em></label>");
            sb.Append("<div class=\"un-grid2\"><label class=\"un-field\" for=\"un-email\"><span>Email</span><input type=\"email\" id=\"un-email\" maxlength=\"80\" autocomplete=\"email\"><em class=\"un-err\" id=\"un-e-email\"></em></label><label class=\"un-field\" for=\"un-phone\"><span>Phone</span><input type=\"tel\" id=\"un-phone\" maxlength=\"30\" autocomplete=\"tel\"><em class=\"un-err\" id=\"un-e-phone\"></em></label></div>");
            sb.Append("<div class=\"un-grid2\"><label class=\"un-field\" for=\"un-day\"><span>Day</span><input type=\"date\" id=\"un-day\" min=\"" + Fmt.IsoDay(first) + "\" max=\"" + Fmt.IsoDay(DateTime.Today.AddDays(30)) + "\" onchange=\"UnitJs.days()\"><em class=\"un-err\" id=\"un-e-day\"></em></label><label class=\"un-field\" for=\"un-slot\"><span>Time</span><select id=\"un-slot\">" + string.Concat(Weekday.Select(h => "<option value=\"" + h + "\">" + Hour(h) + "</option>")) + "</select><em class=\"un-err\" id=\"un-e-slot\"></em></label></div>");
            sb.Append("<label class=\"un-field\" for=\"un-note\"><span>Anything we should know? (optional)</span><textarea id=\"un-note\" rows=\"3\" maxlength=\"600\"></textarea></label>");
            sb.Append("<div class=\"un-hp\" aria-hidden=\"true\"><label for=\"un-web\">Leave this field empty</label><input type=\"text\" id=\"un-web\" tabindex=\"-1\" autocomplete=\"off\"></div>");
            sb.Append("<button type=\"submit\" class=\"un-btn un-wide\" id=\"un-send\">Request a tour</button><div class=\"un-sent\" id=\"un-sent\" role=\"status\" aria-live=\"polite\"></div></form>");
            return sb.ToString();
        }

        private static string Hour(int h)
        {
            return new DateTime(2000, 1, 1, h, 0, 0).ToString("h:mm tt", Fmt.Us);
        }

        private static bool Allowed(string visitor)
        {
            lock (Recent)
            {
                if (!Recent.TryGetValue(visitor, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Recent[visitor] = list;
                }
                list.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-10));
                return list.Count < 3;
            }
        }

        private static void Noted(string visitor)
        {
            lock (Recent)
            {
                if (!Recent.TryGetValue(visitor, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Recent[visitor] = list;
                }
                list.Add(DateTime.UtcNow);
            }
        }

        public async Task<ApiResponse> Tour()
        {
            ApiResponse response = new ApiResponse();
            PublicData d = await Store.LoadPublic();
            string unitId = Ui.Clip(GetDataValue("unit"), 60);
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == unitId && Rent.Offered(x));
            string name = Ui.Clip(GetDataValue("name"), 60);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string note = Ui.Clip((GetDataValue("note") ?? string.Empty).Replace("\r", string.Empty), 600);
            string trap = (GetDataValue("web") ?? string.Empty).Trim();
            bool dayOk = Fmt.ParseDate(GetDataValue("day"), out DateTime day);
            int slot = Fmt.Int(GetDataValue("slot"));
            string dayErr = !dayOk ? "Please pick a day." : day < DateTime.Today.AddDays(1) || day > DateTime.Today.AddDays(30) ? "Pick a day in the next 30 days, from tomorrow." : day.DayOfWeek == DayOfWeek.Sunday ? "We do not run tours on Sunday. Pick another day." : string.Empty;
            string slotErr = dayErr.Length == 0 && !(day.DayOfWeek == DayOfWeek.Saturday ? Saturday : Weekday).Contains(slot) ? "Pick one of the times shown for that day." : string.Empty;
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 ? "Please enter your name." : string.Empty,
                ["email"] = !Ui.IsEmail(email) ? "Please enter a valid email address." : string.Empty,
                ["phone"] = !Ui.IsPhone(phone) ? "Please enter a phone number so the agent can reach you." : string.Empty,
                ["day"] = dayErr,
                ["slot"] = slotErr
            };
            if (u == null)
            {
                response.SetElementContents("un-sent", "<span class=\"un-serr2\">This home is no longer available for tours.</span>");
                response.ExecuteScript("UnitJs.failed();");
                return response;
            }
            DateTime when = dayOk ? day.Date.AddHours(slot) : DateTime.MinValue;
            if (e.Values.All(v => v.Length == 0))
            {
                List<ShowingRec> taken = await Store.Rows<ShowingRec>("showings");
                if (taken.Any(x => x.UnitId == u.Id && x.When == when && x.Status != "Cancelled"))
                {
                    e["slot"] = "That time is already booked for this home. Pick another time.";
                }
            }
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("un-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                response.SetElementContents("un-sent", string.Empty);
                response.ExecuteScript("UnitJs.failed();");
                return response;
            }
            string who = "tour:" + Local.Visitor();
            if (trap.Length > 0)
            {
                response.SetElementContents("un-sent", string.Empty);
                response.SetElementContents("un-pop-t", "Tour requested");
                response.SetElementContents("un-pop-m", "<p>Thank you. Your request was received.</p>");
                response.ExecuteScript("UnitJs.sent();");
                return response;
            }
            if (!Allowed(who))
            {
                response.SetElementContents("un-sent", "<span class=\"un-serr2\">You have sent several requests in a short time. Please wait a few minutes, or call us on " + Ui.Esc(d.Site.Phone) + ".</span>");
                response.ExecuteScript("UnitJs.failed();");
                return response;
            }
            Noted(who);
            ShowingRec rec = new ShowingRec { Id = Fmt.NewId("s-"), UnitId = u.Id, Name = name, Email = email, Phone = phone, When = when, Status = "Requested", Note = note, Source = "website", Created = DateTime.Now };
            using (await Store.Hold())
            {
                List<ShowingRec> rows = await Store.Rows<ShowingRec>("showings");
                if (rows.Any(x => x.UnitId == u.Id && x.When == when && x.Status != "Cancelled"))
                {
                    response.SetElementContents("un-e-slot", "That time was just booked. Pick another time.");
                    response.ExecuteScript("UnitJs.failed();");
                    return response;
                }
                rows.Add(rec);
                await Store.SaveRows("showings", rows);
            }
            PropertyRec p = d.Properties.First(x => x.Id == u.PropertyId);
            await Store.Log("visitor", "tour request", p.Name + " · " + u.Number + " · " + Fmt.When(when), true);
            if (Mailer.Ready && Ui.IsEmail(d.Site.NotifyEmail))
            {
                string result = await Mailer.Send(d.Site.NotifyEmail, "Tour request: " + p.Name + " " + u.Number, "Who: " + name + " <" + email + ">, " + phone + "\nHome: " + p.Name + " · " + u.Number + "\nWhen: " + Fmt.When(when) + "\n\n" + note + "\n");
                if (result.Length > 0)
                {
                    await Store.Log("system", "notification email", result, false);
                }
            }
            response.SetElementContents("un-sent", string.Empty);
            response.SetElementContents("un-pop-t", "Tour requested");
            response.SetElementContents("un-pop-m", "<p>Thank you, " + Ui.Esc(name) + ". We have your request to see <b>" + Ui.Esc(p.Name + " · " + u.Number) + "</b> on <b>" + Ui.Esc(Fmt.When(when)) + "</b>.</p><p>A leasing agent will confirm by email or phone, usually the same working day.</p>");
            response.ExecuteScript("UnitJs.sent();");
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
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"un-btn" + (main ? string.Empty : " un-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"un-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"un-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"un-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"un-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "un-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"un-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"un-wrap un-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            List<BlockItem> more = b.Items.Where(i => i.Media != string.Empty && !Blocks.IsVideo(i.Media) && media.Any(m => m.Path == i.Media)).ToList();
            string text = "<div class=\"un-wrap un-bhero-in\"><div class=\"un-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"un-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "un-blead") + Btns(site, b) + "</div></div>";
            if (mv != string.Empty && more.Count > 0)
            {
                MediaItem? first = media.FirstOrDefault(m => m.Path == b.Media);
                List<(string Html, string Cap)> all = new List<(string, string)> { (mv, first?.Caption ?? string.Empty) };
                foreach (BlockItem it in more)
                {
                    MediaItem? m = media.FirstOrDefault(x => x.Path == it.Media);
                    all.Add(("<img src=\"" + Ui.Esc(it.Media) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : it.Title) + "\" decoding=\"async\">", it.Title));
                }
                StringBuilder sl = new StringBuilder("<div class=\"un-bhero-p un-slides\" id=\"un-slides\" aria-roledescription=\"carousel\" aria-label=\"Pictures\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<figure class=\"un-slide" + (k == 0 ? " un-on" : string.Empty) + "\" aria-roledescription=\"slide\" aria-label=\"" + (k + 1) + " of " + all.Count + "\"" + (k == 0 ? string.Empty : " aria-hidden=\"true\"") + ">" + all[k].Html + (all[k].Cap == string.Empty ? string.Empty : "<figcaption>" + Ui.Esc(all[k].Cap) + "</figcaption>") + "</figure>");
                }
                sl.Append("<div class=\"un-sctl\"><button type=\"button\" class=\"un-sarr\" aria-label=\"Previous picture\" onclick=\"UnitJs.slide(-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 6l-6 6 6 6\"/></svg></button><div class=\"un-sdots\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<button type=\"button\" class=\"un-sdot" + (k == 0 ? " un-on" : string.Empty) + "\" aria-label=\"Show picture " + (k + 1) + "\" aria-pressed=\"" + (k == 0 ? "true" : "false") + "\" onclick=\"UnitJs.slideTo(" + k + ")\"></button>");
                }
                sl.Append("</div><button type=\"button\" class=\"un-sarr\" aria-label=\"Next picture\" onclick=\"UnitJs.slide(1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg></button></div></div>");
                return "<section class=\"un-bhero un-bslides\" id=\"" + Ui.Esc(b.Id) + "\">" + sl + text + "</section>";
            }
            return "<section class=\"un-bhero" + (mv == string.Empty ? " un-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"un-bhero-p\">" + mv + "</div>" : string.Empty) + text + "</section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"un-bsplit" + (b.Flip ? " un-bflip" : string.Empty) + (b.Media == string.Empty ? " un-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"un-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"un-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"un-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"un-bcards un-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"un-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                sb.Append(it.Link != string.Empty ? "<a class=\"un-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"un-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>" : "<div class=\"un-bcard\">" + inner + "</div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"un-bsteps un-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
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
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"un-bstats un-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"un-bpeople un-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                string[] parts = it.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string ini = parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
                sb.Append("<div class=\"un-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"un-bnoface\">" + Ui.Esc(ini) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"un-bfaq\">" + Head(site, b, false) + "<div class=\"un-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"un-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"un-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"un-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "un-blead") + "</div>" + Btns(site, b) + "</div>");
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
                    "text" => Sec(b, "<div class=\"un-btext\">" + Kick(d.Site, b) + H2(d.Site, b, first ? "h1" : "h2") + Rich(d.Site, b.Text, string.Empty) + "</div>"),
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
            StringBuilder sb = new StringBuilder("<a class=\"un-skip\" href=\"#un-main\">Skip to content</a><header class=\"un-head\" id=\"un-head\"><div class=\"un-wrap un-head-row\"><a class=\"un-logo\" href=\"Home\" aria-label=\"" + Ui.Esc(d.Site.Legal) + " home\">" + Logo(40) + "<span><b>" + Ui.Esc(d.Site.Brand) + "</b><small>Property Management</small></span></a>");
            sb.Append("<nav class=\"un-nav\" id=\"un-nav\" aria-label=\"Main\"><div class=\"un-nav-top\"><b>Menu</b><button type=\"button\" class=\"un-ibtn\" aria-label=\"Close menu\" onclick=\"UnitJs.closeNav()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div><ul class=\"un-menu\">");
            foreach ((string page, string name) in NavItems)
            {
                sb.Append("<li><a class=\"un-ml" + (page == active ? " un-on" : string.Empty) + "\" href=\"" + page + "\"" + (page == active ? " aria-current=\"page\"" : string.Empty) + ">" + name + "</a></li>");
            }
            sb.Append("<li class=\"un-mmanage\"><a class=\"un-ml" + (active == "Portal" ? " un-on" : string.Empty) + "\" href=\"Portal\">Resident portal</a></li></ul></nav><a class=\"un-btn un-btn-ghost un-btn-sm un-head-mg\" href=\"Portal\">Resident portal</a><a class=\"un-btn un-btn-sm un-head-cta\" href=\"Units\">See units</a><button type=\"button\" class=\"un-burger\" aria-label=\"Open menu\" onclick=\"UnitJs.openNav()\"><svg viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M4 12h16M4 17h16\"/></svg></button></div><div class=\"un-scrim\" id=\"un-scrim\" onclick=\"UnitJs.closeNav()\"></div></header>");
            return sb.ToString();
        }

        private static string FootHtml(PublicData d)
        {
            SiteData s = d.Site;
            StringBuilder sb = new StringBuilder("<footer class=\"un-foot\"><div class=\"un-wrap un-foot-grid\"><div class=\"un-fbrand\"><a class=\"un-logo\" href=\"Home\">" + Logo(40) + "<span><b>" + Ui.Esc(s.Brand) + "</b><small>Property Management</small></span></a><p>" + Ui.Esc(s.Tagline) + "</p><p class=\"un-fcontact\"><a href=\"tel:" + Ui.Esc(s.Phone) + "\">" + Ui.Esc(s.Phone) + "</a><br><a href=\"mailto:" + Ui.Esc(s.Email) + "\">" + Ui.Esc(s.Email) + "</a></p></div>");
            sb.Append("<div><h3>Rent</h3><ul><li><a href=\"Units\">Available units</a></li><li><a href=\"Properties\">Properties</a></li><li><a href=\"Policies\">Policies</a></li><li><a href=\"Contact\">Contact</a></li></ul></div>");
            sb.Append("<div><h3>Properties</h3><ul>" + string.Concat(d.Properties.Take(6).Select(p => "<li><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Name) + "</a></li>")) + "</ul></div>");
            sb.Append("<div><h3>Office</h3><ul class=\"un-fbr\"><li><b>" + Ui.Esc(s.Street) + "</b><span>" + Ui.Esc(s.City + ", " + s.Region + " " + s.Zip) + "</span></li><li><span>" + Lines(s.Hours) + "</span></li><li><span><a href=\"Portal\">Resident portal</a> &middot; <a href=\"AdminLogin\">Staff</a></span></li></ul></div></div>");
            sb.Append("<div class=\"un-foot-b\"><div class=\"un-wrap\"><span>&copy; " + DateTime.Now.Year + " " + Ui.Esc(s.Legal) + ". Equal housing opportunity.</span></div></div></footer>");
            return sb.ToString();
        }

        private static string Facts(UnitRec u)
        {
            return "<ul class=\"un-specs\"><li title=\"Bedrooms\">" + Icon("bed") + Ui.Esc(Fmt.Beds(u.Beds)) + "</li><li title=\"Bathrooms\">" + Icon("bath") + Ui.Esc(Fmt.Baths(u.Baths)) + "</li>" + (u.Sqft > 0 ? "<li title=\"Size\">" + Icon("area") + u.Sqft.ToString("N0", Fmt.Us) + " sq ft</li>" : string.Empty) + (u.PetsOk ? "<li title=\"Pets\">" + Icon("paw") + "Pets OK</li>" : string.Empty) + "</ul>";
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
            return "<article class=\"un-ccard\"><a class=\"un-ccard-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, photo, p?.Name ?? u.Number, false, true) + "<span class=\"un-ready" + (Rent.MoveInDate(u) <= DateTime.Today ? " un-now" : string.Empty) + "\">" + Ready(u) + "</span></a><div class=\"un-ccard-b\"><div class=\"un-ccard-h\"><h3><a href=\"" + href + "\">" + Ui.Esc((p?.Name ?? string.Empty) + " · " + u.Number) + "</a></h3><small>" + Ui.Esc(p == null ? string.Empty : p.Kind + " · " + p.City) + "</small></div>" + Facts(u) + "<div class=\"un-ccard-f\"><span class=\"un-from\"><b>" + Fmt.Whole(u.Rent) + "</b><small>a month</small></span><a class=\"un-btn un-btn-sm\" href=\"" + href + "\">View</a></div></div></article>";
        }

        private static string PropertyCard(PublicData d, PropertyRec p)
        {
            int open = d.Units.Count(u => u.PropertyId == p.Id && Rent.Offered(u));
            List<UnitRec> mine = d.Units.Where(u => u.PropertyId == p.Id).ToList();
            string rents = mine.Count == 0 ? string.Empty : "From " + Fmt.Whole(mine.Min(u => u.Rent)) + " a month";
            return "<article class=\"un-pcard\"><a class=\"un-pcard-img\" href=\"Property?p=" + Ui.Esc(p.Id) + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, p.Media, p.Name, false, true) + "</a><div class=\"un-pcard-b\"><small class=\"un-kind\">" + Ui.Esc(p.Kind) + "</small><h3><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Name) + "</a></h3><p class=\"un-addr\">" + Icon("pin") + "<span>" + Ui.Esc(p.Street + ", " + p.City) + "</span></p><p class=\"un-pmeta\"><span>" + Ui.Esc(rents) + "</span><b class=\"un-open" + (open == 0 ? " un-none" : string.Empty) + "\">" + (open == 0 ? "Fully rented" : Ui.Count(open, "unit ready", "units ready")) + "</b></p></div></article>";
        }

        private static string UnitSearch(PublicData d, string title, string prop, int beds, int max, string move, bool pets, bool full)
        {
            StringBuilder sb = new StringBuilder("<form class=\"un-search\" id=\"find\" action=\"Units\" method=\"get\" onsubmit=\"return UnitJs.find(this)\">" + (title.Length > 0 ? "<h2 class=\"un-search-t\">" + Ui.Esc(title) + "</h2>" : string.Empty) + "<div class=\"un-search-row" + (full ? " un-search-full" : string.Empty) + "\">");
            if (full)
            {
                sb.Append("<label class=\"un-sf\"><span>" + Icon("home") + "Property</span><select name=\"p\" aria-label=\"Property\"><option value=\"\">Any property</option>" + string.Concat(d.Properties.Select(p => "<option value=\"" + Ui.Esc(p.Id) + "\"" + (p.Id == prop ? " selected" : string.Empty) + ">" + Ui.Esc(p.Name) + "</option>")) + "</select></label>");
            }
            sb.Append("<label class=\"un-sf\"><span>" + Icon("bed") + "Bedrooms</span><select name=\"b\" aria-label=\"Bedrooms\">" + string.Concat(new[] { (-1, "Any"), (0, "Studio"), (1, "1 or more"), (2, "2 or more"), (3, "3 or more"), (4, "4 or more") }.Select(o => "<option value=\"" + (o.Item1 < 0 ? string.Empty : o.Item1.ToString(CultureInfo.InvariantCulture)) + "\"" + (o.Item1 == beds ? " selected" : string.Empty) + ">" + o.Item2 + "</option>")) + "</select></label>");
            sb.Append("<label class=\"un-sf\"><span>" + Icon("card") + "Most rent</span><select name=\"r\" aria-label=\"Most rent a month\">" + string.Concat(new[] { 0, 1300, 1600, 2000, 2500, 3000 }.Select(v => "<option value=\"" + (v == 0 ? string.Empty : v.ToString(CultureInfo.InvariantCulture)) + "\"" + (v == max ? " selected" : string.Empty) + ">" + (v == 0 ? "No limit" : "Up to " + Fmt.Whole(v)) + "</option>")) + "</select></label>");
            sb.Append("<label class=\"un-sf\"><span>" + Icon("calendar") + "Move in by</span><input type=\"date\" name=\"m\" value=\"" + Ui.Esc(move) + "\" min=\"" + Fmt.IsoDay(DateTime.Today) + "\"></label>");
            sb.Append("<label class=\"un-sf un-sf-chk\"><input type=\"checkbox\" name=\"pets\" value=\"1\"" + (pets ? " checked" : string.Empty) + "><span>" + Icon("paw") + "Pets</span></label>");
            sb.Append("<button type=\"submit\" class=\"un-btn un-sbtn\">Search</button></div></form>");
            return sb.ToString();
        }
    }
}
