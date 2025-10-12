using PiranaSecuritySystem.Models;
using System.Collections.Generic;

namespace PiranaSecuritySystem.ViewModels
{
    public class GuardTrainingResultsViewModel
    {
        public Guard Guard { get; set; }
        public List<TrainingSession> TrainingSessions { get; set; }
        public List<AssessmentResult> AssessmentResults { get; set; }
    }
}