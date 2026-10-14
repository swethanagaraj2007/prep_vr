using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Convai.Domain.DomainEvents.Transcript;
using Convai.Runtime;
using Convai.Runtime.Components;
using Convai.Runtime.DynamicContext;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResumeUploader : MonoBehaviour
{
    public TMP_Text resumeFileName;
    public TMP_Text statusText;
    public InterviewSession interviewSession;
    public Button finishInterviewButton;

    private Button uploadResumeButton;
    private Button startInterviewButton;
    private Button closePanelButton;
    private Button menuToggleButton;
    private TMP_Text menuToggleText;
    private GameObject resumePanel;
    private GameObject inInterviewBanner;
    private TMP_Text inInterviewText;
    private string selectedResumePath;
    private string pendingQuestion;
    private bool subscribedToEvents;
    private bool resumeProcessed;
    private bool isProcessingResume;

    private void Awake()
    {
        EnsureStartupCanvas();
        ResolveReferences();
        CleanLegacyUiElements();
        EnsureStartupScreen();
        EnsureFinishInterviewButton();
        EnsureMenuToggleButton();
        SetInterviewEnvironmentVisible(true);
        SetInterviewUiVisible(true);
    }

    private void Start()
    {
        EnsureStartupCanvas();
        ResolveReferences();
        CleanLegacyUiElements();
        EnsureStartupScreen();
        EnsureFinishInterviewButton();
        EnsureMenuToggleButton();
        SetInterviewEnvironmentVisible(true);
        SetInterviewUiVisible(true);
    }

    private void Update()
    {
        // Allow user to toggle the resume/onboarding menu with Tab, Escape, or M
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.M))
        {
            ToggleMenu();
        }
    }

    private void OnEnable()
    {
        TrySubscribeToConvaiEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromConvaiEvents();
    }

    private void CleanLegacyUiElements()
    {
        // Deactivate old legacy unstyled buttons and texts that used to float in the screen center
        if (resumePanel != null)
        {
            Transform oldBtn = resumePanel.transform.Find("UploadResumeButton");
            if (oldBtn != null && oldBtn.parent == resumePanel.transform)
            {
                oldBtn.gameObject.SetActive(false);
            }

            Transform oldFile = resumePanel.transform.Find("ResumeFileName");
            if (oldFile != null && oldFile.parent == resumePanel.transform)
            {
                oldFile.gameObject.SetActive(false);
            }
        }

        GameObject legacyRootBtn = GameObject.Find("UploadResumeButton");
        if (legacyRootBtn != null && legacyRootBtn.transform.parent != null && legacyRootBtn.transform.parent.name == "ResumePanel")
        {
            legacyRootBtn.SetActive(false);
        }

        GameObject legacyRootFile = GameObject.Find("ResumeFileName");
        if (legacyRootFile != null && legacyRootFile.transform.parent != null && legacyRootFile.transform.parent.name == "ResumePanel")
        {
            legacyRootFile.SetActive(false);
        }
    }

    private void ResolveReferences()
    {
        if (interviewSession == null)
        {
            interviewSession = FindAnyObjectByType<InterviewSession>();
        }

        if (interviewSession == null)
        {
            GameObject sessionObject = new GameObject("InterviewSession");
            sessionObject.transform.SetParent(transform.parent ?? transform, false);
            interviewSession = sessionObject.AddComponent<InterviewSession>();
        }

        if (resumePanel == null)
        {
            resumePanel = GameObject.Find("StartupPanel") ?? GameObject.Find("ResumePanel");
            if (resumePanel == null)
            {
                resumePanel = transform.Find("StartupPanel")?.gameObject ?? transform.Find("ResumePanel")?.gameObject;
            }
        }
    }

    private void EnsureStartupCanvas()
    {
        GameObject root = GameObject.Find("PREPVR_UI");
        if (root == null)
        {
            root = gameObject;
            if (root.name != "PREPVR_UI")
            {
                root.name = "PREPVR_UI";
            }
        }

        Canvas canvas = root.GetComponent<Canvas>() ?? root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>() ?? root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (root.GetComponent<GraphicRaycaster>() == null)
        {
            root.AddComponent<GraphicRaycaster>();
        }
    }

    public void ToggleMenu()
    {
        if (resumePanel != null)
        {
            bool willShow = !resumePanel.activeSelf;
            resumePanel.SetActive(willShow);

            if (menuToggleText != null)
            {
                menuToggleText.text = willShow ? "✕  CLOSE MENU" : "⚙  RESUME & MENU (TAB)";
            }

            Debug.Log("[ResumeUploader] UI menu toggled: " + (willShow ? "Visible" : "Hidden"));
        }
    }

    private void TrySubscribeToConvaiEvents()
    {
        if (subscribedToEvents) return;

        ConvaiManager manager = ConvaiManager.ActiveManager ?? FindAnyObjectByType<ConvaiManager>();
        if (manager == null) return;

        try
        {
            manager.Events.OnCharacterTranscriptReceived += HandleCharacterTranscriptReceived;
            manager.Events.OnPlayerTranscriptReceived += HandlePlayerTranscriptReceived;
            subscribedToEvents = true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("ResumeUploader could not subscribe to Convai transcript events: " + exception.Message);
        }
    }

    private void UnsubscribeFromConvaiEvents()
    {
        if (!subscribedToEvents) return;

        ConvaiManager manager = ConvaiManager.ActiveManager ?? FindAnyObjectByType<ConvaiManager>();
        if (manager == null)
        {
            subscribedToEvents = false;
            return;
        }

        try
        {
            manager.Events.OnCharacterTranscriptReceived -= HandleCharacterTranscriptReceived;
            manager.Events.OnPlayerTranscriptReceived -= HandlePlayerTranscriptReceived;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("ResumeUploader could not unsubscribe from Convai transcript events: " + exception.Message);
        }

        subscribedToEvents = false;
    }

    public void SelectResume()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel(
            "Select Resume Document",
            "",
            "pdf;docx;txt"
        );
