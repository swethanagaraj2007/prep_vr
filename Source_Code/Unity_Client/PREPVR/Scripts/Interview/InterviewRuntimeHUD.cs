using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PrepVR.Interview
{
    /// <summary>
    /// Spawns and manages the in-interview heads-up display.
    /// Works seamlessly in both Desktop (Screen Space Overlay) and VR (World Space Canvas).
    /// Displays:
    /// - 10-Minute Global Timer countdown (MM:SS)
    /// - Current Phase Badge (HR Introduction / Resume Deep Dive / Wrap-Up)
    /// - Candidate Name & Mode
    /// - Silent Behaviour Evaluator status
    /// - Early Exit / Complete Interview button
    /// </summary>
    public class InterviewRuntimeHUD : MonoBehaviour
    {
        [Header("UI Element References")]
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI candidateInfoText;
        [SerializeField] private TextMeshProUGUI evaluatorStatusText;
        [SerializeField] private Button endInterviewButton;

        public static InterviewRuntimeHUD Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnbindTimer();
        }

        public static InterviewRuntimeHUD CreateHUD(Transform parent = null, bool isVR = false)
        {
            GameObject hudObj = new GameObject("InterviewRuntimeHUD");
            if (parent != null) hudObj.transform.SetParent(parent, false);

            InterviewRuntimeHUD hud = hudObj.AddComponent<InterviewRuntimeHUD>();
            hud.BuildUIHierarchy(isVR);
            return hud;
        }

        private void BuildUIHierarchy(bool isVR)
        {
            // 1. Root Canvas
            hudCanvas = gameObject.AddComponent<Canvas>();
            if (isVR)
            {
                hudCanvas.renderMode = RenderMode.WorldSpace;
                hudCanvas.transform.position = new Vector3(0f, 1.8f, 1.8f);
                hudCanvas.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
                RectTransform rt = hudCanvas.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(900f, 180f);
                hudCanvas.transform.localScale = Vector3.one * 0.001f;
            }
            else
            {
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            gameObject.AddComponent<GraphicRaycaster>();

            // 2. Top Bar Panel
            GameObject panelObj = new GameObject("HUDTopPanel");
            panelObj.transform.SetParent(hudCanvas.transform, false);
            RectTransform panelRt = panelObj.AddComponent<RectTransform>();

            if (isVR)
            {
                panelRt.anchorMin = Vector2.zero;
                panelRt.anchorMax = Vector2.one;
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;
            }
            else
            {
                panelRt.anchorMin = new Vector2(0.1f, 0.88f);
                panelRt.anchorMax = new Vector2(0.9f, 0.98f);
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;
            }

            Image panelImg = panelObj.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.09f, 0.16f, 0.88f);

            // 3. Timer Text (Left/Center)
            GameObject timerObj = new GameObject("TimerText");
            timerObj.transform.SetParent(panelObj.transform, false);
            RectTransform timerRt = timerObj.AddComponent<RectTransform>();
            timerRt.anchorMin = new Vector2(0.02f, 0.1f);
            timerRt.anchorMax = new Vector2(0.25f, 0.9f);
            timerRt.offsetMin = Vector2.zero;
            timerRt.offsetMax = Vector2.zero;

            timerText = timerObj.AddComponent<TextMeshProUGUI>();
            timerText.text = "⏱ 10:00";
            timerText.fontSize = isVR ? 32 : 36;
            timerText.fontStyle = FontStyles.Bold;
            timerText.alignment = TextAlignmentOptions.MidlineLeft;
            timerText.color = new Color(0.22f, 0.74f, 0.97f); // Cyan

            // 4. Phase Text (Center)
            GameObject phaseObj = new GameObject("PhaseText");
            phaseObj.transform.SetParent(panelObj.transform, false);
            RectTransform phaseRt = phaseObj.AddComponent<RectTransform>();
            phaseRt.anchorMin = new Vector2(0.26f, 0.5f);
            phaseRt.anchorMax = new Vector2(0.70f, 0.95f);
            phaseRt.offsetMin = Vector2.zero;
            phaseRt.offsetMax = Vector2.zero;

            phaseText = phaseObj.AddComponent<TextMeshProUGUI>();
            phaseText.text = "Phase 1: HR & Background Introduction";
            phaseText.fontSize = isVR ? 20 : 22;
            phaseText.fontStyle = FontStyles.Bold;
            phaseText.alignment = TextAlignmentOptions.MidlineLeft;
            phaseText.color = Color.white;

            // 5. Candidate & Evaluator Status (Center Bottom)
            GameObject evalObj = new GameObject("EvaluatorText");
            evalObj.transform.SetParent(panelObj.transform, false);
            RectTransform evalRt = evalObj.AddComponent<RectTransform>();
            evalRt.anchorMin = new Vector2(0.26f, 0.05f);
            evalRt.anchorMax = new Vector2(0.70f, 0.50f);
            evalRt.offsetMin = Vector2.zero;
            evalRt.offsetMax = Vector2.zero;

            evaluatorStatusText = evalObj.AddComponent<TextMeshProUGUI>();
            string candName = PREPVRSessionManager.Instance?.Profile?.name ?? "Candidate";
            evaluatorStatusText.text = $"👤 {candName}  |  👁 Silent Evaluator: Active (Vision & Posture)";
            evaluatorStatusText.fontSize = isVR ? 14 : 16;
            evaluatorStatusText.alignment = TextAlignmentOptions.MidlineLeft;
            evaluatorStatusText.color = new Color(0.60f, 0.70f, 0.85f);

            // 6. End Interview Early Button (Right)
            GameObject btnObj = new GameObject("EndInterviewButton");
            btnObj.transform.SetParent(panelObj.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.74f, 0.15f);
            btnRt.anchorMax = new Vector2(0.98f, 0.85f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.85f, 0.25f, 0.25f, 0.90f);

            endInterviewButton = btnObj.AddComponent<Button>();
            ColorBlock cb = endInterviewButton.colors;
            cb.highlightedColor = new Color(0.95f, 0.35f, 0.35f);
            cb.pressedColor = new Color(0.70f, 0.15f, 0.15f);
            endInterviewButton.colors = cb;

            GameObject btnTextObj = new GameObject("ButtonText");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btnTextRt = btnTextObj.AddComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;

            TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "END INTERVIEW";
            btnText.fontSize = isVR ? 16 : 18;
            btnText.fontStyle = FontStyles.Bold;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            endInterviewButton.onClick.AddListener(OnEndInterviewClicked);

            BindTimer();
        }

        private void BindTimer()
        {
            if (InterviewTimerManager.Instance != null)
            {
                InterviewTimerManager.Instance.OnTimerTicked += HandleTimerTicked;
                InterviewTimerManager.Instance.OnPhaseChanged += HandlePhaseChanged;
                InterviewTimerManager.Instance.OnCustomStageChanged += HandleCustomStageChanged;

                if (phaseText != null)
                {
                    phaseText.text = InterviewTimerManager.Instance.IsFinalHRInterview
                        ? InterviewTimerManager.Instance.CurrentCustomStage
                        : InterviewTimerManager.Instance.GetPhaseDisplayName(InterviewTimerManager.Instance.CurrentPhase);
                }
            }
        }

        private void UnbindTimer()
        {
            if (InterviewTimerManager.Instance != null)
            {
                InterviewTimerManager.Instance.OnTimerTicked -= HandleTimerTicked;
                InterviewTimerManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
                InterviewTimerManager.Instance.OnCustomStageChanged -= HandleCustomStageChanged;
            }
        }

        private void HandleCustomStageChanged(string stageName)
        {
            if (phaseText != null)
            {
                phaseText.text = stageName;
            }
        }

        private void HandleTimerTicked(float remainingSeconds, float elapsedSeconds)
        {
            if (timerText != null)
            {
                string timeStr = InterviewTimerManager.FormatTime(remainingSeconds);
                timerText.text = $"⏱ {timeStr}";

                if (remainingSeconds <= 60f)
                {
                    timerText.color = new Color(0.95f, 0.30f, 0.30f); // Red warning under 1 min
                }
                else if (remainingSeconds <= 180f)
                {
                    timerText.color = new Color(0.95f, 0.75f, 0.20f); // Amber under 3 min
                }
            }
        }

        private void HandlePhaseChanged(InterviewPhase phase)
        {
            if (phaseText != null && InterviewTimerManager.Instance != null)
            {
                phaseText.text = InterviewTimerManager.Instance.GetPhaseDisplayName(phase);
            }
        }

        private void OnEndInterviewClicked()
        {
            Debug.Log("[InterviewRuntimeHUD] End Interview clicked.");
            if (InterviewTimerManager.Instance != null)
            {
                InterviewTimerManager.Instance.EndInterviewEarly();
            }
        }
    }
}
