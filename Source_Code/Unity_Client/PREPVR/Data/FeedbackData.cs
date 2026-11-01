using System;
using System.Collections.Generic;

namespace PrepVR.Model
{
    [Serializable]
    public class CompetencyEvaluation
    {
        public string categoryName;
        public string rating; // "Exemplary", "Proficient", "Developing", "Needs Focus", "Insufficient Evidence"
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
    public class QuestionFeedback
    {
        public int questionIndex;
        public string stageRole = "";
        public string question = "";
        public string candidateAnswer = "";
        public string whatWentWell = "";
        public string whatCouldBeImproved = "";
        public string relevanceSpecificity = "";
        public string suggestedStrongerStructure = "";
    }

    [Serializable]
    public class SpeechAnalysisMetrics
    {
        public int totalWordsSpoken = 0;
        public float speakingPaceWpm = 0f;
        public string paceAssessment = "Not Assessed";
        public int fillerWordCount = 0;
        public List<string> fillerWordsDetected = new List<string>();
        public string pauseAssessment = "Standard conversational pauses";
        public string clarityOrganization = "Clear conversational flow";
        public string verbalConfidenceIndicators = "Verbal delivery was audible and sustained across panel questions.";
        public string sensorLimitationsDisclaimer = "Hardware Limitation Note: Acoustic pitch, vocal emotion modulation, and corneal eye-tracking are not captured by current hardware sensors. Gaze direction reflects VR headset / camera forward orientation. Visual criteria are strictly limited to observable orientation.";
    }

    [Serializable]
    public class OverallRubricData
    {
        public int overallScore = 0; // 0 - 100
        public string scoringScale = "0-100 (Exemplary: 85-100, Proficient: 70-84, Developing: 50-69, Needs Focus: <50)";
        public int communicationScore = 0;
        public int relevanceScore = 0;
        public int structureClarityScore = 0;
        public int specificityScore = 0;
        public int technicalCorrectnessScore = 0;
        public int behaviouralQualityScore = 0;
        public int concisenessScore = 0;
        public string justification = "";
    }

    [Serializable]
    public class ResumeAlignmentData
    {
        public List<string> demonstratedSkills = new List<string>();
        public List<string> undemonstratedSkills = new List<string>();
        public string alignmentSummary = "";
    }

    [Serializable]
    public class ActionPlanItem
    {
        public int priority;
        public string title = "";
        public string recommendation = "";
        public string practiceQuestion = "";
    }

    [Serializable]
    public class FeedbackData
    {
        // High-level session metadata
        public string candidateName = "Candidate";
        public string candidateEducation = "Engineering Student";
        public string interviewType = "Placement Practice — Resume & Behavioural Interview";
        public string interviewDate = "";
        public string interviewDurationFormatted = "10:00";
        public float interviewDurationSeconds = 600f;
        public int totalQuestionsAsked = 0;
        public int totalCandidateResponses = 0;
        public string overallSummary = "";
        public string overallReadinessLevel = "Placement Ready - Proficient";

        // Section A: Overall Performance & Scoring Rubric
        public OverallRubricData rubric = new OverallRubricData();

        // Section B: Speech-Based Analysis
        public SpeechAnalysisMetrics speechMetrics = new SpeechAnalysisMetrics();

        // Section C: Question-by-Question Feedback
        public List<QuestionFeedback> questionFeedbackList = new List<QuestionFeedback>();

        // Section D: Resume Alignment
        public ResumeAlignmentData resumeAlignmentReport = new ResumeAlignmentData();

        // Section E: Action Plan
        public List<ActionPlanItem> prioritizedActionPlan = new List<ActionPlanItem>();

        // 10 Core Placement Competency Evaluations (Preserved for compatibility)
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

        // Placement Strengths & Areas for Improvement
        public List<string> strengths = new List<string>();
        public List<string> areasForImprovement = new List<string>();
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
