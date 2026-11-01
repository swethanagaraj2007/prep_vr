using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convai.Runtime.Components;
using Convai.Domain.DomainEvents.Transcript;

namespace PrepVR.Interview
{
    /// <summary>
    /// Automated test runner to test and verify the Single Interviewer 10-Minute System in Play Mode.
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
        private int turnCount = 0;

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
                Debug.Log("[AutomatedTester] Subscribed to InterviewOrchestrator.OnInterviewerQuestionFinished.");
            }
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.Events.OnCharacterTranscriptReceived -= HandleInterviewerTranscript;
                manager.Events.OnPlayerTranscriptReceived    -= HandlePlayerTranscript;
            }

            if (InterviewOrchestrator.Instance != null)
            {
                InterviewOrchestrator.Instance.OnInterviewerQuestionFinished -= HandleInterviewerQuestionFinished;
            }
        }

        private void HandlePlayerTranscript(PlayerTranscriptReceived e)
        {
            if (!testActive) return;

            string text = e.Message.Text ?? "";
            if (string.IsNullOrWhiteSpace(text)) return;

            if (text.IndexOf("begin Stage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("The candidate explained:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("The candidate answered:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Please begin the interview", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            // If candidate spoke verbally via mic, cancel automated response
            if (!string.IsNullOrEmpty(lastDeliveredAnswer) && text.Trim() != lastDeliveredAnswer.Trim())
            {
                Debug.Log($"[AutomatedTester] Candidate spoke verbally via mic (\"{text}\"). Cancelling automated test response.");
                CancelPendingResponse();
            }
        }

        private void HandleInterviewerTranscript(CharacterTranscriptReceived e)
        {
            if (!testActive) return;
            string text = e.Message.Text ?? "";
            string speaker = e.Message.DisplayName ?? "Interviewer";
            if (string.IsNullOrWhiteSpace(text)) return;

            // If interviewer started speaking again while an automated response was scheduled, cancel it
            if (pendingResponseCoroutine != null && !isResponding)
            {
                Debug.Log($"[AutomatedTester] Interviewer speech detected while response scheduled. Resetting schedule.");
                CancelPendingResponse();
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

            // Ignore closing statements
            if (fullQuestion.IndexOf("concludes our interview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullQuestion.IndexOf("conclude our interview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullQuestion.IndexOf("feedback will be available shortly", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullQuestion.IndexOf("Have a wonderful day", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.Log("[AutomatedTester] Detected closing statement. Interview concluding.");
                return;
            }

            string candidateAnswer = null;

            CandidateProfile profile = PREPVRSessionManager.Instance?.Profile;
            string knownName = profile?.name;
            string candGreeting = !string.IsNullOrWhiteSpace(knownName) && knownName != "Candidate" ? knownName : "Candidate";
            string primaryProj = profile?.projects != null && profile.projects.Count > 0 ? profile.projects[0] : "Software Engineering Architecture";
            string secondaryProj = profile?.projects != null && profile.projects.Count > 1 ? profile.projects[1] : primaryProj;
            string skillsStr = profile?.skills != null && profile.skills.Count > 0 
                ? string.Join(", ", profile.skills.GetRange(0, Math.Min(4, profile.skills.Count))) 
                : "Python, C#, backend services, databases";
            string eduStr = !string.IsNullOrWhiteSpace(profile?.education) ? profile.education : "Computer Science and Engineering";

            turnCount++;

            if (orchestrator.CurrentState == InterviewState.FinalQuestion)
            {
                candidateAnswer = "I don't have anything further to add, but I want to thank you for the wonderful conversation today. It was great discussing my experience, technical projects, and background with you.";
            }
            else
            {
                switch (orchestrator.CurrentSection)
                {
                    case InterviewSection.Welcome_Introduction:
                        candidateAnswer = $"Thank you for having me today. My name is {candGreeting}. I completed my degree in {eduStr}. My core technical skills include {skillsStr}, and I've built projects including {primaryProj} and {secondaryProj}.";
                        break;

                    case InterviewSection.Background_Education_HR:
                        candidateAnswer = "During my academic and technical training, I focused on building scalable backend services, agile team collaboration with Git, and continuous integration, which developed my passion for solving real-world engineering challenges.";
                        break;

                    case InterviewSection.Resume_Technical_Skills:
                        if (turnCount % 2 == 1)
                        {
                            candidateAnswer = $"In {primaryProj}, I designed the core system architecture using {skillsStr}. I structured clean service boundaries, established database schemas, and exposed RESTful APIs to maintain modularity and high performance.";
                        }
                        else
                        {
                            candidateAnswer = $"For data persistence and communication in {primaryProj}, I implemented indexing, connection pooling, and error retries to ensure reliable throughput under concurrent workloads.";
                        }
                        break;

                    case InterviewSection.Projects_Problem_Solving:
                        if (turnCount % 2 == 1)
                        {
                            candidateAnswer = $"One of the most complex technical challenges in {primaryProj} was diagnosing unexpected latency during peak request volumes. I profiled database queries, identified unindexed lookups, and added an in-memory caching layer that reduced response latency by 60%.";
                        }
                        else
                        {
                            candidateAnswer = $"My primary personal contribution to {primaryProj} was architecting the end-to-end data pipeline, writing comprehensive automated integration tests, and setting up Docker containerization for reliable deployments.";
                        }
                        break;

                    case InterviewSection.Behavioural_Situational:
                    default:
                        if (turnCount % 2 == 1)
                        {
                            candidateAnswer = "In our team project, when priorities shifted close to a deadline, I organized a quick sync with team members to re-scope non-critical features, reassigned tasks based on individual strengths, and ensured our core deliverables met high quality standards.";
                        }
                        else
                        {
                            candidateAnswer = "When a teammate and I had differing opinions on technical architecture, I scheduled a design review, documented the trade-offs of both approaches with benchmark data, and we aligned collaboratively on the best solution for the product.";
                        }
                        break;
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

                if (isResponding) yield break;

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
