using System.Globalization;
using System.Text;
using PropertyManagement.Core;
using PropertyManagement.Models;
using SkyNet;

namespace PropertyManagement.codes
{
    public class AdminOwners : WebPage
    {
        private const string AreaKey = "owners";
        private const int Size = 12;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Owners | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminOwnersJs.expired();");
                return null;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                await Store.Log(c.U.Id, "open " + AreaKey, "no access", false);
                response.ExecuteScript("AdminOwnersJs.denied();");
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
            response.SetElementContents("or-brand", Ui.Esc(site.Brand));
            response.SetElementContents("or-menu", await Auth.Menu("or", "AdminOwners", c));
            response.SetElementContents("or-who", Auth.Who("or", c));
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminOwnersJs.toast('" + Ui.Js(message) + "');");
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
            return "<div class=\"or-pager\"><span>Page " + page + " of " + pages + " &middot; " + Ui.Count(count, one, many) + "</span><div><button type=\"button\" class=\"or-btn or-btn-ghost or-btn-sm\"" + (page <= 1 ? " disabled" : string.Empty) + " onclick=\"AdminOwnersJs.go(" + (page - 1) + ")\">Previous</button><button type=\"button\" class=\"or-btn or-btn-ghost or-btn-sm\"" + (page >= pages ? " disabled" : string.Empty) + " onclick=\"AdminOwnersJs.go(" + (page + 1) + ")\">Next</button></div></div>";
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
            return "<span class=\"or-pill or-pill-" + k + "\">" + Ui.Esc(status) + "</span>";
        }

        private static string Err(string key)
        {
            return "<em class=\"or-err\" id=\"or-e-" + key + "\"></em>";
        }

        private static string Field(string key, string label, string input, string hint = "")
        {
            return "<label class=\"or-field\" for=\"or-f-" + key + "\"><span>" + Ui.Esc(label) + "</span>" + input + (hint.Length > 0 ? "<small>" + Ui.Esc(hint) + "</small>" : string.Empty) + Err(key) + "</label>";
        }

        private static string Text(string key, string value, int max, string type = "text", string extra = "")
        {
            return "<input type=\"" + type + "\" id=\"or-f-" + key + "\" data-k=\"" + key + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\"" + extra + ">";
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
            response.SetElementContents("or-main", "<div class=\"or-ph\"><div><h1>Owners</h1><p>The people and companies whose properties you manage, with their contact details, fee and the properties they hold. Shares are kept on Ownership; statements and payouts on Payouts.</p></div>" + (Can(c, 'A') ? "<button type=\"button\" class=\"or-btn\" onclick=\"AdminOwnersJs.add()\">Add an owner</button>" : string.Empty) + "</div><div class=\"or-tools\"><input type=\"search\" id=\"or-q\" placeholder=\"Search owners\" maxlength=\"40\" oninput=\"AdminOwnersJs.type()\" aria-label=\"Search owners\"></div><div id=\"or-list\">" + ListHtml(d, string.Empty, 1) + "</div>");
            return response;
        }

        private static string ListHtml(RentData d, string term, int page)
        {
            List<OwnerRec> rows = d.Owners.Where(o => term.Length == 0 || Ui.Has(o.Name, term) || Ui.Has(o.Company, term)).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"or-panel or-empty\">" + (d.Owners.Count == 0 ? "No owners yet. Add the first one and choose their properties." : "No owners match.") + "</div>";
            }
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            page = Math.Min(page, pages);
            List<string> gaps = d.Properties.Select(p => (p, Books.HeldOn(d, p.Id, DateTime.Today))).Where(x => x.Item2 > 0 && x.Item2 < 100m).Select(x => x.p.Name + ": " + Books.Pct(100m - x.Item2) + " has no owner today.").ToList();
            StringBuilder sb = new StringBuilder((gaps.Count > 0 ? "<p class=\"or-note or-gap\">" + Ui.Esc(string.Join(" ", gaps)) + "</p>" : string.Empty) + "<div class=\"or-wrapt\"><table class=\"or-table\"><thead><tr><th>Owner</th><th>Properties</th><th class=\"or-num\">Units</th></tr></thead><tbody>");
            foreach (OwnerRec o in rows.Skip((page - 1) * Size).Take(Size))
            {
                List<(string PropertyId, decimal Share)> held = Books.HoldingsOn(d, o.Id, DateTime.Today);
                sb.Append("<tr class=\"or-row\" onclick=\"AdminOwnersJs.edit('" + Ui.Js(o.Id) + "')\"><td class=\"or-first\"><b>" + Ui.Esc(o.Name) + "</b>" + (o.Company.Length > 0 ? "<small>" + Ui.Esc(o.Company) + "</small>" : string.Empty) + "</td><td data-label=\"Properties\">" + Ui.Esc(held.Count == 0 ? "None today" : string.Join(", ", held.Select(h => Rent.PropertyName(d, h.PropertyId) + (h.Share < 100m ? " (" + Books.Pct(h.Share) + ")" : string.Empty)))) + "</td><td class=\"or-num\" data-label=\"Units\">" + d.Units.Count(u => held.Any(h => h.PropertyId == u.PropertyId)) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Pager(page, pages, rows.Count, "owner", "owners"));
            return sb.ToString();
        }