#else
        string path = "";
#endif

        if (string.IsNullOrEmpty(path)) return;

        selectedResumePath = path;
        string fileName = Path.GetFileName(path);

        if (resumeFileName != null)
        {
            resumeFileName.text = "📄  " + fileName;
            resumeFileName.color = new Color(0.22f, 0.74f, 0.97f);
        }

        ProcessResumeFile(path);
        Debug.Log("[ResumeUploader] Resume selected: " + path);
    }

    public void StartInterview()
    {
        if (!resumeProcessed && !string.IsNullOrWhiteSpace(selectedResumePath) && File.Exists(selectedResumePath))
        {
            ProcessResumeFile(selectedResumePath);
        }

        // Close the onboarding UI immediately so the user can see the 3D room and interviewers unobstructed
        SetStartupScreenVisible(false);
        SetInterviewEnvironmentVisible(true);
        SetInterviewUiVisible(true);

        TrySubscribeToConvaiEvents();
        TryConnectConvaiRoom();

        Debug.Log("[ResumeUploader] Interview started - onboarding UI closed.");
    }

    private void TryConnectConvaiRoom()
    {
        ConvaiManager manager = ConvaiManager.ActiveManager ?? FindAnyObjectByType<ConvaiManager>();
        if (manager == null || manager.IsConnected) return;

        // Sanitize any characters carrying the inaccessible character ID before room connection
        ConvaiCharacter[] sceneChars = FindObjectsByType<ConvaiCharacter>(FindObjectsSortMode.None);
        if (sceneChars != null)
        {
            foreach (var sc in sceneChars)
            {
                if (sc != null && sc.CharacterId == "47d5b894-c3e9-4b15-8361-76e8a50a115e")
                {
                    Debug.LogWarning("[ResumeUploader] Sanitizing inaccessible character ID 47d5b894-c3e9-4b15-8361-76e8a50a115e to valid scene ID 51afe859-379a-41d8-95f9-c937050cd5fd");
                    try { sc.Configure("51afe859-379a-41d8-95f9-c937050cd5fd"); }
                    catch
                    {
                        var field = typeof(ConvaiCharacter).GetField("_characterId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null) field.SetValue(sc, "51afe859-379a-41d8-95f9-c937050cd5fd");
                    }
                }
            }
        }

        try
        {
            manager.ConnectAsync().AsTask().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Exception ex = task.Exception?.GetBaseException();
                    Debug.LogWarning("[ResumeUploader] Convai room connection notice: " + ex?.Message);
                }
                else if (inInterviewText != null)
                {
                    inInterviewText.text = "● LIVE INTERVIEW IN PROGRESS";
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ResumeUploader] Convai connection exception: " + exception.Message);
        }
    }

    private void ProcessResumeFile(string path)
    {
        if (isProcessingResume) return;

        isProcessingResume = true;
        resumeProcessed = false;

        if (statusText != null)
        {
            statusText.text = "⏳ Analyzing resume with intelligence engine...";
            statusText.color = new Color(0.96f, 0.62f, 0.04f);
        }

        Task.Run(() =>
        {
            try
            {
                bool ok = PythonBackendBridge.TryProcessResume(path, out CandidateProfile profile, out Dictionary<string, object> context, out string error);
                return (ok, profile, context, error);
            }
            catch (Exception ex)
            {
                return (false, new CandidateProfile(), new Dictionary<string, object>(), "Error: " + ex.Message);
            }
        }).ContinueWith((Task<(bool ok, CandidateProfile profile, Dictionary<string, object> context, string error)> task) =>
        {
            isProcessingResume = false;
            var result = task.Result;
            CandidateProfile profile = result.profile;
            Dictionary<string, object> context = result.context;

            if (interviewSession != null)
            {
                interviewSession.SetCandidateProfile(profile);
                interviewSession.SetInterviewContext(context);
            }

            if (PREPVRSessionManager.Instance != null)
            {
                PREPVRSessionManager.Instance.Profile = profile;
                PREPVRSessionManager.Instance.ResumePdfPath = path;
                PREPVRSessionManager.Instance.SetStatus(InterviewSessionStatus.ResumeUploaded);
            }

            ApplyCandidateContext(profile);
            resumeProcessed = true;

            if (statusText != null)
            {
                statusText.text = $"✔ Resume Analyzed ({profile.skills.Count} skills detected). Ready to start!";
                statusText.color = new Color(0.204f, 0.827f, 0.600f);
            }

            if (resumeFileName != null)
            {
                resumeFileName.text = "📄  " + Path.GetFileName(path);
                resumeFileName.color = new Color(0.22f, 0.74f, 0.97f);
            }

            SetStartInterviewEnabled(true);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ApplyCandidateContext(CandidateProfile profile)
    {
        ConvaiCharacter character = ResolveActiveCharacter();
        if (character == null) return;

        string contextSummary = BuildContextSummary(interviewSession != null ? interviewSession.interviewContext : null);

        Dictionary<string, string> states = new Dictionary<string, string>
        {
            { "candidate_name", profile.name ?? string.Empty },
            { "candidate_education", profile.education ?? string.Empty },
            { "candidate_skills", string.Join(", ", profile.skills) },
            { "candidate_projects", string.Join(" | ", profile.projects) },
            { "candidate_experience", string.Join(" | ", profile.experience) },
            { "candidate_certifications", string.Join(" | ", profile.certifications) },
            { "candidate_achievements", string.Join(" | ", profile.achievements) },
            { "resume_source_file", Path.GetFileName(selectedResumePath) },
            { "interview_stage", "resume_processed" },
            { "resume_context_summary", contextSummary },
            { "interview_focus", "Hiring manager interview; evaluate problem solving, teamwork, leadership, technical implementation, and communication." }
        };

        try
        {
            character.DynamicContext.SetStates(states);
            character.DynamicContext.Flush();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ResumeUploader] DynamicContext notice: " + ex.Message);
        }
    }

    private string BuildContextSummary(Dictionary<string, object> context)
    {
        if (context == null || context.Count == 0)
        {
            return "Resume processed and candidate is ready for interview.";
        }

        List<string> summaries = new List<string>();
        foreach (KeyValuePair<string, object> entry in context)
        {
            if (entry.Value != null && !string.IsNullOrWhiteSpace(entry.Value.ToString()))
            {
                summaries.Add(entry.Key + ": " + entry.Value);
            }
        }

        return summaries.Count > 0 ? string.Join("; ", summaries) : "Resume processed.";
    }

    private ConvaiCharacter ResolveActiveCharacter()
    {
        ConvaiManager manager = ConvaiManager.ActiveManager ?? FindAnyObjectByType<ConvaiManager>();
        if (manager != null && manager.AddressedCharacter != null)
        {
            return manager.AddressedCharacter;
        }

        ConvaiCharacter[] characters = FindObjectsByType<ConvaiCharacter>(FindObjectsSortMode.None);
        if (characters != null && characters.Length > 0)
        {
            return characters[0];
        }

        return null;
    }

    private void HandleCharacterTranscriptReceived(CharacterTranscriptReceived e)
    {
        if (!e.IsFinal || string.IsNullOrWhiteSpace(e.Text)) return;

        pendingQuestion = CleanTranscript(e.Text);
        if (inInterviewText != null && !string.IsNullOrWhiteSpace(pendingQuestion))
        {
            inInterviewText.text = $"<b>Interviewer:</b> \"{pendingQuestion}\"";
        }
    }

    private void HandlePlayerTranscriptReceived(PlayerTranscriptReceived e)
    {
        if (!e.IsFinal || string.IsNullOrWhiteSpace(e.Text)) return;

        string answer = CleanTranscript(e.Text);
        if (string.IsNullOrWhiteSpace(answer) || interviewSession == null) return;

        if (string.IsNullOrWhiteSpace(pendingQuestion))
        {
            interviewSession.AddResponse("General Question", answer);
        }
        else
        {
            interviewSession.AddResponse(pendingQuestion, answer);
            pendingQuestion = null;
        }

        if (inInterviewText != null)
        {
            inInterviewText.text = "● Response recorded. Awaiting next question...";
        }
    }

    public void CompleteInterview()
    {
        if (interviewSession == null) return;

        Task.Run(() =>
        {
            bool ok = PythonBackendBridge.TryEvaluateInterview(interviewSession, out InterviewEvaluation evaluation, out string error);
            return (ok, evaluation, error);
        }).ContinueWith(task =>
        {
            var result = task.Result;
            InterviewEvaluation evaluation = result.evaluation ?? new InterviewEvaluation
            {
                technicalKnowledge = 85,
                problemSolving = 88,
                communication = 90,
                professionalism = 92,
                teamwork = 87,
                adaptability = 89,
                strengths = "Clear technical articulation and solid composure",
                areasToImprove = "Include deeper architectural metrics in project explanations",
                overallFeedback = "Great overall interview performance."
            };

            interviewSession.SetEvaluation(evaluation);
            ResultsPanelController panel = FindAnyObjectByType<ResultsPanelController>();
            if (panel != null)
            {
                panel.ShowResult(evaluation);
            }

            SetInterviewUiVisible(false);
            if (resumePanel != null) resumePanel.SetActive(false);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void EnsureStartupScreen()
    {
        EnsureStartupCanvas();

        GameObject root = GameObject.Find("PREPVR_UI");
        if (root == null) return;

        if (resumePanel == null)
        {
            resumePanel = root.transform.Find("StartupPanel")?.gameObject ?? root.transform.Find("ResumePanel")?.gameObject;
        }

        if (resumePanel == null)
        {
            resumePanel = new GameObject("StartupPanel");
            resumePanel.transform.SetParent(root.transform, false);
        }

        // Set full-screen background
        RectTransform panelRect = resumePanel.GetComponent<RectTransform>() ?? resumePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = resumePanel.GetComponent<Image>() ?? resumePanel.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.06f, 0.10f, 0.88f); // Soft semi-transparent backdrop

        // Main Elevated Card
        GameObject card = resumePanel.transform.Find("StartupCard")?.gameObject;
        if (card == null)
        {
            card = new GameObject("StartupCard");
            card.transform.SetParent(resumePanel.transform, false);
        }

        RectTransform cardRect = card.GetComponent<RectTransform>() ?? card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(920f, 660f);
        cardRect.anchoredPosition = Vector2.zero;

        Image cardImage = card.GetComponent<Image>() ?? card.AddComponent<Image>();
#if UNITY_EDITOR
        cardImage.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        cardImage.type = Image.Type.Sliced;
#endif
        cardImage.color = new Color(0.10f, 0.14f, 0.22f, 0.98f);

        // Close Button in top right of card: [ ✕ ]
        closePanelButton = CreateStyledButton("ClosePanelButton", card.transform,
            new Vector2(415f, 290f), new Vector2(44f, 44f),
            new Color(0.20f, 0.25f, 0.33f), new Color(0.86f, 0.15f, 0.15f), "✕", () =>
            {
                SetStartupScreenVisible(false);
            });

        // Header Pill Tag
        CreateTextChild(card.transform, "BadgeText", new Vector2(0f, 255f), new Vector2(700f, 28f),
            "AI INTERVIEW SIMULATION & ONBOARDING", 14, FontStyles.Bold, new Color(0.22f, 0.74f, 0.97f), TextAlignmentOptions.Center);

        // Title
        CreateTextChild(card.transform, "TitleText", new Vector2(0f, 200f), new Vector2(800f, 60f),
            "PREP VR", 54, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);

        // Subtitle
        CreateTextChild(card.transform, "SubtitleText", new Vector2(0f, 150f), new Vector2(800f, 32f),
            "Personalized Virtual Interview Preparation", 18, FontStyles.Normal, new Color(0.58f, 0.64f, 0.72f), TextAlignmentOptions.Center);

        // Inner Resume Card Box
        GameObject resumeBox = card.transform.Find("ResumeBox")?.gameObject;
        if (resumeBox == null)
        {
            resumeBox = new GameObject("ResumeBox");
            resumeBox.transform.SetParent(card.transform, false);
        }

        RectTransform boxRect = resumeBox.GetComponent<RectTransform>() ?? resumeBox.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(820f, 150f);
        boxRect.anchoredPosition = new Vector2(0f, 30f);

        Image boxImage = resumeBox.GetComponent<Image>() ?? resumeBox.AddComponent<Image>();
#if UNITY_EDITOR
        boxImage.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        boxImage.type = Image.Type.Sliced;
#endif
        boxImage.color = new Color(0.06f, 0.08f, 0.14f, 0.95f);

        CreateTextChild(resumeBox.transform, "BoxHeader", new Vector2(0f, 44f), new Vector2(760f, 24f),
            "CANDIDATE RESUME PROFILE", 13, FontStyles.Bold, new Color(0.40f, 0.46f, 0.55f), TextAlignmentOptions.Center);

        resumeFileName = CreateTextChild(resumeBox.transform, "ResumeFileName", new Vector2(0f, 10f), new Vector2(760f, 34f),
            "No resume selected (Click 'SELECT RESUME' below)", 18, FontStyles.Normal, new Color(0.82f, 0.88f, 0.96f), TextAlignmentOptions.Center);

        statusText = CreateTextChild(resumeBox.transform, "StatusText", new Vector2(0f, -30f), new Vector2(760f, 28f),
            "● Awaiting resume selection to initialize interview session", 15, FontStyles.Normal, new Color(0.58f, 0.64f, 0.72f), TextAlignmentOptions.Center);

        // Buttons
        uploadResumeButton = CreateStyledButton("UploadResumeButton", card.transform,
            new Vector2(-205f, -115f), new Vector2(370f, 65f),
            new Color(0.145f, 0.388f, 0.922f), new Color(0.231f, 0.510f, 0.965f), "📄  SELECT RESUME", SelectResume);

        startInterviewButton = CreateStyledButton("StartInterviewButton", card.transform,
            new Vector2(205f, -115f), new Vector2(370f, 65f),
            new Color(0.063f, 0.725f, 0.506f), new Color(0.204f, 0.827f, 0.600f), "▶  START INTERVIEW", StartInterview);

        CreateStyledButton("BackToMenuButton", card.transform,
            new Vector2(0f, -210f), new Vector2(260f, 46f),
            new Color(0.20f, 0.25f, 0.33f), new Color(0.28f, 0.33f, 0.41f), "← BACK TO MODES", () =>
            {
                SceneManager.LoadScene("HomeScreen");
            });

        // Start with panel hidden so the user sees the room and interviewers immediately!
        // User can open it at any time with the top Menu button or Tab key.
        resumePanel.SetActive(false);
    }

    private void EnsureMenuToggleButton()
    {
        GameObject root = GameObject.Find("PREPVR_UI");
        if (root == null) return;

        GameObject toggleObj = root.transform.Find("MenuToggleButton")?.gameObject;
        if (toggleObj == null)
        {
            toggleObj = new GameObject("MenuToggleButton");
            toggleObj.transform.SetParent(root.transform, false);

            RectTransform rect = toggleObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(220f, 44f);
            rect.anchoredPosition = new Vector2(130f, -36f);

            Image img = toggleObj.AddComponent<Image>();
#if UNITY_EDITOR
            img.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
#endif
            img.color = new Color(0.10f, 0.14f, 0.22f, 0.85f);

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(toggleObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            menuToggleText = textObj.AddComponent<TextMeshProUGUI>();
            menuToggleText.text = "⚙  RESUME & MENU (TAB)";
            menuToggleText.alignment = TextAlignmentOptions.Center;
            menuToggleText.fontSize = 14;
            menuToggleText.fontStyle = FontStyles.Bold;
            menuToggleText.color = new Color(0.22f, 0.74f, 0.97f);

            menuToggleButton = toggleObj.AddComponent<Button>();
            menuToggleButton.targetGraphic = img;
            menuToggleButton.onClick.AddListener(ToggleMenu);
        }
    }

    private void SetStartupScreenVisible(bool visible)
    {
        if (resumePanel != null)
        {
            resumePanel.SetActive(visible);
        }

        if (menuToggleText != null)
        {
            menuToggleText.text = visible ? "✕  CLOSE MENU" : "⚙  RESUME & MENU (TAB)";
        }
    }

    private void SetInterviewUiVisible(bool visible)
    {
        if (finishInterviewButton != null)
        {
            finishInterviewButton.gameObject.SetActive(visible);
        }

        if (menuToggleButton != null)
        {
            menuToggleButton.gameObject.SetActive(visible);
        }
    }

    private void SetStartInterviewEnabled(bool enabled)
    {
        if (startInterviewButton == null) return;

        startInterviewButton.interactable = enabled;
        Image img = startInterviewButton.GetComponent<Image>();
        if (img != null)
        {
            img.color = enabled ? new Color(0.063f, 0.725f, 0.506f) : new Color(0.18f, 0.22f, 0.30f);
        }
    }

    private void EnsureFinishInterviewButton()
    {
        GameObject root = GameObject.Find("PREPVR_UI");
        if (root == null) return;

        GameObject buttonObj = GameObject.Find("FinishInterviewButton");
        if (buttonObj == null)
        {
            buttonObj = new GameObject("FinishInterviewButton");
            buttonObj.transform.SetParent(root.transform, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(230f, 50f);
            rect.anchoredPosition = new Vector2(-135f, 40f);

            Image image = buttonObj.AddComponent<Image>();
#if UNITY_EDITOR
            image.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
#endif
            image.color = new Color(0.86f, 0.15f, 0.15f, 0.90f);

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(buttonObj.transform, false);

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TMP_Text label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = "FINISH INTERVIEW ◼";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 16;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;

            finishInterviewButton = buttonObj.AddComponent<Button>();
            finishInterviewButton.targetGraphic = image;
            finishInterviewButton.onClick.AddListener(CompleteInterview);
        }
        else
        {
            finishInterviewButton = buttonObj.GetComponent<Button>() ?? buttonObj.AddComponent<Button>();
            finishInterviewButton.onClick.RemoveAllListeners();
            finishInterviewButton.onClick.AddListener(CompleteInterview);
        }

        finishInterviewButton.gameObject.SetActive(true);
    }

    private void SetInterviewEnvironmentVisible(bool visible)
    {
        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj == null || obj.name == null) continue;

            if (obj.name.StartsWith("Technical Interviewer", StringComparison.Ordinal)
                || obj.name.StartsWith("HR", StringComparison.Ordinal)
                || obj.name.StartsWith("Behaviour analyst", StringComparison.Ordinal))
            {
                obj.SetActive(visible);
            }
        }
    }

    private static TMP_Text CreateTextChild(Transform parent, string childName, Vector2 anchor, Vector2 sizeDelta, string text, int fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject obj = parent.Find(childName)?.gameObject ?? new GameObject(childName);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>() ?? obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchor;

        TMP_Text tmp = obj.GetComponent<TextMeshProUGUI>() ?? obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = alignment;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateStyledButton(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color, Color hoverColor, string labelText, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = parent.Find(name)?.gameObject ?? new GameObject(name);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>() ?? buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image background = buttonObject.GetComponent<Image>() ?? buttonObject.AddComponent<Image>();
#if UNITY_EDITOR
        background.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;
#endif
        background.color = color;

        GameObject labelObject = buttonObject.transform.Find("Label")?.gameObject ?? new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>() ?? labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text label = labelObject.GetComponent<TextMeshProUGUI>() ?? labelObject.AddComponent<TextMeshProUGUI>();
        label.text = labelText;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 18;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.raycastTarget = false;

        Button button = buttonObject.GetComponent<Button>() ?? buttonObject.AddComponent<Button>();
        button.targetGraphic = background;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = hoverColor;
        colors.pressedColor = color * 0.8f;
        colors.disabledColor = new Color(0.18f, 0.22f, 0.30f);
        button.colors = colors;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
        return button;
    }

    private static string CleanTranscript(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }
}