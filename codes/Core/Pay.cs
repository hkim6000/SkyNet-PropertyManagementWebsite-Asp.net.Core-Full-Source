using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PropertyManagement.Models;

namespace PropertyManagement.Core
{
    public static class Pay
    {
        private static readonly HttpClient Http = new HttpClient { BaseAddress = new Uri("https://api.stripe.com/"), Timeout = TimeSpan.FromSeconds(30) };

        public static string File => Path.Combine(Store.DataDir, "payment.json");

        public const string Method = "Card (Stripe)";

        public static Task<PaySettings> Settings()
        {
            return Store.Read(File, () => new PaySettings());
        }

        public static Task SaveSettings(PaySettings s)
        {
            return Store.Write(File, s);
        }

        public static async Task<bool> Ready()
        {
            PaySettings s = await Settings();
            return s.Enabled && (s.SecretKey.StartsWith("sk_", StringComparison.Ordinal) || s.SecretKey.StartsWith("rk_", StringComparison.Ordinal)) && s.WebhookSecret.StartsWith("whsec_", StringComparison.Ordinal);
        }

        public static string Mask(string key)
        {
            return key.Length < 12 ? (key.Length == 0 ? string.Empty : "set") : key.Substring(0, 8) + "…" + key.Substring(key.Length - 4);
        }

        public static long Cents(decimal amount)
        {
            return (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
        }

        public static async Task<(string Url, string Error)> Checkout(RentData d, LeaseRec l, ResidentRec r, decimal amount, string baseUrl)
        {
            PaySettings s = await Settings();
            PayAttempt a = new PayAttempt { Id = Fmt.NewId("p-"), LeaseId = l.Id, ResidentId = r.Id, Amount = amount, Status = "Processing", Source = "stripe", Created = DateTime.Now };
            using (await Store.Hold())
            {
                List<PayAttempt> all = await Store.Rows<PayAttempt>("payattempts");
                all.Add(a);
                await Store.SaveRows("payattempts", all);
            }
            Dictionary<string, string> form = new Dictionary<string, string>
            {
                ["mode"] = "payment",
                ["success_url"] = baseUrl + "Portal?paid={CHECKOUT_SESSION_ID}",
                ["cancel_url"] = baseUrl + "Portal",
                ["client_reference_id"] = a.Id,
                ["customer_email"] = r.Email,
                ["line_items[0][quantity]"] = "1",
                ["line_items[0][price_data][currency]"] = "usd",
                ["line_items[0][price_data][unit_amount]"] = Cents(amount).ToString(CultureInfo.InvariantCulture),
                ["line_items[0][price_data][product_data][name]"] = "Rent payment, lease " + l.Code,
                ["line_items[0][price_data][product_data][description]"] = Rent.UnitLabel(d, l.UnitId),
                ["metadata[attempt]"] = a.Id,
                ["metadata[lease]"] = l.Id,
                ["metadata[resident]"] = r.Id,
                ["payment_intent_data[metadata][lease]"] = l.Code
            };
            using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, "v1/checkout/sessions") { Content = new FormUrlEncodedContent(form) };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.SecretKey);
            req.Headers.Add("Idempotency-Key", "checkout-" + a.Id + "-" + Cents(amount).ToString(CultureInfo.InvariantCulture));
            string url = string.Empty;
            string session = string.Empty;
            string error = string.Empty;
            try
            {
                using HttpResponseMessage res = await Http.SendAsync(req);
                string body = await res.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(body);
                if (res.IsSuccessStatusCode)
                {
                    session = doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
                    url = doc.RootElement.GetProperty("url").GetString() ?? string.Empty;
                }
                else
                {
                    error = doc.RootElement.TryGetProperty("error", out JsonElement e) && e.TryGetProperty("message", out JsonElement m) ? m.GetString() ?? "The payment page could not be opened." : "The payment page could not be opened.";
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                error = "The payment service did not answer. Try again in a moment.";
            }
            using (await Store.Hold())
            {
                List<PayAttempt> all = await Store.Rows<PayAttempt>("payattempts");
                PayAttempt? x = all.FirstOrDefault(p => p.Id == a.Id);
                if (x != null)
                {
                    x.Status = error.Length > 0 ? "Failed" : "Open";
                    x.Session = session;
                    await Store.SaveRows("payattempts", all);
                }
            }
            if (error.Length > 0)
            {
                await Store.Log("system", "online payment", error, false);
            }
            return (url, error);
        }