        private async Task Refresh(ApiResponse response)
        {
            RentData d = await Rent.Load();
            response.SetElementContents("or-list", ListHtml(d, Term(GetDataValue("term")), PageNo(GetDataValue("pg"))));
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
            OwnerRec? o = d.Owners.FirstOrDefault(x => x.Id == id);
            if (id.Length > 0 && o == null)
            {
                return await Refuse(response, c, "open owner", id, "That owner was not found.");
            }
            if (o == null && !Can(c, 'A'))
            {
                return await Refuse(response, c, "add owner", string.Empty, "You can view owners but not add them.");
            }
            response.SetElementContents("or-mbody", FormHtml(c, d, o, string.Empty));
            response.ExecuteScript("AdminOwnersJs.opened('" + Ui.Js(o == null ? "New owner" : o.Name) + "');");
            return response;
        }

        private static string HoldingsHtml(RentData d, OwnerRec r)
        {
            List<OwnershipRec> rows = d.Ownership.Where(x => x.OwnerId == r.Id).OrderBy(x => x.Until == null ? 0 : 1).ThenBy(x => d.Properties.FindIndex(p => p.Id == x.PropertyId)).ThenByDescending(x => x.From ?? DateTime.MinValue).ToList();
            if (rows.Count == 0)
            {
                return "<p class=\"or-mini\">No properties yet.</p>";
            }
            StringBuilder sb = new StringBuilder("<ul class=\"or-holds\">");
            foreach (OwnershipRec x in rows)
            {
                bool now = Books.Covers(x, DateTime.Today);
                string from = x.From == null ? "the first record" : Fmt.Day(x.From.Value);
                string when = x.Until == null ? "since " + from : from + " to " + Fmt.Day(x.Until.Value);
                sb.Append("<li" + (now ? string.Empty : " class=\"or-past\"") + "><b>" + Ui.Esc(Rent.PropertyName(d, x.PropertyId)) + "</b><span>" + Ui.Esc(Books.Pct(x.Share)) + "</span><small>" + Ui.Esc(when) + "</small></li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static string FormHtml(Ctx c, RentData d, OwnerRec? o, string note)
        {
            bool isNew = o == null;
            bool edit = isNew ? Can(c, 'A') : Can(c, 'E');
            OwnerRec r = o ?? new OwnerRec();
            string ro = edit ? string.Empty : " readonly";
            string dis = edit ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<div class=\"or-stack\" id=\"or-wrap\"><input type=\"hidden\" id=\"or-f-id\" value=\"" + Ui.Esc(r.Id) + "\">" + (note.Length > 0 ? "<p class=\"or-ok\">" + Ui.Esc(note) + "</p>" : string.Empty));
            sb.Append("<form class=\"or-stack" + (isNew ? string.Empty : " or-panel") + "\" id=\"or-form\" onsubmit=\"return AdminOwnersJs.save()\" novalidate>" + (isNew ? string.Empty : "<h3>Owner details</h3>"));
            sb.Append("<div class=\"or-grid2\">" + Field("name", "Owner name", Text("name", r.Name, 80, "text", ro)) + Field("company", "Company (optional)", Text("company", r.Company, 80, "text", ro)) + "</div>");
            sb.Append("<div class=\"or-grid2\">" + Field("email", "Email", Text("email", r.Email, 80, "email", ro)) + Field("phone", "Phone", Text("phone", r.Phone, 30, "tel", ro)) + "</div>");
            sb.Append(Field("street", "Mailing address", Text("street", r.Street, 80, "text", ro)) + "<div class=\"or-grid3\">" + Field("city", "City", Text("city", r.City, 40, "text", ro)) + Field("region", "State", Text("region", r.Region, 30, "text", ro)) + Field("zip", "ZIP code", Text("zip", r.Zip, 12, "text", ro)) + "</div>");
            if (!isNew)
            {
                sb.Append("<div class=\"or-field\"><span>Properties and ownership share</span>" + HoldingsHtml(d, r) + "</div>");
            }
            sb.Append("<div class=\"or-grid2\">" + Field("fee", "Management fee % (leave empty for the standard " + d.Site.ManagementFee.ToString("0.#", Fmt.Us) + "%)", Text("fee", r.Fee == null ? string.Empty : Num(r.Fee.Value), 6, "text", ro + " inputmode=\"decimal\"")) + Field("payto", "Pay to (bank or check details)", Text("payto", r.PayTo, 120, "text", ro)) + "</div>");
            sb.Append(Field("note", "Note", Text("note", r.Note, 300, "text", ro)));
            sb.Append("<div class=\"or-acts\">" + (edit ? "<button type=\"submit\" class=\"or-btn\">Save</button>" : string.Empty) + "<button type=\"button\" class=\"or-btn or-btn-ghost\" onclick=\"AdminOwnersJs.closeModal()\">" + (edit ? "Cancel" : "Close") + "</button>" + (!isNew && Can(c, 'D') && !d.Payouts.Any(p => p.OwnerId == r.Id) && !d.Ownership.Any(x => x.OwnerId == r.Id) ? "<button type=\"button\" class=\"or-btn or-btn-danger or-push\" onclick=\"AdminOwnersJs.del('" + Ui.Js(r.Id) + "','" + Ui.Js(r.Name) + "')\">Delete</button>" : string.Empty) + "</div></form></div>");
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
                return await Refuse(response, c, isNew ? "add owner" : "edit owner", id, "You do not have the right to " + (isNew ? "add" : "change") + " owners.");
            }
            RentData d = await Rent.Load();
            string name = Ui.Clip(GetDataValue("name"), 80);
            string email = Ui.Clip(GetDataValue("email"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string feeText = Ui.Clip(GetDataValue("fee"), 6);
            decimal fee = feeText.Length == 0 ? -1 : Dec(feeText);
            Dictionary<string, string> e = new Dictionary<string, string>
            {
                ["name"] = name.Length < 2 ? "Enter the owner's name." : string.Empty,
                ["company"] = string.Empty,
                ["email"] = email.Length > 0 && !Ui.IsEmail(email) ? "Enter a valid email address, or leave it empty." : string.Empty,
                ["phone"] = phone.Length > 0 && !Ui.IsPhone(phone) ? "Enter a valid phone number, or leave it empty." : string.Empty,
                ["fee"] = feeText.Length > 0 && (fee < 0 || fee > 50) ? "Enter a percentage from 0 to 50, or leave it empty." : string.Empty
            };
            foreach (KeyValuePair<string, string> kv in e)
            {
                response.SetElementContents("or-e-" + kv.Key, kv.Value);
            }
            if (e.Values.Any(v => v.Length > 0))
            {
                return response;
            }
            string oid;
            using (await Store.Hold())
            {
                List<OwnerRec> all = await Store.Rows<OwnerRec>("owners");
                OwnerRec? o = all.FirstOrDefault(x => x.Id == id);
                if (isNew)
                {
                    string key = "o-" + Ui.Slug(name);
                    while (all.Any(x => x.Id == key))
                    {
                        key += "-2";
                    }
                    o = new OwnerRec { Id = key };
                    all.Add(o);
                }
                if (o == null)
                {
                    return await Refuse(response, c, "edit owner", id, "That owner was not found.");
                }
                o.Name = name;
                o.Company = Ui.Clip(GetDataValue("company"), 80);
                o.Email = email;
                o.Phone = phone;
                o.Street = Ui.Clip(GetDataValue("street"), 80);
                o.City = Ui.Clip(GetDataValue("city"), 40);
                o.Region = Ui.Clip(GetDataValue("region"), 30);
                o.Zip = Ui.Clip(GetDataValue("zip"), 12);
                o.Fee = feeText.Length == 0 ? null : fee;
                o.PayTo = Ui.Clip(GetDataValue("payto"), 120);
                o.Note = Ui.Clip(GetDataValue("note"), 300);
                await Store.SaveRows("owners", all);
                oid = o.Id;
            }
            await Store.Log(c.U.Id, isNew ? "add owner" : "edit owner", name, true);
            await Refresh(response);
            RentData d2 = await Rent.Load();
            OwnerRec? fresh = d2.Owners.FirstOrDefault(x => x.Id == oid);
            if (fresh != null)
            {
                response.SetElementContents("or-mbody", FormHtml(c, d2, fresh, "Saved."));
                response.ExecuteScript("AdminOwnersJs.opened('" + Ui.Js(fresh.Name) + "');AdminOwnersJs.clean();");
            }
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
                return await Refuse(response, c, "delete owner", id, "You do not have the right to delete owners.");
            }
            string name;
            using (await Store.Hold())
            {
                RentData d = await Rent.Read();
                OwnerRec? o = d.Owners.FirstOrDefault(x => x.Id == id);
                if (o == null || d.Payouts.Any(p => p.OwnerId == id) || d.Ownership.Any(x => x.OwnerId == id))
                {
                    return await Refuse(response, c, "delete owner", id, o == null ? "That owner was not found." : d.Payouts.Any(p => p.OwnerId == id) ? o.Name + " has payouts in the records, so the owner stays." : o.Name + " has ownership records, so the owner stays.");
                }
                name = o.Name;
                d.Owners.Remove(o);
                await Store.SaveRows("owners", d.Owners);
            }
            await Store.Log(c.U.Id, "delete owner", name, true);
            await Refresh(response);
            response.ExecuteScript("AdminOwnersJs.deleted();");
            return response;
        }
    }
}
