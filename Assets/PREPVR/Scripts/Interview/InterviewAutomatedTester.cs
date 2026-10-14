using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convai.Runtime.Components;
using Convai.Domain.DomainEvents.Transcript;

namespace PrepVR.Interview
{
    /// <summary>
    /// Automated test runner to test and verify the 3-person panel interview in Play Mode.
    /// Responds ONLY when the interviewer has completely finished speaking the entire question.
    /// If a human speaks verbally via microphone, the automated response is immediately cancelled.
    /// </summary>
    public class InterviewAutomatedTester : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private bool testActive = false;
        [SerializeField] private float autoResponseTimeoutSeconds = 2.5f;

        public bool TestActive
        {
            get => testActive;
            set => testActive = value;
        }

        private ConvaiPlayer player;
        private ConvaiManager manager;
        private Coroutine pendingResponseCoroutine;
        private bool isResponding = false;
        private string lastDeliveredAnswer = "";
        private int hrTurn = 0;
        private int technicalTurn = 0;
        private int hiringMgrTurn = 0;

        public static InterviewAutomatedTester Instance { get; private set; }
        public bool IsCandidateSpeakingOrScheduled => isResponding || pendingResponseCoroutine != null;

        public void CancelPendingResponse()
        {
            if (pendingResponseCoroutine != null)
            {
                StopCoroutine(pendingResponseCoroutine);
                pendingResponseCoroutine = null;
            }
            isResponding = false;
            Debug.Log("[AutomatedTester] CancelPendingResponse executed - candidate state reset.");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (!testActive)
            {
                Debug.Log("[AutomatedTester] Automated tester is DISABLED for live interview mode.");
                return;
            }
            StartCoroutine(InitializeTesterCoroutine());
        }

        private IEnumerator InitializeTesterCoroutine()
        {
            Debug.Log("[AutomatedTester] Initializing automated Play Mode interview verification...");
            float wait = 10f;
            while (ConvaiManager.ActiveManager == null && wait > 0f)
            {
                yield return new WaitForSeconds(0.5f);
                wait -= 0.5f;
            }

            manager = ConvaiManager.ActiveManager;
            player = FindAnyObjectByType<ConvaiPlayer>();

            if (manager != null)
            {
                manager.Events.OnCharacterTranscriptReceived += HandleInterviewerTranscript;
                manager.Events.OnPlayerTranscriptReceived    += HandlePlayerTranscript;
                Debug.Log("[AutomatedTester] Subscribed to ConvaiManager transcript events.");
            }
            else
            {
                Debug.LogError("[AutomatedTester] ConvaiManager not found for automated testing.");
            }

            // Subscribe to authoritative Question Finished event from InterviewOrchestrator
            float waitOrch = 10f;
            while (InterviewOrchestrator.Instance == null && waitOrch > 0f)
            {
                yield return new WaitForSeconds(0.5f);
                waitOrch -= 0.5f;
            }

            if (InterviewOrchestrator.Instance != null)
            {
                InterviewOrchestrator.Instance.OnInterviewerQuestionFinished += HandleInterviewerQuestionFinished;
                Debug.Log("[AutomatedTester] Successfully subscribed to InterviewOrchestrator.OnInterviewerQuestionFinished.");
            }
            else
            {
                Debug.LogWarning("[AutomatedTester] InterviewOrchestrator.Instance not found.");
            }
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                try
                {
                    manager.Events.OnCharacterTranscriptReceived -= HandleInterviewerTranscript;
                    manager.Events.OnPlayerTranscriptReceived    -= HandlePlayerTranscript;
                }
                catch { }
            }

