using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using PrepVR.Model;
using PrepVR.Interview;
using PrepVR.Placement;

namespace PrepVR.UI
{
    /// <summary>
    /// Controller for the InterviewOver / Feedback scene.
    /// Presents the interviewee with their comprehensive 13-category placement performance summary,
    /// provides one-click generation and viewing of the real vector PDF report,
    /// and automatically syncs the verified real interview result to the Centralized Placement Backend.
    /// </summary>
    public class InterviewOverController : MonoBehaviour
    {
        [Header("UI Text Displays")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI candidateInfoText;
        [SerializeField] private TextMeshProUGUI readinessBadgeText;
        [SerializeField] private TextMeshProUGUI strengthsText;
        [SerializeField] private TextMeshProUGUI growthAreasText;
        [SerializeField] private TextMeshProUGUI statusFeedbackText;

        [Header("Buttons")]
        [SerializeField] private Button downloadPdfButton;
        [SerializeField] private Button returnHomeButton;

        [Header("Scene Navigation")]
        [SerializeField] private string homeSceneName = "HomeScreen";

        private FeedbackData activeFeedback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "InterviewOver")
            {
                if (FindFirstObjectByType<InterviewOverController>() == null)
                {
                    Canvas canvas = FindFirstObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        canvas.gameObject.AddComponent<InterviewOverController>();
                        Debug.Log("[InterviewOverController] Dynamically attached to Canvas upon InterviewOver scene load.");
                    }
                    else
                    {
                        GameObject go = new GameObject("InterviewOverController");
                        go.AddComponent<InterviewOverController>();
                        Debug.Log("[InterviewOverController] Created standalone InterviewOverController in InterviewOver scene.");
                    }
                }
            }
        }

        private void Start()
        {
            InitializeFeedbackData();
            EnsureStatusText();
            BindUI();
            AutoBindButtons();
            SyncToPlacementBackend();
        }

        private void InitializeFeedbackData()
        {
            if (PREPVRSessionManager.Instance != null && PREPVRSessionManager.Instance.feedbackData != null)
            {
                activeFeedback = PREPVRSessionManager.Instance.feedbackData;
            }
            else
            {
                // Generate from existing session or fallback sample
                CandidateProfile profile = PREPVRSessionManager.Instance?.Profile;
                InterviewTranscriptData transcript = PREPVRSessionManager.Instance?.transcript;
                VisualObservationsData visual = PREPVRSessionManager.Instance?.visualObservations;
                float duration = PREPVRSessionManager.Instance != null ? PREPVRSessionManager.Instance.InterviewDurationSeconds : 600f;

                activeFeedback = FeedbackEngine.GenerateFeedback(profile, transcript, visual, duration);
                if (PREPVRSessionManager.Instance != null)
                {
                    PREPVRSessionManager.Instance.feedbackData = activeFeedback;
                }
            }
        }

        private void EnsureStatusText()
        {
            if (statusFeedbackText != null) return;

            // Search for existing status text in scene
            TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (var t in allTexts)
            {
                if (t.name.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.name.IndexOf("Placement", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    statusFeedbackText = t;
                    return;
                }
            }

            // Create a status text component below the buttons or card
            Transform parent = transform.Find("CardPanel");
            if (parent == null) parent = transform;

            try
            {
                GameObject statusObj = new GameObject("PlacementSyncStatusText");
                statusObj.transform.SetParent(parent, false);
                RectTransform rt = statusObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.22f);
                rt.anchorMax = new Vector2(0.5f, 0.22f);
                rt.sizeDelta = new Vector2(740, 36);
                rt.anchoredPosition = Vector2.zero;

                statusFeedbackText = statusObj.AddComponent<TextMeshProUGUI>();
                statusFeedbackText.fontSize = 17;
                statusFeedbackText.fontStyle = FontStyles.Bold;
                statusFeedbackText.alignment = TextAlignmentOptions.Center;
                statusFeedbackText.color = new Color(0.22f, 0.74f, 0.97f);
                statusFeedbackText.text = "Initializing Placement Central sync...";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InterviewOverController] Note creating status text: {ex.Message}");
            }
        }

        private void BindUI()
        {
            if (activeFeedback == null) return;

            string roll = PREPVRSessionManager.Instance?.RollNumber;
            string rollBadge = !string.IsNullOrWhiteSpace(roll) ? $" [Roll: {roll}]" : "";

            if (titleText != null)
            {
                titleText.text = "PLACEMENT INTERVIEW COMPLETE";
            }

            if (subtitleText != null)
            {
                subtitleText.text = $"PERFORMANCE EVALUATION — {activeFeedback.candidateName.ToUpper()}{rollBadge}";
            }

            if (candidateInfoText != null)
            {
                candidateInfoText.text = $"<b>Candidate:</b> {activeFeedback.candidateName}  |  <b>Roll:</b> {(string.IsNullOrWhiteSpace(roll) ? "N/A" : roll)}  |  <b>Duration:</b> {activeFeedback.interviewDurationFormatted}";
            }

            if (readinessBadgeText != null)
            {
                readinessBadgeText.text = $"★ {activeFeedback.overallReadinessLevel}";
            }

            if (descriptionText != null)
            {
                descriptionText.text = activeFeedback.overallSummary;
            }

            if (strengthsText != null && activeFeedback.strengths != null && activeFeedback.strengths.Count > 0)
            {
                strengthsText.text = "<b>Key Placement Strengths:</b>\n• " + string.Join("\n• ", activeFeedback.strengths);
            }

            if (growthAreasText != null && activeFeedback.areasForImprovement != null && activeFeedback.areasForImprovement.Count > 0)
            {
                growthAreasText.text = "<b>Priority Growth Areas:</b>\n• " + string.Join("\n• ", activeFeedback.areasForImprovement);
            }
        }

        private void AutoBindButtons()
        {
            if (downloadPdfButton == null)
            {
                Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
                foreach (var b in buttons)
                {
                    if (b.name.IndexOf("Feedback", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        b.name.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        downloadPdfButton = b;
                        break;
                    }
                }
            }

            if (returnHomeButton == null)
            {
                Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
                foreach (var b in buttons)
                {
                    if (b.name.IndexOf("Home", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        b.name.IndexOf("Return", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        returnHomeButton = b;
                        break;
                    }
                }
            }

            if (downloadPdfButton != null)
            {
                downloadPdfButton.onClick.RemoveAllListeners();
                downloadPdfButton.onClick.AddListener(OnDownloadPdfReportClicked);

                TextMeshProUGUI btnText = downloadPdfButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = "DOWNLOAD PDF REPORT";
                }
            }

            if (returnHomeButton != null)
            {
                returnHomeButton.onClick.RemoveAllListeners();
                returnHomeButton.onClick.AddListener(OnReturnHomeClicked);
            }
        }

        /// <summary>
        /// Synchronizes the completed interview to the Placement Backend API.
        /// Sends actual final scores, readiness score, feedback, and stage results.
        /// </summary>
        public void SyncToPlacementBackend()
        {
            if (activeFeedback == null)
            {
                InitializeFeedbackData();
            }

            string rollNumber = PREPVRSessionManager.Instance?.RollNumber;
            if (string.IsNullOrWhiteSpace(rollNumber))
            {
                rollNumber = PREPVRSessionManager.Instance?.Profile?.rollNumber;
            }

            if (string.IsNullOrWhiteSpace(rollNumber))
            {
                SetStatus("⚠ Notice: No roll number found. Interview saved locally.", new Color(0.96f, 0.62f, 0.04f));
                return;
            }

            SetStatus($"Syncing results for {rollNumber} to Placement Central...", new Color(0.22f, 0.74f, 0.97f));

            // Ensure vector PDF report is generated for report path
            string reportPath = activeFeedback?.generatedPdfPath;
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                try
                {
                    reportPath = FeedbackPdfGenerator.GeneratePdfReport(activeFeedback, null, null);
                    if (activeFeedback != null) activeFeedback.generatedPdfPath = reportPath;
                }
                catch { }
            }

            PlacementBackendClient.Instance.SendInterviewResult(
                activeFeedback,
                rollNumber,
                reportPath,
                OnPlacementUploadResult
            );
        }

        private void OnPlacementUploadResult(UploadStatus status, string message)
        {
            switch (status)
            {
                case UploadStatus.Success:
                    SetStatus(message, new Color(0.20f, 0.82f, 0.60f));
                    break;
                case UploadStatus.Duplicate:
                    SetStatus(message, new Color(0.22f, 0.74f, 0.97f));
                    break;
                case UploadStatus.ServerOffline:
                    SetStatus(message, new Color(0.96f, 0.62f, 0.04f));
                    break;
                case UploadStatus.Timeout:
                    SetStatus(message, new Color(0.96f, 0.62f, 0.04f));
                    break;
                case UploadStatus.Error:
                default:
                    SetStatus(message, new Color(0.95f, 0.35f, 0.35f));
                    break;
            }
        }

        public void OnDownloadPdfReportClicked()
        {
            if (activeFeedback == null)
            {
                InitializeFeedbackData();
            }

            SetStatus("Generating vector PDF report...", new Color(0.22f, 0.74f, 0.97f));

            string pdfPath = FeedbackPdfGenerator.GeneratePdfReport(
                activeFeedback,
                onCompleted: path =>
                {
                    SetStatus($"✔ Report Generated: {Path.GetFileName(path)}", new Color(0.20f, 0.82f, 0.60f));
                    FeedbackPdfGenerator.OpenPdfFile(path);

                    // Re-sync if path was updated
                    if (activeFeedback != null && activeFeedback.generatedPdfPath != path)
                    {
                        activeFeedback.generatedPdfPath = path;
                        SyncToPlacementBackend();
                    }
                },
                onError: err =>
                {
                    SetStatus($"✖ Error generating PDF: {err}", new Color(0.95f, 0.35f, 0.35f));
                }
            );

            if (!string.IsNullOrWhiteSpace(pdfPath) && File.Exists(pdfPath))
            {
                FeedbackPdfGenerator.OpenPdfFile(pdfPath);
            }
        }

        public void OnReturnHomeClicked()
        {
            Debug.Log($"[InterviewOverController] Returning to {homeSceneName}.");
            if (PREPVRSessionManager.Instance != null)
            {
                PREPVRSessionManager.Instance.ResetSession();
            }
            SceneManager.LoadScene(homeSceneName);
        }

        private void SetStatus(string msg, Color color)
        {
            if (statusFeedbackText != null)
            {
                statusFeedbackText.text = msg;
                statusFeedbackText.color = color;
            }
            Debug.Log($"[InterviewOverController] {msg}");
        }
    }
}
