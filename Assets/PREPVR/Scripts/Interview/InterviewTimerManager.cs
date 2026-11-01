using System;
using UnityEngine;

namespace PrepVR.Interview
{
    public enum InterviewPhase
    {
        Welcome_Introduction = 1,       // 0:00 - 1:00 (0s - 60s)
        Background_HR = 2,              // 1:00 - 3:00 (60s - 180s)
        Resume_Technical = 3,           // 3:00 - 6:00 (180s - 360s)
        Projects_ProblemSolving = 4,    // 6:00 - 8:00 (360s - 480s)
        Behavioral_Situational = 5,     // 8:00 - 9:30 (480s - 570s)
        FinalQuestion_Closing = 6,      // 9:30 - 10:00 (570s - 600s)

        // Backward-compatibility aliases
        HR_Introduction = 1,
        Behavioral_Analysis = 5,
        Closing_WrapUp = 6
    }

    /// <summary>
    /// Manages the exact 600-second (10-minute) authoritative interview countdown timer
    /// for the Single Interviewer System.
    ///
    /// Structured 10-Minute Timeline:
    /// - 00:00–01:00 (60s):  Welcome and candidate introduction
    /// - 01:00–03:00 (180s): Background, education and HR questions
    /// - 03:00–06:00 (360s): Resume-based skills and technical questions
    /// - 06:00–08:00 (480s): Project discussion and problem-solving
    /// - 08:00–09:30 (570s): Behavioural and situational questions (STAR framework)
    /// - 09:30–10:00 (600s): Final question and professional closing
    /// </summary>
    public class InterviewTimerManager : MonoBehaviour
    {
        public const float TOTAL_INTERVIEW_SECONDS = 600f; // 10 minutes total
        public const float SECTION_1_END_SECONDS   = 60f;  // 1m 00s (0:00 - 1:00) Welcome & Introduction
        public const float SECTION_2_END_SECONDS   = 180f; // 3m 00s (1:00 - 3:00) Background & HR Questions
        public const float SECTION_3_END_SECONDS   = 360f; // 6m 00s (3:00 - 6:00) Resume Technical Skills
        public const float SECTION_4_END_SECONDS   = 480f; // 8m 00s (6:00 - 8:00) Projects & Problem-Solving
        public const float SECTION_5_END_SECONDS   = 570f; // 9m 30s (8:00 - 9:30) Behavioural & Situational (STAR)
        public const float SECTION_6_END_SECONDS   = 600f; // 10m 00s (9:30 - 10:00) Final Question & Wrap-Up

        // Backward-compatibility aliases
        public const float STAGE_1_END_SECONDS     = SECTION_2_END_SECONDS;
        public const float STAGE_2_END_SECONDS     = SECTION_3_END_SECONDS;
        public const float STAGE_3_END_SECONDS     = SECTION_5_END_SECONDS;
        public const float STAGE_4_END_SECONDS     = TOTAL_INTERVIEW_SECONDS;
        public const float PHASE_1_DURATION_SECONDS = SECTION_2_END_SECONDS;
        public const float PHASE_2_END_SECONDS      = SECTION_3_END_SECONDS;

        [Header("Runtime Timer State")]
        [SerializeField] private bool isRunning = false;
        [SerializeField] private float elapsedTime = 0f;
        [SerializeField] private InterviewPhase currentPhase = InterviewPhase.Welcome_Introduction;

        public bool IsRunning => isRunning;
        public float ElapsedTime => elapsedTime;
        public float RemainingTime => Mathf.Max(0f, TOTAL_INTERVIEW_SECONDS - elapsedTime);
        public InterviewPhase CurrentPhase => currentPhase;

        // Events
        public event Action<float> OnTimerStarted;
        public event Action<float, float> OnTimerTicked; // remainingSeconds, elapsedSeconds
        public event Action<InterviewPhase> OnPhaseChanged;
        public event Action OnTimerExpired;

        public static InterviewTimerManager Instance { get; private set; }

        public bool IsFinalHRInterview => true;

        public string CurrentCustomStage { get; private set; } = "Welcome & Introduction (0:00 - 1:00)";
        public event Action<string> OnCustomStageChanged;