            if (InterviewOrchestrator.Instance != null)
            {
                try
                {
                    InterviewOrchestrator.Instance.OnInterviewerQuestionFinished -= HandleInterviewerQuestionFinished;
                }
                catch { }
            }
        }

        private void HandlePlayerTranscript(PlayerTranscriptReceived e)
        {
            string text = e.Message.Text ?? "";
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 5) return;

            // Ignore system opening / transition prompts echoed as player transcript
            if (text.IndexOf("begin Stage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Warmly welcome the candidate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Acknowledge the transition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Greet the candidate for this final stage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            // Ignore self-echoed candidate text delivered by this automated tester
            if (!string.IsNullOrEmpty(lastDeliveredAnswer) &&
                (text.IndexOf(lastDeliveredAnswer, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 lastDeliveredAnswer.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return;
            }

            // Human player spoke! Cancel any pending automated response.
            if (pendingResponseCoroutine != null)
            {
                StopCoroutine(pendingResponseCoroutine);
                pendingResponseCoroutine = null;
                isResponding = false;
                Debug.Log($"[AutomatedTester] Human speech detected (\"{text}\"). Canceled automated response timer.");
            }
        }

        private void HandleInterviewerTranscript(CharacterTranscriptReceived e)
        {
            // Only log streaming transcript chunks; DO NOT schedule responses while interviewer is still speaking!
            if (!testActive || !e.Message.IsFinal) return;
            string text = e.Message.Text ?? "";
            string speaker = e.Message.DisplayName ?? "Interviewer";
            if (!string.IsNullOrWhiteSpace(text))
            {
                Debug.Log($"[AutomatedTester] Streamed chunk [{speaker}]: \"{text}\"");
            }
        }

        /// <summary>
        /// Authoritative callback fired ONLY after the interviewer finishes speaking the FULL question.
        /// </summary>
        private void HandleInterviewerQuestionFinished(string speaker, string fullQuestion)
        {
            if (!testActive || isResponding) return;

            var orchestrator = InterviewOrchestrator.Instance;
            if (orchestrator == null || orchestrator.IsTransitioningStage) return;

            Debug.Log($"[AutomatedTester] Full Question Received from [{speaker}]: \"{fullQuestion}\"");

            // Ignore stage handoff / concluding statements
            if (fullQuestion.IndexOf("pass you on", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullQuestion.IndexOf("hand you over", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullQuestion.IndexOf("concludes our interview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullQuestion.IndexOf("conclude our interview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullQuestion.IndexOf("evaluation team will review", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.Log("[AutomatedTester] Detected stage handoff phrase. Awaiting next stage.");
                return;
            }

            string candidateAnswer = null;

            CandidateProfile profile = PREPVRSessionManager.Instance?.Profile;
            string knownName = profile?.name;
            string candGreeting = !string.IsNullOrWhiteSpace(knownName) && knownName != "Swetha N" ? knownName : "Candidate";
            string primaryProj = profile?.projects != null && profile.projects.Count > 0 ? profile.projects[0] : "Software Engineering System Architecture";
            string secondaryProj = profile?.projects != null && profile.projects.Count > 1 ? profile.projects[1] : primaryProj;
            string skillsStr = profile?.skills != null && profile.skills.Count > 0 
                ? string.Join(", ", profile.skills.GetRange(0, Math.Min(4, profile.skills.Count))) 
                : "Python, C#, backend services, databases";
            string eduStr = !string.IsNullOrWhiteSpace(profile?.education) ? profile.education : "Computer Science and Engineering";

            if (orchestrator.CurrentStage == HRInterviewStage.HRInterviewer ||
                (speaker.IndexOf("HR", StringComparison.OrdinalIgnoreCase) >= 0 && orchestrator.CurrentStage == HRInterviewStage.NotStarted))
            {
                hrTurn++;
                if (hrTurn == 1)
                {
                    candidateAnswer = $"Thank you for having me today. My name is {candGreeting}. I completed my degree in {eduStr}. My core technical skills include {skillsStr}, and I've built projects including {primaryProj} and {secondaryProj}.";
                }
                else if (hrTurn == 2)
                {
                    candidateAnswer = $"In my academic and internship work, I have focused on building scalable services, containerizing applications with Docker, and collaborating via Git version control in agile sprints.";
                }
                else
                {
                    candidateAnswer = "I am passionate about software engineering and solving real-world problems with robust architectures, and I am excited about contributing to high-impact technical products.";
                }
            }
            else if (orchestrator.CurrentStage == HRInterviewStage.BehavioralInterviewer)
            {
                // Stage 2: Technical Interviewer
                technicalTurn++;
                if (technicalTurn == 1)
                {
                    candidateAnswer = $"I developed the {primaryProj} project. In this project, I used {skillsStr} to design and build the core architecture, data pipelines, and service interfaces.";
                }
                else if (technicalTurn == 2)
                {
                    candidateAnswer = $"A key architectural decision in {primaryProj} was structuring the service boundaries and database schemas to ensure low latency and high availability under load.";
                }
                else if (technicalTurn == 3)
                {
                    candidateAnswer = $"A major engineering challenge in {primaryProj} was handling edge cases and external integration failures. I implemented asynchronous task handling, retry backoffs, and structured logging to maintain high system reliability.";
                }
                else
                {
                    candidateAnswer = $"Through {primaryProj}, I learned the importance of modular system design, comprehensive unit testing, and graceful failure recovery in production systems.";
                }
            }
            else if (orchestrator.CurrentStage == HRInterviewStage.HiringManager)
            {
                hiringMgrTurn++;
                if (hiringMgrTurn == 1)
                {
                    candidateAnswer = $"When driving {primaryProj}, I focused on end-to-end ownership, organized daily standups, coordinated code reviews on GitHub, and resolved blockers to keep our delivery timeline on track.";
                }
                else if (hiringMgrTurn == 2)
                {
                    candidateAnswer = "When requirements change unexpectedly, I first clarify the core priorities with stakeholders, assess the technical trade-offs, and communicate transparently with the team.";
                }
                else
                {
                    candidateAnswer = "My long-term ambition is to grow as an engineering leader who designs resilient technical systems while mentoring team members and maintaining high engineering excellence.";
                }
            }

            if (!string.IsNullOrEmpty(candidateAnswer))
            {
                if (pendingResponseCoroutine != null)
                {
                    StopCoroutine(pendingResponseCoroutine);
                }
                pendingResponseCoroutine = StartCoroutine(DeliverCandidateResponse(candidateAnswer));
            }
        }

        private IEnumerator DeliverCandidateResponse(string answer)
        {
            try
            {
                Debug.Log($"[AutomatedTester] Candidate response scheduled in {autoResponseTimeoutSeconds}s: \"{answer}\"");
                yield return new WaitForSeconds(autoResponseTimeoutSeconds);

                // Ensure candidate didn't speak verbally in the meantime
                if (isResponding) yield break;

                // Ensure interviewer has not started another turn
                var orchestrator = InterviewOrchestrator.Instance;
                if (orchestrator != null && orchestrator.IsInterviewerSpeakingTurn)
                {
                    Debug.Log("[AutomatedTester] Interviewer currently speaking; deferring response.");
                    yield break;
                }

                isResponding = true;
                if (player == null)
                    player = FindAnyObjectByType<ConvaiPlayer>();

                if (player != null)
                {
                    lastDeliveredAnswer = answer;
                    string reason;
                    bool sent = player.TrySendTextMessage(answer, out reason);
                    if (sent)
                    {
                        Debug.Log($"[AutomatedTester] Candidate verbally responded: \"{answer}\"");
                    }
                    else
                    {
                        Debug.LogWarning($"[AutomatedTester] Candidate response refused: {reason}");
                    }
                }
                else
                {
                    Debug.LogError("[AutomatedTester] ConvaiPlayer not available to send candidate response.");
                }

                yield return new WaitForSeconds(1.5f);
            }
            finally
            {
                isResponding = false;
                pendingResponseCoroutine = null;
            }
        }
    }
}
