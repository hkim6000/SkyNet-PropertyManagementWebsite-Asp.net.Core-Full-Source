using PropertyManagement.Models;

namespace PropertyManagement.Core
{
    public static class Maint
    {
        public static readonly string[] Categories = { "Plumbing", "Electrical", "Heating and cooling", "Appliance", "Doors and locks", "Pests", "Walls and floors", "Windows", "Outside", "Other" };
        public static readonly string[] Priorities = { "Emergency", "High", "Normal", "Low" };
        public static readonly string[] Statuses = { "New", "Scheduled", "In progress", "On hold", "Done", "Cancelled" };
        public static readonly string[] Payers = { "Owner", "Resident", "None" };
        public static readonly string[] Trades = { "General", "Plumbing", "Electrical", "Heating and cooling", "Appliance", "Painting", "Cleaning", "Flooring", "Locksmith", "Pest control", "Landscaping", "Roofing" };
        public static readonly string[] Kinds = { "Move-in", "Move-out", "Routine", "Annual" };
        public static readonly string[] Conditions = { "Good", "Fair", "Poor", "Damaged", "Not there" };
        public static readonly string[] Steps = { "Move-out inspection", "Repairs", "Paint", "Deep clean", "Carpet or floors", "Change locks", "Final walk-through" };

        public static DateTime DueFor(string priority, DateTime from)
        {
            return priority switch
            {
                "Emergency" => from.AddHours(24),
                "High" => from.AddDays(3),
                "Low" => from.AddDays(14),
                _ => from.AddDays(7)
            };
        }

        public static bool Open(WorkOrderRec w)
        {
            return w.Status is not ("Done" or "Cancelled");
        }

        public static bool Overdue(WorkOrderRec w)
        {
            return Open(w) && w.Status != "On hold" && w.Due < DateTime.Now;
        }

        public static string Place(RentData d, WorkOrderRec w)
        {
            return w.UnitId.Length > 0 ? Rent.UnitLabel(d, w.UnitId) : Rent.PropertyName(d, w.PropertyId) + " · common area";
        }

        public static string Vendor(RentData d, string id)
        {
            return d.Vendors.FirstOrDefault(v => v.Id == id)?.Name ?? string.Empty;
        }

        public static List<InspRoom> Checklist(UnitRec u)
        {
            List<InspRoom> rooms = new List<InspRoom>
            {
                Room("Entry and living", "Walls and paint", "Floors", "Windows and blinds", "Lights and outlets", "Smoke alarm"),
                Room("Kitchen", "Cabinets and counters", "Sink and faucet", "Stove and oven", "Fridge", "Dishwasher", "Floors")
            };
            for (int i = 1; i <= Math.Max(0, u.Beds); i++)
            {
                rooms.Add(Room(u.Beds == 1 ? "Bedroom" : "Bedroom " + i, "Walls and paint", "Floors", "Windows and blinds", "Closet", "Lights and outlets"));
            }
            int baths = (int)Math.Ceiling(u.Baths);
            for (int i = 1; i <= Math.Max(1, baths); i++)
            {
                rooms.Add(Room(baths == 1 ? "Bathroom" : "Bathroom " + i, "Toilet", "Sink and faucet", "Tub or shower", "Fan", "Floors"));
            }
            rooms.Add(Room("Whole home", "Doors and locks", "Heating and cooling", "Water heater", "Keys returned"));
            return rooms;
        }

        private static InspRoom Room(string name, params string[] items)
        {
            return new InspRoom { Room = name, Items = items.Select(x => new InspItem { Name = x }).ToList() };
        }

        public static TurnRec NewTurn(UnitRec u, string leaseId, DateTime start, string by)
        {
            return new TurnRec { Id = Fmt.NewId("t-"), UnitId = u.Id, LeaseId = leaseId, Start = start.Date, Target = start.Date.AddDays(10), Status = "Open", Steps = Steps.Select(s => new TurnStep { Name = s }).ToList(), By = by };
        }

        public static InspectionRec NewInspection(UnitRec u, string leaseId, string kind, DateTime date, string inspector, string by)
        {
            return new InspectionRec { Id = Fmt.NewId("i-"), UnitId = u.Id, LeaseId = leaseId, Kind = kind, Date = date, Status = "Scheduled", Inspector = inspector, Rooms = Checklist(u), Created = DateTime.Now, By = by };
        }

        public static LeaseRec? LeaseFor(RentData d, WorkOrderRec w)
        {
            if (w.ResidentId.Length > 0)
            {
                LeaseRec? own = d.Leases.Where(l => l.UnitId == w.UnitId && l.Residents.Contains(w.ResidentId) && l.Status != "Cancelled").OrderByDescending(l => l.Start).FirstOrDefault();
                if (own != null)
                {
                    return own;
                }
            }
            return Rent.CurrentLease(d, w.UnitId) ?? d.Leases.Where(l => l.UnitId == w.UnitId && l.Status == "Ended").OrderByDescending(l => l.MovedOut ?? l.End).FirstOrDefault();
        }
    }
}
