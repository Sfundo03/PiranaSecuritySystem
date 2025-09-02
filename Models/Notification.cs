using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PiranaSecuritySystem.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }
        public string UserId { get; set; } // Can be ResidentId or DirectorId
        public string UserType { get; set; } // "Resident" or "Director"
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string RelatedUrl { get; set; } // URL to relevant page

        public string NotificationType { get; set; } // "Login", "Incident", "System"
        public bool IsImportant { get; set; }

        public virtual ApplicationUser User { get; set; }
    }
}