        public static async Task<string> Confirm(string session)
        {
            if (!session.StartsWith("cs_", StringComparison.Ordinal) || session.Length > 200)
            {
                return "unknown";
            }
            List<PayAttempt> attempts = await Store.Rows<PayAttempt>("payattempts");
            PayAttempt? a = attempts.FirstOrDefault(x => x.Session == session);
            if (a == null)
            {
                return "unknown";
            }
            if (a.Status == "Paid")
            {
                return "paid";
            }
            PaySettings s = await Settings();
            try
            {
                using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "v1/checkout/sessions/" + Uri.EscapeDataString(session));
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.SecretKey);
                using HttpResponseMessage res = await Http.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                {
                    return "pending";
                }
                using JsonDocument doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                return await Settle(doc.RootElement) ? "paid" : "pending";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return "pending";
            }
        }

        private static async Task<bool> Settle(JsonElement o)
        {
            string paid = o.TryGetProperty("payment_status", out JsonElement ps) ? ps.GetString() ?? string.Empty : string.Empty;
            if (paid != "paid")
            {
                return false;
            }
            string session = o.TryGetProperty("id", out JsonElement id) ? id.GetString() ?? string.Empty : string.Empty;
            long cents = o.TryGetProperty("amount_total", out JsonElement at) && at.ValueKind == JsonValueKind.Number ? at.GetInt64() : 0;
            string intent = o.TryGetProperty("payment_intent", out JsonElement pi) && pi.ValueKind == JsonValueKind.String ? pi.GetString() ?? string.Empty : string.Empty;
            string attemptId = o.TryGetProperty("client_reference_id", out JsonElement cr) && cr.ValueKind == JsonValueKind.String ? cr.GetString() ?? string.Empty : string.Empty;
            return await Record(attemptId, session, cents / 100m, intent.Length > 0 ? intent : session, Method);
        }

        public static async Task<bool> Record(string attemptId, string session, decimal amount, string reference, string method)
        {
            bool added = false;
            using (await Store.Hold())
            {
                List<PayAttempt> attempts = await Store.Rows<PayAttempt>("payattempts");
                PayAttempt? a = attempts.FirstOrDefault(x => x.Id == attemptId && (session.Length == 0 || x.Session == session || x.Session.Length == 0));
                if (a == null || amount <= 0)
                {
                    return false;
                }
                if (a.Status == "Paid")
                {
                    return true;
                }
                List<LedgerRec> ledger = await Store.Rows<LedgerRec>("ledger");
                if (!ledger.Any(x => x.Kind == "Payment" && (x.Reference == reference || (session.Length > 0 && x.Reference == session))))
                {
                    LedgerRec g = new LedgerRec { Id = Fmt.NewId("g-"), LeaseId = a.LeaseId, When = DateTime.Now, Kind = "Payment", Label = "Online payment", Amount = -amount, Method = method, Reference = reference, By = "resident" };
                    ledger.Add(g);
                    await Store.SaveRows("ledger", ledger);
                    a.LedgerId = g.Id;
                    added = true;
                }
                a.Status = "Paid";
                a.Paid = DateTime.Now;
                if (session.Length > 0)
                {
                    a.Session = session;
                }
                await Store.SaveRows("payattempts", attempts);
            }
            if (added)
            {
                await Store.Log("resident", "online payment", Fmt.Money(amount) + " · " + reference, true);
            }
            return true;
        }

        public static bool Verify(string payload, string header, string secret, DateTime nowUtc)
        {
            if (secret.Length == 0 || header.Length == 0)
            {
                return false;
            }
            string t = string.Empty;
            List<string> sigs = new List<string>();
            foreach (string part in header.Split(','))
            {
                string[] kv = part.Trim().Split('=', 2);
                if (kv.Length != 2)
                {
                    continue;
                }
                if (kv[0] == "t")
                {
                    t = kv[1];
                }
                else if (kv[0] == "v1")
                {
                    sigs.Add(kv[1]);
                }
            }
            if (!long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ts) || sigs.Count == 0)
            {
                return false;
            }
            if (Math.Abs((nowUtc - DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime).TotalSeconds) > 300)
            {
                return false;
            }
            byte[] want;
            using (HMACSHA256 h = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                want = h.ComputeHash(Encoding.UTF8.GetBytes(t + "." + payload));
            }
            foreach (string s in sigs)
            {
                byte[] got;
                try
                {
                    got = Convert.FromHexString(s);
                }
                catch (FormatException)
                {
                    continue;
                }
                if (CryptographicOperations.FixedTimeEquals(got, want))
                {
                    return true;
                }
            }
            return false;
        }

        public static async Task<(int Status, string Body)> Handle(string payload, string header)
        {
            PaySettings s = await Settings();
            if (!Verify(payload, header, s.WebhookSecret, DateTime.UtcNow))
            {
                await Store.Log("system", "payment webhook", "signature check failed", false);
                return (400, "bad signature");
            }
            try
            {
                using JsonDocument doc = JsonDocument.Parse(payload);
                string type = doc.RootElement.GetProperty("type").GetString() ?? string.Empty;
                if (type is "checkout.session.completed" or "checkout.session.async_payment_succeeded")
                {
                    await Settle(doc.RootElement.GetProperty("data").GetProperty("object"));
                }
                return (200, "ok");
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return (400, "bad payload");
            }
        }

        public static async Task Webhook(HttpContext ctx)
        {
            ctx.Request.EnableBuffering();
            string payload;
            using (StreamReader r = new StreamReader(ctx.Request.Body, Encoding.UTF8, false, 4096, true))
            {
                payload = await r.ReadToEndAsync();
            }
            (int status, string body) = await Handle(payload, ctx.Request.Headers["Stripe-Signature"].ToString());
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.WriteAsync(body);
        }
    }
}
