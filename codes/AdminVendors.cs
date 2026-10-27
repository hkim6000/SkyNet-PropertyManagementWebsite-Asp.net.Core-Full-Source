using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminVendors : WebPage
    {
        private const string AreaKey = "vendors";
        private const int Size = 12;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Vendors | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminVendorsJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminVendorsJs.denied();");
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
            response.SetElementContents("vd-brand", Ui.Esc(site.Brand));
            response.SetElementContents("vd-menu", await Auth.Menu("vd", "AdminVendors", c));
            response.SetElementContents("vd-who", Auth.Who("vd", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminVendorsJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"vd-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"vd-btn vd-btn-ghost vd-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminVendorsJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"vd-btn vd-btn-ghost vd-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminVendorsJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"vd-pill vd-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"vd-err\" id=\"vd-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"vd-field\" for=\"vd-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"vd-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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

        private static string Cover(VendorRec v)
        {
            return v.InsuredUntil == null ? "No certificate" : v.InsuredUntil.Value.Date < DateTime.Today ? "Insurance expired" : v.InsuredUntil.Value.Date <= DateTime.Today.AddDays(30) ? "Insurance ending" : "Insured";
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
            response.SetElementContents("vd-main", "<div class=\"vd-ph\"><div><h1>Vendors</h1><p>The plumbers, electricians, cleaners and other trades you send work orders to, with their rate and insurance date.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"vd-btn\" onclick=\"AdminVendorsJs.add()\">Add a vendor</button>" : string.Empty) + "</div><div class=\"vd-tools\"><input type=\"search\" id=\"vd-q\" placeholder=\"Search by name, trade or contact\" maxlength=\"40\" oninput=\"AdminVendorsJs.type()\" aria-label=\"Search vendors\"><select id=\"vd-ft\" onchange=\"AdminVendorsJs.list(true)\" aria-label=\"Trade\">" + Opt(string.Empty, "All trades", string.Empty) + string.Concat(Maint.Trades.Select(t => Opt(t, t, string.Empty))) + "</select><label class=\"vd-check\"><input type=\"checkbox\" id=\"vd-fo\" onchange=\"AdminVendorsJs.list(true)\">Show inactive</label></div><div id=\"vd-list\">" + ListHtml(d, string.Empty, string.Empty, false, 1) + "</div>");
            return response;
        }

        private static string ListHtml(RentData d, string term, string trade, bool all, int page)
        {
            List<VendorRec> rows = d.Vendors.Where(v => (all || v.Active) && (trade.Length == 0 || v.Trade == trade) && (term.Length == 0 || Ui.Has(v.Name, term) || Ui.Has(v.Trade, term) || Ui.Has(v.Contact, term))).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"vd-panel vd-empty\">" + (d.Vendors.Count == 0 ? "No vendors yet. Add the first one." : "No vendors match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"vd-wrapt\"><table class=\"vd-table\"><thead><tr><th>Vendor</th><th>Trade</th><th>Contact</th><th class=\"vd-num\">Rate</th><th class=\"vd-num\">Open jobs</th><th>Insurance</th></tr></thead><tbody>");
            foreach (VendorRec v in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<tr class=\"vd-row\" onclick=\"AdminVendorsJs.edit('" + Ui.Js(v.Id) + "')\"><td class=\"vd-first\"><b>" + Ui.Esc(v.Name) + "</b>" + (v.Active ? string.Empty : "<small>Inactive</small>") + "</td><td data-label=\"Trade\">" + Ui.Esc(v.Trade) + "</td><td data-label=\"Contact\">" + Ui.Esc(v.Contact.Length > 0 ? v.Contact : "-") + "<small class=\"vd-mini\"><br>" + Ui.Esc(v.Phone) + "</small></td><td class=\"vd-num\" data-label=\"Rate\">" + (v.Rate > 0 ? Fmt.Whole(v.Rate) + "/h" : "-") + "</td><td class=\"vd-num\" data-label=\"Open jobs\">" + d.WorkOrders.Count(w => w.VendorId == v.Id && Maint.Open(w)) + "</td><td data-label=\"Insurance\">" + Pill(Cover(v)) + (v.InsuredUntil != null ? "<small class=\"vd-mini\"><br>" + Ui.Esc(Fmt.Day(v.InsuredUntil.Value)) + "</small>" : string.Empty) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "vendor", "vendors"));
            return sb.ToString();
        }

        private async Task<string> ListNow()
        {
            RentData d = await Rent.Load();
            return ListHtml(d, Term(GetDataValue("term")), Ui.Clip(GetDataValue("trade"), 30), (GetDataValue("inact") ?? string.Empty) == "1", PageNo(GetDataValue("pg")));
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("vd-list", await ListNow());
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
            VendorRec? v = d.Vendors.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && v == null)
            {
                return await Refuse(response, c, "open vendor", id, "That vendor was not found.");
            }
            if (v == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add vendor", string.Empty, "You can view vendors but not add them.");
            }
            response.SetElementContents("vd-mbody", FormHtml(c, d, v));
            response.ExecuteScript("AdminVendorsJs.opened('" + Ui.Js(v == null ? "New vendor" : v.Name) + "');");
            return response;
        }

        private static string FormHtml(Ctx c, RentData d, VendorRec? v)
        {
            bool isNew = v == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            VendorRec r = v ?? new VendorRec();
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<form class=\"vd-stack\" id=\"vd-form\" onsubmit=\"return AdminVendorsJs.save()\" novalidate><input type=\"hidden\" id=\"vd-f-id\" value=\"" + Ui.Esc(r.Id) + "\">");
            if (!isNew)
            {
                List<WorkOrderRec> jobs = d.WorkOrders.Where(w => w.VendorId == r.Id).OrderByDescending(w => w.Created).ToList();
                sb.Append("<div class=\"vd-bar\"><span>" + Pill(Cover(r)) + " " + Ui.Count(jobs.Count(Maint.Open), "open job", "open jobs") + " &middot; " + Ui.Count(jobs.Count(w => w.Status == "Done"), "job done", "jobs done") + " &middot; " + Fmt.Money(jobs.Where(w => w.Status == "Done").Sum(w => w.Cost)) + " paid in total</span></div>");
                if (jobs.Count > 0 && Access.Can(c.U, "workorders", 'V'))
                {
                    sb.Append("<div class=\"vd-field\"><span>Latest jobs</span><ul class=\"vd-llist\">" + string.Concat(jobs.Where(w => Access.PropertyOk(c.U, w.PropertyId)).Take(6).Select(w => "<li><a href=\"AdminWorkOrders?code=" + Ui.Esc(w.Code) + "\">" + Ui.Esc(w.Code) + "</a> " + Pill(w.Status) + " <span>" + Ui.Esc(w.Title + " · " + Maint.Place(d, w)) + "</span></li>")) + "</ul></div>");
                }
            }
            sb.Append("<div class=\"vd-grid2\">" + Field("name", "Company name", Text("name", r.Name, 80, "text", ro)) + Field("trade", "Trade", "<select id=\"vd-f-trade\" data-k=\"vtrade\"" + dis + ">" + string.Concat(Maint.Trades.Select(t => Opt(t, t, r.Trade))) + "</select>") + "</div>");
            sb.Append("<div class=\"vd-grid3\">" + Field("contact", "Contact person", Text("contact", r.Contact, 60, "text", ro)) + Field("phone", "Phone", Text("phone", r.Phone, 30, "tel", ro)) + Field("email", "Email", Text("email", r.Email, 80, "email", ro)) + "</div>");
            sb.Append("<div class=\"vd-grid2\">" + Field("rate", "Hourly rate ($)", Text("rate", r.Rate > 0 ? Num(r.Rate) : string.Empty, 8, "text", ro + " inputmode=\"decimal\"")) + Field("ins", "Insurance valid until", Text("ins", r.InsuredUntil == null ? string.Empty : Fmt.IsoDay(r.InsuredUntil.Value), 10, "date", ro), "From the vendor's certificate of insurance.") + "</div>");
            sb.Append("<label class=\"vd-field\" for=\"vd-f-note\"><span>Note</span><textarea id=\"vd-f-note\" data-k=\"note\" rows=\"2\" maxlength=\"500\"" + ro + ">" + Ui.Esc(r.Note) + "</textarea>" + Err("note") + "</label>");
            sb.Append("<label class=\"vd-check\"><input type=\"checkbox\" id=\"vd-f-active\" data-k=\"active\"" + (r.Active || isNew ? " checked" : string.Empty) + dis + ">Active: can be chosen on work orders</label>");
            sb.Append("<div class=\"vd-acts\">" + (edit ? "<button type=\"submit\" class=\"vd-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"vd-btn vd-btn-ghost\" onclick=\"AdminVendorsJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Can(c, 'D') && !d.WorkOrders.Any(w => w.VendorId == r.Id) ? "<button type=\"button\" class=\"vd-btn vd-btn-danger vd-push\" onclick=\"AdminVendorsJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Name) + "')\">Delete</button>" : string.Empty) + "</div></form>");
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
                return await Refuse(response, c, isNew ? "add vendor" : "edit vendor", id, "You do not have the right to " + (isNew ? "add" : "change") + " vendors.");
            }
            string name = Ui.Clip(GetDataValue("name"), 80);
            string trade = Ui.Clip(GetDataValue("vtrade"), 30);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string rateText = Ui.Clip(GetDataValue("rate"), 8);
            decimal rate = rateText.Length == 0 ? 0 : Dec(rateText);
            string insText = Ui.Clip(GetDataValue("ins"), 10);
            bool insOk = Fmt.ParseDate(insText, out DateTime ins);
            List<VendorRec> current = await Store.Rows<VendorRec>("vendors");
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 ? "Enter the company name." : current.Any(x => x.Id != id && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ? "Another vendor already has this name." : string.Empty,
                ["trade"] = !Maint.Trades.Contains(trade) ? "Choose a trade." : string.Empty,
                ["contact"] = string.Empty,
                ["phone"] = !Ui.IsPhone(phone) ? "Enter a phone number." : string.Empty,
                ["email"] = email.Length > 0 && !Ui.IsEmail(email) ? "Enter a valid email address, or leave it empty." : string.Empty,
                ["rate"] = rate < 0 || rate > 2000 ? "Enter the hourly rate in dollars, or leave it empty." : string.Empty,
                ["ins"] = insText.Length > 0 && !insOk ? "Enter a valid date, or leave it empty." : string.Empty,
                ["note"] = string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("vd-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            using (await Store.Hold())
            {
                List<VendorRec> all = await Store.Rows<VendorRec>("vendors");
                VendorRec? v = all.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    string key = "v-" + Ui.Slug(name);
                    while (all.Any(x => x.Id == key))
                    {
                        key += "-2";
                    }
                    v = new VendorRec { Id = key };
                    all.Add(v);
                }
                if (v == null)
                {
                    return await Refuse(response, c, "edit vendor", id, "That vendor was not found.");
                }
                v.Name = name;
                v.Trade = trade;
                v.Contact = Ui.Clip(GetDataValue("contact"), 60);
                v.Phone = phone;
                v.Email = email;
                v.Rate = rate;
                v.InsuredUntil = insText.Length == 0 ? null : ins.Date;
                v.Note = Ui.Clip((GetDataValue("note") ?? string.Empty).Replace("\r", string.Empty), 500);
                v.Active = (GetDataValue("active") ?? string.Empty) == "1";
                await Store.SaveRows("vendors", all);
            }
            await Store.Log(c.U.Id, isNew ? "add vendor" : "edit vendor", name, true);
            response.SetElementContents("vd-list", await ListNow());
            response.ExecuteScript("AdminVendorsJs.saved();");
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
                return await Refuse(response, c, "delete vendor", id, "You do not have the right to delete vendors.");
            }
            string name;
            using (await Store.Hold())
            {
                RentData d = await Rent.Read();
                VendorRec? v = d.Vendors.FirstOrDefault(x => x.Id == id);
                if (v == null)
                {
                    return await Refuse(response, c, "delete vendor", id, "That vendor was not found.");
                }
                if (d.WorkOrders.Any(w => w.VendorId == id))
                {
                    return await Refuse(response, c, "delete vendor", v.Name, v.Name + " is on work orders, so the record stays. Mark it inactive instead.");
                }
                name = v.Name;
                d.Vendors.Remove(v);
                await Store.SaveRows("vendors", d.Vendors);
            }
            await Store.Log(c.U.Id, "delete vendor", name, true);
            response.SetElementContents("vd-list", await ListNow());
            response.ExecuteScript("AdminVendorsJs.deleted();");
            return response;
        }
    }
}
