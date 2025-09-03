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
        public DateTime PayPeriodStart { get; set; }

        [Required]
        [Display(Name = "Pay Period End")]
        [DataType(DataType.Date)]
        public DateTime PayPeriodEnd { get; set; }

        [Display(Name = "Total Hours Worked")]
        public double TotalHours { get; set; }

        [Display(Name = "Hourly Rate")]
        [DataType(DataType.Currency)]
        public decimal HourlyRate { get; set; }

        [Display(Name = "Gross Pay")]
        [DataType(DataType.Currency)]
        public decimal GrossPay { get; set; }

        [Display(Name = "Tax Amount")]
        [DataType(DataType.Currency)]
        public decimal TaxAmount { get; set; }

        [Display(Name = "Net Pay")]
        [DataType(DataType.Currency)]
        public decimal NetPay { get; set; }

        [Display(Name = "Pay Date")]
        [DataType(DataType.Date)]
        public DateTime PayDate { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } // Pending, Processed, Paid

        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } // Bank Transfer, Cash, etc.
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
        public decimal Rate { get; set; }

        [Display(Name = "Effective Date")]
        [DataType(DataType.Date)]
        public DateTime EffectiveDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }
    }

    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        [Required]
        public int GuardId { get; set; }

        [ForeignKey("GuardId")]
        public virtual Guard Guard { get; set; }

        [Required]
        [Display(Name = "Check-In Time")]
        public DateTime CheckInTime { get; set; }

        [Display(Name = "Check-Out Time")]
        public DateTime? CheckOutTime { get; set; }

        [Display(Name = "Hours Worked")]
        public double HoursWorked { get; set; }

        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime AttendanceDate { get; set; }
    }

    public class TaxConfiguration
    {
        [Key]
        public int TaxConfigId { get; set; }

        [Display(Name = "Tax Year")]
        public int TaxYear { get; set; }

        [Display(Name = "Tax Percentage")]
        public decimal TaxPercentage { get; set; }

        [Display(Name = "Tax Threshold")]
        [DataType(DataType.Currency)]
        public decimal TaxThreshold { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }
    }
}