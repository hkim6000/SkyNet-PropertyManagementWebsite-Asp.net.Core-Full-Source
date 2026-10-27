using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class Apply : WebPage
    {
        private const int MaxPeople = 4;
        private static readonly Dictionary<string, List<DateTime>> Recent = new Dictionary<string, List<DateTime>>();
        private static readonly int[] Terms = { 6, 12, 18, 24 };

        public override async Task OnInitialized()
        {
            await Seed.Keep();
            PublicData d = await Store.LoadPublic();
            string id = Ui.Clip(QueryValue("u"), 60);
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == id);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, follow");
            string main;
            if (u == null || !Rent.Offered(u))
            {
                HtmlDoc.SetTitle("Apply | " + d.Site.Legal);
                main = "<div class=\"ap-wrap ap-ph\"><div class=\"ap-crumbs\"><a href=\"Home\">Home</a> / <a href=\"Units\">Available units</a></div><h1>Choose a home first</h1></div><section class=\"ap-wrap ap-sec\"><p class=\"ap-hint\">Applications are made for one home. Open a unit and press Apply for this home.</p><div class=\"ap-bbtns\"><a class=\"ap-btn\" href=\"Units\">See available units</a></div></section>";
            }
            else
            {
                PropertyRec p = d.Properties.First(x => x.Id == u.PropertyId);
                HtmlDoc.SetTitle("Apply for " + p.Name + " " + u.Number + " | " + d.Site.Legal);
                main = FormHtml(d, p, u);
            }
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_head}", HeadHtml(d, "Units"))
                .Replace("{plhd_main}", main)
                .Replace("{plhd_foot}", FootHtml(d));
        }

        private static string Field(string id, string label, string type, int max, string auto, string extra)
        {
            return "<label class=\"ap-field\" for=\"ap-" + id + "\"><span>" + label + "</span><input type=\"" + type + "\" id=\"ap-" + id + "\"" + (max > 0 ? " maxlength=\"" + max + "\"" : string.Empty) + (auto.Length > 0 ? " autocomplete=\"" + auto + "\"" : string.Empty) + extra + "><em class=\"ap-err\" id=\"ap-e-" + id + "\"></em></label>";
        }

        private static string Person(int i)
        {
            string a = "a" + i;
            StringBuilder sb = new StringBuilder("<fieldset class=\"ap-person\" id=\"ap-p" + i + "\"" + (i == 0 ? string.Empty : " hidden") + "><legend>" + (i == 0 ? "You" : "Co-applicant " + i) + "</legend>");
            if (i > 0)
            {
                sb.Append("<button type=\"button\" class=\"ap-btn ap-btn-ghost ap-btn-sm ap-premove\" onclick=\"ApplyJs.drop(" + i + ")\">Remove</button>");
            }
            sb.Append("<div class=\"ap-grid2\">" + Field(a + "-first", "First name", "text", 40, i == 0 ? "given-name" : "off", string.Empty) + Field(a + "-last", "Last name", "text", 40, i == 0 ? "family-name" : "off", string.Empty) + "</div>");
            sb.Append("<div class=\"ap-grid2\">" + Field(a + "-email", "Email", "email", 80, i == 0 ? "email" : "off", string.Empty) + Field(a + "-phone", i == 0 ? "Phone" : "Phone (optional)", "tel", 30, i == 0 ? "tel" : "off", string.Empty) + "</div>");
            sb.Append("<div class=\"ap-grid3\">" + Field(a + "-birth", "Date of birth", "date", 0, i == 0 ? "bday" : "off", " max=\"" + Fmt.IsoDay(DateTime.Today.AddYears(-18)) + "\"") + Field(a + "-employer", "Employer or school", "text", 80, i == 0 ? "organization" : "off", string.Empty) + Field(a + "-income", "Monthly income before tax", "text", 12, "off", " inputmode=\"decimal\" placeholder=\"$\"") + "</div></fieldset>");
            return sb.ToString();
        }

        private static string FormHtml(PublicData d, PropertyRec p, UnitRec u)
        {
            SiteData s = d.Site;
            DateTime first = Rent.MoveInDate(u) < DateTime.Today.AddDays(3) ? DateTime.Today.AddDays(3) : Rent.MoveInDate(u);
            StringBuilder sb = new StringBuilder("<div class=\"ap-wrap ap-ph\"><div class=\"ap-crumbs\"><a href=\"Home\">Home</a> / <a href=\"Units\">Available units</a> / <a href=\"Unit?u=" + Ui.Esc(u.Id) + "\">" + Ui.Esc(p.Name + " · " + u.Number) + "</a> / Apply</div><h1>Apply for " + Ui.Esc(p.Name + " · " + u.Number) + "</h1><div class=\"ap-rich ap-blead\"><p>One application for everyone aged 18 or over who will live in the home. It takes about ten minutes. We answer within two working days.</p></div></div>");
            sb.Append("<section class=\"ap-wrap ap-sec\" id=\"ap-out\"><div class=\"ap-book\"><form class=\"ap-bmain\" id=\"ap-form\" onsubmit=\"return ApplyJs.send()\" novalidate><input type=\"hidden\" id=\"ap-unit\" value=\"" + Ui.Esc(u.Id) + "\"><input type=\"hidden\" id=\"ap-count\" value=\"1\">");
            sb.Append("<div class=\"ap-panel ap-form\"><h2>The lease</h2><div class=\"ap-grid3\">" + Field("move", "Move-in date", "date", 0, "off", " value=\"" + Fmt.IsoDay(first) + "\" min=\"" + Fmt.IsoDay(first) + "\" max=\"" + Fmt.IsoDay(first.AddDays(90)) + "\"") + "<label class=\"ap-field\" for=\"ap-months\"><span>Lease length</span><select id=\"ap-months\">" + string.Concat(Terms.Select(t => "<option value=\"" + t + "\"" + (t == 12 ? " selected" : string.Empty) + ">" + t + " months</option>")) + "</select></label><label class=\"ap-field\" for=\"ap-occ\"><span>People living there</span><select id=\"ap-occ\">" + string.Concat(Enumerable.Range(1, 8).Select(n => "<option value=\"" + n + "\">" + n + "</option>")) + "</select><em class=\"ap-err\" id=\"ap-e-occ\"></em></label></div>");
            sb.Append("<label class=\"ap-field\" for=\"ap-pets\"><span>Pets (kind and size, or leave empty)</span><input type=\"text\" id=\"ap-pets\" maxlength=\"120\" placeholder=\"" + (u.PetsOk ? "For example: one cat" : "No pets are allowed in this home") + "\"" + (u.PetsOk ? string.Empty : " disabled") + "><em class=\"ap-err\" id=\"ap-e-pets\"></em></label></div>");
            sb.Append("<div class=\"ap-panel ap-form\"><h2>Who is applying</h2>");
            for (int i = 0; i < MaxPeople; i++)
            {
                sb.Append(Person(i));
            }
            sb.Append("<div class=\"ap-padd\"><button type=\"button\" class=\"ap-btn ap-btn-ghost ap-btn-sm\" id=\"ap-add\" onclick=\"ApplyJs.add()\">Add a co-applicant</button><small class=\"ap-hint\">Up to " + MaxPeople + " adults on one application.</small></div></div>");
            sb.Append("<div class=\"ap-panel ap-form\"><h2>Where you live now</h2>" + Field("addr", "Current address", "text", 160, "street-address", string.Empty) + "<div class=\"ap-grid3\">" + Field("lord", "Landlord or manager (optional)", "text", 80, "off", string.Empty) + Field("lordph", "Their phone (optional)", "tel", 30, "off", string.Empty) + "<label class=\"ap-field\" for=\"ap-years\"><span>Years at this address</span><select id=\"ap-years\">" + string.Concat(new[] { (0, "Less than 1"), (1, "1"), (2, "2"), (3, "3"), (5, "5 or more") }.Select(o => "<option value=\"" + o.Item1 + "\">" + o.Item2 + "</option>")) + "</select></label></div>");
            sb.Append("<label class=\"ap-field\" for=\"ap-msg\"><span>Anything else we should know? (optional)</span><textarea id=\"ap-msg\" rows=\"4\" maxlength=\"1500\"></textarea></label></div>");
            sb.Append("<div class=\"ap-panel ap-form\"><label class=\"ap-check\" for=\"ap-agree\"><input type=\"checkbox\" id=\"ap-agree\"><span>The information is true. I agree to a credit and background check for each applicant, and to the <a href=\"Policies\" target=\"_blank\" rel=\"noopener\">rental policies</a>.</span></label><em class=\"ap-err\" id=\"ap-e-agree\"></em>");
            sb.Append("<div class=\"ap-hp\" aria-hidden=\"true\"><label for=\"ap-web\">Leave this field empty</label><input type=\"text\" id=\"ap-web\" tabindex=\"-1\" autocomplete=\"off\"></div><button type=\"submit\" class=\"ap-btn ap-wide\" id=\"ap-send\">Send application</button><p class=\"ap-serr2\" id=\"ap-e-all\" role=\"alert\"></p></div></form>");
            sb.Append("<aside class=\"ap-side\"><div class=\"ap-panel ap-stack\">" + Pic(d.Media, u.Photos.FirstOrDefault() ?? p.Media, p.Name, true, true) + "<h3>" + Ui.Esc(p.Name + " · " + u.Number) + "</h3>" + Facts(u) + "<dl class=\"ap-tripdl\"><dt>Rent</dt><dd><b>" + Fmt.Whole(u.Rent) + "</b> a month</dd><dt>Deposit</dt><dd>" + Fmt.Whole(u.Deposit) + "</dd><dt>Ready</dt><dd>" + Ui.Esc(Ready(u)) + "</dd><dt>Fee</dt><dd>" + Fmt.Whole(s.ApplicationFee) + " per adult, paid when we start the review</dd><dt>Income</dt><dd>About " + Fmt.Whole(u.Rent * s.IncomeTimes) + " a month for the household</dd></dl></div></aside></div></section>");
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
                list.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-30));
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

        public async Task<ApiResponse> Send()
        {
            ApiResponse response = new ApiResponse();
            PublicData d = await Store.LoadPublic();
            string unitId = Ui.Clip(GetDataValue("unit"), 60);
            UnitRec? u = d.Units.FirstOrDefault(x => x.Id == unitId && Rent.Offered(x));
            if (u == null)
            {
                response.SetElementContents("ap-e-all", "This home is no longer available. Please choose another one.");
                response.ExecuteScript("ApplyJs.failed();");
                return response;
            }
            PropertyRec p = d.Properties.First(x => x.Id == u.PropertyId);
            Dictionary<string, string> e = new Dictionary<string, string>();
            DateTime first = Rent.MoveInDate(u) < DateTime.Today.AddDays(3) ? DateTime.Today.AddDays(3) : Rent.MoveInDate(u);
            bool moveOk = Fmt.ParseDate(GetDataValue("move"), out DateTime move);
            e["move"] = !moveOk ? "Please pick a move-in date." : move < first ? "The earliest move-in date is " + Fmt.Day(first) + "." : move > first.AddDays(90) ? "Pick a date within 90 days of " + Fmt.Day(first) + "." : string.Empty;
            int months = Fmt.Int(GetDataValue("months"));
            months = Terms.Contains(months) ? months : 12;
            int occ = Fmt.Int(GetDataValue("occ"));
            string pets = u.PetsOk ? Ui.Clip(GetDataValue("pets"), 120) : string.Empty;
            int count = Math.Clamp(Fmt.Int(GetDataValue("count")), 1, MaxPeople);
            List<Applicant> people = new List<Applicant>();
            for (int i = 0; i < MaxPeople; i++)
            {
                string a = "a" + i;
                if (i >= count)
                {
                    foreach (string f in new[] { "first", "last", "email", "phone", "birth", "employer", "income" })
                    {
                        e[a + "-" + f] = string.Empty;
                    }
                    continue;
                }
                string fn = Ui.Clip(GetDataValue(a + "first"), 40);
                string ln = Ui.Clip(GetDataValue(a + "last"), 40);
                string em = Ui.Clip(GetDataValue(a + "email"), 80);
                string ph = Ui.Clip(GetDataValue(a + "phone"), 30);
                string emp = Ui.Clip(GetDataValue(a + "employer"), 80);
                string incText = Ui.Clip(GetDataValue(a + "income"), 12);
                decimal inc = incText.Length == 0 ? 0m : Fmt.Dec(incText);
                bool bOk = Fmt.ParseDate(GetDataValue(a + "birth"), out DateTime birth);
                e[a + "-first"] = fn.Length < 1 ? "Enter the first name." : string.Empty;
                e[a + "-last"] = ln.Length < 1 ? "Enter the last name." : string.Empty;
                e[a + "-email"] = !Ui.IsEmail(em) ? "Enter a valid email address." : people.Any(x => x.Email.Equals(em, StringComparison.OrdinalIgnoreCase)) ? "Each applicant needs their own email address." : string.Empty;
                e[a + "-phone"] = i == 0 && !Ui.IsPhone(ph) ? "Enter a phone number." : i > 0 && ph.Length > 0 && !Ui.IsPhone(ph) ? "Enter a valid phone number, or leave it empty." : string.Empty;
                e[a + "-birth"] = !bOk ? "Enter the date of birth." : birth < DateTime.Today.AddYears(-110) ? "Check the year." : Fmt.Age(birth, DateTime.Today) < 18 ? "Applicants must be 18 or over. Younger people are listed as people living there." : string.Empty;
                e[a + "-employer"] = string.Empty;
                e[a + "-income"] = inc < 0 || inc > 1000000 ? "Enter the monthly amount in dollars, or leave it empty." : string.Empty;
                people.Add(new Applicant { First = fn, Last = ln, Email = em, Phone = ph, Birth = bOk ? birth : null, Employer = emp, Income = inc < 0 ? 0 : inc });
            }
            e["occ"] = occ < count || occ > 8 ? "Include every applicant in the number of people living there." : string.Empty;
            e["pets"] = string.Empty;
            string addr = Ui.Clip(GetDataValue("addr"), 160);
            string lord = Ui.Clip(GetDataValue("lord"), 80);
            string lordph = Ui.Clip(GetDataValue("lordph"), 30);
            int years = Math.Clamp(Fmt.Int(GetDataValue("years")), 0, 5);
            string msg = Ui.Clip((GetDataValue("msg") ?? string.Empty).Replace("\r", string.Empty), 1500);
            e["addr"] = addr.Length < 6 ? "Enter your current address." : string.Empty;
            e["lord"] = string.Empty;
            e["lordph"] = lordph.Length > 0 && !Ui.IsPhone(lordph) ? "Enter a valid phone number, or leave it empty." : string.Empty;
            e["agree"] = (GetDataValue("agree") ?? string.Empty).Trim() != "1" ? "Please tick the box to agree before sending." : string.Empty;
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("ap-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                response.SetElementContents("ap-e-all", "Please check the fields marked in red.");
                response.ExecuteScript("ApplyJs.failed();");
                return response;
            }
            response.SetElementContents("ap-e-all", string.Empty);
            string trap = (GetDataValue("web") ?? string.Empty).Trim();
            string who = "apply:" + Local.Visitor();
            if (trap.Length > 0)
            {
                response.SetElementContents("ap-out", "<div class=\"ap-panel ap-stack ap-done\"><h2>Thank you</h2><p>Your application was received.</p></div>");
                response.ExecuteScript("ApplyJs.done();");
                return response;
            }
            if (!Allowed(who))
            {
                response.SetElementContents("ap-e-all", "Several applications were sent from here in a short time. Please wait, or call us on " + d.Site.Phone + ".");
                response.ExecuteScript("ApplyJs.failed();");
                return response;
            }
            Noted(who);
            ApplicationRec rec;
            using (await Store.Hold())
            {
                List<ApplicationRec> rows = await Store.Rows<ApplicationRec>("applications");
                rec = new ApplicationRec { Id = Fmt.NewId("a-"), Code = Fmt.Code("APP-", rows.Select(x => x.Code)), UnitId = u.Id, MoveIn = move.Date, Months = months, People = people, Occupants = occ, Pets = pets, Address = addr, Landlord = lord, LandlordPhone = lordph, YearsAt = years, Message = msg, Status = "New", Created = DateTime.Now, Source = "website" };
                rows.Add(rec);
                await Store.SaveRows("applications", rows);
            }
            await Store.Log("visitor", "application", rec.Code + " · " + p.Name + " · " + u.Number, true);
            if (Mailer.Ready && Ui.IsEmail(d.Site.NotifyEmail))
            {
                string result = await Mailer.Send(d.Site.NotifyEmail, "New application " + rec.Code + ": " + p.Name + " " + u.Number, "Applicants: " + string.Join(", ", people.Select(x => x.First + " " + x.Last + " <" + x.Email + ">")) + "\nHome: " + p.Name + " · " + u.Number + "\nMove in: " + Fmt.Day(rec.MoveIn) + ", " + months + " months\nPeople: " + occ + "\nPets: " + (pets.Length > 0 ? pets : "none") + "\n\n" + msg + "\n");
                if (result.Length > 0)
                {
                    await Store.Log("system", "notification email", result, false);
                }
            }
            decimal income = people.Sum(x => x.Income);
            decimal need = u.Rent * d.Site.IncomeTimes;
            StringBuilder ok = new StringBuilder("<div class=\"ap-panel ap-stack ap-done\"><div class=\"ap-code\"><small>Application</small><b>" + Ui.Esc(rec.Code) + "</b></div><h2>Thank you, " + Ui.Esc(people[0].First) + ". Your application is in.</h2><p>Keep this reference. We will email " + Ui.Esc(people[0].Email) + " within two working days.</p>");
            ok.Append("<dl class=\"ap-tripdl\"><dt>Home</dt><dd>" + Ui.Esc(p.Name + " · " + u.Number) + "</dd><dt>Move in</dt><dd>" + Ui.Esc(Fmt.Day(rec.MoveIn)) + "</dd><dt>Lease</dt><dd>" + months + " months at " + Fmt.Whole(u.Rent) + " a month</dd><dt>Applicants</dt><dd>" + Ui.Esc(string.Join(", ", people.Select(x => x.First + " " + x.Last))) + "</dd><dt>Fee</dt><dd>" + Fmt.Whole(d.Site.ApplicationFee * people.Count) + " (" + Fmt.Whole(d.Site.ApplicationFee) + " for each adult), paid when we start the review</dd></dl>");
            if (income > 0 && income < need)
            {
                ok.Append("<p class=\"ap-hint\">Your household income is below " + d.Site.IncomeTimes.ToString("0.#", Fmt.Us) + " times the rent. A guarantor can help; we will explain the options when we call.</p>");
            }
            ok.Append("<h3>What happens next</h3><ol class=\"ap-next\"><li>A leasing agent reviews the application and calls your current landlord.</li><li>We run the credit and background checks once the fee is paid.</li><li>We answer by email. If approved, we prepare the lease and confirm the move-in date.</li></ol><div class=\"ap-bbtns\"><a class=\"ap-btn ap-btn-ghost\" href=\"Units\">Back to units</a><a class=\"ap-btn ap-btn-ghost\" href=\"Contact\">Ask a question</a></div></div>");
            response.SetElementContents("ap-out", ok.ToString());
            response.ExecuteScript("ApplyJs.done();");
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
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"ap-btn" + (main ? string.Empty : " ap-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"ap-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"ap-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"ap-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"ap-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "ap-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"ap-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"ap-wrap ap-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            List<BlockItem> more = b.Items.Where(i => i.Media != string.Empty && !Blocks.IsVideo(i.Media) && media.Any(m => m.Path == i.Media)).ToList();
            string text = "<div class=\"ap-wrap ap-bhero-in\"><div class=\"ap-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"ap-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "ap-blead") + Btns(site, b) + "</div></div>";
            if (mv != string.Empty && more.Count > 0)
            {
                MediaItem? first = media.FirstOrDefault(m => m.Path == b.Media);
                List<(string Html, string Cap)> all = new List<(string, string)> { (mv, first?.Caption ?? string.Empty) };
                foreach (BlockItem it in more)
                {
                    MediaItem? m = media.FirstOrDefault(x => x.Path == it.Media);
                    all.Add(("<img src=\"" + Ui.Esc(it.Media) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : it.Title) + "\" decoding=\"async\">", it.Title));
                }
                StringBuilder sl = new StringBuilder("<div class=\"ap-bhero-p ap-slides\" id=\"ap-slides\" aria-roledescription=\"carousel\" aria-label=\"Pictures\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<figure class=\"ap-slide" + (k == 0 ? " ap-on" : string.Empty) + "\" aria-roledescription=\"slide\" aria-label=\"" + (k + 1) + " of " + all.Count + "\"" + (k == 0 ? string.Empty : " aria-hidden=\"true\"") + ">" + all[k].Html + (all[k].Cap == string.Empty ? string.Empty : "<figcaption>" + Ui.Esc(all[k].Cap) + "</figcaption>") + "</figure>");
                }
                sl.Append("<div class=\"ap-sctl\"><button type=\"button\" class=\"ap-sarr\" aria-label=\"Previous picture\" onclick=\"ApplyJs.slide(-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 6l-6 6 6 6\"/></svg></button><div class=\"ap-sdots\">");
                for (int k = 0; k < all.Count; k++)
                {
                    sl.Append("<button type=\"button\" class=\"ap-sdot" + (k == 0 ? " ap-on" : string.Empty) + "\" aria-label=\"Show picture " + (k + 1) + "\" aria-pressed=\"" + (k == 0 ? "true" : "false") + "\" onclick=\"ApplyJs.slideTo(" + k + ")\"></button>");
                }
                sl.Append("</div><button type=\"button\" class=\"ap-sarr\" aria-label=\"Next picture\" onclick=\"ApplyJs.slide(1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg></button></div></div>");
                return "<section class=\"ap-bhero ap-bslides\" id=\"" + Ui.Esc(b.Id) + "\">" + sl + text + "</section>";
            }
            return "<section class=\"ap-bhero" + (mv == string.Empty ? " ap-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"ap-bhero-p\">" + mv + "</div>" : string.Empty) + text + "</section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"ap-bsplit" + (b.Flip ? " ap-bflip" : string.Empty) + (b.Media == string.Empty ? " ap-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"ap-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"ap-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"ap-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"ap-bcards ap-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"ap-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                sb.Append(it.Link != string.Empty ? "<a class=\"ap-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"ap-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>" : "<div class=\"ap-bcard\">" + inner + "</div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"ap-bsteps ap-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
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
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"ap-bstats ap-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"ap-bpeople ap-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                string[] parts = it.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string ini = parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
                sb.Append("<div class=\"ap-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"ap-bnoface\">" + Ui.Esc(ini) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"ap-bfaq\">" + Head(site, b, false) + "<div class=\"ap-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"ap-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"ap-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"ap-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "ap-blead") + "</div>" + Btns(site, b) + "</div>");
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
                    "text" => Sec(b, "<div class=\"ap-btext\">" + Kick(d.Site, b) + H2(d.Site, b, first ? "h1" : "h2") + Rich(d.Site, b.Text, string.Empty) + "</div>"),
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
            StringBuilder sb = new StringBuilder("<a class=\"ap-skip\" href=\"#ap-main\">Skip to content</a><header class=\"ap-head\" id=\"ap-head\"><div class=\"ap-wrap ap-head-row\"><a class=\"ap-logo\" href=\"Home\" aria-label=\"" + Ui.Esc(d.Site.Legal) + " home\">" + Logo(40) + "<span><b>" + Ui.Esc(d.Site.Brand) + "</b><small>Property Management</small></span></a>");
            sb.Append("<nav class=\"ap-nav\" id=\"ap-nav\" aria-label=\"Main\"><div class=\"ap-nav-top\"><b>Menu</b><button type=\"button\" class=\"ap-ibtn\" aria-label=\"Close menu\" onclick=\"ApplyJs.closeNav()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div><ul class=\"ap-menu\">");
            foreach ((string page, string name) in NavItems)
            {
                sb.Append("<li><a class=\"ap-ml" + (page == active ? " ap-on" : string.Empty) + "\" href=\"" + page + "\"" + (page == active ? " aria-current=\"page\"" : string.Empty) + ">" + name + "</a></li>");
            }
            sb.Append("<li class=\"ap-mmanage\"><a class=\"ap-ml" + (active == "Portal" ? " ap-on" : string.Empty) + "\" href=\"Portal\">Resident portal</a></li></ul></nav><a class=\"ap-btn ap-btn-ghost ap-btn-sm ap-head-mg\" href=\"Portal\">Resident portal</a><a class=\"ap-btn ap-btn-sm ap-head-cta\" href=\"Units\">See units</a><button type=\"button\" class=\"ap-burger\" aria-label=\"Open menu\" onclick=\"ApplyJs.openNav()\"><svg viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M4 12h16M4 17h16\"/></svg></button></div><div class=\"ap-scrim\" id=\"ap-scrim\" onclick=\"ApplyJs.closeNav()\"></div></header>");
            return sb.ToString();
        }

        private static string FootHtml(PublicData d)
        {
            SiteData s = d.Site;
            StringBuilder sb = new StringBuilder("<footer class=\"ap-foot\"><div class=\"ap-wrap ap-foot-grid\"><div class=\"ap-fbrand\"><a class=\"ap-logo\" href=\"Home\">" + Logo(40) + "<span><b>" + Ui.Esc(s.Brand) + "</b><small>Property Management</small></span></a><p>" + Ui.Esc(s.Tagline) + "</p><p class=\"ap-fcontact\"><a href=\"tel:" + Ui.Esc(s.Phone) + "\">" + Ui.Esc(s.Phone) + "</a><br><a href=\"mailto:" + Ui.Esc(s.Email) + "\">" + Ui.Esc(s.Email) + "</a></p></div>");
            sb.Append("<div><h3>Rent</h3><ul><li><a href=\"Units\">Available units</a></li><li><a href=\"Properties\">Properties</a></li><li><a href=\"Policies\">Policies</a></li><li><a href=\"Contact\">Contact</a></li></ul></div>");
            sb.Append("<div><h3>Properties</h3><ul>" + string.Concat(d.Properties.Take(6).Select(p => "<li><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Name) + "</a></li>")) + "</ul></div>");
            sb.Append("<div><h3>Office</h3><ul class=\"ap-fbr\"><li><b>" + Ui.Esc(s.Street) + "</b><span>" + Ui.Esc(s.City + ", " + s.Region + " " + s.Zip) + "</span></li><li><span>" + Lines(s.Hours) + "</span></li><li><span><a href=\"Portal\">Resident portal</a> &middot; <a href=\"AdminLogin\">Staff</a></span></li></ul></div></div>");
            sb.Append("<div class=\"ap-foot-b\"><div class=\"ap-wrap\"><span>&copy; " + DateTime.Now.Year + " " + Ui.Esc(s.Legal) + ". Equal housing opportunity.</span></div></div></footer>");
            return sb.ToString();
        }

        private static string Facts(UnitRec u)
        {
            return "<ul class=\"ap-specs\"><li title=\"Bedrooms\">" + Icon("bed") + Ui.Esc(Fmt.Beds(u.Beds)) + "</li><li title=\"Bathrooms\">" + Icon("bath") + Ui.Esc(Fmt.Baths(u.Baths)) + "</li>" + (u.Sqft > 0 ? "<li title=\"Size\">" + Icon("area") + u.Sqft.ToString("N0", Fmt.Us) + " sq ft</li>" : string.Empty) + (u.PetsOk ? "<li title=\"Pets\">" + Icon("paw") + "Pets OK</li>" : string.Empty) + "</ul>";
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
            return "<article class=\"ap-ccard\"><a class=\"ap-ccard-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, photo, p?.Name ?? u.Number, false, true) + "<span class=\"ap-ready" + (Rent.MoveInDate(u) <= DateTime.Today ? " ap-now" : string.Empty) + "\">" + Ready(u) + "</span></a><div class=\"ap-ccard-b\"><div class=\"ap-ccard-h\"><h3><a href=\"" + href + "\">" + Ui.Esc((p?.Name ?? string.Empty) + " · " + u.Number) + "</a></h3><small>" + Ui.Esc(p == null ? string.Empty : p.Kind + " · " + p.City) + "</small></div>" + Facts(u) + "<div class=\"ap-ccard-f\"><span class=\"ap-from\"><b>" + Fmt.Whole(u.Rent) + "</b><small>a month</small></span><a class=\"ap-btn ap-btn-sm\" href=\"" + href + "\">View</a></div></div></article>";
        }

        private static string PropertyCard(PublicData d, PropertyRec p)
        {
            int open = d.Units.Count(u => u.PropertyId == p.Id && Rent.Offered(u));
            List<UnitRec> mine = d.Units.Where(u => u.PropertyId == p.Id).ToList();
            string rents = mine.Count == 0 ? string.Empty : "From " + Fmt.Whole(mine.Min(u => u.Rent)) + " a month";
            return "<article class=\"ap-pcard\"><a class=\"ap-pcard-img\" href=\"Property?p=" + Ui.Esc(p.Id) + "\" tabindex=\"-1\" aria-hidden=\"true\">" + Pic(d.Media, p.Media, p.Name, false, true) + "</a><div class=\"ap-pcard-b\"><small class=\"ap-kind\">" + Ui.Esc(p.Kind) + "</small><h3><a href=\"Property?p=" + Ui.Esc(p.Id) + "\">" + Ui.Esc(p.Name) + "</a></h3><p class=\"ap-addr\">" + Icon("pin") + "<span>" + Ui.Esc(p.Street + ", " + p.City) + "</span></p><p class=\"ap-pmeta\"><span>" + Ui.Esc(rents) + "</span><b class=\"ap-open" + (open == 0 ? " ap-none" : string.Empty) + "\">" + (open == 0 ? "Fully rented" : Ui.Count(open, "unit ready", "units ready")) + "</b></p></div></article>";
        }

        private static string UnitSearch(PublicData d, string title, string prop, int beds, int max, string move, bool pets, bool full)
        {
            StringBuilder sb = new StringBuilder("<form class=\"ap-search\" id=\"find\" action=\"Units\" method=\"get\" onsubmit=\"return ApplyJs.find(this)\">" + (title.Length > 0 ? "<h2 class=\"ap-search-t\">" + Ui.Esc(title) + "</h2>" : string.Empty) + "<div class=\"ap-search-row" + (full ? " ap-search-full" : string.Empty) + "\">");
            if (full)
            {
                sb.Append("<label class=\"ap-sf\"><span>" + Icon("home") + "Property</span><select name=\"p\" aria-label=\"Property\"><option value=\"\">Any property</option>" + string.Concat(d.Properties.Select(p => "<option value=\"" + Ui.Esc(p.Id) + "\"" + (p.Id == prop ? " selected" : string.Empty) + ">" + Ui.Esc(p.Name) + "</option>")) + "</select></label>");
            }
            sb.Append("<label class=\"ap-sf\"><span>" + Icon("bed") + "Bedrooms</span><select name=\"b\" aria-label=\"Bedrooms\">" + string.Concat(new[] { (-1, "Any"), (0, "Studio"), (1, "1 or more"), (2, "2 or more"), (3, "3 or more"), (4, "4 or more") }.Select(o => "<option value=\"" + (o.Item1 < 0 ? string.Empty : o.Item1.ToString(CultureInfo.InvariantCulture)) + "\"" + (o.Item1 == beds ? " selected" : string.Empty) + ">" + o.Item2 + "</option>")) + "</select></label>");
            sb.Append("<label class=\"ap-sf\"><span>" + Icon("card") + "Most rent</span><select name=\"r\" aria-label=\"Most rent a month\">" + string.Concat(new[] { 0, 1300, 1600, 2000, 2500, 3000 }.Select(v => "<option value=\"" + (v == 0 ? string.Empty : v.ToString(CultureInfo.InvariantCulture)) + "\"" + (v == max ? " selected" : string.Empty) + ">" + (v == 0 ? "No limit" : "Up to " + Fmt.Whole(v)) + "</option>")) + "</select></label>");
            sb.Append("<label class=\"ap-sf\"><span>" + Icon("calendar") + "Move in by</span><input type=\"date\" name=\"m\" value=\"" + Ui.Esc(move) + "\" min=\"" + Fmt.IsoDay(DateTime.Today) + "\"></label>");
            sb.Append("<label class=\"ap-sf ap-sf-chk\"><input type=\"checkbox\" name=\"pets\" value=\"1\"" + (pets ? " checked" : string.Empty) + "><span>" + Icon("paw") + "Pets</span></label>");
            sb.Append("<button type=\"submit\" class=\"ap-btn ap-sbtn\">Search</button></div></form>");
            return sb.ToString();
        }
    }
}
