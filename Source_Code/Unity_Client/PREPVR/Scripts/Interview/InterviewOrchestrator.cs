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
using Convai.Shared.Abstractions;
using Convai.Shared.Types;
using PrepVR.Model;
using PrepVR.Placement;

namespace PrepVR.Interview
{
    // -----------------------------------------------------------------------
    // Core Interview State Machine (Prompt Required States)
    // -----------------------------------------------------------------------
    public enum InterviewState
    {
        Initializing,
        Connecting,
        InterviewStarting,
        InterviewInProgress,
        FinalQuestion,
        ClosingConversation,
        WaitingForClosingAudio,
        InterviewCompleted
    }

    // -----------------------------------------------------------------------
    // Structured 10-Minute Interview Sections (Adaptive Topic Coverage Guide)
    // -----------------------------------------------------------------------
    public enum InterviewSection
    {
        Welcome_Introduction = 1,       // 00:00 - 01:00 (0s - 60s)
        Background_Education_HR = 2,    // 01:00 - 03:00 (60s - 180s)
        Resume_Technical_Skills = 3,    // 03:00 - 06:00 (180s - 360s)
        Projects_Problem_Solving = 4,   // 06:00 - 08:00 (360s - 480s)
        Behavioural_Situational = 5,    // 08:00 - 09:30 (480s - 570s)
        Final_Question_Closing = 6      // 09:30 - 10:00 (570s - 600s)
    }

    // Backward-compatibility stage enum
    public enum HRInterviewStage
    {
        NotStarted,
        HRInterviewer,           // Single interviewer conducts the session
        BehavioralInterviewer,
        HiringManager,
        InterviewCompleted
    }

    /// <summary>
    /// Master orchestrator for the Single Interviewer System in PREP VR.
    /// Operates a complete 10-minute structured AI interview with a single AI interviewer
    /// covering HR, resume technical skills, project discussions, and behavioural questions.
    /// </summary>
    public class InterviewOrchestrator : MonoBehaviour, IConversationTargetProvider
    {
        public const string TARGET_SCENE_NAME = "InterviewRoom";
        public const string NEXT_SCENE_NAME   = "InterviewOver";

        // ---- Single Interviewer Character IDs -----------------------------
        public const string HR_CHARACTER_ID_SCENE     = "c493d354-3215-4c62-8906-ed3ae499004b";
        public const string HR_CHARACTER_ID_USER      = "96d2f7ff-c33b-4b1f-8c58-b5798da695e2";
        public const string HR_CHARACTER_ID_ALT       = "39e56af2-5a98-406f-90e6-ddb52728b542";
        public const string HR_CHARACTER_ID_ACCOUNT   = "28288a88-b324-43b6-920f-c302fbe5dec2";
        public const string HR_CHARACTER_ID_LEGACY    = "d5ac8c30-012b-444e-b3ca-306603364d42";

        // Aliases
        public const string HR_CHARACTER_ID = HR_CHARACTER_ID_SCENE;
        public const string HR_TRIGGER_NAME = "StartInterview";

        // ---- 10-Minute Schedule Boundaries (in seconds) -------------------
        public const float TOTAL_INTERVIEW_SECONDS   = 600f; // 10 minutes (600s) total target duration
        public const float SECTION_1_END_SECONDS     = 60f;  // 1:00 Welcome and introduction
        public const float SECTION_2_END_SECONDS     = 180f; // 3:00 Background, education and HR questions
        public const float SECTION_3_END_SECONDS     = 360f; // 6:00 Resume-based skills and technical questions
        public const float SECTION_4_END_SECONDS     = 480f; // 8:00 Project discussion and problem-solving
        public const float SECTION_5_END_SECONDS     = 570f; // 9:30 Behavioural and situational questions (STAR)
        public const float SECTION_6_END_SECONDS     = 600f; // 10:00 Final question and professional closing

        // Legacy compatibility aliases
        public const float STAGE_1_END_SECONDS = SECTION_2_END_SECONDS;
        public const float STAGE_2_END_SECONDS = SECTION_3_END_SECONDS;
        public const float STAGE_3_END_SECONDS = SECTION_5_END_SECONDS;
        public const float STAGE_1_TARGET_SECONDS = STAGE_1_END_SECONDS;
        public const float STAGE_2_TARGET_SECONDS = STAGE_2_END_SECONDS;
        public const float STAGE_3_TARGET_SECONDS = STAGE_3_END_SECONDS;

        // ---- Discovered Single Interviewer --------------------------------
        [Header("Single AI Interviewer")]
        [SerializeField] private ConvaiCharacter interviewer;
        [SerializeField] private ConvaiPlayer convaiPlayer;

        // Backward compatibility property
        public ConvaiCharacter hrInterviewer => interviewer;

        // ---- Interview State Machine --------------------------------------
        [Header("Interview State Machine")]
        [SerializeField] private InterviewState currentState = InterviewState.Initializing;
        [SerializeField] private InterviewSection currentSection = InterviewSection.Welcome_Introduction;
        [SerializeField] private bool interviewActive = false;

        public InterviewState CurrentState => currentState;
        public InterviewSection CurrentSection => currentSection;
        public bool InterviewActive => interviewActive;

        // Backward compatibility stage property
        public HRInterviewStage CurrentStage
        {
            get
            {
                if (currentState == InterviewState.InterviewCompleted) return HRInterviewStage.InterviewCompleted;
                if (!interviewActive) return HRInterviewStage.NotStarted;
                return HRInterviewStage.HRInterviewer;
            }
        }

        // Question and Answer Tracking
        private int totalQuestionsAsked = 0;
        private int totalCandidateAnswers = 0;
        private int currentSectionQuestionCount = 0;
        private int currentSectionAnswerCount = 0;

        public int TotalQuestionsAsked => totalQuestionsAsked;
        public int TotalCandidateAnswers => totalCandidateAnswers;
        public int HrCandidateAnswerCount => totalCandidateAnswers;
        public int BehaviorCandidateAnswerCount => currentSectionAnswerCount;
        public int HiringMgrCandidateAnswerCount => currentSectionAnswerCount;

        [Header("Speech Naturalness & Pacing")]
        [SerializeField] [Range(0.75f, 1.0f)] private float speakingPacingRate = 0.88f;
        public float SpeakingPacingRate => speakingPacingRate;

        private float interviewStartTime = 0f;
        public float AuthoritativeElapsedTime => InterviewTimerManager.Instance != null
            ? InterviewTimerManager.Instance.ElapsedTime
            : (interviewStartTime > 0f ? (Time.time - interviewStartTime) : 0f);

        public float StageElapsedTime => AuthoritativeElapsedTime;

        private readonly List<string> interviewTranscriptLines = new();
        private string lastRecordedQuestion = "";
        private bool isTransitioningStage = false;
        private bool isInitializing = false;
        private bool isResumeFallbackActive = false;

        // ---- Turn-Taking State Machine ------------------------------------
        public enum TurnState
        {
            NotStarted,
            InterviewerSpeaking,  // Single interviewer speaking question or greeting
            WaitingForCandidate,  // Interviewer finished; candidate mic is active; waiting for speech
            CandidateSpeaking,    // Interviewer finished; candidate mic is active; candidate is speaking
            ProcessingAnswer,     // Candidate finished speaking; debounce expired; answer is being processed
            ClosingSpeaking,      // Interviewer speaking closing statement
            Concluding            // Closing statement finished; finalizing session and transitioning scene
        }

        [Header("Turn State Machine")]
        [SerializeField] private TurnState currentTurnState = TurnState.NotStarted;
        public TurnState CurrentTurnState => currentTurnState;

        private bool isHandoffSpeechCompleted = false;
        public bool IsHandoffSpeechCompleted => isHandoffSpeechCompleted;

        public bool IsCandidateSpeakingOrTurnActive =>
            currentTurnState == TurnState.WaitingForCandidate ||
            currentTurnState == TurnState.CandidateSpeaking ||
            currentTurnState == TurnState.ProcessingAnswer ||
            candidateAnswerBuffer.Length > 0 ||
            (InterviewAutomatedTester.Instance != null && InterviewAutomatedTester.Instance.IsCandidateSpeakingOrScheduled);

        // Turn tracking & microphone gating
        public event Action<string, string> OnInterviewerQuestionFinished;
        public event Action<InterviewState> OnInterviewStateChanged;
        public event Action<InterviewSection> OnInterviewSectionChanged;

        private int currentTurnId = 0;
        private bool isInterviewerSpeakingTurn = false;
        private readonly StringBuilder currentTurnQuestionBuilder = new();
        private bool characterAudioCompletedReceived = false;
        private bool characterTurnCompletedReceived = false;
        private float lastInterviewerSpeechStartTime = 0f;
        private float lastInterviewerSpeechStopTime = 0f;
        private Coroutine turnCompletionCoroutine = null;
        private Coroutine turnGraceTimeoutCoroutine = null;
        private readonly object turnLock = new object();

