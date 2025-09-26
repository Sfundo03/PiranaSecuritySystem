using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PiranaSecuritySystem.ViewModels
{
    public class GuardCheckInViewModel
    {
        public int GuardId { get; set; }
        public string SiteUsername { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string ExpectedTime { get; set; }
        public string ActualTime { get; set; }
        public bool IsLate { get; set; }
        public string Token { get; set; }
        public int? RosterId { get; set; }
    }
}