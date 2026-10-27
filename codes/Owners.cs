using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class Owners : WebPage
    {
        private static readonly Dictionary<string, List<DateTime>> Recent = new Dictionary<string, List<DateTime>>();
        private static readonly string[] Kinds = { "House", "Townhome", "Condo", "Small building (2 to 10 homes)", "Larger building (over 10 homes)" };
        private static readonly string[] States = { "Rented now", "Empty now", "Not yet bought" };

        public override async Task OnInitialized()
        {
            PublicData d = await Store.LoadPublic();
            HtmlDoc.SetTitle("Property management for owners | " + d.Site.Legal);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", "Tenant finding, rent collection, maintenance and monthly statements for rental owners in " + d.Site.City + ".");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_head}", HeadHtml(d, "Owners"))
                .Replace("{plhd_main}", PageHtml(d, "owners"))
                .Replace("{plhd_foot}", FootHtml(d));
        }

        private string DataPart(PublicData d, Block b, bool first)
        {
            if (b.Source != "ownerform")
            {
                return string.Empty;
            }
            SiteData s = d.Site;
            StringBuilder sb = new StringBuilder("<section class=\"ow-wrap ow-sec\" id=\"owner\"><div class=\"ow-ctwo\"><div class=\"ow-details\">" + Head(s, b, true, first ? "h1" : "h2") + "<ul class=\"ow-contact\"><li>" + Icon("phone") + "<a href=\"tel:" + Ui.Esc(s.Phone) + "\">" + Ui.Esc(s.Phone) + "</a></li><li>" + Icon("mail") + "<a href=\"mailto:" + Ui.Esc(s.Email) + "\">" + Ui.Esc(s.Email) + "</a></li></ul>");
            sb.Append("<div class=\"ow-fee\"><small>Management fee</small><b>" + s.ManagementFee.ToString("0.#", Fmt.Us) + "%</b><span>of rent collected. No fee while the home is empty.</span></div></div>");
            sb.Append("<form class=\"ow-form ow-panel\" id=\"ow-form\" onsubmit=\"return OwnersJs.send()\" novalidate><h2>About you and the property</h2>");
            sb.Append("<label class=\"ow-field\" for=\"ow-name\"><span>Your name</span><input type=\"text\" id=\"ow-name\" maxlength=\"60\" autocomplete=\"name\"><em class=\"ow-err\" id=\"ow-e-name\"></em></label>");
            sb.Append("<div class=\"ow-grid2\"><label class=\"ow-field\" for=\"ow-email\"><span>Email</span><input type=\"email\" id=\"ow-email\" maxlength=\"80\" autocomplete=\"email\"><em class=\"ow-err\" id=\"ow-e-email\"></em></label><label class=\"ow-field\" for=\"ow-phone\"><span>Phone</span><input type=\"tel\" id=\"ow-phone\" maxlength=\"30\" autocomplete=\"tel\"><em class=\"ow-err\" id=\"ow-e-phone\"></em></label></div>");
            sb.Append("<label class=\"ow-field\" for=\"ow-addr\"><span>Property address</span><input type=\"text\" id=\"ow-addr\" maxlength=\"160\" autocomplete=\"off\"><em class=\"ow-err\" id=\"ow-e-addr\"></em></label>");
            sb.Append("<div class=\"ow-grid3\"><label class=\"ow-field\" for=\"ow-kind\"><span>Type</span><select id=\"ow-kind\">" + string.Concat(Kinds.Select(k => "<option>" + Ui.Esc(k) + "</option>")) + "</select></label><label class=\"ow-field\" for=\"ow-homes\"><span>Number of homes</span><input type=\"text\" id=\"ow-homes\" maxlength=\"4\" inputmode=\"numeric\" value=\"1\"><em class=\"ow-err\" id=\"ow-e-homes\"></em></label><label class=\"ow-field\" for=\"ow-state\"><span>Right now it is</span><select id=\"ow-state\">" + string.Concat(States.Select(k => "<option>" + Ui.Esc(k) + "</option>")) + "</select></label></div>");
            sb.Append("<label class=\"ow-field\" for=\"ow-msg\"><span>Anything else? (optional)</span><textarea id=\"ow-msg\" rows=\"4\" maxlength=\"2000\" placeholder=\"Current rent, repairs needed, when you would like us to start\"></textarea><em class=\"ow-err\" id=\"ow-e-msg\"></em></label>");
            sb.Append("<div class=\"ow-hp\" aria-hidden=\"true\"><label for=\"ow-web\">Leave this field empty</label><input type=\"text\" id=\"ow-web\" tabindex=\"-1\" autocomplete=\"off\"></div>");
            sb.Append("<button type=\"submit\" class=\"ow-btn ow-wide\" id=\"ow-send\">Ask for a call back</button><div class=\"ow-sent\" id=\"ow-sent\" role=\"status\" aria-live=\"polite\"></div></form></div></section>");
            return sb.ToString();
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

        public async Task<ApiResponse> Ask()
        {
            ApiResponse response = new ApiResponse();
            PublicData d = await Store.LoadPublic();
            string name = Ui.Clip(GetDataValue("name"), 60);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string addr = Ui.Clip(GetDataValue("addr"), 160);
            string kind = Ui.Clip(GetDataValue("kind"), 60);
            kind = Kinds.Contains(kind) ? kind : Kinds[0];
            string state = Ui.Clip(GetDataValue("state"), 30);
            state = States.Contains(state) ? state : States[0];
            int homes = Fmt.Int(GetDataValue("homes"));
            string msg = (GetDataValue("msg") ?? string.Empty).Replace("\r", string.Empty).Trim();
            string trap = (GetDataValue("web") ?? string.Empty).Trim();
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 ? "Please enter your name." : string.Empty,
                ["email"] = !Ui.IsEmail(email) ? "Please enter a valid email address." : string.Empty,
                ["phone"] = !Ui.IsPhone(phone) ? "Please enter a phone number so we can call you back." : string.Empty,
                ["addr"] = addr.Length < 6 ? "Please enter the property address." : string.Empty,
                ["homes"] = homes < 1 || homes > 999 ? "Enter a number from 1 to 999." : string.Empty,
                ["msg"] = msg.Length > 2000 ? "Please keep it under 2,000 characters." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("ow-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                response.SetElementContents("ow-sent", string.Empty);
                response.ExecuteScript("OwnersJs.failed();");
                return response;
            }
            string who = "ow:" + Local.Visitor();
            if (trap.Length > 0)
            {
                response.SetElementContents("ow-sent", string.Empty);
                response.SetElementContents("ow-pop-t", "Request received");
                response.SetElementContents("ow-pop-m", "<p>Thank you. Your request was received.</p>");
                response.ExecuteScript("OwnersJs.sent();");
                return response;
            }
            if (!Allowed(who))
            {
                response.SetElementContents("ow-sent", "<span class=\"ow-serr2\">You have sent several requests in a short time. Please wait a few minutes, or call us on " + Ui.Esc(d.Site.Phone) + ".</span>");
                response.ExecuteScript("OwnersJs.failed();");
                return response;
            }
            Noted(who);
            string text = "Property: " + addr + "\nType: " + kind + "\nHomes: " + homes + "\nRight now: " + state + (msg.Length > 0 ? "\n\n" + msg : string.Empty);
            InquiryRec rec = new InquiryRec { Id = Fmt.NewId("m-"), Name = name, Email = email, Phone = phone, Topic = "Owner inquiry", Text = text, When = DateTime.UtcNow, Status = "New" };
            using (await Store.Hold())
            {
                List<InquiryRec> rows = await Store.Rows<InquiryRec>("messages");
                rows.Add(rec);
                await Store.SaveRows("messages", rows);
            }
            await Store.Log("visitor", "owner inquiry", name, true);
            if (Mailer.Ready && Ui.IsEmail(d.Site.NotifyEmail))
            {
                string result = await Mailer.Send(d.Site.NotifyEmail, "Owner inquiry from " + name, "From: " + name + " <" + email + ">\nPhone: " + phone + "\n" + text + "\n");
                if (result.Length > 0)
                {
                    await Store.Log("system", "notification email", result, false);
                }
            }
            response.SetElementContents("ow-sent", string.Empty);
            response.SetElementContents("ow-pop-t", "Request received");
            response.SetElementContents("ow-pop-m", "<p>Thank you, " + Ui.Esc(name) + ". We have your request.</p><p>We will call you on <b>" + Ui.Esc(phone) + "</b> within one working day.</p>");
            response.ExecuteScript("OwnersJs.sent();");
            return response;
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
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"ow-btn" + (main ? string.Empty : " ow-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"ow-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"ow-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"ow-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"ow-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "ow-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"ow-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"ow-wrap ow-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            List<BlockItem> more = b.Items.Where(i => i.Media != string.Empty && !Blocks.IsVideo(i.Media) && media.Any(m => m.Path == i.Media)).ToList();
            string text = "<div class=\"ow-wrap ow-bhero-in\"><div class=\"ow-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"ow-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "ow-blead") + Btns(site, b) + "</div></div>";
            if (mv != string.Empty && more.Count > 0)
            {
                MediaItem? first = media.FirstOrDefault(m => m.Path == b.Media);
                List<(string Html, string Cap)> all = new List<(string, string)> { (mv, first?.Caption ?? string.Empty) };
                foreach (BlockItem it in more)
                {
                    MediaItem? m = media.FirstOrDefault(x => x.Path == it.Media);
                    all.Add(("<img src=\"" + Ui.Esc(it.Media) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : it.Title) + "\" decoding=\"async\">", it.Title));
                }
                StringBuilder sl = new StringBuilder("<div class=\"ow-bhero-p ow-slides\" id=\"ow-slides\" aria-roledescription=\"carousel\" aria-label=\"Pictures\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<figure class=\"ow-slide" + (k == 0 ? " ow-on" : string.Empty) + "\" aria-roledescription=\"slide\" aria-label=\"" + (k + 1) + " of " + all.Count + "\"" + (k == 0 ? string.Empty : " aria-hidden=\"true\"") + ">" + all[k].Html + (all[k].Cap == string.Empty ? string.Empty : "<figcaption>" + Ui.Esc(all[k].Cap) + "</figcaption>") + "</figure>");
                }
                sl.Append("<div class=\"ow-sctl\"><button type=\"button\" class=\"ow-sarr\" aria-label=\"Previous picture\" onclick=\"OwnersJs.slide(-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 6l-6 6 6 6\"/></svg></button><div class=\"ow-sdots\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<button type=\"button\" class=\"ow-sdot" + (k == 0 ? " ow-on" : string.Empty) + "\" aria-label=\"Show picture " + (k + 1) + "\" aria-pressed=\"" + (k == 0 ? "true" : "false") + "\" onclick=\"OwnersJs.slideTo(" + k + ")\"></button>");
                }
                sl.Append("</div><button type=\"button\" class=\"ow-sarr\" aria-label=\"Next picture\" onclick=\"OwnersJs.slide(1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg></button></div></div>");
                return "<section class=\"ow-bhero ow-bslides\" id=\"" + Ui.Esc(b.Id) + "\">" + sl + text + "</section>";
            }
            return "<section class=\"ow-bhero" + (mv == string.Empty ? " ow-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"ow-bhero-p\">" + mv + "</div>" : string.Empty) + text + "</section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"ow-bsplit" + (b.Flip ? " ow-bflip" : string.Empty) + (b.Media == string.Empty ? " ow-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"ow-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"ow-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"ow-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"ow-bcards ow-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"ow-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                sb.Append(it.Link != string.Empty ? "<a class=\"ow-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"ow-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>" : "<div class=\"ow-bcard\">" + inner + "</div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"ow-bsteps ow-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
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
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"ow-bstats ow-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"ow-bpeople ow-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                string[] parts = it.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string ini = parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
                sb.Append("<div class=\"ow-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"ow-bnoface\">" + Ui.Esc(ini) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"ow-bfaq\">" + Head(site, b, false) + "<div class=\"ow-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"ow-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"ow-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"ow-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "ow-blead") + "</div>" + Btns(site, b) + "</div>");
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
                    "text" => Sec(b, "<div class=\"ow-btext\">" + Kick(d.Site, b) + H2(d.Site, b, first ? "h1" : "h2") + Rich(d.Site, b.Text, string.Empty) + "</div>"),
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
            StringBuilder sb = new StringBuilder("<a class=\"ow-skip\" href=\"#ow-main\">Skip to content</a><header class=\"ow-head\" id=\"ow-head\"><div class=\"ow-wrap ow-head-row\"><a class=\"ow-logo\" href=\"Home\" aria-label=\"" + Ui.Esc(d.Site.Legal) + " home\">" + Logo(40) + "<span><b>" + Ui.Esc(d.Site.Brand) + "</b><small>Property Management</small></span></a>");
            sb.Append("<nav class=\"ow-nav\" id=\"ow-nav\" aria-label=\"Main\"><div class=\"ow-nav-top\"><b>Menu</b><button type=\"button\" class=\"ow-ibtn\" aria-label=\"Close menu\" onclick=\"OwnersJs.closeNav()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div><ul class=\"ow-menu\">");
            foreach ((string page, string name) in NavItems)
            {
                sb.Append("<li><a class=\"ow-ml" + (page == active ? " ow-on" : string.Empty) + "\" href=\"" + page + "\"" + (page == active ? " aria-current=\"page\"" : string.Empty) + ">" + name + "</a></li>");
            }
            sb.Append("<li class=\"ow-mmanage\"><a class=\"ow-ml" + (active == "Portal" ? " ow-on" : string.Empty) + "\" href=\"Portal\">Resident portal</a></li></ul></nav><a class=\"ow-btn ow-btn-ghost ow-btn-sm ow-head-mg\" href=\"Portal\">Resident portal</a><a class=\"ow-btn ow-btn-sm ow-head-cta\" href=\"Units\">See units</a><button type=\"button\" class=\"ow-burger\" aria-label=\"Open menu\" onclick=\"OwnersJs.openNav()\"><svg viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M4 12h16M4 17h16\"/></svg></button></div><div class=\"ow-scrim\" id=\"ow-scrim\" onclick=\"OwnersJs.closeNav()\"></div></header>");
            return sb.ToString();
        }

        private static string FootHtml(PublicData d)
        {
            SiteData s = d.Site;
            StringBuilder sb = new StringBuilder("<footer class=\"ow-foot\"><div class=\"ow-wrap ow-foot-grid\"><div class=\"ow-fbrand\"><a class=\"ow-logo\" href=\"Home\">" + Logo(40) + "<span><b>" + Ui.Esc(s.Brand) + "</b><small>Property Management</small></span></a><p>" + Ui.Esc(s.Tagline) + "</p><p class=\"ow-fcontact\"><a href=\"tel:" + Ui.Esc(s.Phone) + "\">" + Ui.Esc(s.Phone) + "</a><br><a href=\"mailto:" + Ui.Esc(s.Email) + "\">" + Ui.Esc(s.Email) + "</a></p></div>");
            sb.Append("<div><h3>Rent</h3><ul><li><a href=\"Units\">Available units</a></li><li><a href=\"Properties\">Properties</a></li><li><a href=\"Policies\">Policies</a></li><li><a href=\"Contact\">Contact</a></li></ul></div>");
            sb.Append("<div><h3>Properties</h3><ul>" + string.Concat(d.Properties.Take(6).Select(p => "<li><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Name) + "</a></li>")) + "</ul></div>");
            sb.Append("<div><h3>Office</h3><ul class=\"ow-fbr\"><li><b>" + Ui.Esc(s.Street) + "</b><span>" + Ui.Esc(s.City + ", " + s.Region + " " + s.Zip) + "</span></li><li><span>" + Lines(s.Hours) + "</span></li><li><span><a href=\"Portal\">Resident portal</a> &middot; <a href=\"AdminLogin\">Staff</a></span></li></ul></div></div>");
            sb.Append("<div class=\"ow-foot-b\"><div class=\"ow-wrap\"><span>&copy; " + DateTime.Now.Year + " " + Ui.Esc(s.Legal) + ". Equal housing opportunity.</span></div></div></footer>");
            return sb.ToString();
        }

        private static string Facts(UnitRec u)
        {
            return "<ul class=\"ow-specs\"><li title=\"Bedrooms\">" + Icon("bed") + Ui.Esc(Fmt.Beds(u.Beds)) + "</li><li title=\"Bathrooms\">" + Icon("bath") + Ui.Esc(Fmt.Baths(u.Baths)) + "</li>" + (u.Sqft > 0 ? "<li title=\"Size\">" + Icon("area") + u.Sqft.ToString("N0", Fmt.Us) + " sq ft</li>" : string.Empty) + (u.PetsOk ? "<li title=\"Pets\">" + Icon("paw") + "Pets OK</li>" : string.Empty) + "</ul>";
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
            return "<article class=\"ow-ccard\"><a class=\"ow-ccard-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, photo, p?.Name ?? u.Number, false, true) + "<span class=\"ow-ready" + (Rent.MoveInDate(u) <= DateTime.Today ? " ow-now" : string.Empty) + "\">" + Ready(u) + "</span></a><div class=\"ow-ccard-b\"><div class=\"ow-ccard-h\"><h3><a href=\"" + href + "\">" + Ui.Esc((p?.Name ?? string.Empty) + " · " + u.Number) + "</a></h3><small>" + Ui.Esc(p == null ? string.Empty : p.Kind + " · " + p.City) + "</small></div>" + Facts(u) + "<div class=\"ow-ccard-f\"><span class=\"ow-from\"><b>" + Fmt.Whole(u.Rent) + "</b><small>a month</small></span><a class=\"ow-btn ow-btn-sm\" href=\"" + href + "\">View</a></div></div></article>";
        }

        private static string PropertyCard(PublicData d, PropertyRec p)
        {
            int open = d.Units.Count(u => u.PropertyId == p.Id && Rent.Offered(u));
            List<UnitRec> mine = d.Units.Where(u => u.PropertyId == p.Id).ToList();
            string rents = mine.Count == 0 ? string.Empty : "From " + Fmt.Whole(mine.Min(u => u.Rent)) + " a month";
            return "<article class=\"ow-pcard\"><a class=\"ow-pcard-img\" href=\"Property?p=" + Ui.Esc(p.Id) + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, p.Media, p.Name, false, true) + "</a><div class=\"ow-pcard-b\"><small class=\"ow-kind\">" + Ui.Esc(p.Kind) + "</small><h3><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Name) + "</a></h3><p class=\"ow-addr\">" + Icon("pin") + "<span>" + Ui.Esc(p.Street + ", " + p.City) + "</span></p><p class=\"ow-pmeta\"><span>" + Ui.Esc(rents) + "</span><b class=\"ow-open" + (open == 0 ? " ow-none" : string.Empty) + "\">" + (open == 0 ? "Fully rented" : Ui.Count(open, "unit ready", "units ready")) + "</b></p></div></article>";
        }

        private static string UnitSearch(PublicData d, string title, string prop, int beds, int max, string move, bool pets, bool full)
        {
            StringBuilder sb = new StringBuilder("<form class=\"ow-search\" id=\"find\" action=\"Units\" method=\"get\" onsubmit=\"return OwnersJs.find(this)\">" + (title.Length > 0 ? "<h2 class=\"ow-search-t\">" + Ui.Esc(title) + "</h2>" : string.Empty) + "<div class=\"ow-search-row" + (full ? " ow-search-full" : string.Empty) + "\">");
            if (full)
            {
                sb.Append("<label class=\"ow-sf\"><span>" + Icon("home") + "Property</span><select name=\"p\" aria-label=\"Property\"><option value=\"\">Any property</option>" + string.Concat(d.Properties.Select(p => "<option value=\"" + Ui.Esc(p.Id) + "\"" + (p.Id == prop ? " selected" : string.Empty) + ">" + Ui.Esc(p.Name) + "</option>")) + "</select></label>");
            }
            sb.Append("<label class=\"ow-sf\"><span>" + Icon("bed") + "Bedrooms</span><select name=\"b\" aria-label=\"Bedrooms\">" + string.Concat(new[] { (-1, "Any"), (0, "Studio"), (1, "1 or more"), (2, "2 or more"), (3, "3 or more"), (4, "4 or more") }.Select(o => "<option value=\"" + (o.Item1 < 0 ? string.Empty : o.Item1.ToString(CultureInfo.InvariantCulture)) + "\"" + (o.Item1 == beds ? " selected" : string.Empty) + ">" + o.Item2 + "</option>")) + "</select></label>");
            sb.Append("<label class=\"ow-sf\"><span>" + Icon("card") + "Most rent</span><select name=\"r\" aria-label=\"Most rent a month\">" + string.Concat(new[] { 0, 1300, 1600, 2000, 2500, 3000 }.Select(v => "<option value=\"" + (v == 0 ? string.Empty : v.ToString(CultureInfo.InvariantCulture)) + "\"" + (v == max ? " selected" : string.Empty) + ">" + (v == 0 ? "No limit" : "Up to " + Fmt.Whole(v)) + "</option>")) + "</select></label>");
            sb.Append("<label class=\"ow-sf\"><span>" + Icon("calendar") + "Move in by</span><input type=\"date\" name=\"m\" value=\"" + Ui.Esc(move) + "\" min=\"" + Fmt.IsoDay(DateTime.Today) + "\"></label>");
            sb.Append("<label class=\"ow-sf ow-sf-chk\"><input type=\"checkbox\" name=\"pets\" value=\"1\"" + (pets ? " checked" : string.Empty) + "><span>" + Icon("paw") + "Pets</span></label>");
            sb.Append("<button type=\"submit\" class=\"ow-btn ow-sbtn\">Search</button></div></form>");
            return sb.ToString();
        }
    }
}
