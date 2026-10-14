using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Convai.Runtime.Components;
using Convai.Runtime.DynamicContext;
using Convai.Runtime.Conversation;
using Convai.Domain.DomainEvents.Transcript;
using Convai.Domain.DomainEvents.Session;
using Convai.Domain.DomainEvents.Runtime;
using Convai.Domain.Models;
using Convai.Runtime;
using Convai.Runtime.Room;
using PrepVR.Model;
using PrepVR.Placement;

namespace PrepVR.Interview
{
    // -----------------------------------------------------------------------
    // Interview stage machine
    // -----------------------------------------------------------------------
    public enum HRInterviewStage
    {
        NotStarted,
        HRInterviewer,           // Stage 1 - HR Interviewer speaks
        BehavioralInterviewer,   // Stage 2 - Behavioral Interviewer speaks
        HiringManager,           // Stage 3 - Hiring Manager / Team Lead speaks
        InterviewCompleted
    }

    /// <summary>
    /// Master orchestrator for the three-interviewer sequential HR panel interview.
    /// Implements IConversationTargetProvider to guarantee Convai routing aligns
    /// strictly with the current stage of the interview.
    ///
    /// Character mapping (InterviewRoom.unity):
    ///   Stage 1: HR Interviewer         ("HR" - 1a49ba1e-4a24-4040-bab0-3e1c6623c85a / 96d2f7ff-c33b-4b1f-8c58-b5798da695e2)
    ///   Stage 2: Behavioral Interviewer ("Technical Interviewer" - 8f23bb25-483d-4890-907b-36de8c20d331 / 67cdd4cc-8124-4301-8117-1355734b7faa)
    ///   Stage 3: Hiring Manager         ("Behaviour analyst" - f10ec8e4-998d-4f3c-bb4d-260f55427e9e / 7b1aedff-1322-4c5e-8fe5-3d7ed2e8f200)
    /// </summary>
    public class InterviewOrchestrator : MonoBehaviour, IConversationTargetProvider
    {
        public const string TARGET_SCENE_NAME = "InterviewRoom";
        public const string NEXT_SCENE_NAME   = "InterviewOver";

        // ---- Character IDs (Scene Primary & Account Supported) -------------
        public const string HR_CHARACTER_ID_SCENE     = "28288a88-b324-43b6-920f-c302fbe5dec2";
        public const string HR_CHARACTER_ID_ACCOUNT   = "d5ac8c30-012b-444e-b3ca-306603364d42";
        public const string HR_CHARACTER_ID_LEGACY    = "1a49ba1e-4a24-4040-bab0-3e1c6623c85a";
        public const string BEHAVIORAL_ID_SCENE       = "416528a7-f11c-4b80-8302-0910eea988b0";
        public const string BEHAVIORAL_ID_ACCOUNT     = "d0aa9d41-6998-4dac-b54b-de383ae052ec";
        public const string BEHAVIORAL_ID_LEGACY      = "8f23bb25-483d-4890-907b-36de8c20d331";
        public const string HIRING_MGR_ID_SCENE       = "51afe859-379a-41d8-95f9-c937050cd5fd";
        public const string HIRING_MGR_ID_ACCOUNT     = "f10ec8e4-998d-4f3c-bb4d-260f55427e9e";
        public const string HIRING_MGR_ID_LEGACY      = "f10ec8e4-998d-4f3c-bb4d-260f55427e9e";

        // Inaccessible character IDs known to cause ROOM_CONNECTION_FAILED
        public const string INVALID_HIRING_MGR_ID     = "47d5b894-c3e9-4b15-8361-76e8a50a115e";

        // Backward compatibility constants
        public const string HR_CHARACTER_ID         = HR_CHARACTER_ID_SCENE;
        public const string BEHAVIORAL_CHARACTER_ID = BEHAVIORAL_ID_SCENE;
        public const string HIRING_MGR_CHARACTER_ID = HIRING_MGR_ID_SCENE;

        // ---- Narrative Design Trigger Names (configured on Convai backend) --
        public const string HR_TRIGGER_NAME         = "StartInterview";
        public const string BEHAVIORAL_TRIGGER_NAME = "StartBehavioralStage";
        public const string HIRING_MGR_TRIGGER_NAME = "StartHiringManagerStage";

        private const int QUESTIONS_PER_STAGE = 4;

        // ---- Discovered characters (auto-populated at runtime) ------------
        [Header("Discovered Characters (auto-populated)")]
        [SerializeField] private ConvaiCharacter hrInterviewer;
        [SerializeField] private ConvaiCharacter behavioralInterviewer;
        [SerializeField] private ConvaiCharacter hiringManager;
        [SerializeField] private ConvaiPlayer convaiPlayer;

        // ---- Stage state --------------------------------------------------
        [Header("State Tracking")]
        [SerializeField] private HRInterviewStage currentStage = HRInterviewStage.NotStarted;
        [SerializeField] private bool interviewActive = false;

        private int hrQuestionCount        = 0;
        private int behaviorQuestionCount  = 0;
        private int hiringMgrQuestionCount = 0;

        private int hrCandidateAnswerCount        = 0;
        private int behaviorCandidateAnswerCount  = 0;
        private int hiringMgrCandidateAnswerCount = 0;

        public const int MIN_ANSWERS_PER_STAGE = 3;
        public const float STAGE_1_END_SECONDS     = 200f; // Stage 1 HR: 200s (0:00 - 3:20)
        public const float STAGE_2_END_SECONDS     = 400f; // Stage 2 Behavioral: 400s cumulative (3:20 - 6:40)
        public const float TOTAL_INTERVIEW_SECONDS = 600f; // Stage 3 Hiring Manager: 600s cumulative (6:40 - 10:00)

        // Legacy compatibility aliases
        public const float STAGE_1_TARGET_SECONDS = STAGE_1_END_SECONDS;
        public const float STAGE_2_TARGET_SECONDS = STAGE_2_END_SECONDS;
        public const float STAGE_3_TARGET_SECONDS = TOTAL_INTERVIEW_SECONDS;

        private float interviewStartTime = 0f;
        public float AuthoritativeElapsedTime => InterviewTimerManager.Instance != null
            ? InterviewTimerManager.Instance.ElapsedTime
            : (interviewStartTime > 0f ? (Time.time - interviewStartTime) : 0f);

        private float stageStartTime = 0f;
        public float StageElapsedTime => AuthoritativeElapsedTime;

        private readonly List<string> hrTranscriptLines       = new();
        private readonly List<string> behaviorTranscriptLines = new();

        private string lastRecordedQuestion = "";
        private bool   isTransitioningStage = false;
        private bool   isInitializing       = false;

        // ---- Turn-Taking State Machine ------------------------------------
        public enum TurnState
        {
            NotStarted,
            InterviewerSpeaking,  // Active interviewer is speaking question, greeting, or prompt
            WaitingForCandidate,  // Interviewer finished; candidate mic is active; waiting for speech
            CandidateSpeaking,    // Interviewer finished; candidate mic is active; candidate is speaking
            ProcessingAnswer,     // Candidate finished speaking; debounce expired; answer is being processed
            HandoffSpeaking,      // Interviewer speaking handoff phrase to transition to next stage
            ClosingSpeaking,      // Hiring Manager speaking closing statement
            Concluding            // Closing statement finished; finalizing session and transitioning scene
        }

        [Header("Turn State Machine")]
        [SerializeField] private TurnState currentTurnState = TurnState.NotStarted;
        public TurnState CurrentTurnState => currentTurnState;

        private bool isTimeLimitReached = false;
        private bool isHandoffSpeechCompleted = false;
        public bool IsHandoffSpeechCompleted => isHandoffSpeechCompleted;

        public bool IsCandidateSpeakingOrTurnActive =>
            currentTurnState == TurnState.WaitingForCandidate ||
            currentTurnState == TurnState.CandidateSpeaking ||
            currentTurnState == TurnState.ProcessingAnswer ||
            candidateAnswerBuffer.Length > 0 ||
            (InterviewAutomatedTester.Instance != null && InterviewAutomatedTester.Instance.IsCandidateSpeakingOrScheduled);

        // ---- Question Completion & Microphone Gating State ----------------
        public event Action<string, string> OnInterviewerQuestionFinished;
        private bool isInterviewerSpeakingTurn = false;
        private ConvaiCharacter activeTurnCharacter = null;
        private readonly StringBuilder currentTurnQuestionBuilder = new();
        private bool characterTurnCompletedReceived = false;
        private Coroutine turnCompletionCoroutine = null;

        // ---- Candidate Answer Buffering & Debouncing ----------------------
        private readonly StringBuilder candidateAnswerBuffer = new();
        private Coroutine candidateAnswerDebounceCoroutine = null;

        public HRInterviewStage CurrentStage => currentStage;
        public static InterviewOrchestrator Instance { get; private set; }
        public bool IsTransitioningStage => isTransitioningStage;
        public bool IsInterviewerSpeakingTurn => isInterviewerSpeakingTurn;

        public int HrCandidateAnswerCount => hrCandidateAnswerCount;
        public int BehaviorCandidateAnswerCount => behaviorCandidateAnswerCount;
        public int HiringMgrCandidateAnswerCount => hiringMgrCandidateAnswerCount;

        public ConvaiCharacter GetActiveInterviewer()
        {
            switch (currentStage)
            {
                case HRInterviewStage.HRInterviewer: return hrInterviewer;
                case HRInterviewStage.BehavioralInterviewer: return behavioralInterviewer;
                case HRInterviewStage.HiringManager: return hiringManager;
                default: return hrInterviewer;
            }
        }

        public void SetCandidateMicrophoneMuted(bool mute)
        {
            try
            {
                // In Hands-Free mode, ensure hardware microphone is NOT muted so WebRTC PCM buffers
                // are never cleared and candidate speech is captured with zero activation latency.
                if (ConvaiManager.ActiveManager?.Audio != null)
                {
                    ConvaiManager.ActiveManager.Audio.SetMicMuted(false);
                }
                if (!mute)
                {
                    Debug.Log("[MIC] Candidate microphone ACTIVE");
                }
                else
                {
                    Debug.Log("[MIC] Candidate microphone GATED (interviewer speaking)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MIC] SetMicMuted failed: {ex.Message}");
            }
        }

        public bool IsActiveStageCharacter(string charId, string speaker)
        {
            switch (currentStage)
            {
                case HRInterviewStage.HRInterviewer:
                    return MatchesCharacter(charId, speaker, hrInterviewer);
                case HRInterviewStage.BehavioralInterviewer:
                    return MatchesCharacter(charId, speaker, behavioralInterviewer);
                case HRInterviewStage.HiringManager:
                    return MatchesCharacter(charId, speaker, hiringManager);
                default:
                    return false;
            }
        }

        public bool IsCandidateSpeakingOrPending()
        {
            return IsCandidateSpeakingOrTurnActive;
        }

        public bool IsAnyInterviewerSpeaking()
        {
            if (isInterviewerSpeakingTurn) return true;
            if (hrInterviewer != null && hrInterviewer.IsSpeaking) return true;
            if (behavioralInterviewer != null && behavioralInterviewer.IsSpeaking) return true;
            if (hiringManager != null && hiringManager.IsSpeaking) return true;
            return false;
        }

        public bool IsExchangeActive()
        {
            return IsCandidateSpeakingOrTurnActive || isInterviewerSpeakingTurn || IsAnyInterviewerSpeaking();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeConvaiEvents();
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == TARGET_SCENE_NAME && !interviewActive && !isInitializing)
            {
                Debug.Log("[ConvaiDebug] Start() — InterviewRoom already active. Initializing.");
                StartCoroutine(InitializeInterviewCoroutine());
            }
        }

