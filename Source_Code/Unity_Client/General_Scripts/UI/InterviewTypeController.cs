using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrepVR.UI
{
    /// <summary>
    /// Controller for the InterviewType selection screen.
    /// Handles Behavioural interview launch, Technical interview coming-soon message, and back navigation.
    /// </summary>
    public class InterviewTypeController : MonoBehaviour
    {
        [Header("Target Scenes")]
        [Tooltip("The existing Interview Room scene name.")]
        [SerializeField] private string behaviouralInterviewSceneName = "ResumeUpload";

        [Tooltip("The Home Screen scene name.")]
        [SerializeField] private string homeScreenSceneName = "HomeScreen";

        [Header("Coming Soon Notification")]
        [Tooltip("TextMeshPro label to display the Coming Soon message.")]
        [SerializeField] private TextMeshProUGUI comingSoonText;

        [Tooltip("Optional panel or container holding the notification.")]
        [SerializeField] private GameObject comingSoonContainer;

        [Tooltip("Default message to display.")]
        [SerializeField] private string comingSoonMessage = "Technical Interview - Coming Soon";

        [Tooltip("Duration in seconds before the notification auto-hides (0 to stay visible).")]
        [SerializeField] private float displayDuration = 3.5f;

        private Coroutine hideCoroutine;

        private void Awake()
        {
            if (comingSoonContainer != null)
            {
                comingSoonContainer.SetActive(false);
            }
            else if (comingSoonText != null)
            {
                comingSoonText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Called when the Final HR Interview button is clicked.
        /// Loads the existing InterviewRoom scene flow via ResumeUpload.
        /// </summary>
        public void OnBehaviouralInterviewClicked()
        {
            if (PREPVRSessionManager.Instance != null)
            {
                PREPVRSessionManager.Instance.SelectedInterviewType = "Final HR Interview";
            }
            Debug.Log("[InterviewFlow] Selected interview type = Final HR Interview");
            Debug.Log("[Interview] A. Final HR interview selected");
            Debug.Log($"[InterviewTypeController] Loading Final HR Interview flow scene: {behaviouralInterviewSceneName}");
            SceneManager.LoadScene(behaviouralInterviewSceneName);
        }

        /// <summary>
        /// Called when the Technical Interview button is clicked.
        /// Keeps the button visible, does NOT load any scene, and displays the Coming Soon notice.
        /// </summary>
        public void OnTechnicalInterviewClicked()
        {
            if (PREPVRSessionManager.Instance != null)
            {
                PREPVRSessionManager.Instance.SelectedInterviewType = "Technical Interview";
            }
            Debug.Log("[InterviewFlow] Selected interview type = Technical Interview");
            Debug.Log("[InterviewTypeController] Technical Interview clicked - showing 'Coming Soon' message.");

            if (comingSoonText != null)
            {
                comingSoonText.text = comingSoonMessage;
                comingSoonText.gameObject.SetActive(true);
            }

            if (comingSoonContainer != null)
            {
                comingSoonContainer.SetActive(true);
            }

            if (displayDuration > 0f)
            {
                if (hideCoroutine != null)
                {
                    StopCoroutine(hideCoroutine);
                }
                hideCoroutine = StartCoroutine(AutoHideComingSoonRoutine());
            }
        }

        private IEnumerator AutoHideComingSoonRoutine()
        {
            yield return new WaitForSeconds(displayDuration);

            if (comingSoonContainer != null)
            {
                comingSoonContainer.SetActive(false);
            }
            else if (comingSoonText != null)
            {
                comingSoonText.gameObject.SetActive(false);
            }

            hideCoroutine = null;
        }

        /// <summary>
        /// Called when the BACK button is clicked.
        /// Returns to HomeScreen.unity.
        /// </summary>
        public void OnBackClicked()
        {
            Debug.Log($"[InterviewTypeController] Returning to HomeScreen: {homeScreenSceneName}");
            SceneManager.LoadScene(homeScreenSceneName);
        }
    }
}