        // Candidate Answer Buffering & Debouncing
        private readonly StringBuilder candidateAnswerBuffer = new();
        private Coroutine candidateAnswerDebounceCoroutine = null;

        public static InterviewOrchestrator Instance { get; private set; }
        public bool IsTransitioningStage => isTransitioningStage;
        public bool IsInterviewerSpeakingTurn => isInterviewerSpeakingTurn;

        public ConvaiCharacter GetActiveInterviewer() => interviewer;

        private bool isCandidateMicMuted = false;

        public void SetCandidateMicrophoneMuted(bool mute, string reason = "unspecified")
        {
            try
            {
                isCandidateMicMuted = mute;
                ConvaiManager manager = ConvaiManager.ActiveManager;
                if (manager?.Audio != null)
                {
                    if (!mute)
                    {
                        // Ensure microphone listening pipeline is started and capturing audio
                        manager.Audio.StartListeningAsync();
                    }
                    manager.Audio.SetMicMuted(mute);
                    bool actualMuted = manager.Audio.IsMicMuted;
                    Debug.Log($"[MIC_GATE] Convai Audio.SetMicMuted({mute}) executed (Reason: {reason}). Actual IsMicMuted={actualMuted} at {AuthoritativeElapsedTime:F2}s");
                }
                else
                {
                    Debug.LogWarning($"[MIC_GATE] SetCandidateMicrophoneMuted({mute}) called but ConvaiManager.ActiveManager?.Audio is null. Reason: {reason}");
                }

                if (!mute)
                {
                    Debug.Log($"[MIC] Candidate microphone ACTIVE (Candidate may speak) [Reason: {reason}]");
                }
                else
                {
                    Debug.Log($"[MIC] Candidate microphone GATED (interviewer speaking / processing) [Reason: {reason}]");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MIC] SetMicMuted failed: {ex.Message}");
            }
        }

        public bool IsActiveStageCharacter(string charId, string speaker)
        {
            return MatchesCharacter(charId, speaker, interviewer);
        }

        public bool IsCandidateSpeakingOrPending() => IsCandidateSpeakingOrTurnActive;

        public bool IsAnyInterviewerSpeaking()
        {
            if (isInterviewerSpeakingTurn) return true;
            if (interviewer != null && interviewer.IsSpeaking) return true;
            return false;
        }

        public bool IsExchangeActive()
        {
            return IsCandidateSpeakingOrTurnActive || isInterviewerSpeakingTurn || IsAnyInterviewerSpeaking();
        }

        private void SetState(InterviewState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
            Debug.Log($"[InterviewState] State changed to: {newState} at {AuthoritativeElapsedTime:F2}s");
            OnInterviewStateChanged?.Invoke(newState);
        }

        private void SetSection(InterviewSection newSection)
        {
            if (currentSection == newSection) return;
            currentSection = newSection;
            currentSectionQuestionCount = 0;
            currentSectionAnswerCount = 0;
            Debug.Log($"[InterviewSection] Section changed to: {newSection} at {AuthoritativeElapsedTime:F2}s");
            OnInterviewSectionChanged?.Invoke(newSection);

            if (InterviewTimerManager.Instance != null)
            {
                InterviewTimerManager.Instance.SetCustomStage(GetSectionDisplayName(newSection));
            }
        }

        private string GetSectionDisplayName(InterviewSection section)
        {
            switch (section)
            {
                case InterviewSection.Welcome_Introduction:
                    return "Welcome & Introduction (0:00 - 1:00)";
                case InterviewSection.Background_Education_HR:
                    return "Background & HR Questions (1:00 - 3:00)";
                case InterviewSection.Resume_Technical_Skills:
                    return "Technical Skills & Architecture (3:00 - 6:00)";
                case InterviewSection.Projects_Problem_Solving:
                    return "Projects & Problem Solving (6:00 - 8:00)";
                case InterviewSection.Behavioural_Situational:
                    return "Behavioural & Situational (8:00 - 9:30)";
                case InterviewSection.Final_Question_Closing:
                    return "Final Question & Wrap-Up (9:30 - 10:00)";
                default:
                    return "Interview In Progress";
            }
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
                Debug.Log("[InterviewOrchestrator] Start() — InterviewRoom already active. Initializing single interviewer session.");
                StartCoroutine(InitializeInterviewCoroutine());
            }
        }