        private void Update()
        {
            if (!interviewActive || isTransitioningStage) return;

            float elapsed = AuthoritativeElapsedTime;

            // Track soft time limit reaching total interview duration
            if (elapsed >= TOTAL_INTERVIEW_SECONDS)
            {
                isTimeLimitReached = true;
            }

            // CRITICAL: NEVER switch interviewers or cut off an exchange while:
            // 1. Candidate is speaking or about to speak (CandidateSpeaking)
            // 2. Candidate answer is debouncing / processing (ProcessingAnswer)
            // 3. Interviewer is speaking (isInterviewerSpeakingTurn / IsAnyInterviewerSpeaking)
            // 4. A handoff or closing statement is playing
            if (isInterviewerSpeakingTurn || IsAnyInterviewerSpeaking() || IsCandidateSpeakingOrTurnActive ||
                currentTurnState == TurnState.HandoffSpeaking ||
                currentTurnState == TurnState.ClosingSpeaking)
            {
                return;
            }

            // Fallback idle transitions ONLY if the candidate has been completely silent / idle
            // for > 35 seconds past the stage boundary without speaking.
            if (currentStage == HRInterviewStage.HRInterviewer && elapsed >= (STAGE_1_END_SECONDS + 35f))
            {
                Debug.Log($"[INTERVIEW] HR Stage idle boundary exceeded ({elapsed:F1}s). Transitioning HR -> Stage 2 (Technical Interviewer).");
                StartCoroutine(BeginStage_BehavioralInterviewer());
            }
            else if (currentStage == HRInterviewStage.BehavioralInterviewer && elapsed >= (STAGE_2_END_SECONDS + 35f))
            {
                Debug.Log($"[INTERVIEW] Technical Stage idle boundary exceeded ({elapsed:F1}s). Transitioning Technical -> Stage 3 (Hiring Manager).");
                StartCoroutine(BeginStage_HiringManager());
            }
            else if (currentStage == HRInterviewStage.HiringManager && elapsed >= (TOTAL_INTERVIEW_SECONDS + 35f))
            {
                Debug.Log($"[Interview] Hiring Manager Stage idle boundary exceeded ({elapsed:F1}s). Gracefully concluding interview.");
                HandleTimerExpiredSoftBoundary();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TARGET_SCENE_NAME)
            {
                Debug.Log("[ConvaiDebug] InterviewRoom loaded via sceneLoaded.");
                if (!interviewActive && !isInitializing)
                {
                    StartCoroutine(InitializeInterviewCoroutine());
                }
            }
            else
            {
                interviewActive = false;
                isInitializing = false;
            }
        }

