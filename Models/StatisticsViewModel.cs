using System;
using System.Collections.Generic;
using System.Linq;

namespace PiranaSecuritySystem.ViewModels
{
    public class StatisticsViewModel
    {
        public int TotalIncidents { get; set; }
        public int ResolvedIncidents { get; set; }
        public int PendingIncidents { get; set; }
        public int InProgressIncidents { get; set; }
        public int HighPriorityIncidents { get; set; }
        public int CriticalPriorityIncidents { get; set; }

        public int ThisMonthIncidents { get; set; }

        public int TotalGuards { get; set; }
        public int TotalCheckIns { get; set; }
        public int TodayCheckIns { get; set; }
        public int CurrentOnDuty { get; set; }

        public double AverageResolutionTime { get; set; }

        public List<MonthlyIncidentData> MonthlyIncidents { get; set; }
        public List<IncidentTypeData> IncidentByType { get; set; }
        public List<IncidentStatusData> IncidentByStatus { get; set; }
    }
}