        private void Update()
        {
            if (!interviewActive || isTransitioningStage) return;

            float elapsed = AuthoritativeElapsedTime;

            // Watchdog 1: If waiting for candidate response and no interviewer is speaking, ensure mic is active
            if ((currentTurnState == TurnState.WaitingForCandidate || currentTurnState == TurnState.CandidateSpeaking) &&
                !isInterviewerSpeakingTurn && !IsAnyInterviewerSpeaking() && isCandidateMicMuted)
            {
                SetCandidateMicrophoneMuted(false, "Update watchdog - waiting for candidate with muted mic");
            }

            // Watchdog 2: If interviewer speaking turn had audio stopped for > 1.0s and turn didn't complete, force complete
            if (isInterviewerSpeakingTurn && characterAudioCompletedReceived && lastInterviewerSpeechStopTime > 0f &&
                (Time.time - lastInterviewerSpeechStopTime > 1.0f))
            {
                CompleteInterviewerSpeakingTurn(interviewer, "Interviewer", "Update watchdog - audio stopped for > 1.0s");
            }

            // Do not alter states or schedule while an exchange or closing audio is playing
            if (isInterviewerSpeakingTurn || IsAnyInterviewerSpeaking() || IsCandidateSpeakingOrTurnActive ||
                currentTurnState == TurnState.ClosingSpeaking || currentTurnState == TurnState.Concluding ||
                currentState == InterviewState.WaitingForClosingAudio || currentState == InterviewState.InterviewCompleted)
            {
                return;
            }

            // Adaptive timeline progression guide:
            // When candidate is not speaking and no question is playing, update current section based on elapsed time:
            if (elapsed < SECTION_1_END_SECONDS)
            {
                SetSection(InterviewSection.Welcome_Introduction);
            }
            else if (elapsed < SECTION_2_END_SECONDS)
            {
                SetSection(InterviewSection.Background_Education_HR);
            }
            else if (elapsed < SECTION_3_END_SECONDS)
            {
                SetSection(InterviewSection.Resume_Technical_Skills);
            }
            else if (elapsed < SECTION_4_END_SECONDS)
            {
                SetSection(InterviewSection.Projects_Problem_Solving);
            }
            else if (elapsed < SECTION_5_END_SECONDS)
            {
                SetSection(InterviewSection.Behavioural_Situational);
            }
            else if (elapsed < TOTAL_INTERVIEW_SECONDS)
            {
                SetSection(InterviewSection.Final_Question_Closing);
                if (currentState == InterviewState.InterviewInProgress)
                {
                    Debug.Log($"[INTERVIEW] 9:30 boundary reached ({elapsed:F1}s). Initiating Final Question phase.");
                    StartCoroutine(InitiateFinalQuestionPhase());
                }
            }
            else
            {
                // Authoritative 10-Minute target reached while completely idle: trigger closing if not already concluding
                if (currentState != InterviewState.ClosingConversation &&
                    currentState != InterviewState.WaitingForClosingAudio &&
                    currentState != InterviewState.InterviewCompleted)
                {
                    Debug.Log($"[INTERVIEW] Authoritative 600-second (10-minute) interview duration reached while idle ({elapsed:F1}s). Triggering closing conversation.");
                    StartCoroutine(InitiateClosingConversation());
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TARGET_SCENE_NAME)
            {
                Debug.Log("[InterviewOrchestrator] InterviewRoom loaded via sceneLoaded.");
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
        // IConversationTargetProvider Implementation (Single Interviewer)
        // ====================================================================
        public ConvaiCharacter ResolveTarget(IReadOnlyList<ConvaiCharacter> candidates, ConvaiCharacter current)
        {
            // In the Single Interviewer System, the active target is ALWAYS the single interviewer
            return interviewer ?? current;
        }

        // ====================================================================
        // Main initialization
        // ====================================================================
        private IEnumerator InitializeInterviewCoroutine()
        {
            isInitializing = true;
            SetState(InterviewState.Initializing);
            Debug.Log("[Interview] Initializing Single Interviewer System for InterviewRoom");

            totalQuestionsAsked = 0;
            totalCandidateAnswers = 0;
            currentSectionQuestionCount = 0;
            currentSectionAnswerCount = 0;
            interviewTranscriptLines.Clear();
            lastRecordedQuestion = "";
            isTransitioningStage = false;
            currentTurnState = TurnState.NotStarted;
            SetSection(InterviewSection.Welcome_Introduction);

            // Ensure CandidateProfile has rich resume data
            EnsureCandidateProfile();

            // 1. Discover the single Convai Character in the scene
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
            interviewer = null;

            if (all != null && all.Length > 0)
            {
                // Match by ID or take the primary interviewer in scene
                foreach (var ch in all)
                {
                    string id = ch.CharacterId ?? "";
                    if (id == HR_CHARACTER_ID_SCENE || id == HR_CHARACTER_ID_USER ||
                        id == HR_CHARACTER_ID_ACCOUNT || id == HR_CHARACTER_ID_ALT || id == HR_CHARACTER_ID_LEGACY)
                    {
                        interviewer = ch;
                        break;
                    }
                }

                if (interviewer == null)
                {
                    // Match by name or take first active character
                    foreach (var ch in all)
                    {
                        string name = ((ch.CharacterName ?? "") + " " + ch.gameObject.name).ToLowerInvariant();
                        if (name.Contains("hr") || name.Contains("interviewer"))
                        {
                            interviewer = ch;
                            break;
                        }
                    }
                }

                if (interviewer == null)
                {
                    interviewer = all[0];
                }
            }

            Debug.Log($"[ConvaiDebug] Single Interviewer resolved: {interviewer != null} ({interviewer?.gameObject.name} | {interviewer?.CharacterName} | {interviewer?.CharacterId})");

            if (interviewer == null)
            {
                Debug.LogError("[InterviewOrchestrator] No ConvaiCharacter found in InterviewRoom! Halting initialization.");
                isInitializing = false;
                yield break;
            }

            // Attach & calibrate specialized mouth repair controller
            EnsureAvatarMouthController(interviewer, "Interviewer");

            // 2. Locate ConvaiManager
            SetState(InterviewState.Connecting);
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
                if (ConvaiManager.ActiveManager.ConversationTargeting != null)
                {
                    ConvaiManager.ActiveManager.ConversationTargeting.Mode = ConversationTargetingMode.Manual;
                    Debug.Log("[ConvaiDebug] Configured ConversationTargeting to MANUAL mode.");
                }
                ConvaiManager.ActiveManager.SetConversationTargetProvider(this);
            }

            convaiPlayer = FindAnyObjectByType<ConvaiPlayer>();
            Debug.Log($"[ConvaiDebug] ConvaiPlayer found: {convaiPlayer != null}");

            // 3. Attach character event listeners
            WireCharacterDebugEvents(interviewer, "Interviewer");

            // 4. Setup session components and event subscriptions
            SetupTimer();
            SetupCameraController();
            SetupVisualEvaluator();
            SetupRuntimeHUD();
            SubscribeConvaiEvents();
            EnsureMicrophoneConfigured();

            // 5. Await Convai room connection
            yield return WaitForRoomConnection(25f);
            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null || !manager.IsConnected)
            {
                Debug.LogError("[ConvaiDebug] Convai room failed to connect within timeout. Halting interview startup.");
                isInitializing = false;
                yield break;
            }

            Debug.Log("[Interview] C. Convai Manager ready and room connected.");

            // 6. Await interviewer authoritative readiness & session membership
            yield return WaitForAuthoritativeReadiness(interviewer, "Interviewer", 35f);
            if (!IsCharacterAuthoritativelyReady(interviewer))
            {
                Debug.LogError("[ConvaiDebug] Single interviewer failed authoritative readiness check. Halting interview.");
                isInitializing = false;
                yield break;
            }

            Debug.Log("[Interview] D. Single Interviewer ready and joined session.");

            // Disable ConvaiPushToTalkController on ConvaiManager so it never blocks hands-free speech
            if (manager != null)
            {
                var ptt = manager.GetComponent<ConvaiPushToTalkController>();
                if (ptt != null)
                {
                    ptt.enabled = false;
                    Debug.Log("[Interview] Disabled ConvaiPushToTalkController for pure hands-free speech.");
                }
            }

            // Ensure candidate microphone device is configured and listening pipeline started
            EnsureMicrophoneConfigured();
            if (manager?.Audio != null)
            {
                Debug.Log("[MIC] Starting initial Convai microphone capture pipeline (StartListeningAsync)...");
                manager.Audio.StartListeningAsync();
            }
            SetCandidateMicrophoneMuted(false, "Pre-warming microphone for Single Interviewer start");

            // Isolate active interviewer: ensure remote audio enabled & set conversation target
            IsolateActiveInterviewer(interviewer);

            // Focus camera and visual evaluator on the single interviewer
            InterviewCameraController.Instance?.FocusOn(interviewer.transform);
            if (InterviewVisualEvaluator.Instance != null)
            {
                InterviewVisualEvaluator.Instance.SetActiveSpeaker(interviewer.transform);
            }

            // 7. Begin Interview Starting State
            SetState(InterviewState.InterviewStarting);
            interviewActive = true;
            isInitializing = false;

            if (PREPVRSessionManager.Instance != null)
                PREPVRSessionManager.Instance.SetStatus(InterviewSessionStatus.InProgress);

            // 8. Inject dynamic context for the single interviewer conducting the complete 10-minute interview
            ApplySectionDynamicContext(InterviewSection.Welcome_Introduction);

            yield return null;

            // 9. Request opening response from interviewer
            string openingPrompt = "Please begin the interview now. Say: \"Welcome to the interview. Could you tell me your name and walk me through your background?\"";
            Debug.Log("[Interview] Triggering single interviewer opening speech...");
            yield return StartCoroutine(TriggerCharacterResponse(interviewer, HR_TRIGGER_NAME, openingPrompt, "Interviewer"));

            // 10. Start 10-minute timer once interview actually begins
            interviewStartTime = Time.time;
            if (InterviewTimerManager.Instance != null && !InterviewTimerManager.Instance.IsRunning)
            {
                InterviewTimerManager.Instance.StartTimer();
            }

            SetState(InterviewState.InterviewInProgress);
        }

        private void EnsureCandidateProfile()
        {
            if (PREPVRSessionManager.Instance == null) return;
            CandidateProfile p = PREPVRSessionManager.Instance.Profile;

            // 1. If not already loaded, try loading uploaded resume via PythonBackendBridge ONLY if explicitly provided by user
            if ((p == null || p.skills == null || p.skills.Count == 0 || p.projects == null || p.projects.Count == 0) &&
                !string.IsNullOrWhiteSpace(PREPVRSessionManager.Instance.ResumePdfPath))
            {
                string resumePath = PREPVRSessionManager.Instance.ResumePdfPath;
                if (File.Exists(resumePath))
                {
                    Debug.Log($"[RESUME] Attempting to extract candidate profile from user-selected resume: {resumePath}");
                    if (PythonBackendBridge.TryProcessResume(resumePath, out CandidateProfile parsedProfile, out _, out string parseError))
                    {
                        if (parsedProfile != null && parsedProfile.skills != null && parsedProfile.skills.Count > 0)
                        {
                            p = parsedProfile;
                            PREPVRSessionManager.Instance.Profile = p;
                            isResumeFallbackActive = false;
                            Debug.Log($"[RESUME] Successfully parsed candidate profile from {Path.GetFileName(resumePath)}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[RESUME] PythonBackendBridge parse warning on {resumePath}: {parseError}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[RESUME] User-specified resume file not found at: {resumePath}");
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
                    isResumeFallbackActive = false;
                    Debug.Log($"[InterviewOrchestrator] Adopted CandidateProfile from scene InterviewSession: {p.name}");
                }
            }

            // 3. Fallback if resume extraction was unavailable
            if (p == null) p = new CandidateProfile();
            if (string.IsNullOrWhiteSpace(p.name) || p.name == "Candidate")
            {
                p.name = ""; // Captured dynamically from spoken introduction
            }

            if (p.skills == null || p.skills.Count == 0)
            {
                isResumeFallbackActive = true;
                Debug.LogWarning("[RESUME] Resume extraction unavailable. Gracefully falling back to general interview questions without fabricated credentials.");
                p.name = "";
                p.education = "Higher Education / Technical Degree";
                p.skills = new List<string>();
                p.projects = new List<string>();
                p.experience = new List<string>();
                p.certifications = new List<string>();
                p.achievements = new List<string>();
                PREPVRSessionManager.Instance.Profile = p;
            }

            Debug.Log($"[RESUME]\nCandidate profile created:\nName: {(string.IsNullOrWhiteSpace(p.name) ? "(Awaiting Spoken Introduction)" : p.name)}\nEducation: {p.education}\nSkills: {string.Join(", ", p.skills)}\nProjects: {string.Join(" | ", p.projects)}\nExperience: {(p.experience != null && p.experience.Count > 0 ? string.Join(" | ", p.experience) : "None")}\nFallback Mode: {isResumeFallbackActive}");
        }

        private const string NATURAL_SPEECH_DELIVERY_INSTRUCTION =
            "DELIVERY & PACING STYLE: Speak calmly, clearly, professionally, and conversationally at a measured, deliberate pace (approx 0.88x speed). " +
            "Use natural, concise sentences and brief conversational pauses. " +
            "Avoid long monologues, repeated phrases, or robotic statements. " +
            "Respond thoughtfully and directly to what the candidate actually just said. " +
            "Ask exactly ONE clear question at a time, then stop speaking immediately and wait for the candidate to answer.";

        private void ApplySectionDynamicContext(InterviewSection section)
        {
            if (interviewer?.DynamicContext == null) return;
            CandidateProfile profile = PREPVRSessionManager.Instance?.Profile;
            string candName = !string.IsNullOrWhiteSpace(profile?.name) ? profile.name : "";
            string primaryProj = GetPrimaryProject(profile);
            string primarySkills = GetPrimarySkills(profile);
            string resumeSummary = BuildResumeSummary(profile);

            string sectionInstruction = "";
            switch (section)
            {
                case InterviewSection.Welcome_Introduction:
                    sectionInstruction =
                        "CURRENT SECTION: 1. Welcome & Introduction (00:00 - 01:00). " +
                        "You are conducting a complete, professional 10-minute one-on-one interview from beginning to end. " +
                        $"{NATURAL_SPEECH_DELIVERY_INSTRUCTION} " +
                        "1. Warmly welcome the candidate to the interview. " +
                        "2. Wait for the candidate to introduce themselves and tell you their name. " +
                        "3. Acknowledge what they share and listen attentively.";
                    break;

                case InterviewSection.Background_Education_HR:
                    sectionInstruction =
                        "CURRENT SECTION: 2. Background, Education & HR Questions (01:00 - 03:00). " +
                        $"{NATURAL_SPEECH_DELIVERY_INSTRUCTION} " +
                        "1. Ask questions regarding their academic background, career motivation, personal work ethic, and communication skills. " +
                        "2. Ground your questions directly in their spoken responses and resume background. " +
                        "3. Ask ONE clear question at a time and wait for their answer.";
                    break;

                case InterviewSection.Resume_Technical_Skills:
                    sectionInstruction =
                        "CURRENT SECTION: 3. Resume-Based Skills & Technical Questions (03:00 - 06:00). " +
                        $"{NATURAL_SPEECH_DELIVERY_INSTRUCTION} " +
                        (isResumeFallbackActive
                            ? "Resume data was unavailable, so ask about core software engineering fundamentals, data structures, APIs, and modern programming practices. "
                            : $"Directly explore the candidate's actual resume skills ({primarySkills}) and their primary technical project ({primaryProj}). ") +
                        "1. Ask about technical architecture, choice of programming languages, libraries, data storage, and engineering trade-offs. " +
                        "2. Ask probing follow-up questions about the technical choices they explain. " +
                        "3. Ask ONE clear question at a time and wait for their response.";
                    break;

                case InterviewSection.Projects_Problem_Solving:
                    sectionInstruction =
                        "CURRENT SECTION: 4. Project Discussion & Problem-Solving (06:00 - 08:00). " +
                        $"{NATURAL_SPEECH_DELIVERY_INSTRUCTION} " +
                        (isResumeFallbackActive
                            ? "Ask the candidate to walk through a significant project they built, focusing on problem-solving, architectural hurdles, and debugging complex bugs. "
                            : $"Dive deep into {primaryProj}. Ask about challenging bugs, debugging strategies, performance bottlenecks, edge cases, and their specific personal contribution. ") +
                        "1. Ask how they arrived at design decisions and what they learned. " +
                        "2. Ask ONE clear question at a time and wait for their response.";
                    break;

                case InterviewSection.Behavioural_Situational:
                    sectionInstruction =
                        "CURRENT SECTION: 5. Behavioural & Situational Questions (STAR Framework) (08:00 - 09:30). " +
                        $"{NATURAL_SPEECH_DELIVERY_INSTRUCTION} " +
                        "1. Ask situational questions evaluating teamwork, handling interpersonal conflict, managing tight deadlines, taking accountability, and adaptability when project requirements change. " +
                        "2. Evaluate their responses using the STAR method (Situation, Task, Action, Result). " +
                        "3. Ask ONE clear question at a time and wait for their response.";
                    break;

                case InterviewSection.Final_Question_Closing:
                    sectionInstruction =
                        "CURRENT SECTION: 6. Final Question & Professional Closing (09:30 - 10:00). " +
                        $"{NATURAL_SPEECH_DELIVERY_INSTRUCTION} " +
                        "The 10-minute interview is wrapping up. " +
                        "Ask if the candidate has anything important to add or any questions for you. " +
                        "After their brief response, deliver a warm, polite closing statement thanking them for their time.";
                    break;
            }

            try
            {
                var dict = new Dictionary<string, string>
                {
                    { "interview_type", "Comprehensive 10-Minute Technical & HR Interview" },
                    { "interviewer_role", "Lead AI Interviewer" },
                    { "candidate_name", candName },
                    { "resume_context", isResumeFallbackActive ? "Personalized resume data temporarily unavailable; using general technical and behavioral context." : resumeSummary },
                    { "current_section", section.ToString() },
                    { "instructions", sectionInstruction }
                };

                interviewer.DynamicContext.SetStates(dict, ConvaiRespondMode.Silent);
                interviewer.DynamicContext.Flush();
                Debug.Log($"[AI] Context updated for Section {section} (Resume Fallback: {isResumeFallbackActive}).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AI] Error updating interviewer DynamicContext: {ex.Message}");
            }
        }

        private readonly Dictionary<ConvaiCharacter, Action> charReadyActions = new();
        private readonly Dictionary<ConvaiCharacter, Action> charSpeechStartActions = new();
        private readonly Dictionary<ConvaiCharacter, Action> charSpeechStopActions = new();
        private readonly Dictionary<ConvaiCharacter, Action<bool>> charTurnCompleteActions = new();

        private void WireCharacterDebugEvents(ConvaiCharacter ch, string role)
        {
            if (ch == null) return;
            UnwireCharacterDebugEvents(ch);

            Action onReady = () =>
            {
                Debug.Log($"[ConvaiDebug] {role} character initialized (OnCharacterReady event)");
                Debug.Log("[Interview] D. Single Interviewer character ready");
            };
            Action onSpeechStart = () =>
            {
                Debug.Log($"[AUDIO] Interviewer [{role}] audio response started at {AuthoritativeElapsedTime:F2}s");
                StartInterviewerSpeakingTurn(ch, role);
            };
            Action onSpeechStop = () =>
            {
                Debug.Log($"[ConvaiDebug] Character audio completed ({role}) at {AuthoritativeElapsedTime:F2}s");
                OnInterviewerAudioStopped(ch, role, $"ConvaiCharacter.OnSpeechStopped({role})");
            };
            Action<bool> onTurnComplete = (wasInterrupted) =>
            {
                Debug.Log($"[AUDIO] Character.OnTurnCompleted received ({role}, interrupted={wasInterrupted}) at {AuthoritativeElapsedTime:F2}s");
                OnInterviewerTurnCompleted(ch, role, wasInterrupted, $"ConvaiCharacter.OnTurnCompleted({role})");
            };

            charReadyActions[ch] = onReady;
            charSpeechStartActions[ch] = onSpeechStart;
            charSpeechStopActions[ch] = onSpeechStop;
            charTurnCompleteActions[ch] = onTurnComplete;

            ch.OnCharacterReady += onReady;
            ch.OnSpeechStarted += onSpeechStart;
            ch.OnSpeechStopped += onSpeechStop;
            ch.OnTurnCompleted += onTurnComplete;
        }

        private void UnwireCharacterDebugEvents(ConvaiCharacter ch)
        {
            if (ch == null) return;
            if (charReadyActions.TryGetValue(ch, out var onReady))
            {
                ch.OnCharacterReady -= onReady;
                charReadyActions.Remove(ch);
            }
            if (charSpeechStartActions.TryGetValue(ch, out var onSpeechStart))
            {
                ch.OnSpeechStarted -= onSpeechStart;
                charSpeechStartActions.Remove(ch);
            }
            if (charSpeechStopActions.TryGetValue(ch, out var onSpeechStop))
            {
                ch.OnSpeechStopped -= onSpeechStop;
                charSpeechStopActions.Remove(ch);
            }
            if (charTurnCompleteActions.TryGetValue(ch, out var onTurnComplete))
            {
                ch.OnTurnCompleted -= onTurnComplete;
                charTurnCompleteActions.Remove(ch);
            }
        }

        private void EnsureMicrophoneConfigured()
        {
            try
            {
                string[] devices = Microphone.devices;
                int count = devices != null ? devices.Length : 0;
                Debug.Log("[MIC] --- Microphone Discovery & Initialization Audit ---");
                Debug.Log($"[MIC] Total available recording devices detected: {count}");

                if (count == 0)
                {
                    Debug.LogWarning("[MIC] No recording microphone devices detected by Unity Microphone.devices! Please check hardware and Windows audio settings.");
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    Debug.Log($"[MIC] Device [{i}]: \"{devices[i]}\"");
                }

                string selectedMode = PREPVRSessionManager.Instance?.SelectedMode ?? "DESK";
                string targetDevice = devices[0];

                bool isOculusVirtual = targetDevice.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       targetDevice.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0;

                if (selectedMode == "DESK" || Application.isEditor)
                {
                    if (isOculusVirtual)
                    {
                        Debug.LogWarning($"[MIC] Default recording device is \"{targetDevice}\". Searching for physical fallback microphone...");
                        string physicalCandidate = null;
                        for (int i = 0; i < count; i++)
                        {
                            string d = devices[i];
                            if (d.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) < 0 &&
                                d.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                physicalCandidate = d;
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(physicalCandidate))
                        {
                            targetDevice = physicalCandidate;
                            Debug.Log($"[MIC] Selected physical fallback microphone: \"{targetDevice}\"");
                        }
                    }
                }

                ConvaiManager manager = ConvaiManager.ActiveManager;
                if (manager != null && manager.TryGetRuntimeSettingsService(out IConvaiRuntimeSettingsService runtimeSettings))
                {
                    Debug.Log($"[MIC] Applying preferred microphone device to Convai RuntimeSettings: \"{targetDevice}\"");
                    runtimeSettings.Apply(new ConvaiRuntimeSettingsPatch
                    {
                        PreferredMicrophoneDeviceId = targetDevice
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MIC] EnsureMicrophoneConfigured warning: {ex.Message}");
            }
        }

        public void StartInterviewerSpeakingTurn(ConvaiCharacter character, string speakerRole)
        {
            lock (turnLock)
            {
                if (isInterviewerSpeakingTurn && (character == interviewer || character == null))
                {
                    return;
                }

                currentTurnId++;
                isInterviewerSpeakingTurn = true;
                characterAudioCompletedReceived = false;
                characterTurnCompletedReceived = false;
                lastInterviewerSpeechStartTime = Time.time;
                lastInterviewerSpeechStopTime = 0f;
                currentTurnQuestionBuilder.Clear();

                if (currentTurnState != TurnState.ClosingSpeaking)
                {
                    currentTurnState = TurnState.InterviewerSpeaking;
                }
            }

            SetCandidateMicrophoneMuted(true, $"Interviewer turn #{currentTurnId} started");
            Debug.Log($"[TURN] Interviewer response started (Turn #{currentTurnId}) at {AuthoritativeElapsedTime:F2}s");

            if (turnCompletionCoroutine != null) StopCoroutine(turnCompletionCoroutine);
            if (turnGraceTimeoutCoroutine != null) StopCoroutine(turnGraceTimeoutCoroutine);
            turnCompletionCoroutine = StartCoroutine(WatchdogTurnCompletionCoroutine(character, speakerRole, currentTurnId));
        }

        private float lastAudioStoppedEventTime = 0f;
        private float lastTurnCompletedEventTime = 0f;

        public void OnInterviewerAudioStopped(ConvaiCharacter ch, string role, string source)
        {
            if (!isInterviewerSpeakingTurn) return;

            float now = Time.time;
            if (now - lastAudioStoppedEventTime < 0.1f) return;
            lastAudioStoppedEventTime = now;
            lastInterviewerSpeechStopTime = now;
            characterAudioCompletedReceived = true;

            Debug.Log($"[AUDIO_LIFECYCLE] Interviewer audio stopped (Source: {source}, TurnId: #{currentTurnId}, TurnCompleted: {characterTurnCompletedReceived}) at {AuthoritativeElapsedTime:F2}s");

            if (characterTurnCompletedReceived)
            {
                CompleteInterviewerSpeakingTurn(ch, role, $"{source} (TurnCompleted already confirmed)");
                return;
            }

            // Start safety silence watchdog in case TurnCompleted does not arrive
            if (turnGraceTimeoutCoroutine != null) StopCoroutine(turnGraceTimeoutCoroutine);
            turnGraceTimeoutCoroutine = StartCoroutine(GraceTimeoutCompleteTurnCoroutine(ch, role, currentTurnId, 3.5f, "SpeechStopped safety silence window elapsed"));
        }

        public void OnInterviewerTurnCompleted(ConvaiCharacter ch, string role, bool wasInterrupted, string source)
        {
            if (!isInterviewerSpeakingTurn) return;

            float now = Time.time;
            if (now - lastTurnCompletedEventTime < 0.1f) return;
            lastTurnCompletedEventTime = now;
            characterTurnCompletedReceived = true;

            Debug.Log($"[AUDIO_LIFECYCLE] Interviewer turn completed received (Source: {source}, Interrupted: {wasInterrupted}, AudioStopped: {characterAudioCompletedReceived}) at {AuthoritativeElapsedTime:F2}s");

            if (wasInterrupted)
            {
                CompleteInterviewerSpeakingTurn(ch, role, $"{source} (Turn interrupted)");
                return;
            }

            bool isStillSpeaking = ch != null && ch.IsSpeaking;
            if (characterAudioCompletedReceived || !isStillSpeaking)
            {
                CompleteInterviewerSpeakingTurn(ch, role, $"{source} (Audio already stopped / confirmed idle)");
                return;
            }

            // Audio might be finishing final syllables (e.g. 200-400ms)
            if (turnGraceTimeoutCoroutine != null) StopCoroutine(turnGraceTimeoutCoroutine);
            turnGraceTimeoutCoroutine = StartCoroutine(GraceTimeoutCompleteTurnCoroutine(ch, role, currentTurnId, 0.7f, "TurnCompleted audio tail grace window elapsed"));
        }

        private IEnumerator GraceTimeoutCompleteTurnCoroutine(ConvaiCharacter ch, string role, int turnId, float delaySeconds, string reason)
        {
            yield return new WaitForSeconds(delaySeconds);

            if (isInterviewerSpeakingTurn && currentTurnId == turnId)
            {
                bool isStillSpeaking = ch != null && ch.IsSpeaking;
                if (!isStillSpeaking)
                {
                    CompleteInterviewerSpeakingTurn(ch, role, reason);
                }
                else
                {
                    Debug.Log($"[AUDIO_LIFECYCLE] Grace timeout expired for turn #{turnId}, but character is still actively speaking. Allowing natural speech completion.");
                }
            }
        }

        private IEnumerator WatchdogTurnCompletionCoroutine(ConvaiCharacter character, string speakerRole, int turnId)
        {
            float maxTurnDuration = 30f; // 30s max bounded turn safety
            float elapsed = 0f;

            while (elapsed < maxTurnDuration)
            {
                yield return new WaitForSeconds(0.2f);
                elapsed += 0.2f;

                if (!isInterviewerSpeakingTurn || currentTurnId != turnId)
                {
                    yield break;
                }

                if (characterAudioCompletedReceived && characterTurnCompletedReceived)
                {
                    CompleteInterviewerSpeakingTurn(character, speakerRole, "Both audio stopped and turn completed confirmed");
                    yield break;
                }

                if (characterTurnCompletedReceived && characterAudioCompletedReceived && lastInterviewerSpeechStopTime > 0f && (Time.time - lastInterviewerSpeechStopTime >= 0.4f))
                {
                    CompleteInterviewerSpeakingTurn(character, speakerRole, "TurnCompleted and audio stopped confirmed");
                    yield break;
                }

                if (candidateAnswerBuffer.Length > 0 && characterAudioCompletedReceived && characterTurnCompletedReceived)
                {
                    CompleteInterviewerSpeakingTurn(character, speakerRole, "Candidate speech detected while interviewer quiet");
                    yield break;
                }
            }

            if (isInterviewerSpeakingTurn && currentTurnId == turnId)
            {
                Debug.LogWarning($"[TURN_{turnId}] Fallback turn duration timeout reached for interviewer. Forcing turn completion.");
                CompleteInterviewerSpeakingTurn(character, speakerRole, $"Watchdog timeout ({maxTurnDuration}s)");
            }
        }

        public void CompleteInterviewerSpeakingTurn(ConvaiCharacter character, string speakerRole, string completionSource)
        {
            lock (turnLock)
            {
                if (!isInterviewerSpeakingTurn) return;

                isInterviewerSpeakingTurn = false;

                if (turnCompletionCoroutine != null)
                {
                    StopCoroutine(turnCompletionCoroutine);
                    turnCompletionCoroutine = null;
                }
                if (turnGraceTimeoutCoroutine != null)
                {
                    StopCoroutine(turnGraceTimeoutCoroutine);
                    turnGraceTimeoutCoroutine = null;
                }
            }

            string fullQuestion = currentTurnQuestionBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(fullQuestion))
            {
                fullQuestion = lastRecordedQuestion;
            }

            lastRecordedQuestion = fullQuestion;
            Debug.Log($"[AUDIO] Interviewer finished speaking. (Source: {completionSource}) at {AuthoritativeElapsedTime:F2}s");
            Debug.Log($"[QUESTION] Generated question/statement: \"{fullQuestion}\"");

            // Record transcript
            if (PREPVRSessionManager.Instance != null && !string.IsNullOrWhiteSpace(fullQuestion))
            {
                PREPVRSessionManager.Instance.RecordTranscript("Interviewer", fullQuestion, isQuestion: true);
            }

            if (InterviewVisualEvaluator.Instance != null)
            {
                InterviewVisualEvaluator.Instance.RecordQuestionEnded(AuthoritativeElapsedTime);
            }

            // Handle special states
            if (currentState == InterviewState.ClosingConversation || currentTurnState == TurnState.ClosingSpeaking)
            {
                Debug.Log("[Interview] Interviewer finished speaking final closing statement. Entering WaitingForClosingAudio state.");
                SetState(InterviewState.WaitingForClosingAudio);
                StartCoroutine(WaitForClosingAudioAndCompleteCoroutine());
                return;
            }

            totalQuestionsAsked++;
            currentSectionQuestionCount++;
            interviewTranscriptLines.Add($"Interviewer: {fullQuestion}");

            // Candidate turn begins -> unmute microphone
            Debug.Log("[TURN] Interviewer finished. Candidate microphone ACTIVE.");
            currentTurnState = TurnState.WaitingForCandidate;
            SetCandidateMicrophoneMuted(false, "Interviewer finished speaking; candidate may respond");

            // Enforce clean neutral idle mouth pose on completed speaker
            interviewer?.GetComponent<InterviewAvatarMouthController>()?.EnforceNeutralMouthPoseImmediate();

            // Fire event
            OnInterviewerQuestionFinished?.Invoke("Interviewer", fullQuestion);
        }

        private void EnsureAvatarMouthController(ConvaiCharacter character, string role)
        {
            if (character == null) return;
            var controller = character.GetComponent<InterviewAvatarMouthController>();
            if (controller == null)
            {
                controller = character.gameObject.AddComponent<InterviewAvatarMouthController>();
            }
            controller.InitializeComponents();
        }

        private void IsolateActiveInterviewer(ConvaiCharacter active)
        {
            if (active == null) return;
            try
            {
                if (!active.IsRemoteAudioEnabled) active.EnableRemoteAudio();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConvaiDebug] Failed enabling remote audio on {active.CharacterName}: {ex.Message}");
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

        private string GetPrimarySkills(CandidateProfile profile)
        {
            if (profile?.skills != null && profile.skills.Count > 0)
            {
                int count = Math.Min(profile.skills.Count, 4);
                return string.Join(", ", profile.skills.GetRange(0, count));
            }
            return "software engineering and technical design";
        }

        private void SafeSetConversationTarget(ConvaiCharacter target)
        {
            if (target == null) return;
            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null) return;

            if (manager.ConversationTargeting != null)
            {
                manager.ConversationTargeting.Mode = ConversationTargetingMode.Manual;
            }

            if (manager.ConversationTarget == target || manager.AddressedCharacter == target)
            {
                return;
            }

            Debug.Log($"[ConvaiDebug] Addressing {target.CharacterName} ({target.CharacterId}) via manager.TalkTo.");
            manager.TalkTo(target);
        }

        private IEnumerator TriggerCharacterResponse(ConvaiCharacter target, string triggerName, string prompt, string roleLabel)
        {
            if (target == null) yield break;

            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null || !manager.IsConnected)
            {
                Debug.LogError($"[Interview] Cannot trigger response: ConvaiManager is not connected.");
                yield break;
            }

            try
            {
                if (!target.IsRemoteAudioEnabled) target.EnableRemoteAudio();
            }
            catch { }

            SafeSetConversationTarget(target);
            yield return WaitForConversationInputAvailability(10f);
            yield return StartCoroutine(SendPlayerAgentPrompt(target, prompt, roleLabel));
        }

        private IEnumerator SendPlayerAgentPrompt(ConvaiCharacter target, string prompt, string roleLabel)
        {
            if (target == null) yield break;

            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null || !manager.IsConnected) yield break;

            try
            {
                if (!target.IsRemoteAudioEnabled) target.EnableRemoteAudio();
            }
            catch { }

            if (convaiPlayer == null) convaiPlayer = FindAnyObjectByType<ConvaiPlayer>();
            if (convaiPlayer == null)
            {
                Debug.LogError("[Interview] ConvaiPlayer not found in scene!");
                yield break;
            }

            SafeSetConversationTarget(target);
            yield return WaitForConversationInputAvailability(10f);

            bool sent = false;
            string reason = null;
            float retryTimeout = 10f;
            float elapsed = 0f;

            while (!sent && elapsed < retryTimeout)
            {
                if (manager.ConversationAvailability.CanAcceptPlayerInput())
                {
                    sent = convaiPlayer.TrySendTextMessage(prompt, out reason);
                    if (sent)
                    {
                        Debug.Log($"[Interview] Prompt request successfully delivered to Convai runtime: \"{prompt}\"");
                        break;
                    }
                }
                else
                {
                    reason = $"ConversationAvailability ({manager.ConversationAvailability}) not ready yet";
                }

                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            if (!sent)
            {
                Debug.LogError($"[Interview] Failed to send prompt after {retryTimeout}s: {reason}");
            }
        }

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
            if (character == null) yield break;

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

            if (!IsCharacterAuthoritativelyReady(character))
            {
                Debug.LogError($"[ConvaiDebug] Timeout waiting for {label} readiness (waited {timeoutSeconds}s).");
            }
        }

        private IEnumerator WaitForConversationInputAvailability(float timeoutSeconds = 10f)
        {
            ConvaiManager manager = ConvaiManager.ActiveManager;
            if (manager == null) yield break;

            yield return null;

            if (manager.ConversationAvailability.CanAcceptPlayerInput()) yield break;

            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (manager.ConversationAvailability.CanAcceptPlayerInput()) yield break;
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            Debug.LogWarning($"[ConvaiDebug] Timed out waiting for conversation availability ({timeoutSeconds}s). Current: {manager.ConversationAvailability}");
        }

        private bool MatchesCharacter(string charId, string speaker, ConvaiCharacter target)
        {
            if (target == null) return false;
            if (!string.IsNullOrEmpty(charId) && string.Equals(charId.Trim(), target.CharacterId?.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(speaker))
            {
                string sTrim = speaker.Trim();
                if (!string.IsNullOrEmpty(target.CharacterName) && sTrim.IndexOf(target.CharacterName.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (target.gameObject != null && target.gameObject.name.Trim().IndexOf(sTrim, StringComparison.OrdinalIgnoreCase) >= 0)
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

            if (interviewer != null && !MatchesCharacter(charId, speaker, interviewer)) return;

            lastRecordedQuestion = text;

            if (!isInterviewerSpeakingTurn && interviewer != null)
            {
                StartInterviewerSpeakingTurn(interviewer, speaker);
            }

            if (e.Message.IsFinal)
            {
                string chunk = text.Trim();
                string current = currentTurnQuestionBuilder.ToString();
                if (!current.Contains(chunk))
                {
                    currentTurnQuestionBuilder.Append(chunk + " ");
                    Debug.Log($"[QUESTION] Streamed sentence chunk: \"{chunk}\"");
                }
            }
        }

        private void HandlePlayerTranscript(PlayerTranscriptReceived e)
        {
            if (!interviewActive) return;

            string text = e.Message.Text ?? "";
            if (string.IsNullOrWhiteSpace(text)) return;

            bool isFinal = e.Message.IsFinal || e.Phase != TranscriptionPhase.Interim;
            if (!isFinal) return;

            // Ignore system prompts echoed into player transcript
            if (text.StartsWith("[PROMPT]") ||
                text.IndexOf("In your persona as the", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("The candidate explained:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("The candidate answered:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("The candidate introduced:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Please begin the interview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Welcome to the interview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("tell me your name", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            Debug.Log("[STT] Candidate speech detected");
            Debug.Log($"[STT] Transcript: \"{text}\" (Phase: {e.Phase}, Final: {isFinal}) at {AuthoritativeElapsedTime:F2}s");

            if (isInterviewerSpeakingTurn)
            {
                CompleteInterviewerSpeakingTurn(interviewer, "Interviewer", "Candidate player transcript received");
            }

            string chunk = text.Trim();
            string currentBuf = candidateAnswerBuffer.ToString();
            if (!currentBuf.Contains(chunk))
            {
                candidateAnswerBuffer.Append(chunk + " ");
            }

            currentTurnState = TurnState.CandidateSpeaking;

            if (candidateAnswerDebounceCoroutine != null)
            {
                StopCoroutine(candidateAnswerDebounceCoroutine);
            }
            candidateAnswerDebounceCoroutine = StartCoroutine(ProcessCandidateAnswerDebouncedCoroutine());
        }

        private IEnumerator ProcessCandidateAnswerDebouncedCoroutine()
        {
            // 1.4 seconds of silence to allow natural pause without cutting off complete thoughts
            yield return new WaitForSeconds(1.4f);

            string fullAnswer = candidateAnswerBuffer.ToString().Trim();
            candidateAnswerBuffer.Clear();
            candidateAnswerDebounceCoroutine = null;

            if (string.IsNullOrWhiteSpace(fullAnswer)) yield break;

            currentTurnState = TurnState.ProcessingAnswer;
            SetCandidateMicrophoneMuted(true, "Candidate finished speaking; processing answer");
            Debug.Log($"[ANSWER] Complete candidate answer received: \"{fullAnswer}\"");

            // Extract candidate name from spoken introduction if available
            if (PREPVRSessionManager.Instance != null)
            {
                CandidateProfile prof = PREPVRSessionManager.Instance.Profile;
                if (prof == null || string.IsNullOrWhiteSpace(prof.name) || prof.name == "Candidate")
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
                        Debug.Log($"[CANDIDATE] Spoken candidate name captured: {spokenName}");
                    }
                }
            }

            totalCandidateAnswers++;
            currentSectionAnswerCount++;
            interviewTranscriptLines.Add($"Candidate: {fullAnswer}");

            if (PREPVRSessionManager.Instance != null)
            {
                PREPVRSessionManager.Instance.RecordTranscript("Candidate", fullAnswer, isQuestion: false);
                PREPVRSessionManager.Instance.RecordQAResponse(lastRecordedQuestion, fullAnswer);
            }

            if (InterviewVisualEvaluator.Instance != null)
            {
                float now = PREPVRSessionManager.Instance?.InterviewDurationSeconds ?? 0f;
                InterviewVisualEvaluator.Instance.RecordAnswerStarted(now);
            }

            float elapsed = AuthoritativeElapsedTime;

            // ---------------------------------------------------------------
            // State-Specific Routing & Phase Transitions
            // ---------------------------------------------------------------
            if (currentState == InterviewState.FinalQuestion)
            {
                Debug.Log($"[INTERVIEW] Candidate completed response to final wrap-up question. Initiating closing conversation.");
                StartCoroutine(InitiateClosingConversation());
                yield break;
            }

            // Approaching 9:30 (570s): Transition to FinalQuestion state
            if (elapsed >= SECTION_5_END_SECONDS || elapsed >= (TOTAL_INTERVIEW_SECONDS - 30f))
            {
                Debug.Log($"[INTERVIEW] 9:30 boundary reached ({elapsed:F1}s). Candidate completed answer. Initiating FinalQuestion phase.");
                StartCoroutine(InitiateFinalQuestionPhase());
                yield break;
            }

            // Update DynamicContext for current section guide
            InterviewSection currentSec = currentSection;
            if (elapsed >= SECTION_4_END_SECONDS) currentSec = InterviewSection.Behavioural_Situational;
            else if (elapsed >= SECTION_3_END_SECONDS) currentSec = InterviewSection.Projects_Problem_Solving;
            else if (elapsed >= SECTION_2_END_SECONDS) currentSec = InterviewSection.Resume_Technical_Skills;
            else if (elapsed >= SECTION_1_END_SECONDS) currentSec = InterviewSection.Background_Education_HR;

            SetSection(currentSec);
            ApplySectionDynamicContext(currentSec);

            // Construct tailored authoritative follow-up prompt based on the active section
            CandidateProfile profile = PREPVRSessionManager.Instance?.Profile;
            string primaryProject = GetPrimaryProject(profile);
            string primarySkills = GetPrimarySkills(profile);
            string followUpPrompt = "";

            switch (currentSec)
            {
                case InterviewSection.Welcome_Introduction:
                case InterviewSection.Background_Education_HR:
                    if (totalCandidateAnswers <= 1)
                    {
                        followUpPrompt = isResumeFallbackActive
                            ? $"The candidate just introduced themselves: \"{fullAnswer}\". In your persona as the lead interviewer, warmly acknowledge what they shared, welcome them, and ask your next question exploring their background, education, and interest in technology. Ask ONE clear question."
                            : $"The candidate just introduced themselves: \"{fullAnswer}\". In your persona as the lead interviewer, warmly acknowledge what they shared, welcome them, and ask your next question exploring what motivated their technical projects such as {primaryProject} or their work with {primarySkills}. Ask ONE clear question.";
                    }
                    else
                    {
                        followUpPrompt = $"The candidate just answered: \"{fullAnswer}\". In your persona as the lead interviewer, directly acknowledge what they explained, and ask a relevant follow-up question probing what they just shared regarding their technical education, motivations, or career aspirations. Ask ONE clear question.";
                    }
                    break;

                case InterviewSection.Resume_Technical_Skills:
                    if (currentSectionAnswerCount <= 1)
                    {
                        followUpPrompt = isResumeFallbackActive
                            ? $"The candidate just answered: \"{fullAnswer}\". In your persona as the lead interviewer, directly acknowledge their response, and ask an in-depth technical follow-up question probing how they design system architectures and structure modular code. Ask ONE clear question."
                            : $"The candidate just answered: \"{fullAnswer}\". In your persona as the lead interviewer, directly acknowledge what they explained, and ask an in-depth technical follow-up question probing the architecture, data flow, or choice of technology stack in {primaryProject} using {primarySkills}. Ask ONE clear question.";
                    }
                    else
                    {
                        followUpPrompt = isResumeFallbackActive
                            ? $"The candidate just answered: \"{fullAnswer}\". In your persona as the lead interviewer, directly acknowledge what they explained, and ask a technical follow-up question about API design, caching, data persistence, and error handling. Ask ONE clear question."
                            : $"The candidate just answered: \"{fullAnswer}\". In your persona as the lead interviewer, directly acknowledge what they explained, and ask a technical follow-up question probing how they handled database schema design, API integration, or system performance in {primaryProject}. Ask ONE clear question.";
                    }
                    break;

                case InterviewSection.Projects_Problem_Solving:
                    if (currentSectionAnswerCount <= 1)
                    {
                        followUpPrompt = isResumeFallbackActive
                            ? $"The candidate just answered: \"{fullAnswer}\". In your persona as the lead interviewer, directly acknowledge their answer, and ask them to describe the most challenging technical bug or architectural obstacle they faced and how they diagnosed it. Ask ONE clear question."
                            : $"The candidate just answered: \"{fullAnswer}\". In your persona as the lead interviewer, directly acknowledge their answer, and ask an in-depth problem-solving question about a difficult technical challenge, edge case, or trade-off they resolved while building {primaryProject}. Ask ONE clear question.";
                    }
                    else
                    {
                        followUpPrompt = $"The candidate just answered: \"{fullAnswer}\". In your persona as the lead interviewer, directly acknowledge their answer, and ask a relevant follow-up question probing their specific personal contributions, debugging methodology, and what they learned from that challenge. Ask ONE clear question.";
                    }
                    break;

                case InterviewSection.Behavioural_Situational:
                default:
                    if (currentSectionAnswerCount <= 1)
                    {
                        followUpPrompt = $"The candidate just answered: \"{fullAnswer}\". In your persona as the lead interviewer, directly acknowledge what they explained, and ask a behavioural STAR question about a time they had to collaborate with a difficult team member or resolve a disagreement on technical direction. Ask ONE clear question.";
                    }
                    else
                    {
                        followUpPrompt = $"The candidate just answered: \"{fullAnswer}\". In your persona as the lead interviewer, directly acknowledge their response, and ask a situational question about managing tight deadlines, handling shifting requirements, or taking accountability when a technical issue occurred. Ask ONE clear question.";
                    }
                    break;
            }

            if (interviewer != null)
            {
                Debug.Log($"[CONVAI] Dispatching tailored follow-up prompt for Section {currentSec} (Answer #{totalCandidateAnswers})");
                StartCoroutine(SendPlayerAgentPrompt(interviewer, followUpPrompt, "Interviewer"));
            }
        }

        private IEnumerator InitiateFinalQuestionPhase()
        {
            if (currentState == InterviewState.FinalQuestion ||
                currentState == InterviewState.ClosingConversation ||
                currentState == InterviewState.WaitingForClosingAudio ||
                currentState == InterviewState.InterviewCompleted)
            {
                yield break;
            }

            SetState(InterviewState.FinalQuestion);
            SetSection(InterviewSection.Final_Question_Closing);
            ApplySectionDynamicContext(InterviewSection.Final_Question_Closing);

            string finalQuestionPrompt =
                "[PROMPT] We are at 9 minutes and 30 seconds into the interview. " +
                "As the interviewer, begin wrapping up smoothly. Say: " +
                "\"We are approaching the end of our scheduled interview time today. Do you have anything important you would like to add, or any final questions for me before we conclude?\"";

            if (interviewer != null)
            {
                Debug.Log("[INTERVIEW] Dispatching Final Question to interviewer.");
                yield return StartCoroutine(SendPlayerAgentPrompt(interviewer, finalQuestionPrompt, "Interviewer"));
            }
        }

        private IEnumerator InitiateClosingConversation()
        {
            if (currentState == InterviewState.ClosingConversation ||
                currentState == InterviewState.WaitingForClosingAudio ||
                currentState == InterviewState.InterviewCompleted)
            {
                yield break;
            }

            SetState(InterviewState.ClosingConversation);
            currentTurnState = TurnState.ClosingSpeaking;

            CandidateProfile profile = PREPVRSessionManager.Instance?.Profile;
            string candName = !string.IsNullOrWhiteSpace(profile?.name) ? profile.name : "there";

            string closingPrompt =
                $"[PROMPT] The interview has reached its scheduled conclusion. " +
                "Deliver your final warm, professional closing statement now: " +
                "\"Thank you for taking the time to speak with me today. It was great learning about your background, skills and experiences. That brings us to the end of our interview. We appreciate your participation, and your interview feedback will be available shortly. Have a wonderful day!\"";

            if (interviewer != null)
            {
                Debug.Log("[INTERVIEW] Dispatching professional closing statement to interviewer.");
                yield return StartCoroutine(SendPlayerAgentPrompt(interviewer, closingPrompt, "Interviewer"));
            }
            else
            {
                StartCoroutine(WaitForClosingAudioAndCompleteCoroutine());
            }
        }

        private IEnumerator WaitForClosingAudioAndCompleteCoroutine()
        {
            SetState(InterviewState.WaitingForClosingAudio);
            Debug.Log("[INTERVIEW] State: WaitingForClosingAudio. Allowing final closing audio to finish playing completely without interruption.");

            // Wait for audio to actually begin if it hasn't yet
            float waitToStart = 0f;
            while (!isInterviewerSpeakingTurn && waitToStart < 6f)
            {
                yield return new WaitForSeconds(0.2f);
                waitToStart += 0.2f;
            }

            // Monitor until the closing audio finishes completely
            float maxClosingWait = 35f;
            float elapsed = 0f;
            while (elapsed < maxClosingWait)
            {
                yield return new WaitForSeconds(0.3f);
                elapsed += 0.3f;

                bool isSpeaking = interviewer != null && interviewer.IsSpeaking;
                AudioSource audio = interviewer?.GetComponentInChildren<AudioSource>();
                bool isAudioPlaying = audio != null && audio.isPlaying;

                if (!isInterviewerSpeakingTurn && !isSpeaking && !isAudioPlaying)
                {
                    Debug.Log($"[INTERVIEW] Confirmed closing speech audio finished completely after {elapsed:F1}s.");
                    break;
                }
            }

            // Extra safety buffer (1.2s) so the last syllable naturally completes and mouth pose resets
            yield return new WaitForSeconds(1.2f);
            interviewer?.GetComponent<InterviewAvatarMouthController>()?.EnforceNeutralMouthPoseImmediate();

            // Transition to InterviewCompleted
            yield return StartCoroutine(ConcludeInterviewCoroutine());
        }

        private IEnumerator ConcludeInterviewCoroutine()
        {
            if (currentState == InterviewState.InterviewCompleted) yield break;
            SetState(InterviewState.InterviewCompleted);
            currentTurnState = TurnState.Concluding;
            isTransitioningStage = true;

            Debug.Log("[Interview] === CONCLUDING 10-MINUTE INTERVIEW. ALL SPEECH COMPLETE. ===");

            interviewActive = false;

            // Stop audio and ensure neutral mouth pose
            StopInterviewerAudio();

            if (InterviewVisualEvaluator.Instance != null)
                InterviewVisualEvaluator.Instance.FinalizeObservations();

            // Generate comprehensive final feedback from actual interview responses
            GenerateFinalFeedback();

            // Dispatch interview result to Centralized Placement Backend
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

            // Stop Convai conversation cleanly
            try { interviewer?.StopConversationAsync(); } catch { }

            // Disconnect Convai room cleanly
            try
            {
                ConvaiManager mgr = ConvaiManager.ActiveManager;
                if (mgr != null && mgr.IsConnected)
                {
                    Debug.Log("[Interview] Cleanly disconnecting Convai room...");
                    mgr.DisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Interview] Note during Convai disconnect: {ex.Message}");
            }

            yield return new WaitForSeconds(1.5f);

            Debug.Log($"[Interview] Transitioning to {NEXT_SCENE_NAME}.");
            SceneManager.LoadScene(NEXT_SCENE_NAME);
        }

        private void StopInterviewerAudio()
        {
            if (interviewer == null) return;
            try
            {
                AudioSource audio = interviewer.GetComponentInChildren<AudioSource>();
                if (audio != null && audio.isPlaying)
                {
                    audio.Stop();
                }
                interviewer.GetComponent<InterviewAvatarMouthController>()?.EnforceNeutralMouthPoseImmediate();
            }
            catch { }
        }

        private void GenerateFinalFeedback()
        {
            if (PREPVRSessionManager.Instance == null) return;

            CandidateProfile profile = PREPVRSessionManager.Instance.Profile;
            InterviewTranscriptData transcript = PREPVRSessionManager.Instance.transcript;
            VisualObservationsData visual = PREPVRSessionManager.Instance.visualObservations;
            float duration = PREPVRSessionManager.Instance.InterviewDurationSeconds;

            FeedbackData fb = FeedbackEngine.GenerateFeedback(profile, transcript, visual, duration);
            PREPVRSessionManager.Instance.feedbackData = fb;

            Debug.Log($"[Interview] Feedback for {fb.candidateName} generated successfully. Readiness Level: {fb.overallReadinessLevel}");
        }

        private void SetupTimer()
        {
            InterviewTimerManager timer = FindAnyObjectByType<InterviewTimerManager>();
            if (timer == null)
                timer = new GameObject("InterviewTimerManager").AddComponent<InterviewTimerManager>();
            timer.OnTimerExpired += HandleTimerExpiredSoftBoundary;
        }

        private void HandleTimerExpiredSoftBoundary()
        {
            Debug.Log("[INTERVIEW] Timer expired event received (10m reached). Permitting active conversation turn and closing statement to complete gracefully.");

            if (!isInterviewerSpeakingTurn && !IsAnyInterviewerSpeaking() && !IsCandidateSpeakingOrTurnActive &&
                currentState != InterviewState.ClosingConversation &&
                currentState != InterviewState.WaitingForClosingAudio &&
                currentState != InterviewState.InterviewCompleted)
            {
                Debug.Log("[INTERVIEW] No conversation turn active at timer expiration. Initiating closing conversation.");
                StartCoroutine(InitiateClosingConversation());
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
                interviewer != null ? interviewer.transform : null,
                null
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
            if (p.skills?.Count > 0) sb.Append($"Skills: {string.Join(", ", p.skills)}. ");
            if (p.experience?.Count > 0) sb.Append($"Experience: {string.Join(" | ", p.experience)}. ");
            if (p.projects?.Count > 0)
            {
                sb.Append("Projects: ");
                for (int i = 0; i < p.projects.Count; i++) sb.Append($"[{i + 1}] {p.projects[i]}; ");
            }
            if (p.certifications?.Count > 0) sb.Append($"Certifications: {string.Join(", ", p.certifications)}. ");
            if (p.achievements?.Count > 0) sb.Append($"Achievements: {string.Join(", ", p.achievements)}. ");
            return sb.ToString();
        }

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
                Debug.Log("[ConvaiDebug] Subscribed to ConvaiManager events for single interviewer.");
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
            UnwireCharacterDebugEvents(interviewer);
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
                else if (currentTurnState == TurnState.InterviewerSpeaking || isInterviewerSpeakingTurn)
                {
                    CompleteInterviewerSpeakingTurn(interviewer, "Interviewer", "PlayerSpeakingStateChanged(IsSpeaking=true)");
                    currentTurnState = TurnState.CandidateSpeaking;
                }
            }
        }

        private void HandleCharacterTurnCompleted(CharacterTurnCompleted e)
        {
            if (!interviewActive) return;
            Debug.Log($"[AUDIO] Character turn completed: characterId={e.CharacterId}, wasInterrupted={e.WasInterrupted} at {AuthoritativeElapsedTime:F2}s");
            OnInterviewerTurnCompleted(interviewer, "Interviewer", e.WasInterrupted, "manager.Events.OnCharacterTurnCompleted");
        }

        private void HandleSpeechStateChanged(CharacterSpeechStateChanged e)
        {
            Debug.Log($"[ConvaiDebug] Character speech state: characterId={e.CharacterId}, isSpeaking={e.IsSpeaking} at {AuthoritativeElapsedTime:F2}s");

            if (e.IsSpeaking)
            {
                Debug.Log($"[AUDIO] Interviewer audio response received/started at {AuthoritativeElapsedTime:F2}s");
                if (interviewer != null && !isInterviewerSpeakingTurn)
                {
                    StartInterviewerSpeakingTurn(interviewer, "Interviewer");
                }
            }
            else
            {
                Debug.Log($"[AUDIO] Interviewer stopped speaking at {AuthoritativeElapsedTime:F2}s");
                OnInterviewerAudioStopped(interviewer, "Interviewer", "manager.Events.OnCharacterSpeechStateChanged(!IsSpeaking)");
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
    }
}
