using System;
using System.Collections.Generic;

namespace PrepVR.Model
{
    [Serializable]
    public class CompetencyEvaluation
    {
        public string categoryName;
        public string rating; // "Exemplary", "Proficient", "Developing", "Needs Focus"
        public string observation;
        public string recommendation;

        public CompetencyEvaluation() { }

        public CompetencyEvaluation(string name, string rating, string observation, string recommendation)
        {
            this.categoryName = name;
            this.rating = rating;
            this.observation = observation;
            this.recommendation = recommendation;
        }
    }

    [Serializable]
    public class FeedbackData
    {
        // High-level session metadata
        public string candidateName = "Candidate";
        public string candidateEducation = "Engineering Student";
        public string interviewType = "Behavioural & Resume Placement Interview";
        public string interviewDate = "";
        public string interviewDurationFormatted = "10:00";
        public float interviewDurationSeconds = 600f;
        public int totalQuestionsAsked = 0;
        public int totalCandidateResponses = 0;
        public string overallSummary = "";
        public string overallReadinessLevel = "Placement Ready - Proficient";

        // 13 Required Placement Competency Evaluations
        public CompetencyEvaluation resumeAlignment = new CompetencyEvaluation();
        public CompetencyEvaluation projectDepth = new CompetencyEvaluation();
        public CompetencyEvaluation problemSolving = new CompetencyEvaluation();
        public CompetencyEvaluation technicalFluency = new CompetencyEvaluation();
        public CompetencyEvaluation candidateOwnership = new CompetencyEvaluation();
        public CompetencyEvaluation handlingUncertainty = new CompetencyEvaluation();
        public CompetencyEvaluation continuousLearning = new CompetencyEvaluation();
        public CompetencyEvaluation communicationStructure = new CompetencyEvaluation();
        public CompetencyEvaluation professionalPresence = new CompetencyEvaluation();
        public CompetencyEvaluation activeListening = new CompetencyEvaluation();

        // 11. Key Placement Strengths
        public List<string> strengths = new List<string>();

        // 12. Priority Areas for Growth
        public List<string> areasForImprovement = new List<string>();

        // 13. Actionable Placement Preparation Recommendations
        public List<string> actionableRecommendations = new List<string>();
        public List<string> practicePlan = new List<string>();

        // Factual, Observable Demeanor Records (No fabricated percentages)
        public string observableEyeContact = "";
        public string observablePostureBodyLanguage = "";
        public string observableEngagement = "";

        // Generated PDF output path
        public string generatedPdfPath = "";
        public string generatedTimestamp = "";

        public List<CompetencyEvaluation> GetAllEvaluations()
        {
            return new List<CompetencyEvaluation>
            {
                resumeAlignment,
                projectDepth,
                problemSolving,
                technicalFluency,
                candidateOwnership,
                handlingUncertainty,
                continuousLearning,
                communicationStructure,
                professionalPresence,
                activeListening
            };
        }
    }
}