        public void SetCustomStage(string stageName)
        {
            CurrentCustomStage = stageName;
            Debug.Log($"[InterviewTimerManager] Interview Section updated to: {stageName}");
            OnCustomStageChanged?.Invoke(stageName);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void StartTimer(float initialElapsed = 0f)
        {
            elapsedTime = initialElapsed;
            hasFiredTimerExpired = false;
            isRunning = true;

            currentPhase = DeterminePhase(elapsedTime);
            CurrentCustomStage = GetPhaseDisplayName(currentPhase);
            Debug.Log($"[InterviewTimerManager] 10-Minute single-interviewer timer started. Current Section: {CurrentCustomStage}");
            OnTimerStarted?.Invoke(TOTAL_INTERVIEW_SECONDS);
            OnPhaseChanged?.Invoke(currentPhase);
            OnCustomStageChanged?.Invoke(CurrentCustomStage);
        }

        public void PauseTimer()
        {
            isRunning = false;
        }

        public void ResumeTimer()
        {
            if (elapsedTime < TOTAL_INTERVIEW_SECONDS)
            {
                isRunning = true;
            }
        }

        public void StopTimer()
        {
            isRunning = false;
        }

        private void Update()
        {
            if (!isRunning) return;

            elapsedTime += Time.deltaTime;

            // Sync with session manager
            if (PREPVRSessionManager.Instance != null)
            {
                PREPVRSessionManager.Instance.InterviewDurationSeconds = elapsedTime;
            }

            OnTimerTicked?.Invoke(RemainingTime, elapsedTime);

            InterviewPhase newPhase = DeterminePhase(elapsedTime);
            if (newPhase != currentPhase)
            {
                currentPhase = newPhase;
                CurrentCustomStage = GetPhaseDisplayName(currentPhase);
                Debug.Log($"[InterviewTimerManager] Authoritative Section changed to: {CurrentCustomStage} at {FormatTime(elapsedTime)}");
                OnPhaseChanged?.Invoke(currentPhase);
                OnCustomStageChanged?.Invoke(CurrentCustomStage);
            }

            // Soft interview duration limit reached (10 minutes total)
            if (elapsedTime >= TOTAL_INTERVIEW_SECONDS && !hasFiredTimerExpired)
            {
                hasFiredTimerExpired = true;
                Debug.Log($"[InterviewTimerManager] Soft time limit reached ({TOTAL_INTERVIEW_SECONDS}s). Permitting active conversation turn and closing statement to complete gracefully.");
                OnTimerExpired?.Invoke();
            }
        }

        private bool hasFiredTimerExpired = false;
        public bool IsTimeLimitReached => hasFiredTimerExpired || elapsedTime >= TOTAL_INTERVIEW_SECONDS;

        public void EndInterviewEarly()
        {
            if (!isRunning && elapsedTime >= TOTAL_INTERVIEW_SECONDS) return;

            Debug.Log($"[InterviewTimerManager] Interview ended early by user at {FormatTime(elapsedTime)}.");
            isRunning = false;
            OnTimerExpired?.Invoke();
        }

        public InterviewPhase DeterminePhase(float seconds)
        {
            if (seconds < SECTION_1_END_SECONDS)
            {
                return InterviewPhase.Welcome_Introduction;
            }
            else if (seconds < SECTION_2_END_SECONDS)
            {
                return InterviewPhase.Background_HR;
            }
            else if (seconds < SECTION_3_END_SECONDS)
            {
                return InterviewPhase.Resume_Technical;
            }
            else if (seconds < SECTION_4_END_SECONDS)
            {
                return InterviewPhase.Projects_ProblemSolving;
            }
            else if (seconds < SECTION_5_END_SECONDS)
            {
                return InterviewPhase.Behavioral_Situational;
            }
            else
            {
                return InterviewPhase.FinalQuestion_Closing;
            }
        }

        public static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:D2}:{s:D2}";
        }

        public string GetPhaseDisplayName(InterviewPhase phase)
        {
            switch (phase)
            {
                case InterviewPhase.Welcome_Introduction:
                    return "Welcome & Introduction (0:00 - 1:00)";
                case InterviewPhase.Background_HR:
                    return "Background & HR Questions (1:00 - 3:00)";
                case InterviewPhase.Resume_Technical:
                    return "Technical Skills & Architecture (3:00 - 6:00)";
                case InterviewPhase.Projects_ProblemSolving:
                    return "Projects & Problem Solving (6:00 - 8:00)";
                case InterviewPhase.Behavioral_Situational:
                    return "Behavioural & Situational (8:00 - 9:30)";
                case InterviewPhase.FinalQuestion_Closing:
                    return "Final Question & Wrap-Up (9:30 - 10:00)";
                default:
                    return "Interview In Progress";
            }
        }
    }
}
