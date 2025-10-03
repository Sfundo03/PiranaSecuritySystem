using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PiranaSecuritySystem.Models
{
    public class Payroll
    {
        [Key]
        [Display(Name = "Payroll ID")]
        public int PayrollId { get; set; }

        [Required]
        [Display(Name = "Guard")]
        public int GuardId { get; set; }

        [ForeignKey("GuardId")]
        public virtual Guard Guard { get; set; }

        [Required]
        [Display(Name = "Pay Period Start")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime PayPeriodStart { get; set; }

        [Required]
        [Display(Name = "Pay Period End")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime PayPeriodEnd { get; set; }

        [Display(Name = "Total Hours Worked")]
        [DisplayFormat(DataFormatString = "{0:F2}")]
        public double TotalHours { get; set; }

        [Display(Name = "Hourly Rate")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "R {0:F2}")]
        public decimal HourlyRate { get; set; }

        [Display(Name = "Gross Pay")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "R {0:F2}")]
        public decimal GrossPay { get; set; }

        [Display(Name = "Tax Amount")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "R {0:F2}")]
        public decimal TaxAmount { get; set; }

        [Display(Name = "Net Pay")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "R {0:F2}")]
        public decimal NetPay { get; set; }

        [Display(Name = "Pay Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime PayDate { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } // Pending, Processed, Paid

        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } // Bank Transfer, Cash, etc.

        // Helper property to get the month and year for validation
        [NotMapped]
        public int PayrollMonth => PayPeriodStart.Month;

        [NotMapped]
        public int PayrollYear => PayPeriodStart.Year;

        // Helper method to calculate tax percentage (for display purposes)
        [NotMapped]
        public decimal TaxPercentageApplied => GrossPay > 0 ? (TaxAmount / GrossPay) * 100 : 0;
    }

    public class GuardRate
    {
        [Key]
        public int GuardRateId { get; set; }

        [Required]
        [Display(Name = "Guard")]
        public int GuardId { get; set; }

        [ForeignKey("GuardId")]
        public virtual Guard Guard { get; set; }

        [Required]
        [Display(Name = "Hourly Rate")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "R {0:F2}")]
        public decimal Rate { get; set; }

        [Display(Name = "Effective Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
        public DateTime EffectiveDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }
    }

    public class TaxConfiguration
    {
        [Key]
        public int TaxConfigId { get; set; }

        [Required]
        [Display(Name = "Tax Year")]
        [Range(2020, 2030, ErrorMessage = "Please enter a valid tax year")]
        public int TaxYear { get; set; }

        [Required]
        [Display(Name = "Tax Percentage")]
        [Range(0, 100, ErrorMessage = "Tax percentage must be between 0 and 100")]
        public decimal TaxPercentage { get; set; }

        [Required]
        [Display(Name = "Tax Threshold")]
        [DataType(DataType.Currency)]
        [DisplayFormat(DataFormatString = "R {0:F2}")]
        [Range(0, double.MaxValue, ErrorMessage = "Tax threshold must be a positive value")]
        public decimal TaxThreshold { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }
    }
}