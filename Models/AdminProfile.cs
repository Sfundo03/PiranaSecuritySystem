using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PiranaSecuritySystem.Models
{
    public class AdminProfile
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }
        public string Department { get; set; }
        public DateTime? LastLoginDate { get; set; }

    }
}