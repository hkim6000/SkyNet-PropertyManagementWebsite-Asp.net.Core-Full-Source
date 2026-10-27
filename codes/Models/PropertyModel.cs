using System.Text.Json.Serialization;

namespace PropertyManagement.Models
{
    public class SiteData
    {
        public string Brand { get; set; } = "Maple Residential";
        public string Legal { get; set; } = "Maple Residential Property Management";
        public string Tagline { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string Hours { get; set; } = string.Empty;
        public string NotifyEmail { get; set; } = string.Empty;
        public decimal ApplicationFee { get; set; } = 45m;
        public decimal IncomeTimes { get; set; } = 3m;
        public int RentDueDay { get; set; } = 1;
        public int GraceDays { get; set; } = 5;
        public decimal LateFee { get; set; } = 75m;
        public decimal ManagementFee { get; set; } = 8m;
        public int MaxVideoMb { get; set; } = 30;
        public Dictionary<string, PageDoc> Pages { get; set; } = new Dictionary<string, PageDoc>();
    }

    public class PropertyRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = "Apartments";
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int YearBuilt { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> Amenities { get; set; } = new List<string>();
        public string Pets { get; set; } = string.Empty;
        public string Parking { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public List<string> Gallery { get; set; } = new List<string>();
        public bool Show { get; set; } = true;
        public int Order { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class UnitRec
    {
        public string Id { get; set; } = string.Empty;
        public string PropertyId { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public int Beds { get; set; } = 1;
        public decimal Baths { get; set; } = 1m;
        public int Sqft { get; set; }
        public int Floor { get; set; } = 1;
        public decimal Rent { get; set; }
        public decimal Deposit { get; set; }
        public string Status { get; set; } = "Vacant";
        public DateTime? Available { get; set; }
        public bool PetsOk { get; set; }
        public List<string> Features { get; set; } = new List<string>();
        public List<string> Photos { get; set; } = new List<string>();
        public string Plan { get; set; } = string.Empty;
        public bool Listed { get; set; } = true;
        public string Note { get; set; } = string.Empty;
        public DateTime StatusSince { get; set; }
    }

    public class ShowingRec
    {
        public string Id { get; set; } = string.Empty;
        public string UnitId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string Status { get; set; } = "Requested";
        public string Note { get; set; } = string.Empty;
        public string Source { get; set; } = "website";
        public string AgentId { get; set; } = string.Empty;
        public DateTime Created { get; set; }
    }

    public class Applicant
    {
        public string First { get; set; } = string.Empty;
        public string Last { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime? Birth { get; set; }
        public string Employer { get; set; } = string.Empty;
        public decimal Income { get; set; }
    }

    public class ApplicationRec
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string UnitId { get; set; } = string.Empty;
        public DateTime MoveIn { get; set; }
        public int Months { get; set; } = 12;
        public List<Applicant> People { get; set; } = new List<Applicant>();
        public int Occupants { get; set; } = 1;
        public string Pets { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Landlord { get; set; } = string.Empty;
        public string LandlordPhone { get; set; } = string.Empty;
        public int YearsAt { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = "New";
        public string Decision { get; set; } = string.Empty;
        public List<string> Checks { get; set; } = new List<string>();
        public DateTime Created { get; set; }
        public string Source { get; set; } = "website";
        public string DecidedBy { get; set; } = string.Empty;
        public DateTime? DecidedAt { get; set; }
        public string LeaseId { get; set; } = string.Empty;
    }

    public class LeaseRec
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string UnitId { get; set; } = string.Empty;
        public List<string> Residents { get; set; } = new List<string>();
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public decimal Rent { get; set; }
        public decimal Deposit { get; set; }
        public string Status { get; set; } = "Upcoming";
        public DateTime? MovedIn { get; set; }
        public DateTime? NoticeOn { get; set; }
        public DateTime? MoveOut { get; set; }
        public DateTime? MovedOut { get; set; }
        public string RenewalOf { get; set; } = string.Empty;
        public string RenewedBy { get; set; } = string.Empty;
        public string ApplicationId { get; set; } = string.Empty;
        public string Pets { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class ResidentRec
    {
        public string Id { get; set; } = string.Empty;
        public string First { get; set; } = string.Empty;
        public string Last { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Emergency { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string Invite { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string ResetToken { get; set; } = string.Empty;
        public DateTime? ResetUntil { get; set; }
        public DateTime? LastSeen { get; set; }
        public DateTime Created { get; set; }
    }

    public class LedgerRec
    {
        public string Id { get; set; } = string.Empty;
        public string LeaseId { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string Kind { get; set; } = "Rent";
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
    }

    public class NoticeRec
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<string> Properties { get; set; } = new List<string>();
        public bool Pinned { get; set; }
        public DateTime From { get; set; }
        public DateTime? Until { get; set; }
        public DateTime Created { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class InquiryRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Topic { get; set; } = "General";
        public string Area { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string Status { get; set; } = "New";
    }

    public class VendorRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Trade { get; set; } = "General";
        public string Contact { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public DateTime? InsuredUntil { get; set; }
        public bool Active { get; set; } = true;
        public string Note { get; set; } = string.Empty;
    }

    public class WoNote
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool Shared { get; set; }
    }

    public class WorkOrderRec
    {
        public string Id { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string PropertyId { get; set; } = string.Empty;
        public string UnitId { get; set; } = string.Empty;
        public string Category { get; set; } = "Other";
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Priority { get; set; } = "Normal";
        public string Status { get; set; } = "New";
        public string Source { get; set; } = "staff";
        public string ResidentId { get; set; } = string.Empty;
        public bool EntryOk { get; set; }
        public string Pets { get; set; } = string.Empty;
        public List<string> Photos { get; set; } = new List<string>();
        public string AssignedTo { get; set; } = string.Empty;
        public string VendorId { get; set; } = string.Empty;
        public DateTime? Scheduled { get; set; }
        public DateTime Due { get; set; }
        public DateTime? Done { get; set; }
        public decimal Cost { get; set; }
        public string BillTo { get; set; } = "Owner";
        public string ChargeId { get; set; } = string.Empty;
        public string InspectionId { get; set; } = string.Empty;
        public string TurnId { get; set; } = string.Empty;
        public List<WoNote> Notes { get; set; } = new List<WoNote>();
        public DateTime Created { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class InspItem
    {
        public string Name { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public class InspRoom
    {
        public string Room { get; set; } = string.Empty;
        public List<InspItem> Items { get; set; } = new List<InspItem>();
    }

    public class InspectionRec
    {
        public string Id { get; set; } = string.Empty;
        public string UnitId { get; set; } = string.Empty;
        public string LeaseId { get; set; } = string.Empty;
        public string Kind { get; set; } = "Routine";
        public DateTime Date { get; set; }
        public string Status { get; set; } = "Scheduled";
        public string Inspector { get; set; } = string.Empty;
        public List<InspRoom> Rooms { get; set; } = new List<InspRoom>();
        public string Summary { get; set; } = string.Empty;
        public DateTime? Finished { get; set; }
        public DateTime Created { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class TurnStep
    {
        public string Name { get; set; } = string.Empty;
        public bool Done { get; set; }
        public DateTime? DoneAt { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class TurnRec
    {
        public string Id { get; set; } = string.Empty;
        public string UnitId { get; set; } = string.Empty;
        public string LeaseId { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime Target { get; set; }
        public string Status { get; set; } = "Open";
        public List<TurnStep> Steps { get; set; } = new List<TurnStep>();
        public string Note { get; set; } = string.Empty;
        public DateTime? Finished { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class OwnerRec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Properties { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, decimal>? Shares { get; set; }
        public decimal? Fee { get; set; }
        public string PayTo { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
    }

    public class OwnershipRec
    {
        public string Id { get; set; } = string.Empty;
        public string PropertyId { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public decimal Share { get; set; }
        public DateTime? From { get; set; }
        public DateTime? Until { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
        public DateTime Created { get; set; }
    }

    public class ExpenseRec
    {
        public string Id { get; set; } = string.Empty;
        public string PropertyId { get; set; } = string.Empty;
        public string UnitId { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string Category { get; set; } = "Other";
        public string Payee { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime? CoverFrom { get; set; }
        public DateTime? CoverTo { get; set; }
        public string WorkOrderId { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class PayoutRec
    {
        public string Id { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string Month { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime When { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string By { get; set; } = string.Empty;
    }

    public class DepositLine
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class DepositRec
    {
        public string Id { get; set; } = string.Empty;
        public string LeaseId { get; set; } = string.Empty;
        public decimal Held { get; set; }
        public decimal Owed { get; set; }
        public List<DepositLine> Lines { get; set; } = new List<DepositLine>();
        public decimal Refund { get; set; }
        public string Status { get; set; } = "Draft";
        public string Method { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public DateTime? Finished { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class PayAttempt
    {
        public string Id { get; set; } = string.Empty;
        public string LeaseId { get; set; } = string.Empty;
        public string ResidentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Open";
        public string Session { get; set; } = string.Empty;
        public string LedgerId { get; set; } = string.Empty;
        public string Source { get; set; } = "stripe";
        public DateTime Created { get; set; }
        public DateTime? Paid { get; set; }
    }

    public class PaySettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public DateTime? Updated { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class MediaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public string Kind { get; set; } = "photo";
        public string Name { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public DateTime Added { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class BlockItem
    {
        public string Icon { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class Block
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public bool Hidden { get; set; }
        public string Kicker { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label2 { get; set; } = string.Empty;
        public string Link2 { get; set; } = string.Empty;
        public bool Flip { get; set; }
        public List<BlockItem> Items { get; set; } = new List<BlockItem>();
    }

    public class PageVersion
    {
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public List<Block> Blocks { get; set; } = new List<Block>();
    }

    public class PageDoc
    {
        public List<Block> Blocks { get; set; } = new List<Block>();
        public List<PageVersion> Versions { get; set; } = new List<PageVersion>();
        public DateTime Saved { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class AdminUser
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public bool MustChange { get; set; }
        public bool Active { get; set; } = true;
        public bool Manager { get; set; }
        public string SetId { get; set; } = string.Empty;
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
        public bool AllProperties { get; set; } = true;
        public List<string> Properties { get; set; } = new List<string>();
    }

    public class PermSet
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public Dictionary<string, string> Perms { get; set; } = new Dictionary<string, string>();
    }

    public class SessionRec
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
    }

    public class ActivityRec
    {
        public DateTime When { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; } = true;
    }

    public class PublicData
    {
        public SiteData Site { get; set; } = new SiteData();
        public List<MediaItem> Media { get; set; } = new List<MediaItem>();
        public List<PropertyRec> Properties { get; set; } = new List<PropertyRec>();
        public List<UnitRec> Units { get; set; } = new List<UnitRec>();
    }
}
