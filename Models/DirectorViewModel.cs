// In ViewModels/DirectorViewModels.cs
using PiranaSecuritySystem.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace PiranaSecuritySystem.ViewModels
{
    public class DirectorDashboardViewModel
    {
        public int TotalIncidents { get; set; }
        public int ResolvedIncidents { get; set; }
        public int PendingIncidents { get; set; }
        public int InProgressIncidents { get; set; }
        public int HighPriorityIncidents { get; set; }
        public int CriticalPriorityIncidents { get; set; }
        public int ThisMonthIncidents { get; set; }
        public List<IncidentReport> RecentIncidents { get; set; }
        public List<IncidentTypeCount> IncidentTypes { get; set; }
    }

    public class StatisticsViewModel
    {
        public int TotalIncidents { get; set; }
        public int ResolvedIncidents { get; set; }
        public int PendingIncidents { get; set; }
        public int InProgressIncidents { get; set; }
        public int HighPriorityIncidents { get; set; }
        public int CriticalPriorityIncidents { get; set; }
        public int ThisMonthIncidents { get; set; }
        public List<IncidentTypeCount> IncidentByType { get; set; }
        public List<IncidentStatusCount> IncidentByStatus { get; set; }
        public List<IncidentPriorityCount> IncidentByPriority { get; set; }
        public List<MonthlyIncidentCount> MonthlyIncidents { get; set; }
        public List<dynamic> ResolutionTimes { get; set; }
    }

    public class ReportsViewModel
    {
        public int ReportYear { get; set; }
        public List<SelectListItem> AvailableYears { get; set; }
        public List<MonthlyIncidentCount> MonthlyIncidents { get; set; }
        public List<IncidentTypeCount> IncidentByType { get; set; }
        public List<IncidentStatusCount> IncidentByStatus { get; set; }
        public List<IncidentPriorityCount> IncidentByPriority { get; set; }
        public double AverageResolutionTime { get; set; }
        public int TotalResolvedIncidents { get; set; }
        public List<ResolutionTimeByType> ResolutionTimeByType { get; set; }
    }

    public class IncidentDetailsViewModel
    {
        public IncidentReport Incident { get; set; }
        public List<IncidentUpdate> Updates { get; set; }
    }

    public class IncidentTypeCount
    {
        public string Type { get; set; }
        public int Count { get; set; }
    }

    public class IncidentStatusCount
    {
        public string Status { get; set; }
        public int Count { get; set; }
    }

    public class IncidentPriorityCount
    {
        public string Priority { get; set; }
        public int Count { get; set; }
    }

    public class MonthlyIncidentCount
    {
        public int Month { get; set; }
        public string MonthName { get; set; }
        public int Count { get; set; }
    }

    public class ResolutionTimeByType
    {
        public string Type { get; set; }
        public double AverageDays { get; set; }
        public int Count { get; set; }
    }

    public class IncidentUpdate
    {
        public int UpdateId { get; set; }
        public int IncidentId { get; set; }
        public string UpdatedBy { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public string Notes { get; set; }
        public DateTime UpdateDate { get; set; }
    }
}