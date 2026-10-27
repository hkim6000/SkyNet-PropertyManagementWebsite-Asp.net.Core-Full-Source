using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class Properties : WebPage
    {
        public override async Task OnInitialized()
        {
            await Seed.Keep();
            PublicData d = await Store.LoadPublic();
            HtmlDoc.SetTitle("Properties | " + d.Site.Legal);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", "Apartments, townhomes and houses managed by " + d.Site.Legal + " in " + d.Site.City + ".");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_head}", HeadHtml(d, "Properties"))
                .Replace("{plhd_main}", PageHtml(d, "properties"))
                .Replace("{plhd_foot}", FootHtml(d));
        }

        private string DataPart(PublicData d, Block b, bool first)
        {
            if (b.Source != "propertylist")
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder(Head(d.Site, b, true, first ? "h1" : "h2") + "<div class=\"pp-plist\">");
            foreach (PropertyRec p in d.Properties)
            {
                List<UnitRec> mine = d.Units.Where(u => u.PropertyId == p.Id).ToList();
                int open = mine.Count(Rent.Offered);
                string beds = mine.Count == 0 ? string.Empty : (mine.Min(u => u.Beds) == mine.Max(u => u.Beds) ? Fmt.Beds(mine[0].Beds) : Fmt.Beds(mine.Min(u => u.Beds)) + " to " + Fmt.Beds(mine.Max(u => u.Beds)));
                sb.Append("<article class=\"pp-prow\" id=\"p-" + Ui.Esc(p.Id) + "\"><a class=\"pp-prow-img\" href=\"Property?p=" + Ui.Esc(p.Id) + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, p.Media, p.Name, false, false) + "</a><div class=\"pp-prow-b\"><small class=\"pp-kind\">" + Ui.Esc(p.Kind) + "</small><h2><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Name) + "</a></h2><p class=\"pp-addr\">" + Icon("pin") + "<span>" + Ui.Esc(p.Street + ", " + p.City + ", " + p.Region + " " + p.Zip) + "</span></p><p>" + Ui.Esc(p.Summary) + "</p>");
                sb.Append("<ul class=\"pp-chips\">" + string.Concat(p.Amenities.Take(5).Select(a => "<li>" + Ui.Esc(a) + "</li>")) + "</ul>");
                sb.Append("<p class=\"pp-pmeta\"><span>" + Ui.Esc(beds) + (mine.Count > 0 ? " &middot; from " + Fmt.Whole(mine.Min(u => u.Rent)) + " a month" : string.Empty) + "</span><b class=\"pp-open" + (open == 0 ? " pp-none" : string.Empty) + "\">" + (open == 0 ? "Fully rented" : Ui.Count(open, "unit ready", "units ready")) + "</b></p><div class=\"pp-bbtns\"><a class=\"pp-btn pp-btn-sm\" href=\"Property?p=" + Ui.Esc(p.Id) + "\">View property</a>" + (open > 0 ? "<a class=\"pp-btn pp-btn-ghost pp-btn-sm\" href=\"Units?p=" + Ui.Esc(p.Id) + "\">See units</a>" : string.Empty) + "</div></div></article>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
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
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"pp-btn" + (main ? string.Empty : " pp-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"pp-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"pp-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"pp-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"pp-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "pp-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"pp-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"pp-wrap pp-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            List<BlockItem> more = b.Items.Where(i => i.Media != string.Empty && !Blocks.IsVideo(i.Media) && media.Any(m => m.Path == i.Media)).ToList();
            string text = "<div class=\"pp-wrap pp-bhero-in\"><div class=\"pp-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"pp-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "pp-blead") + Btns(site, b) + "</div></div>";
            if (mv != string.Empty && more.Count > 0)
            {
                MediaItem? first = media.FirstOrDefault(m => m.Path == b.Media);
                List<(string Html, string Cap)> all = new List<(string, string)> { (mv, first?.Caption ?? string.Empty) };
                foreach (BlockItem it in more)
                {
                    MediaItem? m = media.FirstOrDefault(x => x.Path == it.Media);
                    all.Add(("<img src=\"" + Ui.Esc(it.Media) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : it.Title) + "\" decoding=\"async\">", it.Title));
                }
                StringBuilder sl = new StringBuilder("<div class=\"pp-bhero-p pp-slides\" id=\"pp-slides\" aria-roledescription=\"carousel\" aria-label=\"Pictures\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<figure class=\"pp-slide" + (k == 0 ? " pp-on" : string.Empty) + "\" aria-roledescription=\"slide\" aria-label=\"" + (k + 1) + " of " + all.Count + "\"" + (k == 0 ? string.Empty : " aria-hidden=\"true\"") + ">" + all[k].Html + (all[k].Cap == string.Empty ? string.Empty : "<figcaption>" + Ui.Esc(all[k].Cap) + "</figcaption>") + "</figure>");
                }
                sl.Append("<div class=\"pp-sctl\"><button type=\"button\" class=\"pp-sarr\" aria-label=\"Previous picture\" onclick=\"PropertiesJs.slide(-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 6l-6 6 6 6\"/></svg></button><div class=\"pp-sdots\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<button type=\"button\" class=\"pp-sdot" + (k == 0 ? " pp-on" : string.Empty) + "\" aria-label=\"Show picture " + (k + 1) + "\" aria-pressed=\"" + (k == 0 ? "true" : "false") + "\" onclick=\"PropertiesJs.slideTo(" + k + ")\"></button>");
                }
                sl.Append("</div><button type=\"button\" class=\"pp-sarr\" aria-label=\"Next picture\" onclick=\"PropertiesJs.slide(1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg></button></div></div>");
                return "<section class=\"pp-bhero pp-bslides\" id=\"" + Ui.Esc(b.Id) + "\">" + sl + text + "</section>";
            }
            return "<section class=\"pp-bhero" + (mv == string.Empty ? " pp-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"pp-bhero-p\">" + mv + "</div>" : string.Empty) + text + "</section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"pp-bsplit" + (b.Flip ? " pp-bflip" : string.Empty) + (b.Media == string.Empty ? " pp-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"pp-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"pp-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"pp-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"pp-bcards pp-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"pp-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                sb.Append(it.Link != string.Empty ? "<a class=\"pp-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"pp-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>" : "<div class=\"pp-bcard\">" + inner + "</div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"pp-bsteps pp-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
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
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"pp-bstats pp-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"pp-bpeople pp-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                string[] parts = it.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string ini = parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
                sb.Append("<div class=\"pp-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"pp-bnoface\">" + Ui.Esc(ini) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"pp-bfaq\">" + Head(site, b, false) + "<div class=\"pp-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"pp-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"pp-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"pp-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "pp-blead") + "</div>" + Btns(site, b) + "</div>");
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
                    "text" => Sec(b, "<div class=\"pp-btext\">" + Kick(d.Site, b) + H2(d.Site, b, first ? "h1" : "h2") + Rich(d.Site, b.Text, string.Empty) + "</div>"),
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
            StringBuilder sb = new StringBuilder("<a class=\"pp-skip\" href=\"#pp-main\">Skip to content</a><header class=\"pp-head\" id=\"pp-head\"><div class=\"pp-wrap pp-head-row\"><a class=\"pp-logo\" href=\"Home\" aria-label=\"" + Ui.Esc(d.Site.Legal) + " home\">" + Logo(40) + "<span><b>" + Ui.Esc(d.Site.Brand) + "</b><small>Property Management</small></span></a>");
            sb.Append("<nav class=\"pp-nav\" id=\"pp-nav\" aria-label=\"Main\"><div class=\"pp-nav-top\"><b>Menu</b><button type=\"button\" class=\"pp-ibtn\" aria-label=\"Close menu\" onclick=\"PropertiesJs.closeNav()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div><ul class=\"pp-menu\">");
            foreach ((string page, string name) in NavItems)
            {
                sb.Append("<li><a class=\"pp-ml" + (page == active ? " pp-on" : string.Empty) + "\" href=\"" + page + "\"" + (page == active ? " aria-current=\"page\"" : string.Empty) + ">" + name + "</a></li>");
            }
            sb.Append("<li class=\"pp-mmanage\"><a class=\"pp-ml" + (active == "Portal" ? " pp-on" : string.Empty) + "\" href=\"Portal\">Resident portal</a></li></ul></nav><a class=\"pp-btn pp-btn-ghost pp-btn-sm pp-head-mg\" href=\"Portal\">Resident portal</a><a class=\"pp-btn pp-btn-sm pp-head-cta\" href=\"Units\">See units</a><button type=\"button\" class=\"pp-burger\" aria-label=\"Open menu\" onclick=\"PropertiesJs.openNav()\"><svg viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M4 12h16M4 17h16\"/></svg></button></div><div class=\"pp-scrim\" id=\"pp-scrim\" onclick=\"PropertiesJs.closeNav()\"></div></header>");
            return sb.ToString();
        }

        private static string FootHtml(PublicData d)
        {
            SiteData s = d.Site;
            StringBuilder sb = new StringBuilder("<footer class=\"pp-foot\"><div class=\"pp-wrap pp-foot-grid\"><div class=\"pp-fbrand\"><a class=\"pp-logo\" href=\"Home\">" + Logo(40) + "<span><b>" + Ui.Esc(s.Brand) + "</b><small>Property Management</small></span></a><p>" + Ui.Esc(s.Tagline) + "</p><p class=\"pp-fcontact\"><a href=\"tel:" + Ui.Esc(s.Phone) + "\">" + Ui.Esc(s.Phone) + "</a><br><a href=\"mailto:" + Ui.Esc(s.Email) + "\">" + Ui.Esc(s.Email) + "</a></p></div>");
            sb.Append("<div><h3>Rent</h3><ul><li><a href=\"Units\">Available units</a></li><li><a href=\"Properties\">Properties</a></li><li><a href=\"Policies\">Policies</a></li><li><a href=\"Contact\">Contact</a></li></ul></div>");
            sb.Append("<div><h3>Properties</h3><ul>" + string.Concat(d.Properties.Take(6).Select(p => "<li><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Name) + "</a></li>")) + "</ul></div>");
            sb.Append("<div><h3>Office</h3><ul class=\"pp-fbr\"><li><b>" + Ui.Esc(s.Street) + "</b><span>" + Ui.Esc(s.City + ", " + s.Region + " " + s.Zip) + "</span></li><li><span>" + Lines(s.Hours) + "</span></li><li><span><a href=\"Portal\">Resident portal</a> &middot; <a href=\"AdminLogin\">Staff</a></span></li></ul></div></div>");
            sb.Append("<div class=\"pp-foot-b\"><div class=\"pp-wrap\"><span>&copy; " + DateTime.Now.Year + " " + Ui.Esc(s.Legal) + ". Equal housing opportunity.</span></div></div></footer>");
            return sb.ToString();
        }

        private static string Facts(UnitRec u)
        {
            return "<ul class=\"pp-specs\"><li title=\"Bedrooms\">" + Icon("bed") + Ui.Esc(Fmt.Beds(u.Beds)) + "</li><li title=\"Bathrooms\">" + Icon("bath") + Ui.Esc(Fmt.Baths(u.Baths)) + "</li>" + (u.Sqft > 0 ? "<li title=\"Size\">" + Icon("area") + u.Sqft.ToString("N0", Fmt.Us) + " sq ft</li>" : string.Empty) + (u.PetsOk ? "<li title=\"Pets\">" + Icon("paw") + "Pets OK</li>" : string.Empty) + "</ul>";
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
            return "<article class=\"pp-ccard\"><a class=\"pp-ccard-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, photo, p?.Name ?? u.Number, false, true) + "<span class=\"pp-ready" + (Rent.MoveInDate(u) <= DateTime.Today ? " pp-now" : string.Empty) + "\">" + Ready(u) + "</span></a><div class=\"pp-ccard-b\"><div class=\"pp-ccard-h\"><h3><a href=\"" + href + "\">" + Ui.Esc((p?.Name ?? string.Empty) + " · " + u.Number) + "</a></h3><small>" + Ui.Esc(p == null ? string.Empty : p.Kind + " · " + p.City) + "</small></div>" + Facts(u) + "<div class=\"pp-ccard-f\"><span class=\"pp-from\"><b>" + Fmt.Whole(u.Rent) + "</b><small>a month</small></span><a class=\"pp-btn pp-btn-sm\" href=\"" + href + "\">View</a></div></div></article>";
        }

        private static string PropertyCard(PublicData d, PropertyRec p)
        {
            int open = d.Units.Count(u => u.PropertyId == p.Id && Rent.Offered(u));
            List<UnitRec> mine = d.Units.Where(u => u.PropertyId == p.Id).ToList();
            string rents = mine.Count == 0 ? string.Empty : "From " + Fmt.Whole(mine.Min(u => u.Rent)) + " a month";
            return "<article class=\"pp-pcard\"><a class=\"pp-pcard-img\" href=\"Property?p=" + Ui.Esc(p.Id) + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, p.Media, p.Name, false, true) + "</a><div class=\"pp-pcard-b\"><small class=\"pp-kind\">" + Ui.Esc(p.Kind) + "</small><h3><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Name) + "</a></h3><p class=\"pp-addr\">" + Icon("pin") + "<span>" + Ui.Esc(p.Street + ", " + p.City) + "</span></p><p class=\"pp-pmeta\"><span>" + Ui.Esc(rents) + "</span><b class=\"pp-open" + (open == 0 ? " pp-none" : string.Empty) + "\">" + (open == 0 ? "Fully rented" : Ui.Count(open, "unit ready", "units ready")) + "</b></p></div></article>";
        }

        private static string UnitSearch(PublicData d, string title, string prop, int beds, int max, string move, bool pets, bool full)
        {
            StringBuilder sb = new StringBuilder("<form class=\"pp-search\" id=\"find\" action=\"Units\" method=\"get\" onsubmit=\"return PropertiesJs.find(this)\">" + (title.Length > 0 ? "<h2 class=\"pp-search-t\">" + Ui.Esc(title) + "</h2>" : string.Empty) + "<div class=\"pp-search-row" + (full ? " pp-search-full" : string.Empty) + "\">");
            if (full)
            {
                sb.Append("<label class=\"pp-sf\"><span>" + Icon("home") + "Property</span><select name=\"p\" aria-label=\"Property\"><option value=\"\">Any property</option>" + string.Concat(d.Properties.Select(p => "<option value=\"" + Ui.Esc(p.Id) + "\"" + (p.Id == prop ? " selected" : string.Empty) + ">" + Ui.Esc(p.Name) + "</option>")) + "</select></label>");
            }
            sb.Append("<label class=\"pp-sf\"><span>" + Icon("bed") + "Bedrooms</span><select name=\"b\" aria-label=\"Bedrooms\">" + string.Concat(new[] { (-1, "Any"), (0, "Studio"), (1, "1 or more"), (2, "2 or more"), (3, "3 or more"), (4, "4 or more") }.Select(o => "<option value=\"" + (o.Item1 < 0 ? string.Empty : o.Item1.ToString(CultureInfo.InvariantCulture)) + "\"" + (o.Item1 == beds ? " selected" : string.Empty) + ">" + o.Item2 + "</option>")) + "</select></label>");
            sb.Append("<label class=\"pp-sf\"><span>" + Icon("card") + "Most rent</span><select name=\"r\" aria-label=\"Most rent a month\">" + string.Concat(new[] { 0, 1300, 1600, 2000, 2500, 3000 }.Select(v => "<option value=\"" + (v == 0 ? string.Empty : v.ToString(CultureInfo.InvariantCulture)) + "\"" + (v == max ? " selected" : string.Empty) + ">" + (v == 0 ? "No limit" : "Up to " + Fmt.Whole(v)) + "</option>")) + "</select></label>");
            sb.Append("<label class=\"pp-sf\"><span>" + Icon("calendar") + "Move in by</span><input type=\"date\" name=\"m\" value=\"" + Ui.Esc(move) + "\" min=\"" + Fmt.IsoDay(DateTime.Today) + "\"></label>");
            sb.Append("<label class=\"pp-sf pp-sf-chk\"><input type=\"checkbox\" name=\"pets\" value=\"1\"" + (pets ? " checked" : string.Empty) + "><span>" + Icon("paw") + "Pets</span></label>");
            sb.Append("<button type=\"submit\" class=\"pp-btn pp-sbtn\">Search</button></div></form>");
            return sb.ToString();
        }
    }
}