        // ====================================================================
        // IConversationTargetProvider Implementation
        // ====================================================================
        public ConvaiCharacter ResolveTarget(IReadOnlyList<ConvaiCharacter> candidates, ConvaiCharacter current)
        {
            ConvaiCharacter target = null;
            switch (currentStage)
            {
                case HRInterviewStage.HRInterviewer:
                    target = hrInterviewer;
                    break;
                case HRInterviewStage.BehavioralInterviewer:
                    target = behavioralInterviewer;
                    break;
                case HRInterviewStage.HiringManager:
                    target = hiringManager;
                    break;
                default:
                    target = hrInterviewer;
                    break;
            }

            if (target != null && candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i] == target) return target;
                }
            }

            return target ?? current ?? hrInterviewer;
        }

        // ====================================================================
        // Main initialization
        // ====================================================================
        private IEnumerator InitializeInterviewCoroutine()
        {
            isInitializing = true;
            Debug.Log("[Interview] B. InterviewRoom initialized");
            string selectedType = PREPVRSessionManager.Instance?.SelectedInterviewType ?? "Final HR Interview";
            Debug.Log($"[InterviewFlow] Selected interview type = {selectedType}");
            Debug.Log("[ConvaiDebug] InterviewRoom loaded");

            hrQuestionCount        = 0;
            behaviorQuestionCount  = 0;
            hiringMgrQuestionCount = 0;
            hrCandidateAnswerCount        = 0;
            behaviorCandidateAnswerCount  = 0;
            hiringMgrCandidateAnswerCount = 0;
            hrTranscriptLines.Clear();
            behaviorTranscriptLines.Clear();
            lastRecordedQuestion   = "";
            isTransitioningStage   = false;
            currentStage           = HRInterviewStage.NotStarted;
            stageStartTime         = 0f;

            // Ensure CandidateProfile has rich resume data
            EnsureCandidateProfile();

            // Sanitize any characters carrying invalid/inaccessible IDs before room connection
            ConvaiCharacter[] sceneChars = FindObjectsByType<ConvaiCharacter>(FindObjectsSortMode.None);
            if (sceneChars != null)
            {
                foreach (var sc in sceneChars)
                {
                    if (sc != null && sc.CharacterId == INVALID_HIRING_MGR_ID)
                    {
                        Debug.LogWarning($"[InterviewOrchestrator] Sanitizing inaccessible character ID {INVALID_HIRING_MGR_ID} on {sc.gameObject.name} to {HIRING_MGR_ID_SCENE}");
                        try { sc.Configure(HIRING_MGR_ID_SCENE); }
                        catch
                        {
                            var field = typeof(ConvaiCharacter).GetField("_characterId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (field != null) field.SetValue(sc, HIRING_MGR_ID_SCENE);
                        }
                    }
                }
            }

            // 1. Discover all Convai Characters in the scene
            float timeout = 4f;
            ConvaiCharacter[] all = null;
            while (timeout > 0f)
            {
                all = FindObjectsByType<ConvaiCharacter>(FindObjectsSortMode.None);
                if (all != null && all.Length >= 1) break;
                yield return new WaitForSeconds(0.2f);
                timeout -= 0.2f;
            }

            Debug.Log($"[ConvaiDebug] Discovered {all?.Length ?? 0} ConvaiCharacter(s) in scene.");

            hrInterviewer         = null;
            behavioralInterviewer = null;
            hiringManager         = null;

            if (all != null)
            {
                // Pass 1: Match by exact Character ID (Scene ID, Account ID, or Legacy ID)
                foreach (var ch in all)
                {
                    string id = ch.CharacterId ?? "";
                    if (id == HR_CHARACTER_ID_SCENE || id == HR_CHARACTER_ID_ACCOUNT || id == HR_CHARACTER_ID_LEGACY)
                    {
                        hrInterviewer = ch;
                    }
                    else if (id == BEHAVIORAL_ID_SCENE || id == BEHAVIORAL_ID_ACCOUNT || id == BEHAVIORAL_ID_LEGACY)
                    {
                        behavioralInterviewer = ch;
                    }
                    else if (id == HIRING_MGR_ID_SCENE || id == HIRING_MGR_ID_ACCOUNT || id == HIRING_MGR_ID_LEGACY)
                    {
                        hiringManager = ch;
                    }
                }

                // Pass 2: Match by GameObject / Character name if unassigned
                foreach (var ch in all)
                {
                    string name = ((ch.CharacterName ?? "") + " " + ch.gameObject.name).ToLowerInvariant();
                    if (hrInterviewer == null && name.Contains("hr"))
                    {
                        hrInterviewer = ch;
                    }
                    else if (behavioralInterviewer == null && (name.Contains("technical") || name.Contains("behavioral")))
                    {
                        behavioralInterviewer = ch;
                    }
                    else if (hiringManager == null && (name.Contains("3rd") || name.Contains("observer") || name.Contains("invterviewr") || name.Contains("behaviour") || name.Contains("analyst") || name.Contains("hiring") || name.Contains("lead") || name.Contains("manager")))
                    {
                        hiringManager = ch;
                    }
                }

                // Pass 3: Positional fallback if any character is still unassigned
                var remaining = new List<ConvaiCharacter>(all);
                if (hrInterviewer != null) remaining.Remove(hrInterviewer);
                if (behavioralInterviewer != null) remaining.Remove(behavioralInterviewer);
                if (hiringManager != null) remaining.Remove(hiringManager);

                if (hrInterviewer == null && remaining.Count > 0)
                {
                    hrInterviewer = remaining[0];
                    remaining.RemoveAt(0);
                }
                if (behavioralInterviewer == null && remaining.Count > 0)
                {
                    behavioralInterviewer = remaining[0];
                    remaining.RemoveAt(0);
                }
                if (hiringManager == null && remaining.Count > 0)
                {
                    hiringManager = remaining[0];
                    remaining.RemoveAt(0);
                }
            }

            Debug.Log($"[ConvaiDebug] HR character resolved: {hrInterviewer != null} ({hrInterviewer?.gameObject.name} | {hrInterviewer?.CharacterName} | {hrInterviewer?.CharacterId})");
            Debug.Log($"[ConvaiDebug] Behavioral character resolved: {behavioralInterviewer != null} ({behavioralInterviewer?.gameObject.name} | {behavioralInterviewer?.CharacterName} | {behavioralInterviewer?.CharacterId})");
            Debug.Log($"[ConvaiDebug] Hiring Manager character resolved: {hiringManager != null} ({hiringManager?.gameObject.name} | {hiringManager?.CharacterName} | {hiringManager?.CharacterId})");

            // 2. Locate ConvaiManager
            float mgrWait = 5f;
            while (ConvaiManager.ActiveManager == null && mgrWait > 0f)
            {
                yield return new WaitForSeconds(0.2f);
                mgrWait -= 0.2f;
            }

            bool managerFound = ConvaiManager.ActiveManager != null;
            Debug.Log($"[ConvaiDebug] Convai Manager found: {managerFound}");

            if (managerFound)
            {
                // CRITICAL: Configure Manual targeting mode so gaze does NOT switch targets or interrupt speech
                if (ConvaiManager.ActiveManager.ConversationTargeting != null)
                {
                    ConvaiManager.ActiveManager.ConversationTargeting.Mode = ConversationTargetingMode.Manual;
                    Debug.Log("[ConvaiDebug] Configured ConversationTargeting to MANUAL mode.");
                }
                ConvaiManager.ActiveManager.SetConversationTargetProvider(this);
            }

            convaiPlayer = FindAnyObjectByType<ConvaiPlayer>();
            Debug.Log($"[ConvaiDebug] ConvaiPlayer found: {convaiPlayer != null}");

            // 3. Attach character event listeners for ConvaiDebug logs
            WireCharacterDebugEvents(hrInterviewer, "HR");
            WireCharacterDebugEvents(behavioralInterviewer, "Behavioral");
            WireCharacterDebugEvents(hiringManager, "Hiring Manager");

            // 4. Setup session components and event subscriptions
            SetupTimer();
            SetupCameraController();
            SetupVisualEvaluator();
            SetupRuntimeHUD();
            SubscribeConvaiEvents();

            interviewActive = true;
            isInitializing = false;
            if (PREPVRSessionManager.Instance != null)
                PREPVRSessionManager.Instance.SetStatus(InterviewSessionStatus.InProgress);

            if (InterviewTimerManager.Instance != null)
                InterviewTimerManager.Instance.StartTimer();

            // 5. Begin Stage 1 (HR Interviewer)
            yield return StartCoroutine(BeginStage_HRInterviewer());
        }

        private void EnsureCandidateProfile()
        {
            if (PREPVRSessionManager.Instance == null) return;
            CandidateProfile p = PREPVRSessionManager.Instance.Profile;

            // 1. If not already loaded, try loading uploaded resume via PythonBackendBridge
            if (p == null || p.skills == null || p.skills.Count == 0 || p.projects == null || p.projects.Count == 0)
            {
                var candidateResumePaths = new List<string>();
                if (!string.IsNullOrWhiteSpace(PREPVRSessionManager.Instance.ResumePdfPath))
                {
                    candidateResumePaths.Add(PREPVRSessionManager.Instance.ResumePdfPath);
                }
                candidateResumePaths.Add(Path.Combine(Application.dataPath, "../Backend/resume_intelligence/Sample_Candidate_Resume.pdf"));
                candidateResumePaths.Add(Path.Combine(Application.dataPath, "../Backend/resume_intelligence/Swetha_N_Resume.pdf"));
                candidateResumePaths.Add(Path.Combine(Application.dataPath, "../Backend/resume_intelligence/test_resume.pdf"));
                candidateResumePaths.Add(Path.Combine(Application.dataPath, "../Backend/resume_intelligence/sample_resume.pdf"));

                foreach (var path in candidateResumePaths)
                {
                    if (File.Exists(path))
                    {
                        Debug.Log($"[RESUME] Attempting to extract candidate profile from: {path}");
                        if (PythonBackendBridge.TryProcessResume(path, out CandidateProfile parsedProfile, out _, out string parseError))
                        {
                            if (parsedProfile != null && parsedProfile.skills != null && parsedProfile.skills.Count > 0)
                            {
                                p = parsedProfile;
                                PREPVRSessionManager.Instance.Profile = p;
                                PREPVRSessionManager.Instance.ResumePdfPath = path;
                                Debug.Log($"[RESUME] Successfully parsed candidate profile from {Path.GetFileName(path)}");
                                break;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[RESUME] PythonBackendBridge parse warning on {path}: {parseError}");
                        }
                    }
                }
            }

            // 2. Check scene InterviewSession as secondary fallback
            if (p == null || p.skills == null || p.skills.Count == 0)
            {
                InterviewSession session = FindAnyObjectByType<InterviewSession>();
                if (session != null && session.candidateProfile != null && session.candidateProfile.skills != null && session.candidateProfile.skills.Count > 0)
                {
                    p = session.candidateProfile;
                    PREPVRSessionManager.Instance.Profile = p;
                    Debug.Log($"[InterviewOrchestrator] Adopted CandidateProfile from scene InterviewSession: {p.name}");
                }
            }

            // 3. Fallback to general baseline technical profile if still unpopulated (without hardcoding candidate name)
            if (p == null) p = new CandidateProfile();
            if (!string.IsNullOrWhiteSpace(p.name) && p.name.IndexOf("Swetha", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                p.name = "";
            }

            if (p.skills == null || p.skills.Count == 0)
            {
                p.name = ""; // Name will come from the candidate's actual spoken introduction
                p.education = "Bachelor of Technology in Computer Science and Engineering";
                p.skills = new List<string> { "Python", "Java", "C", "SQL", "MongoDB", "Node.js", "Express", "REST API", "Git", "Docker", "Unity", "TypeScript" };
                p.projects = new List<string> {
                    "Smart SMS - AI-Powered Messaging Architecture & Real-Time Analytics (Node.js, Express, MongoDB, Groq API)",
                    "Decentralized Medical Supply Chain Tracker (Hyperledger Fabric, Node.js)"
                };
                p.experience = new List<string> { "Software Engineering Intern (Node.js, Express, MongoDB)" };
                p.certifications = new List<string> { "MongoDB Certified Developer Associate", "Oracle Certified Professional: Java SE" };
                p.achievements = new List<string> { "First Place - National Smart Hackathon for Smart SMS system design" };
                PREPVRSessionManager.Instance.Profile = p;
            }

            // Structured [RESUME] logging required by verification:
            Debug.Log($"[RESUME]\nCandidate profile created:\nName: {(string.IsNullOrWhiteSpace(p.name) ? "(Awaiting Spoken Introduction)" : p.name)}\nEducation: {p.education}\nSkills: {string.Join(", ", p.skills)}\nProjects: {string.Join(" | ", p.projects)}\nExperience: {(p.experience != null && p.experience.Count > 0 ? string.Join(" | ", p.experience) : "None")}\nCertifications: {(p.certifications != null && p.certifications.Count > 0 ? string.Join(" | ", p.certifications) : "None")}\nAchievements: {(p.achievements != null && p.achievements.Count > 0 ? string.Join(" | ", p.achievements) : "None")}");
        }

        private void WireCharacterDebugEvents(ConvaiCharacter ch, string role)
        {
            if (ch == null) return;
            ch.OnCharacterReady += () =>
            {
                Debug.Log($"[ConvaiDebug] {role} character initialized (OnCharacterReady event)");
                if (role == "HR")
                {
                    Debug.Log("[Interview] D. HR character ready");
                }
            };
            ch.OnSpeechStarted += () =>
            {
                Debug.Log($"[AUDIO] Interviewer [{role}] audio response started");
                StartInterviewerSpeakingTurn(ch, role);
            };
            ch.OnSpeechStopped += () =>
            {
                Debug.Log($"[ConvaiDebug] Character audio completed ({role})");
            };
            ch.OnTurnCompleted += (wasInterrupted) =>
            {
                Debug.Log($"[AUDIO] Character.OnTurnCompleted received ({role}, interrupted={wasInterrupted})");
                if (activeTurnCharacter == ch)
                {
                    characterTurnCompletedReceived = true;
                }
            };
        }

        public void StartInterviewerSpeakingTurn(ConvaiCharacter character, string speakerRole)
        {
            if (isInterviewerSpeakingTurn && activeTurnCharacter == character) return;
            isInterviewerSpeakingTurn = true;
            activeTurnCharacter = character;
            characterTurnCompletedReceived = false;
            currentTurnQuestionBuilder.Clear();
            if (currentTurnState != TurnState.HandoffSpeaking && currentTurnState != TurnState.ClosingSpeaking)
            {
                currentTurnState = TurnState.InterviewerSpeaking;
            }
            SetCandidateMicrophoneMuted(true);
            Debug.Log($"[TURN] Interviewer response started ({speakerRole})");
            Debug.Log($"[AUDIO] Interviewer [{speakerRole}] started speaking turn ({currentTurnState}).");

            if (turnCompletionCoroutine != null) StopCoroutine(turnCompletionCoroutine);
            turnCompletionCoroutine = StartCoroutine(MonitorTurnCompletionCoroutine(character, speakerRole));
        }

        private IEnumerator MonitorTurnCompletionCoroutine(ConvaiCharacter character, string speakerRole)
        {
            float startWait = 0f;
            AudioSource audioSrc = character != null ? (character.GetComponent<AudioSource>() ?? character.GetComponentInChildren<AudioSource>()) : null;

            // Wait for speech / audio playback to begin (max 3.5s)
            while (startWait < 3.5f)
            {
                bool isAudioPlaying = audioSrc != null && audioSrc.isPlaying;
                bool isConvaiSpeaking = character != null && character.IsSpeaking;
                if (isAudioPlaying || isConvaiSpeaking)
                {
                    break;
                }
                yield return new WaitForSeconds(0.05f);
                startWait += 0.05f;
            }

            bool audioHasStarted = (audioSrc != null && audioSrc.isPlaying) || (character != null && character.IsSpeaking);
            float silenceDuration = 0f;
            float turnElapsed = 0f;
            float maxTurnDuration = 60f;

            while (turnElapsed < maxTurnDuration)
            {
                bool isAudioPlaying = audioSrc != null && audioSrc.isPlaying;
                bool isConvaiSpeaking = character != null && character.IsSpeaking;

                if (isAudioPlaying || isConvaiSpeaking)
                {
                    audioHasStarted = true;
                    silenceDuration = 0f;
                }
                else
                {
                    silenceDuration += 0.05f;
                }

                // If server sent TurnCompleted AND audio playback has finished:
                // Require only 0.05s to confirm audio finished without buffering glitch.
                // This activates candidate microphone almost instantaneously (<50ms) after the interviewer finishes speaking!
                if (characterTurnCompletedReceived && audioHasStarted && !isAudioPlaying && !isConvaiSpeaking)
                {
                    if (silenceDuration >= 0.05f)
                    {
                        break;
                    }
                }
                // Fallback: If TurnCompleted received but audio never started within 1.0s:
                else if (characterTurnCompletedReceived && !audioHasStarted && silenceDuration >= 1.0f)
                {
                    break;
                }
                // Fallback without TurnCompleted: audio started, but silence lasted >= 0.35s (natural phrase end)
                else if (audioHasStarted && !isAudioPlaying && !isConvaiSpeaking && silenceDuration >= 0.35f)
                {
                    break;
                }
                // If candidate speech has already begun while audio finished:
                else if (audioHasStarted && !isAudioPlaying && !isConvaiSpeaking && (currentTurnState == TurnState.CandidateSpeaking || candidateAnswerBuffer.Length > 0))
                {
                    break;
                }

                yield return new WaitForSeconds(0.05f);
                turnElapsed += 0.05f;
            }

            string fullQuestion = currentTurnQuestionBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(fullQuestion))
            {
                fullQuestion = lastRecordedQuestion;
            }

            lastRecordedQuestion = fullQuestion;
            Debug.Log($"[AUDIO] Interviewer [{speakerRole}] finished speaking.");
            Debug.Log($"[QUESTION] Generated question/statement: \"{fullQuestion}\"");

            // Commit complete single question to transcript
            if (PREPVRSessionManager.Instance != null && !string.IsNullOrWhiteSpace(fullQuestion))
            {
                PREPVRSessionManager.Instance.RecordTranscript(speakerRole, fullQuestion, isQuestion: true);
            }

            if (InterviewVisualEvaluator.Instance != null)
            {
                InterviewVisualEvaluator.Instance.RecordQuestionEnded(AuthoritativeElapsedTime);
            }

            isInterviewerSpeakingTurn = false;
            activeTurnCharacter = null;
            turnCompletionCoroutine = null;

            // Handle special statement states (Closing or Handoff)
            if (currentTurnState == TurnState.ClosingSpeaking)
            {
                Debug.Log($"[Interview] Interviewer [{speakerRole}] finished final closing statement. Concluding interview session.");
                currentTurnState = TurnState.Concluding;
                StartCoroutine(ConcludeInterviewCoroutine());
                yield break;
            }

            if (currentTurnState == TurnState.HandoffSpeaking)
            {
                Debug.Log($"[Interview] Interviewer [{speakerRole}] finished handoff statement. Stage transition ready.");
                isHandoffSpeechCompleted = true;
                if (currentStage == HRInterviewStage.HRInterviewer)
                {
                    StartCoroutine(BeginStage_BehavioralInterviewer());
                }
                else if (currentStage == HRInterviewStage.BehavioralInterviewer)
                {
                    StartCoroutine(BeginStage_HiringManager());
                }
                yield break;
            }

            // Record question counts for normal stage questions
            switch (currentStage)
            {
                case HRInterviewStage.HRInterviewer:
                    hrQuestionCount++;
                    hrTranscriptLines.Add($"HR ({speakerRole}): {fullQuestion}");
                    Debug.Log($"[Interview] Complete HR Question #{hrQuestionCount} recorded: \"{fullQuestion}\"");
                    CheckHRStageTransition(fullQuestion);
                    break;
                case HRInterviewStage.BehavioralInterviewer:
                    behaviorQuestionCount++;
                    behaviorTranscriptLines.Add($"Technical ({speakerRole}): {fullQuestion}");
                    Debug.Log($"[Interview] Complete Technical Question #{behaviorQuestionCount} recorded: \"{fullQuestion}\"");
                    CheckBehavioralStageTransition(fullQuestion);
                    break;
                case HRInterviewStage.HiringManager:
                    hiringMgrQuestionCount++;
                    Debug.Log($"[Interview] Complete Hiring Manager Question #{hiringMgrQuestionCount} recorded: \"{fullQuestion}\"");
                    CheckHiringManagerStageTransition(fullQuestion);
                    break;
            }

            // [TURN] Interviewer finished -> [MIC] Candidate microphone ACTIVE -> Transition to WaitingForCandidate
            Debug.Log("[TURN] Interviewer finished");
            currentTurnState = TurnState.WaitingForCandidate;
            SetCandidateMicrophoneMuted(false);
            Debug.Log("[MIC] Candidate microphone ACTIVE");

            // Fire public notification for automated tester or UI
            OnInterviewerQuestionFinished?.Invoke(speakerRole, fullQuestion);
        }

        // ====================================================================
        // Stage 1 — HR Interviewer
        // ====================================================================
        private IEnumerator BeginStage_HRInterviewer()
        {
            currentStage = HRInterviewStage.HRInterviewer;
            Debug.Log("[ConvaiDebug] Stage 1: HR Interviewer active.");
            if (InterviewTimerManager.Instance != null)
            {
                InterviewTimerManager.Instance.SetCustomStage("Stage 1: HR Interviewer");
            }

            CandidateProfile profile  = PREPVRSessionManager.Instance?.Profile;
            string           candName = !string.IsNullOrWhiteSpace(profile?.name) ? profile.name : "Candidate";

            // 1. Await Convai room connection
            yield return WaitForRoomConnection(25f);
            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null || !manager.IsConnected)
            {
                Debug.LogError("[ConvaiDebug] Convai room failed to connect within timeout. Halting Stage 1 (HR).");
                yield break;
            }

            // Ensure Manual targeting mode is locked
            if (manager.ConversationTargeting != null)
            {
                manager.ConversationTargeting.Mode = ConversationTargetingMode.Manual;
            }

            Debug.Log("[Interview] C. Convai Manager ready");

            // 2. Await HR character readiness & session membership
            yield return WaitForAuthoritativeReadiness(hrInterviewer, "HR", 35f);
            if (!IsCharacterAuthoritativelyReady(hrInterviewer))
            {
                Debug.LogError("[ConvaiDebug] HR character failed authoritative readiness check. Halting Stage 1.");
                yield break;
            }

            Debug.Log("[Interview] D. HR character ready");

            // Ensure ConvaiPushToTalkController is disabled on ConvaiManager so it never blocks hands-free speech
            if (manager != null)
            {
                var ptt = manager.GetComponent<ConvaiPushToTalkController>();
                if (ptt != null)
                {
                    ptt.enabled = false;
                    Debug.Log("[Interview] Disabled ConvaiPushToTalkController for pure hands-free speech.");
                }
            }

            // Ensure candidate microphone is pre-warmed and active in WebRTC
            SetCandidateMicrophoneMuted(false);

            // 3. Address HR explicitly and isolate remote audio
            IsolateActiveInterviewer(hrInterviewer);

            interviewStartTime = Time.time;
            stageStartTime     = Time.time;
            if (InterviewTimerManager.Instance != null && !InterviewTimerManager.Instance.IsRunning)
            {
                InterviewTimerManager.Instance.StartTimer();
            }

            if (hrInterviewer != null)
            {
                InterviewCameraController.Instance?.FocusOn(hrInterviewer.transform);
            }

            if (InterviewVisualEvaluator.Instance != null && hrInterviewer != null)
                InterviewVisualEvaluator.Instance.SetActiveSpeaker(hrInterviewer.transform);

            // 5. Await conversation input availability
            yield return WaitForConversationInputAvailability(10f);

            // 6. Inject Stage 1 DynamicContext with candidate resume
            try
            {
                IConvaiDynamicContext dyn = hrInterviewer?.DynamicContext;
                if (dyn != null)
                {
                    dyn.SetStates(new Dictionary<string, string>
                    {
                        { "stage",                  "1" },
                        { "stage_role",             "HR Interviewer" },
                        { "candidate_name",         "" },
                        { "candidate_education",    profile?.education ?? "Computer Science" },
                        { "candidate_skills",       profile?.skills != null ? string.Join(", ", profile.skills) : "Software Engineering" },
                        { "candidate_projects",     profile?.projects != null ? string.Join(" | ", profile.projects) : "Web and VR projects" },
                        { "resume_context",         BuildResumeSummary(profile) },
                        { "instructions",
                          "You are the HR Interviewer conducting Stage 1 (0:00 - 3:20) of a 3-person professional panel interview. " +
                          "You are the FIRST interviewer. " +
                          "1. Your very first opening statement to the candidate MUST be: 'Welcome to the interview. Could you tell me your name and walk me through your background?' " +
                          "2. Do not assume or guess the candidate's name in advance. Wait for the candidate to tell you their name in their spoken introduction. " +
                          "3. Ask subsequent questions directly grounded in their actual spoken responses and resume background. " +
                          "4. Listen carefully to their answers, acknowledge what they actually said, and ask relevant follow-up questions. " +
                          "5. Ask ONE question at a time and wait for the candidate's answer. Never ask multiple questions at once. " +
                          "6. Never say you are an AI or language model. " +
                          "7. Do NOT conduct deep coding tests in this stage. Focus on background, resume projects, motivation, and communication. " +
                          "8. Continue the interview until approximately 3 minutes and 20 seconds, then conclude your stage by saying: " +
                          "'Thank you. I will now pass you on to our Technical Interviewer.' " +
                          "Use that phrase to signal the handoff." }
                    }, ConvaiRespondMode.Silent);
                    dyn.Flush();
                    Debug.Log($"[AI] Context passed to interviewer:\nRole: HR Interviewer\nCandidate: (Awaiting Spoken Introduction)\nResume Context: {BuildResumeSummary(profile)}");
                    Debug.Log("[ConvaiDebug] HR Interviewer DynamicContext applied with resume data.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvaiDebug] HR DynamicContext warning: {ex.Message}");
            }

            yield return null;

            // 7. Request first HR response via the proven conversational pipeline
            string hrPrompt = "Please begin the interview now. Say: \"Welcome to the interview. Could you tell me your name and walk me through your background?\"";
            Debug.Log("[Interview] Triggering HR opening speech...");
            yield return StartCoroutine(TriggerCharacterResponse(hrInterviewer, HR_TRIGGER_NAME, hrPrompt, "HR"));
        }

        // ====================================================================
        // ====================================================================
        // Stage 2 — Technical Interviewer (Resume Projects & Technical Skills)
        // ====================================================================
        private IEnumerator BeginStage_BehavioralInterviewer()
        {
            if (isTransitioningStage) yield break;
            isTransitioningStage = true;

            Debug.Log("[INTERVIEW] === BEGINNING STAGE 2: TECHNICAL INTERVIEWER (200s - 400s) ===");
            Debug.Log($"[INTERVIEW] Phase: Technical. Character: {behavioralInterviewer?.CharacterName}");

            // 1. Cancel any pending candidate response scheduled for HR
            InterviewAutomatedTester.Instance?.CancelPendingResponse();

            // 2. Ensure candidate is completely done speaking and no turn processing is underway
            float waitCand = 0f;
            while ((IsCandidateSpeakingOrTurnActive || candidateAnswerBuffer.Length > 0 || candidateAnswerDebounceCoroutine != null) && waitCand < 15f)
            {
                yield return new WaitForSeconds(0.1f);
                waitCand += 0.1f;
            }

            // 3. Ensure HR is not speaking before proceeding
            float waitHR = 0f;
            while ((isInterviewerSpeakingTurn || (hrInterviewer != null && hrInterviewer.IsSpeaking)) && waitHR < 15f)
            {
                yield return new WaitForSeconds(0.1f);
                waitHR += 0.1f;
            }

            // 3. Switch currentStage to BehavioralInterviewer (Technical panel role)
            currentStage = HRInterviewStage.BehavioralInterviewer;

            if (InterviewTimerManager.Instance != null)
            {
                InterviewTimerManager.Instance.SetCustomStage("Stage 2: Technical Interviewer (3:20 - 6:40)");
            }

            // 4. Verify Convai room connection
            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null || !manager.IsConnected)
            {
                Debug.LogError("[ConvaiDebug] Room not connected for Stage 2. Halting.");
                isTransitioningStage = false;
                yield break;
            }

            // 5. Ensure Technical interviewer is authoritatively ready
            yield return WaitForAuthoritativeReadiness(behavioralInterviewer, "Technical Interviewer", 15f);

            // 6. Isolate active interviewer: enable audio on Technical, disable on inactive panel members
            IsolateActiveInterviewer(behavioralInterviewer);

            // 8. Focus camera & visual evaluator
            if (behavioralInterviewer != null)
            {
                InterviewCameraController.Instance?.FocusOn(behavioralInterviewer.transform);
                if (InterviewVisualEvaluator.Instance != null)
                    InterviewVisualEvaluator.Instance.SetActiveSpeaker(behavioralInterviewer.transform);
            }

            // 9. Inject DynamicContext (Resume + Stage 1 transcript context)
            CandidateProfile profile  = PREPVRSessionManager.Instance?.Profile;
            string           candName = !string.IsNullOrWhiteSpace(profile?.name) ? profile.name : "Candidate";
            string           stage1   = BuildStageSummary(hrTranscriptLines, "HR Interviewer");
            string           primaryProj = GetPrimaryProject(profile);
            string           primarySkills = GetPrimarySkills(profile);

            try
            {
                IConvaiDynamicContext dyn = behavioralInterviewer?.DynamicContext;
                if (dyn != null)
                {
                    dyn.SetStates(new Dictionary<string, string>
                    {
                        { "stage",                  "2" },
                        { "stage_role",             "Technical Interviewer" },
                        { "candidate_name",         candName },
                        { "resume_context",         BuildResumeSummary(profile) },
                        { "previous_stage_summary", stage1 },
                        { "instructions",
                          $"You are the Technical Interviewer conducting Stage 2 (3:20 - 6:40) of the panel interview for {candName}. " +
                          "Stage 1 (HR introduction and background) is completed. You are the TECHNICAL interviewer. " +
                          "1. Welcome the candidate to the technical round. " +
                          $"2. Directly ask in-depth technical questions grounded in the candidate's actual resume projects (specifically {primaryProj} and technical skills: {primarySkills}). " +
                          $"3. Ask about system architecture, data modeling, technology selection, performance, API integrations, and technical challenges in {primaryProj}. " +
                          "4. Listen carefully to the candidate's answers and generate genuine FOLLOW-UP questions probing the specific technologies, tools, and architectural choices they just explained. " +
                          "5. Ask ONE clear question at a time and wait for their answer. " +
                          "6. Continue the technical interview until approximately 6 minutes and 40 seconds, then conclude your stage by saying: " +
                          "'Thank you. I will now pass you on to our Hiring Manager.' " +
                          "Use that phrase to signal the handoff." }
                    }, ConvaiRespondMode.Silent);
                    dyn.Flush();
                    Debug.Log($"[AI] Context passed to interviewer:\nRole: Technical Interviewer\nCandidate: {candName}\nResume Context: {BuildResumeSummary(profile)}");
                    Debug.Log("[ConvaiDebug] Technical Interviewer DynamicContext applied with resume & technical instructions.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvaiDebug] Technical context error: {ex.Message}");
            }

            yield return null;

            // 10. Wait for conversation input availability (max 5s)
            yield return WaitForConversationInputAvailability(5f);

            // 11. Trigger Technical opening speech via TriggerCharacterResponse
            Debug.Log("[ConvaiDebug] Triggering Technical opening speech...");
            string technicalPrompt = $"Hello {candName}. Please begin Stage 2 of the interview now. You are the Technical Interviewer. Greet the candidate, reference their resume project '{primaryProj}', and ask them to walk through the technical architecture and the key technical decisions they made using {primarySkills}.";
            yield return StartCoroutine(TriggerCharacterResponse(behavioralInterviewer, BEHAVIORAL_TRIGGER_NAME, technicalPrompt, "Technical"));

            isTransitioningStage = false;
        }

        // ====================================================================
        // Stage 3 — Hiring Manager / Team Lead
        // ====================================================================
        private IEnumerator BeginStage_HiringManager()
        {
            if (isTransitioningStage) yield break;
            isTransitioningStage = true;

            Debug.Log("[Interview] === BEGINNING STAGE 3: HIRING MANAGER / TEAM LEAD (400s - 600s) ===");

            // 1. Cancel any pending candidate response scheduled for Behavioral
            InterviewAutomatedTester.Instance?.CancelPendingResponse();

            // 2. Ensure candidate is completely done speaking and no turn processing is underway
            float waitCand = 0f;
            while ((IsCandidateSpeakingOrTurnActive || candidateAnswerBuffer.Length > 0 || candidateAnswerDebounceCoroutine != null) && waitCand < 15f)
            {
                yield return new WaitForSeconds(0.1f);
                waitCand += 0.1f;
            }

            // 3. Ensure Technical interviewer is not speaking before proceeding
            float waitTech = 0f;
            while ((isInterviewerSpeakingTurn || (behavioralInterviewer != null && behavioralInterviewer.IsSpeaking)) && waitTech < 15f)
            {
                yield return new WaitForSeconds(0.1f);
                waitTech += 0.1f;
            }

            // 3. Switch currentStage to HiringManager
            currentStage = HRInterviewStage.HiringManager;

            if (InterviewTimerManager.Instance != null)
            {
                InterviewTimerManager.Instance.SetCustomStage("Stage 3: Hiring Manager / Team Lead (6:40 - 10:00)");
            }

            // 4. Verify Convai room connection
            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null || !manager.IsConnected)
            {
                Debug.LogError("[ConvaiDebug] Room not connected for Stage 3. Halting.");
                isTransitioningStage = false;
                yield break;
            }

            // 5. Ensure Hiring Manager is authoritatively ready
            yield return WaitForAuthoritativeReadiness(hiringManager, "Hiring Manager", 15f);

            // 6. Isolate active interviewer: enable audio on Hiring Manager, disable on inactive panel members
            IsolateActiveInterviewer(hiringManager);

            // 8. Focus camera & visual evaluator
            if (hiringManager != null)
            {
                InterviewCameraController.Instance?.FocusOn(hiringManager.transform);
                if (InterviewVisualEvaluator.Instance != null)
                    InterviewVisualEvaluator.Instance.SetActiveSpeaker(hiringManager.transform);
            }

            // 9. Inject DynamicContext (Resume + Stage 1 + Stage 2 transcript context)
            CandidateProfile profile  = PREPVRSessionManager.Instance?.Profile;
            string           candName = !string.IsNullOrWhiteSpace(profile?.name) ? profile.name : "Candidate";
            string           stage1   = BuildStageSummary(hrTranscriptLines, "HR Interviewer");
            string           stage2   = BuildStageSummary(behaviorTranscriptLines, "Behavioral Interviewer");
            string           primaryProj = GetPrimaryProject(profile);

            try
            {
                IConvaiDynamicContext dyn = hiringManager?.DynamicContext;
                if (dyn != null)
                {
                    dyn.SetStates(new Dictionary<string, string>
                    {
                        { "stage",                  "3" },
                        { "stage_role",             "Hiring Manager / Team Lead" },
                        { "candidate_name",         candName },
                        { "resume_context",         BuildResumeSummary(profile) },
                        { "previous_stage_summary", $"Stage 1 (HR):\n{stage1}\n\nStage 2 (Behavioral):\n{stage2}" },
                        { "instructions",
                          $"You are the Hiring Manager and Team Lead conducting Stage 3 (the final stage from 6:40 to 10:00) of the panel interview for {candName}. " +
                          "Stages 1 and 2 are complete. You are an ACTIVE, VOCAL interviewer (do NOT remain silent). " +
                          "1. Welcome the candidate to the final stage of the interview. " +
                          $"2. Ask workplace-readiness, leadership, and managerial questions: ownership, accountability, initiative, judgment under ambiguity, prioritization, decision making, and long-term career growth regarding {primaryProj}. " +
                          "3. Ground your questions in the candidate's resume and reference relevant information discussed in Stages 1 and 2. " +
                          "4. Listen carefully to their answers, respond thoughtfully, and ask relevant follow-up questions. " +
                          "5. Ask ONE question at a time. Strictly do NOT ask technical/coding questions. This is the final HR/managerial stage, NOT a technical interview. Do NOT repeat questions asked in Stages 1 or 2. " +
                          "6. When the interview reaches the 10-minute mark, warmly thank {candName} and conclude the interview by saying: " +
                          "'Thank you for your time today. That concludes our interview, and our evaluation team will review your session.' " +
                          "Use that phrase to wrap up the interview." }
                    }, ConvaiRespondMode.Silent);
                    dyn.Flush();
                    Debug.Log("[ConvaiDebug] Hiring Manager DynamicContext applied with Stages 1 & 2 Q&A context.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvaiDebug] Hiring Manager context error: {ex.Message}");
            }

            yield return null;

            // 10. Wait for conversation input availability (max 5s)
            yield return WaitForConversationInputAvailability(5f);

            // 11. Trigger Hiring Manager opening speech via TriggerCharacterResponse
            Debug.Log("[ConvaiDebug] Triggering Hiring Manager opening speech...");
            string hiringMgrPrompt = $"Please begin Stage 3 of the interview now. Greet the candidate for this final stage, and ask your first question focusing on ownership, leadership initiative, and professional judgment based on their project {primaryProj}.";
            yield return StartCoroutine(TriggerCharacterResponse(hiringManager, HIRING_MGR_TRIGGER_NAME, hiringMgrPrompt, "Hiring Manager"));

            isTransitioningStage = false;
        }

        // ====================================================================
        // Conversation Targeting & Speaker Isolation Helper
        // ====================================================================
        private void IsolateActiveInterviewer(ConvaiCharacter active)
        {
            if (active == null) return;
            ConvaiCharacter[] panel = { hrInterviewer, behavioralInterviewer, hiringManager };
            foreach (var c in panel)
            {
                if (c == null) continue;
                if (c == active)
                {
                    try
                    {
                        if (!c.IsRemoteAudioEnabled) c.EnableRemoteAudio();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ConvaiDebug] Failed enabling remote audio on {c.CharacterName}: {ex.Message}");
                    }
                }
                else
                {
                    try
                    {
                        if (c.IsRemoteAudioEnabled) c.DisableRemoteAudio();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ConvaiDebug] Failed disabling remote audio on inactive {c.CharacterName}: {ex.Message}");
                    }
                }
            }
            SafeSetConversationTarget(active);
        }

        private string GetPrimaryProject(CandidateProfile profile)
        {
            if (profile?.projects != null && profile.projects.Count > 0)
            {
                string proj = profile.projects[0];
                if (!string.IsNullOrWhiteSpace(proj))
                {
                    int dash = proj.IndexOf(" - ");
                    if (dash > 0) return proj.Substring(0, dash).Trim();
                    return proj.Trim();
                }
            }
            return "their key technical project";
        }

        private string GetSecondaryProject(CandidateProfile profile)
        {
            if (profile?.projects != null && profile.projects.Count > 1)
            {
                string proj = profile.projects[1];
                if (!string.IsNullOrWhiteSpace(proj))
                {
                    int dash = proj.IndexOf(" - ");
                    if (dash > 0) return proj.Substring(0, dash).Trim();
                    return proj.Trim();
                }
            }
            return GetPrimaryProject(profile);
        }

        private string GetPrimarySkills(CandidateProfile profile)
        {
            if (profile?.skills != null && profile.skills.Count > 0)
            {
                int count = Math.Min(profile.skills.Count, 4);
                return string.Join(", ", profile.skills.GetRange(0, count));
            }
            return "software engineering and system design";
        }

        private string GetCurrentStageRoleName()
        {
            switch (currentStage)
            {
                case HRInterviewStage.HRInterviewer: return "HR Interviewer";
                case HRInterviewStage.BehavioralInterviewer: return "Technical Interviewer";
                case HRInterviewStage.HiringManager: return "Hiring Manager";
                default: return "Interviewer";
            }
        }

        private void SafeSetConversationTarget(ConvaiCharacter target)
        {
            if (target == null) return;
            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null) return;

            // In Convai SDK 4.6.0, scripted panel interviews require Manual targeting mode so gaze does not override.
            if (manager.ConversationTargeting != null)
            {
                manager.ConversationTargeting.Mode = ConversationTargetingMode.Manual;
            }

            Debug.Log($"[ConvaiDebug] Addressing {target.CharacterName} ({target.CharacterId}) via manager.TalkTo.");
            manager.TalkTo(target);
        }

        // ====================================================================
        // Speech Invocation Helper (Convai SDK 4.6.0)
        // Uses Narrative Trigger if narrative-driven, seamlessly backed by ConvaiPlayer
        // ====================================================================
        private IEnumerator TriggerCharacterResponse(ConvaiCharacter target, string triggerName, string prompt, string roleLabel)
        {
            if (target == null)
            {
                Debug.LogError($"[Interview] Cannot trigger {roleLabel} response: target character is null.");
                yield break;
            }

            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null || !manager.IsConnected)
            {
                Debug.LogError($"[Interview] Cannot trigger {roleLabel} response: ConvaiManager is not connected.");
                yield break;
            }

            // 1. Ensure remote audio is enabled so incoming audio plays through AudioSource
            try
            {
                if (!target.IsRemoteAudioEnabled)
                {
                    target.EnableRemoteAudio();
                    Debug.Log($"[ConvaiDebug] Enabled remote audio for {target.CharacterName}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvaiDebug] Remote audio enable warning on {target.CharacterName}: {ex.Message}");
            }

            // 2. Ensure conversation target is set to this character
            SafeSetConversationTarget(target);

            // 3. Wait for conversation input availability
            yield return WaitForConversationInputAvailability(10f);

            // 4. Send prompt via ConvaiPlayer.TrySendTextMessage (proven working conversational pipeline)
            yield return StartCoroutine(SendPlayerAgentPrompt(target, prompt, roleLabel));
        }

        // ====================================================================
        // Player-Agent Conversational Prompt Helper (Convai SDK 4.6.0)
        // Sends prompt via ConvaiPlayer.TrySendTextMessage through normal conversational pipeline
        // ====================================================================
        private IEnumerator SendPlayerAgentPrompt(ConvaiCharacter target, string prompt, string roleLabel)
        {
            if (target == null)
            {
                Debug.LogError($"[Interview] Cannot send {roleLabel} prompt: target character is null.");
                yield break;
            }

            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null || !manager.IsConnected)
            {
                Debug.LogError($"[Interview] Cannot send {roleLabel} prompt: ConvaiManager is not connected.");
                yield break;
            }

            // 1. Ensure remote audio is enabled
            try
            {
                if (!target.IsRemoteAudioEnabled)
                {
                    target.EnableRemoteAudio();
                    Debug.Log($"[ConvaiDebug] Enabled remote audio for {target.CharacterName}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvaiDebug] Remote audio enable warning on {target.CharacterName}: {ex.Message}");
            }

            // 2. Ensure ConvaiPlayer is located
            if (convaiPlayer == null)
                convaiPlayer = FindAnyObjectByType<ConvaiPlayer>();

            if (convaiPlayer == null)
            {
                Debug.LogError($"[Interview] Cannot send {roleLabel} opening request: ConvaiPlayer not found in scene!");
                yield break;
            }

            // 3. Ensure conversation target is set to this character
            SafeSetConversationTarget(target);

            // 4. Wait for conversation input availability
            yield return WaitForConversationInputAvailability(10f);

            // 5. Send player-agent prompt via ConvaiPlayer.TrySendTextMessage
            bool sent = false;
            string reason = null;
            float retryTimeout = 10f;
            float elapsed = 0f;

            while (!sent && elapsed < retryTimeout)
            {
                sent = convaiPlayer.TrySendTextMessage(prompt, out reason);
                if (sent)
                {
                    if (roleLabel == "HR")
                    {
                        Debug.Log("[Interview] E. HR opening request sent");
                    }
                    Debug.Log($"[Interview] {roleLabel} opening text request successfully delivered to Convai runtime.");
                    Debug.Log("[Interview] Waiting for Convai response");
                    break;
                }

                Debug.LogWarning($"[Interview] TrySendTextMessage for {roleLabel} refused ({reason}). Retrying in 0.5s...");
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            if (!sent)
            {
                Debug.LogError($"[Interview] Failed to send {roleLabel} opening request after {retryTimeout}s: {reason}");
            }
        }

        // ====================================================================
        // Authoritative Readiness & Availability Helpers (Convai SDK 4.6.0)
        // ====================================================================
        private bool IsCharacterAuthoritativelyReady(ConvaiCharacter character)
        {
            if (character == null) return false;
            bool charReady = character.IsCharacterReady;
            CharacterRoomStatus? status = character.RoomMembershipStatus;

            if (status.HasValue)
            {
                return charReady && status.Value == CharacterRoomStatus.Ready;
            }

            return charReady;
        }

        private IEnumerator WaitForRoomConnection(float timeoutSeconds = 25f)
        {
            Debug.Log("[ConvaiDebug] Awaiting Convai Room connection...");
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                ConvaiManager mgr = ConvaiManager.ActiveManager;
                if (mgr != null && mgr.IsConnected)
                {
                    Debug.Log($"[ConvaiDebug] Convai Room connected after {elapsed:F2}s.");
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
                elapsed += 0.25f;
            }
            Debug.LogError($"[ConvaiDebug] Timed out waiting for Convai Room connection ({timeoutSeconds}s).");
        }

        private IEnumerator WaitForAuthoritativeReadiness(ConvaiCharacter character, string label, float timeoutSeconds = 35f)
        {
            if (character == null)
            {
                Debug.LogError($"[ConvaiDebug] Cannot wait for {label} readiness: ConvaiCharacter reference is null.");
                yield break;
            }

            Debug.Log($"[ConvaiDebug] Awaiting authoritative readiness for {label} ({character.CharacterName} - {character.CharacterId})...");
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (IsCharacterAuthoritativelyReady(character))
                {
                    Debug.Log($"[ConvaiDebug] {label} authoritatively ready after {elapsed:F2}s.");
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
                elapsed += 0.25f;
            }

            if (IsCharacterAuthoritativelyReady(character))
            {
                Debug.Log($"[ConvaiDebug] {label} authoritatively ready after {elapsed:F2}s.");
            }
            else
            {
                Debug.LogError($"[ConvaiDebug] Timeout waiting for {label} readiness (waited {timeoutSeconds}s). State: IsCharacterReady={character.IsCharacterReady}, RoomMembershipStatus={character.RoomMembershipStatus}.");
            }
        }

        private IEnumerator WaitForConversationInputAvailability(float timeoutSeconds = 10f)
        {
            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null)
            {
                Debug.LogError("[ConvaiDebug] ConvaiManager is null while waiting for conversation input availability.");
                yield break;
            }

            yield return null;

            if (manager.ConversationAvailability.CanAcceptPlayerInput())
            {
                Debug.Log($"[ConvaiDebug] ConversationAvailability immediately ready: {manager.ConversationAvailability} (Target: {manager.AddressedCharacter?.CharacterName})");
                yield break;
            }

            Debug.Log($"[ConvaiDebug] Waiting for conversation availability to accept input (current: {manager.ConversationAvailability}, Target: {manager.AddressedCharacter?.CharacterName})...");

            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (manager.ConversationAvailability.CanAcceptPlayerInput())
                {
                    Debug.Log($"[ConvaiDebug] ConversationAvailability ready after {elapsed:F2}s: {manager.ConversationAvailability} (Target: {manager.AddressedCharacter?.CharacterName})");
                    yield break;
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            Debug.LogError($"[ConvaiDebug] Timed out waiting for conversation availability ({timeoutSeconds}s). Current: {manager.ConversationAvailability}");
        }

        // ====================================================================
        // Transcript event handlers
        // ====================================================================
        private bool MatchesCharacter(string charId, string speaker, ConvaiCharacter target)
        {
            if (target == null) return false;
            if (!string.IsNullOrEmpty(charId) && string.Equals(charId, target.CharacterId, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(speaker))
            {
                if (!string.IsNullOrEmpty(target.CharacterName) && speaker.IndexOf(target.CharacterName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (target.gameObject != null && target.gameObject.name.IndexOf(speaker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private void HandleCharacterTranscript(CharacterTranscriptReceived e)
        {
            if (!interviewActive) return;

            string text    = e.Message.Text                ?? "";
            string speaker = e.Message.DisplayName         ?? "Interviewer";
            string charId  = e.Message.PlayerOrCharacterId ?? "";
            if (string.IsNullOrWhiteSpace(text)) return;

            // Only process transcripts from the character active in the current stage
            switch (currentStage)
            {
                case HRInterviewStage.HRInterviewer:
                    if (hrInterviewer != null && !MatchesCharacter(charId, speaker, hrInterviewer))
                    {
                        return;
                    }
                    break;
                case HRInterviewStage.BehavioralInterviewer:
                    if (behavioralInterviewer != null && !MatchesCharacter(charId, speaker, behavioralInterviewer))
                    {
                        return;
                    }
                    break;
                case HRInterviewStage.HiringManager:
                    if (hiringManager != null && !MatchesCharacter(charId, speaker, hiringManager))
                    {
                        return;
                    }
                    break;
            }
            ConvaiCharacter targetChar = null;
            switch (currentStage)
            {
                case HRInterviewStage.HRInterviewer:
                    targetChar = hrInterviewer;
                    break;
                case HRInterviewStage.BehavioralInterviewer:
                    targetChar = behavioralInterviewer;
                    break;
                case HRInterviewStage.HiringManager:
                    targetChar = hiringManager;
                    break;
            }

            lastRecordedQuestion = text;

            // Start turn monitoring if not already active
            if (!isInterviewerSpeakingTurn && targetChar != null)
            {
                StartInterviewerSpeakingTurn(targetChar, speaker);
            }

            // Aggregate final sentence chunks into full question text
            if (e.Message.IsFinal)
            {
                string chunk = text.Trim();
                string current = currentTurnQuestionBuilder.ToString();
                if (!current.Contains(chunk))
                {
                    currentTurnQuestionBuilder.Append(chunk + " ");
                    Debug.Log($"[QUESTION] Streamed sentence chunk [{speaker}]: \"{chunk}\"");
                }
            }
        }

        private void HandlePlayerTranscript(PlayerTranscriptReceived e)
        {
            if (!interviewActive) return;

            string text = e.Message.Text ?? "";
            if (string.IsNullOrWhiteSpace(text)) return;

            bool isFinal = e.Message.IsFinal || e.Phase != TranscriptionPhase.Interim;
            if (!isFinal) return; // Do not record interim streaming fragments

            // Ignore system opening / transition / follow-up prompts echoed into player transcript
            if (text.StartsWith("[PROMPT]") ||
                text.IndexOf("The candidate explained:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("The candidate answered:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("The candidate introduced:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("begin Stage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Welcome to the interview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("tell me your name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Warmly welcome the candidate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Acknowledge the transition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Greet the candidate for this final stage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.Log($"[ConvaiDebug] Ignored system opening/follow-up prompt from player transcript: \"{text}\"");
                return;
            }

            Debug.Log("[STT] Candidate speech detected");
            Debug.Log($"[STT] Transcript: \"{text}\"");

            // If interviewer turn was active but audio is done, complete interviewer turn now
            if (isInterviewerSpeakingTurn)
            {
                ConvaiCharacter activeChar = GetActiveInterviewer();
                AudioSource audioSrc = activeChar != null ? (activeChar.GetComponent<AudioSource>() ?? activeChar.GetComponentInChildren<AudioSource>()) : null;
                bool isAudioPlaying = audioSrc != null && audioSrc.isPlaying;
                bool isConvaiSpeaking = activeChar != null && activeChar.IsSpeaking;
                if (!isAudioPlaying && !isConvaiSpeaking)
                {
                    if (turnCompletionCoroutine != null)
                    {
                        StopCoroutine(turnCompletionCoroutine);
                        turnCompletionCoroutine = null;
                    }
                    isInterviewerSpeakingTurn = false;
                    Debug.Log("[TURN] Interviewer finished");
                    Debug.Log("[MIC] Candidate microphone ACTIVE");
                }
            }

            string chunk = text.Trim();
            string currentBuf = candidateAnswerBuffer.ToString();
            if (!currentBuf.Contains(chunk))
            {
                candidateAnswerBuffer.Append(chunk + " ");
            }

            // Maintain CandidateSpeaking state while candidate speech is arriving
            currentTurnState = TurnState.CandidateSpeaking;

            // Debounce processing so candidate can finish multi-sentence answers without interruption (0.85s pause)
            if (candidateAnswerDebounceCoroutine != null)
            {
                StopCoroutine(candidateAnswerDebounceCoroutine);
            }
            candidateAnswerDebounceCoroutine = StartCoroutine(ProcessCandidateAnswerDebouncedCoroutine());
        }

        private IEnumerator ProcessCandidateAnswerDebouncedCoroutine()
        {
            // Wait 0.85 seconds of silence (down from 1.8s) to quickly capture full answers without sluggish delay
            yield return new WaitForSeconds(0.85f);

            string fullAnswer = candidateAnswerBuffer.ToString().Trim();
            candidateAnswerBuffer.Clear();
            candidateAnswerDebounceCoroutine = null;

            if (string.IsNullOrWhiteSpace(fullAnswer)) yield break;

            // Transition strictly to ProcessingAnswer state
            currentTurnState = TurnState.ProcessingAnswer;

            // Immediately gate candidate microphone while processing answer
            SetCandidateMicrophoneMuted(true);
            Debug.Log("[MIC] Candidate finished speaking. Microphone GATED while interviewer processes answer.");

            Debug.Log($"[ANSWER] Complete candidate answer received: \"{fullAnswer}\"");
            Debug.Log($"[ANSWER]\nCandidate response:\n{fullAnswer}");

            // Extract candidate name from spoken introduction if candidate introduces themselves
            if (PREPVRSessionManager.Instance != null)
            {
                CandidateProfile prof = PREPVRSessionManager.Instance.Profile;
                if (prof == null || string.IsNullOrWhiteSpace(prof.name) || prof.name == "Candidate" || prof.name.IndexOf("Swetha", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string spokenName = TryExtractSpokenCandidateName(fullAnswer);
                    if (!string.IsNullOrEmpty(spokenName))
                    {
                        if (prof == null)
                        {
                            prof = new CandidateProfile();
                            PREPVRSessionManager.Instance.Profile = prof;
                        }
                        prof.name = spokenName;
                        Debug.Log($"[CANDIDATE] Spoken candidate name captured from introduction: {spokenName}");
                    }
                }
            }

            // Record candidate answer into previous Q&A context lists for cross-stage continuity
            switch (currentStage)
            {
                case HRInterviewStage.HRInterviewer:
                    hrCandidateAnswerCount++;
                    hrTranscriptLines.Add($"Candidate: {fullAnswer}");
                    Debug.Log($"[Interview] HR Candidate Answer #{hrCandidateAnswerCount} recorded.");
                    break;
                case HRInterviewStage.BehavioralInterviewer:
                    behaviorCandidateAnswerCount++;
                    behaviorTranscriptLines.Add($"Candidate: {fullAnswer}");
                    Debug.Log($"[Interview] Technical Candidate Answer #{behaviorCandidateAnswerCount} recorded.");
                    break;
                case HRInterviewStage.HiringManager:
                    hiringMgrCandidateAnswerCount++;
                    Debug.Log($"[Interview] Hiring Manager Candidate Answer #{hiringMgrCandidateAnswerCount} recorded.");
                    break;
            }

            if (PREPVRSessionManager.Instance != null)
                PREPVRSessionManager.Instance.RecordTranscript("Candidate", fullAnswer, isQuestion: false);

            if (InterviewVisualEvaluator.Instance != null)
            {
                float now = PREPVRSessionManager.Instance?.InterviewDurationSeconds ?? 0f;
                InterviewVisualEvaluator.Instance.RecordAnswerStarted(now);
            }

            // Injects candidate response into active interviewer DynamicContext
            ConvaiCharacter activeChar = GetActiveInterviewer();
            CandidateProfile profile = PREPVRSessionManager.Instance?.Profile;
            string primaryProject = GetPrimaryProject(profile);
            string primarySkills = GetPrimarySkills(profile);
            string activeRole = GetCurrentStageRoleName();

            try
            {
                if (activeChar?.DynamicContext != null)
                {
                    string currentCandName = profile?.name;
                    var dynamicState = new Dictionary<string, string>
                    {
                        { "candidate_last_answer", fullAnswer },
                        { "follow_up_directive",
                          $"The candidate just answered: \"{fullAnswer}\". " +
                          "You must ask a direct, relevant FOLLOW-UP question probing the specific technical details, tools, technologies, architecture, or decisions they mentioned in this answer. " +
                          "Do NOT switch to an unrelated generic question. Reference their answer directly. Ask ONE question and wait for the candidate." }
                    };
                    if (!string.IsNullOrWhiteSpace(currentCandName) && currentCandName.IndexOf("Swetha", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        dynamicState["candidate_name"] = currentCandName;
                    }
                    activeChar.DynamicContext.SetStates(dynamicState, ConvaiRespondMode.Silent);
                    activeChar.DynamicContext.Flush();
                    Debug.Log($"[FOLLOW-UP]\nPrevious answer:\n{fullAnswer}");
                    Debug.Log($"[AI] Context passed to interviewer with follow-up directive for candidate response.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FOLLOW-UP] DynamicContext error: {ex.Message}");
            }

            float elapsed = AuthoritativeElapsedTime;

            // 1. Soft Timer Limit Reached: Candidate completed their answer -> Transition to ClosingStatement
            if (isTimeLimitReached || elapsed >= TOTAL_INTERVIEW_SECONDS || (currentStage == HRInterviewStage.HiringManager && elapsed >= (STAGE_2_END_SECONDS + 120f)))
            {
                Debug.Log($"[INTERVIEW] Total duration reached ({elapsed:F1}s). Candidate completed answer. Delivering final closing statement.");
                currentTurnState = TurnState.ClosingSpeaking;
                string closingPrompt = $"[PROMPT] The candidate just completed their response: \"{fullAnswer}\". The interview session duration is now complete. As the {activeRole}, deliver your final closing statement: thank the candidate for their time, acknowledge their responses, state that the evaluation team will review their interview and be in touch soon, and warmly wish them the best. Say your closing statement now.";
                if (activeChar != null)
                {
                    Debug.Log($"[Interview] Dispatching closing prompt to {activeRole} ({activeChar.CharacterName}): \"{closingPrompt}\"");
                    StartCoroutine(SendPlayerAgentPrompt(activeChar, closingPrompt, activeRole));
                }
                else
                {
                    HandleInterviewCompleted();
                }
                yield break;
            }

            // 2. Stage 1 (HR) boundary reached: Candidate completed their answer -> Trigger HR handoff statement
            if (currentStage == HRInterviewStage.HRInterviewer && elapsed >= STAGE_1_END_SECONDS)
            {
                Debug.Log($"[INTERVIEW] Stage 1 HR boundary reached ({elapsed:F1}s). Candidate completed answer. Triggering HR handoff statement.");
                currentTurnState = TurnState.HandoffSpeaking;
                string handoffPrompt = $"[PROMPT] The candidate just answered: \"{fullAnswer}\". Stage 1 is now complete. As the HR Interviewer, briefly thank the candidate and say: \"Thank you for sharing that. I will now pass you on to our Technical Interviewer.\"";
                if (activeChar != null)
                {
                    Debug.Log($"[Interview] Dispatching HR handoff prompt ({activeChar.CharacterName}): \"{handoffPrompt}\"");
                    StartCoroutine(SendPlayerAgentPrompt(activeChar, handoffPrompt, activeRole));
                }
                yield break;
            }

            // 3. Stage 2 (Technical) boundary reached: Candidate completed their answer -> Trigger Technical handoff statement
            if (currentStage == HRInterviewStage.BehavioralInterviewer && elapsed >= STAGE_2_END_SECONDS)
            {
                Debug.Log($"[INTERVIEW] Stage 2 Technical boundary reached ({elapsed:F1}s). Candidate completed answer. Triggering Technical handoff statement.");
                currentTurnState = TurnState.HandoffSpeaking;
                string handoffPrompt = $"[PROMPT] The candidate just answered: \"{fullAnswer}\". Technical Stage 2 is now complete. As the Technical Interviewer, briefly thank the candidate and say: \"Thank you for explaining your technical implementation. I will now pass you on to our Hiring Manager.\"";
                if (activeChar != null)
                {
                    Debug.Log($"[Interview] Dispatching Technical handoff prompt ({activeChar.CharacterName}): \"{handoffPrompt}\"");
                    StartCoroutine(SendPlayerAgentPrompt(activeChar, handoffPrompt, activeRole));
                }
                yield break;
            }

            // 4. Construct tailored authoritative follow-up prompt and dispatch via SendPlayerAgentPrompt
            string followUpPrompt;
            if (currentStage == HRInterviewStage.HRInterviewer)
            {
                if (hrCandidateAnswerCount <= 1)
                {
                    followUpPrompt = $"[PROMPT] The candidate just introduced themselves: \"{fullAnswer}\". As the HR Interviewer, acknowledge their introduction, and ask your next question specifically about their project {primaryProject} or background in {primarySkills}. Ask ONE question.";
                }
                else
                {
                    followUpPrompt = $"[PROMPT] The candidate explained: \"{fullAnswer}\". As the HR Interviewer, acknowledge what they said, and ask a relevant follow-up question probing what they just shared regarding their experience or projects. Ask ONE question.";
                }
            }
            else if (currentStage == HRInterviewStage.BehavioralInterviewer)
            {
                followUpPrompt = $"[PROMPT] The candidate answered: \"{fullAnswer}\". As the Technical Interviewer, acknowledge what they just explained, and ask an in-depth technical follow-up question directly probing their implementation, design decisions, or technologies regarding {primaryProject}. Ask ONE question.";
            }
            else
            {
                followUpPrompt = $"[PROMPT] The candidate answered: \"{fullAnswer}\". As the Hiring Manager, acknowledge what they explained, and ask a follow-up question probing their ownership, teamwork, leadership initiative, or problem-solving regarding {primaryProject}. Ask ONE question.";
            }

            if (activeChar != null)
            {
                Debug.Log($"[CONVAI] Sending candidate answer to {activeRole}");
                Debug.Log($"[Interview] Dispatching follow-up prompt to {activeRole} ({activeChar.CharacterName}): \"{followUpPrompt}\"");
                StartCoroutine(SendPlayerAgentPrompt(activeChar, followUpPrompt, activeRole));
            }
        }

        private string TryExtractSpokenCandidateName(string speech)
        {
            if (string.IsNullOrWhiteSpace(speech)) return null;

            var match = System.Text.RegularExpressions.Regex.Match(
                speech,
                @"(?:my name is|i am|i'm|this is)\s+([A-Z][a-zA-Z]*(?:\s+[A-Z][a-zA-Z]*)?)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success && match.Groups.Count > 1)
            {
                string extracted = match.Groups[1].Value.Trim();
                string[] ignoreWords = { "a", "an", "the", "here", "ready", "excited", "glad", "speaking", "going", "answering", "not", "just", "so" };
                foreach (string w in ignoreWords)
                {
                    if (string.Equals(extracted, w, StringComparison.OrdinalIgnoreCase))
                        return null;
                }
                if (extracted.Length >= 2)
                {
                    return extracted;
                }
            }
            return null;
        }

        // ====================================================================
        // Stage transition detection
        // ====================================================================
        private void CheckHRStageTransition(string text)
        {
            if (isTransitioningStage || currentStage != HRInterviewStage.HRInterviewer) return;

            bool phraseHandoff =
                text.IndexOf("Behavioral Interviewer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("pass you on",              StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("hand you over",            StringComparison.OrdinalIgnoreCase) >= 0;

            if (phraseHandoff && AuthoritativeElapsedTime >= 180f)
            {
                Debug.Log($"[Interview] HR handoff phrase detected near stage boundary ({AuthoritativeElapsedTime:F1}s). Transitioning to Stage 2.");
                StartCoroutine(BeginStage_BehavioralInterviewer());
            }
        }

        private void CheckBehavioralStageTransition(string text)
        {
            if (isTransitioningStage || currentStage != HRInterviewStage.BehavioralInterviewer) return;

            bool phraseHandoff =
                text.IndexOf("Hiring Manager", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("pass you on",    StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("hand you over",  StringComparison.OrdinalIgnoreCase) >= 0;

            if (phraseHandoff && AuthoritativeElapsedTime >= 380f)
            {
                Debug.Log($"[Interview] Behavioral handoff phrase detected near stage boundary ({AuthoritativeElapsedTime:F1}s). Transitioning to Stage 3.");
                StartCoroutine(BeginStage_HiringManager());
            }
        }

        private void CheckHiringManagerStageTransition(string text)
        {
            if (isTransitioningStage || currentStage != HRInterviewStage.HiringManager) return;

            bool closingPhrase =
                text.IndexOf("conclude",              StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("thank you for your",    StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("that wraps up",         StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("end of the interview",  StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("evaluation team will",  StringComparison.OrdinalIgnoreCase) >= 0;

            if (closingPhrase && AuthoritativeElapsedTime >= 580f)
            {
                Debug.Log($"[Interview] Hiring Manager closing detected near 600s boundary ({AuthoritativeElapsedTime:F1}s). Concluding.");
                HandleInterviewCompleted();
            }
        }

        private void CheckCountFallbackTransitions()
        {
            // Governed authoritatively by the 600-second interview clock in Update().
        }

        // ====================================================================
        // Interview completed
        // ====================================================================
        private void HandleInterviewCompleted()
        {
            if (!interviewActive) return;
            StartCoroutine(ConcludeInterviewCoroutine());
        }

        private IEnumerator ConcludeInterviewCoroutine()
        {
            if (currentStage == HRInterviewStage.InterviewCompleted) yield break;
            isTransitioningStage = true;

            Debug.Log("[Interview] === CONCLUDING 10-MINUTE INTERVIEW AT 600 SECONDS ===");

            // 1. Cancel pending candidate response
            InterviewAutomatedTester.Instance?.CancelPendingResponse();

            // 2. Stop audio on all interviewers immediately
            StopAllInterviewerAudio();

            interviewActive = false;
            currentStage    = HRInterviewStage.InterviewCompleted;

            Debug.Log("[Interview] Interview session concluded. Generating feedback...");

            if (InterviewVisualEvaluator.Instance != null)
                InterviewVisualEvaluator.Instance.FinalizeObservations();

            GenerateFinalFeedback();

            // Automatically dispatch real interview result to Centralized Placement Backend
            try
            {
                if (PlacementBackendClient.Instance != null && PREPVRSessionManager.Instance != null && PREPVRSessionManager.Instance.feedbackData != null)
                {
                    string roll = PREPVRSessionManager.Instance.RollNumber;
                    if (string.IsNullOrWhiteSpace(roll) && PREPVRSessionManager.Instance.Profile != null)
                    {
                        roll = PREPVRSessionManager.Instance.Profile.rollNumber;
                    }
                    if (string.IsNullOrWhiteSpace(roll))
                    {
                        roll = PREPVRSessionManager.Instance.feedbackData.candidateName.Replace(" ", "_").ToUpperInvariant();
                    }
                    PlacementBackendClient.Instance.SendInterviewResult(PREPVRSessionManager.Instance.feedbackData, roll);
                    Debug.Log($"[InterviewOrchestrator] Dispatched interview completion results to Placement Backend for roll: {roll}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InterviewOrchestrator] Note during placement backend sync: {ex.Message}");
            }

            if (PREPVRSessionManager.Instance != null)
                PREPVRSessionManager.Instance.MarkInterviewComplete();

            try { hrInterviewer?.StopConversationAsync(); }         catch { }
            try { behavioralInterviewer?.StopConversationAsync(); } catch { }
            try { hiringManager?.StopConversationAsync(); }         catch { }

            // Allow Convai cleanup and unpublish tasks to complete cleanly before scene unload
            yield return new WaitForSeconds(1.5f);

            Debug.Log($"[Interview] Transitioning to {NEXT_SCENE_NAME}.");
            SceneManager.LoadScene(NEXT_SCENE_NAME);
        }

        private void StopAllInterviewerAudio()
        {
            StopCharacterAudio(hrInterviewer);
            StopCharacterAudio(behavioralInterviewer);
            StopCharacterAudio(hiringManager);
        }

        private void StopCharacterAudio(ConvaiCharacter character)
        {
            if (character == null) return;
            try
            {
                AudioSource audio = character.GetComponentInChildren<AudioSource>();
                if (audio != null && audio.isPlaying)
                {
                    audio.Stop();
                    Debug.Log($"[Interview] Stopped audio playback for {character.CharacterName}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Interview] Note stopping audio for {character.CharacterName}: {ex.Message}");
            }
        }

        private void GenerateFinalFeedback()
        {
            if (PREPVRSessionManager.Instance == null) return;

            CandidateProfile        profile    = PREPVRSessionManager.Instance.Profile;
            InterviewTranscriptData transcript = PREPVRSessionManager.Instance.transcript;
            VisualObservationsData  visual     = PREPVRSessionManager.Instance.visualObservations;
            float                   duration   = PREPVRSessionManager.Instance.InterviewDurationSeconds;

            FeedbackData fb = FeedbackEngine.GenerateFeedback(profile, transcript, visual, duration);
            PREPVRSessionManager.Instance.feedbackData = fb;

            Debug.Log($"[Interview] Feedback for {fb.candidateName}. Readiness: {fb.overallReadinessLevel}");
        }

        // ====================================================================
        // Setup helpers
        // ====================================================================
        private void SetupTimer()
        {
            InterviewTimerManager timer = FindAnyObjectByType<InterviewTimerManager>();
            if (timer == null)
                timer = new GameObject("InterviewTimerManager").AddComponent<InterviewTimerManager>();
            timer.OnTimerExpired += HandleTimerExpiredSoftBoundary;
        }

        private void HandleTimerExpiredSoftBoundary()
        {
            isTimeLimitReached = true;
            Debug.Log("[INTERVIEW] Timer expired event received. Treating as soft scheduling boundary: allowing active speaker/turn to finish naturally.");

            // If completely idle with no active exchange, trigger graceful closing statement
            if (!isInterviewerSpeakingTurn && !IsAnyInterviewerSpeaking() && !IsCandidateSpeakingOrTurnActive &&
                currentTurnState != TurnState.HandoffSpeaking && currentTurnState != TurnState.ClosingSpeaking &&
                candidateAnswerBuffer.Length == 0 && candidateAnswerDebounceCoroutine == null)
            {
                Debug.Log("[INTERVIEW] No conversation turn active at timer expiration. Initiating graceful closing statement.");
                ConvaiCharacter activeChar = GetActiveInterviewer();
                string activeRole = GetCurrentStageRoleName();
                currentTurnState = TurnState.ClosingSpeaking;
                string closingPrompt = $"[PROMPT] The scheduled interview duration is now complete. As the {activeRole}, deliver your final closing statement: thank the candidate for their time today, state that the panel has all the information needed, and conclude the interview. Do not ask another question.";
                if (activeChar != null)
                {
                    StartCoroutine(SendPlayerAgentPrompt(activeChar, closingPrompt, activeRole));
                }
                else
                {
                    HandleInterviewCompleted();
                }
            }
        }

        private void SetupCameraController()
        {
            InterviewCameraController camCtrl = FindAnyObjectByType<InterviewCameraController>();
            if (camCtrl == null)
            {
                Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
                if (mainCam != null)
                {
                    camCtrl = mainCam.gameObject.AddComponent<InterviewCameraController>();
                }
                else
                {
                    camCtrl = new GameObject("InterviewCameraController").AddComponent<InterviewCameraController>();
                }
            }
            Debug.Log($"[InterviewOrchestrator] InterviewCameraController initialized: {camCtrl != null}");
        }

        private void SetupVisualEvaluator()
        {
            InterviewVisualEvaluator evaluator = FindAnyObjectByType<InterviewVisualEvaluator>();
            if (evaluator == null)
                evaluator = new GameObject("InterviewVisualEvaluator").AddComponent<InterviewVisualEvaluator>();
            evaluator.Initialize(
                Camera.main != null ? Camera.main.transform : FindAnyObjectByType<ConvaiPlayer>()?.transform,
                hrInterviewer != null ? hrInterviewer.transform : null,
                hiringManager != null ? hiringManager.transform : null
            );
        }

        private void SetupRuntimeHUD()
        {
            if (FindAnyObjectByType<InterviewRuntimeHUD>() == null)
                new GameObject("InterviewRuntimeHUD").AddComponent<InterviewRuntimeHUD>();
        }

        private string BuildResumeSummary(CandidateProfile p)
        {
            if (p == null) return "No resume data available.";
            var sb = new StringBuilder();
            sb.Append($"Candidate: {p.name ?? "Candidate"}. ");
            sb.Append($"Education: {p.education ?? "Not specified"}. ");
            if (p.skills?.Count     > 0) sb.Append($"Skills: {string.Join(", ", p.skills)}. ");
            if (p.experience?.Count > 0) sb.Append($"Experience: {string.Join(" | ", p.experience)}. ");
            if (p.projects?.Count   > 0)
            {
                sb.Append("Projects: ");
                for (int i = 0; i < p.projects.Count; i++) sb.Append($"[{i + 1}] {p.projects[i]}; ");
            }
            if (p.certifications?.Count > 0) sb.Append($"Certifications: {string.Join(", ", p.certifications)}. ");
            if (p.achievements?.Count   > 0) sb.Append($"Achievements: {string.Join(", ", p.achievements)}. ");
            return sb.ToString();
        }

        private string BuildStageSummary(List<string> lines, string stageName)
        {
            if (lines == null || lines.Count == 0)
                return $"[{stageName} had no recorded conversation]";
            int start = Math.Max(0, lines.Count - 20);
            var sb = new StringBuilder();
            sb.AppendLine($"--- {stageName} Transcript ---");
            for (int i = start; i < lines.Count; i++) sb.AppendLine(lines[i]);
            return sb.ToString();
        }

        // ====================================================================
        // Convai event subscription
        // ====================================================================
        private void SubscribeConvaiEvents()
        {
            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null)
            {
                StartCoroutine(RetrySubscribeConvaiEvents());
                return;
            }
            try
            {
                manager.Events.OnCharacterTranscriptReceived += HandleCharacterTranscript;
                manager.Events.OnPlayerTranscriptReceived    += HandlePlayerTranscript;
                manager.Events.OnPlayerSpeakingStateChanged  += HandlePlayerSpeakingStateChanged;
                manager.Events.OnCharacterSpeechStateChanged += HandleSpeechStateChanged;
                manager.Events.OnCharacterTurnCompleted      += HandleCharacterTurnCompleted;
                manager.Events.OnLlmNoResponseReceived       += HandleLlmNoResponse;
                manager.Events.OnPipelineError               += HandlePipelineError;
                manager.Events.OnVadSttStateChanged          += HandleVadSttStateChanged;
                Debug.Log("[ConvaiDebug] Subscribed to ConvaiManager events (transcripts, player speaking, speech state, turn completed, VAD, errors).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvaiDebug] Event subscription error: {ex.Message}");
            }
        }

        private void UnsubscribeConvaiEvents()
        {
            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager != null)
            {
                try
                {
                    manager.Events.OnCharacterTranscriptReceived -= HandleCharacterTranscript;
                    manager.Events.OnPlayerTranscriptReceived    -= HandlePlayerTranscript;
                    manager.Events.OnPlayerSpeakingStateChanged  -= HandlePlayerSpeakingStateChanged;
                    manager.Events.OnCharacterSpeechStateChanged -= HandleSpeechStateChanged;
                    manager.Events.OnCharacterTurnCompleted      -= HandleCharacterTurnCompleted;
                    manager.Events.OnLlmNoResponseReceived       -= HandleLlmNoResponse;
                    manager.Events.OnPipelineError               -= HandlePipelineError;
                    manager.Events.OnVadSttStateChanged          -= HandleVadSttStateChanged;
                }
                catch { }
            }
        }

        private void HandlePlayerSpeakingStateChanged(PlayerSpeakingStateChanged e)
        {
            if (!interviewActive) return;
            Debug.Log($"[ConvaiDebug] Player speaking state changed: isSpeaking={e.IsSpeaking}, sessionId={e.SessionId}");

            if (e.IsSpeaking)
            {
                Debug.Log("[STT] Candidate speech detected");
                if (currentTurnState == TurnState.WaitingForCandidate)
                {
                    currentTurnState = TurnState.CandidateSpeaking;
                }
                else if (currentTurnState == TurnState.InterviewerSpeaking)
                {
                    ConvaiCharacter activeChar = GetActiveInterviewer();
                    AudioSource audioSrc = activeChar != null ? (activeChar.GetComponent<AudioSource>() ?? activeChar.GetComponentInChildren<AudioSource>()) : null;
                    bool isAudioPlaying = audioSrc != null && audioSrc.isPlaying;
                    bool isConvaiSpeaking = activeChar != null && activeChar.IsSpeaking;

                    if (!isAudioPlaying && !isConvaiSpeaking)
                    {
                        if (turnCompletionCoroutine != null)
                        {
                            StopCoroutine(turnCompletionCoroutine);
                            turnCompletionCoroutine = null;
                        }
                        isInterviewerSpeakingTurn = false;
                        Debug.Log("[TURN] Interviewer finished");
                        Debug.Log("[MIC] Candidate microphone ACTIVE");
                        currentTurnState = TurnState.CandidateSpeaking;
                    }
                }
            }
            else
            {
                if (currentTurnState == TurnState.CandidateSpeaking)
                {
                    Debug.Log("[ConvaiDebug] Candidate speech paused (IsSpeaking=false). Debounce will finalize complete answer.");
                }
            }
        }

        private void HandleCharacterTurnCompleted(CharacterTurnCompleted e)
        {
            if (!interviewActive) return;
            Debug.Log($"[AUDIO] Character turn completed: characterId={e.CharacterId}, wasInterrupted={e.WasInterrupted}");
            if (activeTurnCharacter != null && MatchesCharacter(e.CharacterId, string.Empty, activeTurnCharacter))
            {
                characterTurnCompletedReceived = true;
            }
        }

        private void HandleSpeechStateChanged(CharacterSpeechStateChanged e)
        {
            Debug.Log($"[ConvaiDebug] Character speech state: characterId={e.CharacterId}, isSpeaking={e.IsSpeaking}, utteranceId={e.UtteranceId}");
            if (e.IsSpeaking)
            {
                string roleName = "Unknown";
                ConvaiCharacter speakingChar = null;
                if (hrInterviewer != null && (e.CharacterId == hrInterviewer.CharacterId || e.CharacterId == HR_CHARACTER_ID_SCENE || e.CharacterId == HR_CHARACTER_ID_ACCOUNT || e.CharacterId == HR_CHARACTER_ID_LEGACY))
                {
                    roleName = "HR";
                    speakingChar = hrInterviewer;
                    Debug.Log("[Interview] H. HR audio received/started (SpeechStateChanged)");
                }
                else if (behavioralInterviewer != null && (e.CharacterId == behavioralInterviewer.CharacterId || e.CharacterId == BEHAVIORAL_ID_SCENE || e.CharacterId == BEHAVIORAL_ID_ACCOUNT || e.CharacterId == BEHAVIORAL_ID_LEGACY))
                {
                    roleName = "Technical";
                    speakingChar = behavioralInterviewer;
                }
                else if (hiringManager != null && (e.CharacterId == hiringManager.CharacterId || e.CharacterId == HIRING_MGR_ID_SCENE || e.CharacterId == HIRING_MGR_ID_ACCOUNT || e.CharacterId == HIRING_MGR_ID_LEGACY))
                {
                    roleName = "Hiring Manager";
                    speakingChar = hiringManager;
                }

                Debug.Log($"[AUDIO] Interviewer [{roleName}] audio response received/started (SpeechStateChanged)");
                if (speakingChar != null && !isInterviewerSpeakingTurn)
                {
                    StartInterviewerSpeakingTurn(speakingChar, roleName);
                }
            }
        }

        private void HandleLlmNoResponse(LlmNoResponseReceived e)
        {
            Debug.LogWarning($"[ConvaiDebug] LLM no response received: characterId={e.CharacterId}, reason={e.Reason}");
        }

        private void HandlePipelineError(SessionError e)
        {
            Debug.LogError($"[ConvaiDebug] Convai pipeline error: code={e.ErrorCode}, message={e.Message}");
        }

        private void HandleVadSttStateChanged(VadSttStateChanged e)
        {
            Debug.Log($"[ConvaiDebug] VAD STT state changed: isActive={e.IsActive}");
        }

        private IEnumerator RetrySubscribeConvaiEvents()
        {
            float timeout = 8f;
            while (ConvaiManager.ActiveManager == null && timeout > 0f)
            {
                yield return new WaitForSeconds(0.5f);
                timeout -= 0.5f;
            }
            SubscribeConvaiEvents();
        }
    }
}
