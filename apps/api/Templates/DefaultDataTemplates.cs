using Hostr.Api.Models;
using System.Text.Json;

namespace Hostr.Api.Templates;

public static class DefaultDataTemplates
{
    public static class RequestItems
    {
        public static List<RequestItem> GetHousekeepingItems(int tenantId) => new()
        {
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Fresh Towels",
                Category = "Housekeeping",
                LlmVisibleName = "towels",
                NotesForStaff = "Clean bath and hand towels delivered to room",
                RequiresQuantity = true,
                RequiresRoomDelivery = true,
                DefaultQuantityLimit = 6,
                SlaMinutes = 30
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Extra Pillows",
                Category = "Housekeeping",
                LlmVisibleName = "pillows",
                NotesForStaff = "Additional pillows for comfort",
                RequiresQuantity = true,
                RequiresRoomDelivery = true,
                DefaultQuantityLimit = 4,
                SlaMinutes = 30
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Extra Blanket",
                Category = "Housekeeping",
                LlmVisibleName = "blanket",
                NotesForStaff = "Additional blanket or duvet",
                RequiresQuantity = true,
                RequiresRoomDelivery = true,
                DefaultQuantityLimit = 3,
                SlaMinutes = 30
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Bed Sheets Change",
                Category = "Housekeeping",
                LlmVisibleName = "sheets",
                NotesForStaff = "Fresh bed linen replacement",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 45
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Room Cleaning",
                Category = "Housekeeping",
                LlmVisibleName = "cleaning",
                NotesForStaff = "Full room cleaning service",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 60
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Bathroom Supplies",
                Category = "Housekeeping",
                LlmVisibleName = "bathroom supplies",
                NotesForStaff = "Soap, shampoo, toilet paper refill",
                RequiresQuantity = false,
                RequiresRoomDelivery = true,
                SlaMinutes = 20
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Laundry Service",
                Category = "Housekeeping",
                LlmVisibleName = "laundry",
                NotesForStaff = "Wash and fold laundry service - pickup and delivery",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 240
            }
        };

