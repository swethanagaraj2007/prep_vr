using System;
using UnityEngine;

namespace PrepVR.Interview
{
    public enum InterviewPhase
    {
        HR_Introduction = 1,     // 0:00 - 2:00 (0s - 120s)
        Resume_Technical = 2,    // 2:00 - 8:00 (120s - 480s)
        Closing_WrapUp = 3       // 8:00 - 10:00 (480s - 600s)
    }

    /// <summary>
    /// Manages the exact 600-second (10-minute) interview countdown timer.
    /// Manages phase shifts between HR Introduction, Resume Technical Deep Dive, and Closing.
    /// Hard cutoff at 600 seconds terminates conversation and saves session state.
    /// </summary>
    public class InterviewTimerManager : MonoBehaviour
    {
        public const float TOTAL_INTERVIEW_SECONDS = 600f; // 10 minutes
        public const float STAGE_1_END_SECONDS     = 200f; // 3m 20s (0:00 - 3:20)
        public const float STAGE_2_END_SECONDS     = 400f; // 6m 40s (3:20 - 6:40)
        public const float STAGE_3_END_SECONDS     = 600f; // 10m 00s (6:40 - 10:00)

        // Aliases for legacy code
        public const float PHASE_1_DURATION_SECONDS = STAGE_1_END_SECONDS;
        public const float PHASE_2_END_SECONDS      = STAGE_2_END_SECONDS;

        [Header("Runtime Timer State")]
        [SerializeField] private bool isRunning = false;
        [SerializeField] private float elapsedTime = 0f;
        [SerializeField] private InterviewPhase currentPhase = InterviewPhase.HR_Introduction;

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

        public string CurrentCustomStage { get; private set; } = "Stage 1: HR Interviewer (0:00 - 3:20)";
        public event Action<string> OnCustomStageChanged;

        public void SetCustomStage(string stageName)
        {
            CurrentCustomStage = stageName;
            Debug.Log($"[InterviewTimerManager] Interview Stage updated to: {stageName}");
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
            Debug.Log($"[InterviewTimerManager] Authoritative 600-second interview timer started. Current Stage: {CurrentCustomStage}");
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
                Debug.Log($"[InterviewTimerManager] Authoritative Stage changed to: {CurrentCustomStage} at {FormatTime(elapsedTime)}");
                OnPhaseChanged?.Invoke(currentPhase);
                OnCustomStageChanged?.Invoke(CurrentCustomStage);
            }

            // Soft interview duration limit reached (10 minutes total or configured duration)
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

        private InterviewPhase DeterminePhase(float seconds)
        {
            if (seconds < STAGE_1_END_SECONDS)
            {
                return InterviewPhase.HR_Introduction;
            }
            else if (seconds < STAGE_2_END_SECONDS)
            {
                return InterviewPhase.Resume_Technical;
            }
            else
            {
                return InterviewPhase.Closing_WrapUp;
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
                case InterviewPhase.HR_Introduction:
                    return "Stage 1: HR Interviewer (0:00 - 3:20)";
                case InterviewPhase.Resume_Technical:
                    return "Stage 2: Behavioral Interviewer (3:20 - 6:40)";
                case InterviewPhase.Closing_WrapUp:
                    return "Stage 3: Hiring Manager / Team Lead (6:40 - 10:00)";
                default:
                    return "Interview In Progress";
            }
        }
    }
}
