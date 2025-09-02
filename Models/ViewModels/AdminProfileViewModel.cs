using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using PiranaSecuritySystem.Models;
using PiranaSecuritySystem.ViewModels;


namespace PiranaSecuritySystem.Models.ViewModels
{
    public class AdminProfileViewModel
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Department { get; set; }
        public DateTime? LastLoginDate { get; set; }

    }
}