        public static List<RequestItem> GetRoomAmenityItems(int tenantId) => new()
        {
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Iron & Board",
                Category = "Room Amenities",
                LlmVisibleName = "iron",
                NotesForStaff = "Iron and ironing board for clothes",
                RequiresQuantity = false,
                RequiresRoomDelivery = true,
                SlaMinutes = 20
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Hair Dryer",
                Category = "Room Amenities",
                LlmVisibleName = "hair dryer",
                NotesForStaff = "Hair dryer for personal grooming",
                RequiresQuantity = false,
                RequiresRoomDelivery = true,
                SlaMinutes = 15
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Extra Hangers",
                Category = "Room Amenities",
                LlmVisibleName = "hangers",
                NotesForStaff = "Additional clothes hangers",
                RequiresQuantity = true,
                RequiresRoomDelivery = true,
                DefaultQuantityLimit = 10,
                SlaMinutes = 15
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Slippers",
                Category = "Room Amenities",
                LlmVisibleName = "slippers",
                NotesForStaff = "Disposable room slippers",
                RequiresQuantity = true,
                RequiresRoomDelivery = true,
                DefaultQuantityLimit = 4,
                SlaMinutes = 15
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Bathrobe",
                Category = "Room Amenities",
                LlmVisibleName = "bathrobe",
                NotesForStaff = "Hotel bathrobe for comfort",
                RequiresQuantity = true,
                RequiresRoomDelivery = true,
                DefaultQuantityLimit = 2,
                SlaMinutes = 20
            }
        };

        public static List<RequestItem> GetMaintenanceItems(int tenantId) => new()
        {
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "AC Repair",
                Category = "Maintenance",
                LlmVisibleName = "air conditioning",
                NotesForStaff = "Air conditioning repair or adjustment",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 120
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Plumbing Issues",
                Category = "Maintenance",
                LlmVisibleName = "plumbing",
                NotesForStaff = "Plumbing repairs - leaks, clogs, water pressure",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 60
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Electrical Problems",
                Category = "Maintenance",
                LlmVisibleName = "electrical",
                NotesForStaff = "Electrical issues - lights, outlets, switches",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 90
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Room Lock Issues",
                Category = "Maintenance",
                LlmVisibleName = "door lock",
                NotesForStaff = "Door lock repair or key card programming",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 30
            }
        };

        public static List<RequestItem> GetFoodBeverageItems(int tenantId) => new()
        {
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Room Service Order",
                Category = "Food & Beverage",
                LlmVisibleName = "room service",
                NotesForStaff = "Room service food order - check menu",
                RequiresQuantity = false,
                RequiresRoomDelivery = true,
                SlaMinutes = 45
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Coffee & Tea Setup",
                Category = "Food & Beverage",
                LlmVisibleName = "coffee tea",
                NotesForStaff = "Coffee, tea, and supplies refill",
                RequiresQuantity = false,
                RequiresRoomDelivery = true,
                SlaMinutes = 15
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Ice Delivery",
                Category = "Food & Beverage",
                LlmVisibleName = "ice",
                NotesForStaff = "Ice bucket delivery",
                RequiresQuantity = true,
                RequiresRoomDelivery = true,
                DefaultQuantityLimit = 3,
                SlaMinutes = 10
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Mini Bar Restock",
                Category = "Food & Beverage",
                LlmVisibleName = "mini bar",
                NotesForStaff = "Mini bar restocking service",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 30
            }
        };

        public static List<RequestItem> GetConciergeItems(int tenantId) => new()
        {
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Restaurant Reservation",
                Category = "Concierge",
                LlmVisibleName = "restaurant reservation",
                NotesForStaff = "Make restaurant reservation for guest",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 15
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Transportation Booking",
                Category = "Concierge",
                LlmVisibleName = "transportation",
                NotesForStaff = "Arrange taxi, uber, or shuttle service",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 20
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Tour Booking",
                Category = "Concierge",
                LlmVisibleName = "tour booking",
                NotesForStaff = "Book local tours or attractions",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 30
            },
            new RequestItem 
            { 
                TenantId = tenantId,
                Name = "Wake-up Call",
                Category = "Concierge",
                LlmVisibleName = "wake up call",
                NotesForStaff = "Schedule wake-up call for guest",
                RequiresQuantity = false,
                RequiresRoomDelivery = false,
                SlaMinutes = 1
            }
        };

        public static List<RequestItem> GetAllDefaultItems(int tenantId)
        {
            var allItems = new List<RequestItem>();
            allItems.AddRange(GetHousekeepingItems(tenantId));
            allItems.AddRange(GetRoomAmenityItems(tenantId));
            allItems.AddRange(GetMaintenanceItems(tenantId));
            allItems.AddRange(GetFoodBeverageItems(tenantId));
            allItems.AddRange(GetConciergeItems(tenantId));
            return allItems;
        }
    }

    public static class FAQs
    {
        public static List<FAQ> GetEssentialFAQs(int tenantId) => new()
        {
            new FAQ
            {
                TenantId = tenantId,
                Question = "What is the WiFi password?",
                Answer = "Our guest WiFi network is 'Hotel_Guest' with password 'Welcome2024!'. The network provides high-speed internet throughout the hotel including all rooms, lobby, restaurant, and pool areas.",
                Language = "en",
                Tags = new[] { "wifi", "internet", "password", "network" }
            },
            new FAQ
            {
                TenantId = tenantId,
                Question = "What time is checkout?",
                Answer = "Standard checkout time is 11:00 AM. Late checkout until 2:00 PM can be arranged subject to availability - just ask at the front desk!",
                Language = "en",
                Tags = new[] { "checkout", "time", "late checkout", "front desk" }
            },
            new FAQ
            {
                TenantId = tenantId,
                Question = "Do you have room service?",
                Answer = "Yes! Room service is available 24/7. You can view our full menu by asking me to 'show menu' or call extension 7 from your room phone.",
                Language = "en",
                Tags = new[] { "room service", "menu", "24/7", "extension", "food" }
            },
            new FAQ
            {
                TenantId = tenantId,
                Question = "Where is the gym?",
                Answer = "Our fitness center is located on the 2nd floor and is open 24/7 for guests. Access with your room key. We have cardio equipment, weights, and towels available.",
                Language = "en",
                Tags = new[] { "gym", "fitness", "2nd floor", "24/7", "equipment", "towels" }
            },
            new FAQ
            {
                TenantId = tenantId,
                Question = "What time is breakfast?",
                Answer = "Breakfast is served daily from 6:30 AM to 10:30 AM in the main restaurant on the ground floor. We offer both continental and full breakfast options.",
                Language = "en",
                Tags = new[] { "breakfast", "time", "restaurant", "continental", "ground floor" }
            },
            new FAQ
            {
                TenantId = tenantId,
                Question = "Do you have parking?",
                Answer = "Yes, we offer complimentary valet parking for all guests. Just pull up to the main entrance and our staff will take care of your vehicle.",
                Language = "en",
                Tags = new[] { "parking", "valet", "complimentary", "free", "car", "vehicle" }
            },
            new FAQ
            {
                TenantId = tenantId,
                Question = "Can I get extra towels?",
                Answer = "Absolutely! I can arrange for housekeeping to bring extra towels to your room. They should arrive within 15-20 minutes. Anything else you need?",
                Language = "en",
                Tags = new[] { "towels", "housekeeping", "extra", "room", "amenities" }
            }
        };
    }

    public static class BusinessInfoTemplates
    {
        public static List<BusinessInfo> GetEssentialBusinessInfo(int tenantId) => new()
        {
            new BusinessInfo
            {
                TenantId = tenantId,
                Category = "wifi_credentials",
                Title = "WiFi Network Information",
                Content = JsonSerializer.Serialize(new
                {
                    network = "Hotel_Guest",
                    password = "Welcome2024!"
                }),
                Tags = new[] { "wifi", "internet", "guest", "network" },
                IsActive = true,
                DisplayOrder = 1
            },
            new BusinessInfo
            {
                TenantId = tenantId,
                Category = "hotel_hours",
                Title = "Hotel Operating Hours",
                Content = JsonSerializer.Serialize(new
                {
                    checkin = "3:00 PM",
                    checkout = "11:00 AM",
                    front_desk = "24/7",
                    breakfast = "6:30 AM - 10:30 AM",
                    gym = "24/7",
                    pool = "6:00 AM - 10:00 PM"
                }),
                Tags = new[] { "hours", "checkin", "checkout", "breakfast", "gym" },
                IsActive = true,
                DisplayOrder = 2
            },
            new BusinessInfo
            {
                TenantId = tenantId,
                Category = "contact_info",
                Title = "Hotel Contact Information",
                Content = JsonSerializer.Serialize(new
                {
                    front_desk = "Extension 0 or (555) 123-4567",
                    room_service = "Extension 7",
                    concierge = "Extension 5",
                    maintenance = "Extension 9"
                }),
                Tags = new[] { "contact", "phone", "extension", "front desk" },
                IsActive = true,
                DisplayOrder = 3
            }
        };
    }

    public static class EmergencyTypes
    {
        public static List<EmergencyType> GetDefaultEmergencyTypes(int tenantId) => new()
        {
            new EmergencyType
            {
                TenantId = tenantId,
                Name = "Fire Emergency",
                Description = "Fire alarm, smoke, or visible fire",
                SeverityLevel = "Critical",
                DetectionKeywords = new[] { "fire", "smoke", "burning", "alarm", "flames" },
                IsActive = true
            },
            new EmergencyType
            {
                TenantId = tenantId,
                Name = "Medical Emergency",
                Description = "Medical assistance required",
                SeverityLevel = "Critical",
                DetectionKeywords = new[] { "medical", "ambulance", "injury", "sick", "emergency", "help" },
                IsActive = true
            },
            new EmergencyType
            {
                TenantId = tenantId,
                Name = "Security Issue",
                Description = "Safety or security concern",
                SeverityLevel = "High",
                DetectionKeywords = new[] { "security", "theft", "suspicious", "unsafe", "intruder" },
                IsActive = true
            },
            new EmergencyType
            {
                TenantId = tenantId,
                Name = "Power Outage",
                Description = "Electrical power failure",
                SeverityLevel = "Medium",
                DetectionKeywords = new[] { "power", "electricity", "outage", "blackout", "lights" },
                IsActive = true
            }
        };
    }

    public static class LostAndFoundCategories
    {
        public static List<LostAndFoundCategory> GetDefaultCategories(int tenantId) => new()
        {
            new LostAndFoundCategory { TenantId = tenantId, Name = "Electronics", Description = "Phones, tablets, laptops, chargers", IsActive = true },
            new LostAndFoundCategory { TenantId = tenantId, Name = "Clothing", Description = "Clothes, shoes, accessories", IsActive = true },
            new LostAndFoundCategory { TenantId = tenantId, Name = "Jewelry", Description = "Rings, watches, necklaces", IsActive = true },
            new LostAndFoundCategory { TenantId = tenantId, Name = "Documents", Description = "ID, passport, tickets, papers", IsActive = true },
            new LostAndFoundCategory { TenantId = tenantId, Name = "Personal Items", Description = "Toiletries, glasses, books", IsActive = true },
            new LostAndFoundCategory { TenantId = tenantId, Name = "Keys", Description = "Car keys, house keys, key fobs", IsActive = true }
        };
    }
}