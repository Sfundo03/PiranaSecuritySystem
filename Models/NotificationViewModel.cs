using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PiranaSecuritySystem.ViewModels
{
    public class NotificationViewModel
    {
        public int NotificationId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string NotificationType { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TimeAgo { get; set; }
        public string RelatedUrl { get; set; }
        public bool IsImportant { get; set; }
        public int PriorityLevel { get; set; }
        public string PriorityClass { get; set; }
    }
}