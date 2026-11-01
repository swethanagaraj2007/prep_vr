using System.IO;
using UnityEngine;

namespace PrepVR.UI
{
    /// <summary>
    /// Opens the interview feedback form PDF or URL.
    /// The target path or URL is easily configurable directly in this script or via Inspector.
    /// </summary>
    public class FeedbackFormOpener : MonoBehaviour
    {
        // =========================================================================
        // CONFIGURATION: Change the default PDF path or URL here in one single place
        // =========================================================================
        public const string DEFAULT_FEEDBACK_URL_OR_PATH = "https://forms.google.com";
        // Alternatively, for a local file:
        // public const string DEFAULT_FEEDBACK_URL_OR_PATH = "Assets/FeedbackForm.pdf";
        // =========================================================================

        [Header("Feedback Form Target")]
        [Tooltip("Web URL (https://...) or absolute/relative file path to the PDF document.")]
        [SerializeField] private string targetPathOrUrl = DEFAULT_FEEDBACK_URL_OR_PATH;

        /// <summary>
        /// Gets or sets the target PDF path or URL.
        /// </summary>
        public string TargetPathOrUrl
        {
            get => targetPathOrUrl;
            set => targetPathOrUrl = value;
        }

        /// <summary>
        /// Opens the feedback form in the user's default browser or PDF viewer.
        /// Integrates with FeedbackPdfGenerator to generate and open the real vector PDF report.
        /// </summary>
        public void OpenFeedbackForm()
        {
            try
            {
                if (PREPVRSessionManager.Instance != null)
                {
                    var data = PREPVRSessionManager.Instance.feedbackData;
                    if (data == null)
                    {
                        var profile = PREPVRSessionManager.Instance.Profile;
                        var transcript = PREPVRSessionManager.Instance.transcript;
                        var visual = PREPVRSessionManager.Instance.visualObservations;
                        float dur = PREPVRSessionManager.Instance.InterviewDurationSeconds;
                        data = PrepVR.Interview.FeedbackEngine.GenerateFeedback(profile, transcript, visual, dur);
                        PREPVRSessionManager.Instance.feedbackData = data;
                    }

                    string generatedPath = PrepVR.Interview.FeedbackPdfGenerator.GeneratePdfReport(
                        data,
                        onCompleted: path => PrepVR.Interview.FeedbackPdfGenerator.OpenPdfFile(path)
                    );

                    // Sync real report path and feedback to placement portal
                    string roll = PREPVRSessionManager.Instance.RollNumber;
                    if (string.IsNullOrWhiteSpace(roll) && PREPVRSessionManager.Instance.Profile != null)
                    {
                        roll = PREPVRSessionManager.Instance.Profile.rollNumber;
                    }
                    if (string.IsNullOrWhiteSpace(roll) && data != null)
                    {
                        roll = data.candidateName.Replace(" ", "_").ToUpperInvariant();
                    }
                    if (!string.IsNullOrWhiteSpace(roll) && PrepVR.Placement.PlacementBackendClient.Instance != null)
                    {
                        PrepVR.Placement.PlacementBackendClient.Instance.SendInterviewResult(data, roll, generatedPath);
                    }

                    if (!string.IsNullOrWhiteSpace(generatedPath) && File.Exists(generatedPath))
                    {
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FeedbackFormOpener] PDF generation bridge error: {ex.Message}. Falling back to default target.");
            }

            string target = string.IsNullOrWhiteSpace(targetPathOrUrl) 
                ? DEFAULT_FEEDBACK_URL_OR_PATH 
                : targetPathOrUrl.Trim();

            Debug.Log($"[FeedbackFormOpener] Opening feedback target: {target}");

            try
            {
                if (!target.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) &&
                    !target.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase) &&
                    !target.StartsWith("file://", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(target))
                    {
                        string fullPath = Path.GetFullPath(target);
                        Application.OpenURL("file:///" + fullPath.Replace('\\', '/'));
                        return;
                    }
                }

                Application.OpenURL(target);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FeedbackFormOpener] Failed to open feedback form at '{target}': {ex.Message}");
            }
        }
    }
}
