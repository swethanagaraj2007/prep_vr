using System;
using System.Collections.Generic;
using UnityEngine;
using PrepVR.Model;
using PrepVR.Interview;

public enum InterviewSessionStatus
{
    NotStarted,
    ResumeUploaded,
    ResumeReviewed,
    InProgress,
    Completed,
    Evaluated
}

/// <summary>
/// Persistent singleton manager for PREP VR interview session state.
/// Preserves mode selection, interview type, candidate profile, transcript,
/// visual observations, evaluation, and feedback data across all scenes.
/// </summary>
public class PREPVRSessionManager : MonoBehaviour
{
    public const float MAX_INTERVIEW_DURATION_SECONDS = 600f; // 10 minutes strict hard cutoff

    private static PREPVRSessionManager _instance;

    public static PREPVRSessionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<PREPVRSessionManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("PREPVRSessionManager");
                    _instance = go.AddComponent<PREPVRSessionManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [Header("Session Configuration")]
    [SerializeField] private string selectedMode = "DESK"; // "DESK" or "VR"
    [SerializeField] private string selectedInterviewType = "Behavioural";

    [Header("Candidate & Resume")]
    [SerializeField] private string rollNumber = string.Empty;
    [SerializeField] private string resumePdfPath = string.Empty;
    public CandidateProfile candidateProfile = new CandidateProfile();

    [Header("Session Tracking")]
    [SerializeField] private InterviewSessionStatus status = InterviewSessionStatus.NotStarted;
    [SerializeField] private float interviewDurationSeconds = 0f;

    [Header("Session Data")]
    public InterviewTranscriptData transcript = new InterviewTranscriptData();
    public VisualObservationsData visualObservations = new VisualObservationsData();
    public FeedbackData feedbackData;
    public InterviewEvaluation legacyEvaluation;

    public string SelectedMode
    {
        get => selectedMode;
        set => selectedMode = value;
    }

    public string SelectedInterviewType
    {
        get => selectedInterviewType;
        set => selectedInterviewType = value;
    }

    public string ResumePdfPath
    {
        get => resumePdfPath;
        set => resumePdfPath = value;
    }

    public string RollNumber
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(rollNumber)) return rollNumber;
            if (candidateProfile != null && !string.IsNullOrWhiteSpace(candidateProfile.rollNumber))
                return candidateProfile.rollNumber;
            return string.Empty;
        }
        set
        {
            rollNumber = value;
            if (candidateProfile != null) candidateProfile.rollNumber = value;
        }
    }

    public CandidateProfile Profile
    {
        get => candidateProfile;
        set => candidateProfile = value ?? new CandidateProfile();
    }

    public InterviewSessionStatus Status
    {
        get => status;
        set => status = value;
    }

    public float InterviewDurationSeconds
    {
        get => interviewDurationSeconds;
        set => interviewDurationSeconds = Mathf.Clamp(value, 0f, MAX_INTERVIEW_DURATION_SECONDS);
    }

    public bool IsTimeUp => interviewDurationSeconds >= MAX_INTERVIEW_DURATION_SECONDS;

    public float RemainingSeconds => Mathf.Max(0f, MAX_INTERVIEW_DURATION_SECONDS - interviewDurationSeconds);

    public event Action<InterviewSessionStatus> OnStatusChanged;
    public event Action OnInterviewCompleted;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeDefaults();

        if (GetComponent<InterviewOrchestrator>() == null)
        {
            gameObject.AddComponent<InterviewOrchestrator>();
        }
    }

    private void InitializeDefaults()
    {
        if (candidateProfile == null) candidateProfile = new CandidateProfile();
        if (transcript == null) transcript = new InterviewTranscriptData();
        if (visualObservations == null) visualObservations = new VisualObservationsData();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static PREPVRSessionManager EnsureInstance()
    {
        return Instance;
    }

    public void SetStatus(InterviewSessionStatus newStatus)
    {
        status = newStatus;
        OnStatusChanged?.Invoke(newStatus);
    }

    public void RecordTranscript(string speaker, string text, bool isQuestion = false)
    {
        if (transcript == null) transcript = new InterviewTranscriptData();
        transcript.AddEntry(interviewDurationSeconds, speaker, text, isQuestion);
    }

    public void RecordQAResponse(string question, string answer)
    {
        if (transcript == null) transcript = new InterviewTranscriptData();
        transcript.AddQAResponse(question, answer);
    }

    public void RecordVisualObservation(string category, string observation)
    {
        if (visualObservations == null) visualObservations = new VisualObservationsData();
        visualObservations.AddObservation(interviewDurationSeconds, category, observation);
    }

    public void MarkInterviewComplete()
    {
        SetStatus(InterviewSessionStatus.Completed);
        OnInterviewCompleted?.Invoke();
        Debug.Log($"[PREPVRSessionManager] Interview marked complete. Total Duration: {interviewDurationSeconds:F1}s, Total Responses: {transcript.TotalResponses}, Total Observations: {visualObservations.entries.Count}");
    }

    public void ResetSession()
    {
        status = InterviewSessionStatus.NotStarted;
        interviewDurationSeconds = 0f;
        rollNumber = string.Empty;
        resumePdfPath = string.Empty;
        candidateProfile = new CandidateProfile();
        transcript = new InterviewTranscriptData();
        visualObservations = new VisualObservationsData();
        feedbackData = null;
        legacyEvaluation = null;
    }
}
