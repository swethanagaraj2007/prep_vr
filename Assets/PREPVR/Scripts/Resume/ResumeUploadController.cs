using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrepVR.Resume
{
    /// <summary>
    /// Controller for the dedicated Resume Upload screen.
    /// Strictly enforces PDF-only resume upload, triggers real PDF parsing,
    /// and populates PREPVRSessionManager.
    /// </summary>
    public class ResumeUploadController : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_InputField rollNumberInputField;
        [SerializeField] private Button browsePdfButton;
        [SerializeField] private Button proceedButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text selectedFileNameText;
        [SerializeField] private TMP_Text statusMessageText;
        [SerializeField] private TMP_Text previewInfoText;
        [SerializeField] private GameObject previewCard;

        [SerializeField] private string targetReviewScene = "InterviewRoom";
        [SerializeField] private string backScene = "InterviewType";

        private bool isProcessing = false;
        private string currentPdfPath = string.Empty;

        // Windows Standalone file dialog struct
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
#endif

        private void Awake()
        {
            EnsureURPCameraData();
            PREPVRSessionManager.EnsureInstance();
            EnsureRollNumberField();

            if (browsePdfButton != null) browsePdfButton.onClick.AddListener(OnBrowsePdfClicked);
            if (proceedButton != null)
            {
                proceedButton.onClick.AddListener(OnProceedClicked);
                SetProceedEnabled(false);
            }
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

            if (previewCard != null) previewCard.SetActive(false);
        }

        private void EnsureURPCameraData()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                mainCam = FindFirstObjectByType<Camera>();
            }

            if (mainCam != null && mainCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() == null)
            {
                mainCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }
        }

        private void Start()
        {
            EnsureRollNumberField();

            // Populate existing roll number if available
            PREPVRSessionManager session = PREPVRSessionManager.Instance;
            if (rollNumberInputField != null)
            {
                if (session != null && !string.IsNullOrWhiteSpace(session.RollNumber))
                {
                    rollNumberInputField.text = session.RollNumber;
                }
                rollNumberInputField.onValueChanged.AddListener(val =>
                {
                    if (PREPVRSessionManager.Instance != null)
                    {
                        PREPVRSessionManager.Instance.RollNumber = val.Trim();
                    }
                });
            }

            // Restore previous resume if user navigated back
            if (session != null && !string.IsNullOrWhiteSpace(session.ResumePdfPath) && File.Exists(session.ResumePdfPath))
            {
                currentPdfPath = session.ResumePdfPath;
                DisplayLoadedProfile(session.Profile, session.ResumePdfPath);
            }
            else
            {
                SetStatus("Please enter your Roll Number and select your PDF resume to begin.", new Color(0.6f, 0.7f, 0.85f));
            }
        }

        public void OnBrowsePdfClicked()
        {
            if (isProcessing) return;

            string selectedPath = OpenPdfFileDialog();
            if (string.IsNullOrWhiteSpace(selectedPath)) return;

            // Strict PDF format validation
            if (!selectedPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("Unsupported file format. Please upload a PDF (.pdf) file only.", new Color(0.95f, 0.35f, 0.35f));
                if (selectedFileNameText != null)
                {
                    selectedFileNameText.text = "Invalid format: " + Path.GetFileName(selectedPath);
                    selectedFileNameText.color = new Color(0.95f, 0.35f, 0.35f);
                }
                SetProceedEnabled(false);
                return;
            }

            if (!File.Exists(selectedPath))
            {
                SetStatus("Selected file could not be found.", new Color(0.95f, 0.35f, 0.35f));
                SetProceedEnabled(false);
                return;
            }

            FileInfo info = new FileInfo(selectedPath);
            if (info.Length == 0)
            {
                SetStatus("Selected PDF file is empty (0 bytes).", new Color(0.95f, 0.35f, 0.35f));
                SetProceedEnabled(false);
                return;
            }

            currentPdfPath = selectedPath;
            if (selectedFileNameText != null)
            {
                selectedFileNameText.text = "[PDF] " + Path.GetFileName(selectedPath);
                selectedFileNameText.color = new Color(0.22f, 0.74f, 0.97f);
            }

            ProcessPdf(selectedPath);
        }

        private string OpenPdfFileDialog()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFilePanel(
                "Select Candidate Resume (PDF Only)",
                "",
                "pdf"
            );
#elif UNITY_STANDALONE_WIN
            OpenFileName ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            ofn.lpstrFilter = "PDF Files (*.pdf)\0*.pdf\0All Files (*.*)\0*.*\0";
            ofn.lpstrFile = new string(new char[260]);
            ofn.nMaxFile = ofn.lpstrFile.Length;
            ofn.lpstrFileTitle = new string(new char[100]);
            ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
            ofn.lpstrTitle = "Select Candidate Resume (PDF Only)";
            ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008; // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR

            if (GetOpenFileName(ofn))
            {
                return ofn.lpstrFile;
            }
            return string.Empty;
#else
            Debug.LogWarning("[ResumeUploadController] File picker not supported on this platform.");
            return string.Empty;
#endif
        }

        private async void ProcessPdf(string pdfPath)
        {
            isProcessing = true;
            SetProceedEnabled(false);
            SetStatus("Parsing resume content and analyzing skills...", new Color(0.96f, 0.62f, 0.04f));

            bool success = false;
            CandidateProfile profile = null;
            string error = null;

            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    success = PythonBackendBridge.TryProcessResume(pdfPath, out profile, out Dictionary<string, object> context, out error);
                }
                catch (Exception ex)
                {
                    success = false;
                    error = ex.Message;
                }
            });

            isProcessing = false;

            if (!success || profile == null)
            {
                SetStatus("Failed to parse PDF: " + (error ?? "Unknown error"), new Color(0.95f, 0.35f, 0.35f));
                SetProceedEnabled(false);
                return;
            }

            PREPVRSessionManager session = PREPVRSessionManager.Instance;
            if (session != null)
            {
                session.ResumePdfPath = pdfPath;
                session.Profile = profile;
                session.SetStatus(InterviewSessionStatus.ResumeUploaded);
            }

            Debug.Log($"[RESUME]\nCandidate profile created:\nName: {profile.name}\nEducation: {profile.education}\nSkills: {string.Join(", ", profile.skills ?? new List<string>())}\nProjects: {string.Join(" | ", profile.projects ?? new List<string>())}\nExperience: {(profile.experience != null && profile.experience.Count > 0 ? string.Join(" | ", profile.experience) : "None")}\nCertifications: {(profile.certifications != null && profile.certifications.Count > 0 ? string.Join(" | ", profile.certifications) : "None")}\nAchievements: {(profile.achievements != null && profile.achievements.Count > 0 ? string.Join(" | ", profile.achievements) : "None")}");

            DisplayLoadedProfile(profile, pdfPath);
        }

        private Coroutine autoProceedCoroutine;

        private void DisplayLoadedProfile(CandidateProfile profile, string pdfPath)
        {
            if (selectedFileNameText != null)
            {
                selectedFileNameText.text = Path.GetFileName(pdfPath);
                selectedFileNameText.color = new Color(0.22f, 0.74f, 0.97f);
            }

            int skillCount = profile?.skills?.Count ?? 0;
            int projCount = profile?.projects?.Count ?? 0;
            string candidateName = !string.IsNullOrWhiteSpace(profile?.name) ? profile.name : "Candidate";

            // Auto-detect roll number from filename or education if not yet entered
            if (rollNumberInputField != null && string.IsNullOrWhiteSpace(rollNumberInputField.text))
            {
                string detectedRoll = ExtractRollNumber(pdfPath, profile);
                if (!string.IsNullOrWhiteSpace(detectedRoll))
                {
                    rollNumberInputField.text = detectedRoll;
                    if (PREPVRSessionManager.Instance != null)
                    {
                        PREPVRSessionManager.Instance.RollNumber = detectedRoll;
                    }
                }
            }

            bool hasRoll = (rollNumberInputField != null && !string.IsNullOrWhiteSpace(rollNumberInputField.text)) ||
                           (PREPVRSessionManager.Instance != null && !string.IsNullOrWhiteSpace(PREPVRSessionManager.Instance.RollNumber));

            if (hasRoll)
            {
                string r = rollNumberInputField != null ? rollNumberInputField.text : PREPVRSessionManager.Instance.RollNumber;
                SetStatus($"[PARSED] {candidateName} (Roll: {r}, {skillCount} skills, {projCount} projects). Entering interview room...", new Color(0.204f, 0.827f, 0.600f));
            }
            else
            {
                SetStatus($"[PARSED] {candidateName} ({skillCount} skills, {projCount} projects). Enter Roll Number below to begin.", new Color(0.22f, 0.74f, 0.97f));
                if (rollNumberInputField != null) rollNumberInputField.Select();
            }

            if (previewInfoText != null)
            {
                string topSkills = profile != null && profile.skills != null && profile.skills.Count > 0 
                    ? string.Join(", ", profile.skills.GetRange(0, Mathf.Min(4, profile.skills.Count))) 
                    : "Skills detected";
                string rollLabel = hasRoll ? $" | <b>Roll:</b> {(rollNumberInputField != null ? rollNumberInputField.text : PREPVRSessionManager.Instance.RollNumber)}" : "";
                previewInfoText.text = $"<b>Candidate:</b> {candidateName}{rollLabel}\n<b>Key Skills:</b> {topSkills}\n<b>Projects:</b> {projCount} detected";
            }

            if (previewCard != null)
            {
                previewCard.SetActive(true);
            }

            SetProceedEnabled(true);

            // Update proceed button text if available
            if (proceedButton != null)
            {
                TMP_Text btnText = proceedButton.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                {
                    btnText.text = "START INTERVIEW >";
                }
            }

            // Auto-transition to InterviewRoom only if roll number is already populated
            if (hasRoll)
            {
                if (autoProceedCoroutine != null) StopCoroutine(autoProceedCoroutine);
                autoProceedCoroutine = StartCoroutine(AutoProceedToInterviewCoroutine(2.0f));
            }
        }

        private string ExtractRollNumber(string pdfPath, CandidateProfile profile)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(pdfPath))
                {
                    string filename = Path.GetFileNameWithoutExtension(pdfPath);
                    var match = System.Text.RegularExpressions.Regex.Match(filename, @"\b([0-9]{2}[A-Za-z]{2,4}[0-9]{2,5}|[A-Za-z]{2,4}[0-9]{3,5})\b");
                    if (match.Success) return match.Value.ToUpperInvariant();
                }
                if (profile != null && !string.IsNullOrWhiteSpace(profile.education))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(profile.education, @"\b([0-9]{2}[A-Za-z]{2,4}[0-9]{2,5}|[A-Za-z]{2,4}[0-9]{3,5})\b");
                    if (match.Success) return match.Value.ToUpperInvariant();
                }
            }
            catch { }
            return string.Empty;
        }

        private void EnsureRollNumberField()
        {
            if (rollNumberInputField != null) return;

            rollNumberInputField = GetComponentInChildren<TMP_InputField>();
            if (rollNumberInputField != null) return;

            // Find CardPanel
            Transform parent = transform.Find("CardPanel");
            if (parent == null)
            {
                Canvas c = GetComponent<Canvas>();
                if (c != null && c.transform.childCount > 1) parent = c.transform.GetChild(1);
            }
            if (parent == null) parent = transform;

            try
            {
                // Create container
                GameObject fieldObj = new GameObject("RollNumberFieldContainer");
                fieldObj.transform.SetParent(parent, false);
                RectTransform rt = fieldObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.61f);
                rt.anchorMax = new Vector2(0.5f, 0.61f);
                rt.sizeDelta = new Vector2(740, 48);
                rt.anchoredPosition = Vector2.zero;

                Image bg = fieldObj.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.11f, 0.18f, 0.95f);

                // Text Area
                GameObject textArea = new GameObject("Text Area");
                textArea.transform.SetParent(fieldObj.transform, false);
                RectTransform taRect = textArea.AddComponent<RectTransform>();
                taRect.anchorMin = Vector2.zero;
                taRect.anchorMax = Vector2.one;
                taRect.offsetMin = new Vector2(16, 4);
                taRect.offsetMax = new Vector2(-16, -4);
                textArea.AddComponent<RectMask2D>();

                // Text
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(textArea.transform, false);
                RectTransform tRect = textObj.AddComponent<RectTransform>();
                tRect.anchorMin = Vector2.zero;
                tRect.anchorMax = Vector2.one;
                tRect.offsetMin = Vector2.zero;
                tRect.offsetMax = Vector2.zero;
                TextMeshProUGUI mainText = textObj.AddComponent<TextMeshProUGUI>();
                mainText.fontSize = 18;
                mainText.color = Color.white;
                mainText.alignment = TextAlignmentOptions.MidlineLeft;

                // Placeholder
                GameObject phObj = new GameObject("Placeholder");
                phObj.transform.SetParent(textArea.transform, false);
                RectTransform phRect = phObj.AddComponent<RectTransform>();
                phRect.anchorMin = Vector2.zero;
                phRect.anchorMax = Vector2.one;
                phRect.offsetMin = Vector2.zero;
                phRect.offsetMax = Vector2.zero;
                TextMeshProUGUI phText = phObj.AddComponent<TextMeshProUGUI>();
                phText.text = "Student Roll Number (e.g. 21CS042)...";
                phText.fontSize = 16;
                phText.color = new Color(0.5f, 0.58f, 0.70f);
                phText.fontStyle = FontStyles.Italic;
                phText.alignment = TextAlignmentOptions.MidlineLeft;

                // TMP_InputField
                rollNumberInputField = fieldObj.AddComponent<TMP_InputField>();
                rollNumberInputField.textViewport = taRect;
                rollNumberInputField.textComponent = mainText;
                rollNumberInputField.placeholder = phText;
                rollNumberInputField.characterLimit = 25;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ResumeUploadController] Note creating roll number field: {ex.Message}");
            }
        }

        private System.Collections.IEnumerator AutoProceedToInterviewCoroutine(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            OnProceedClicked();
        }

        private void SetStatus(string msg, Color color)
        {
            if (statusMessageText != null)
            {
                statusMessageText.text = msg;
                statusMessageText.color = color;
            }
        }

        private void SetProceedEnabled(bool enabled)
        {
            if (proceedButton != null)
            {
                proceedButton.interactable = enabled;
                Image img = proceedButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = enabled ? new Color(0.063f, 0.725f, 0.506f) : new Color(0.18f, 0.22f, 0.30f);
                }
            }
        }

        private bool isTransitioning = false;

        public void OnProceedClicked()
        {
            if (isTransitioning) return;

            if (autoProceedCoroutine != null)
            {
                StopCoroutine(autoProceedCoroutine);
                autoProceedCoroutine = null;
            }

            if (string.IsNullOrWhiteSpace(currentPdfPath) || !File.Exists(currentPdfPath))
            {
                SetStatus("Please select a valid PDF resume first.", new Color(0.95f, 0.35f, 0.35f));
                return;
            }

            // Roll number validation
            string roll = rollNumberInputField != null ? rollNumberInputField.text.Trim() : PREPVRSessionManager.Instance?.RollNumber;
            if (string.IsNullOrWhiteSpace(roll))
            {
                SetStatus("Please enter your Student Roll Number (e.g. 21CS042) to proceed.", new Color(0.95f, 0.35f, 0.35f));
                if (rollNumberInputField != null) rollNumberInputField.Select();
                return;
            }

            if (PREPVRSessionManager.Instance != null)
            {
                PREPVRSessionManager.Instance.RollNumber = roll;
                if (PREPVRSessionManager.Instance.Profile != null)
                {
                    PREPVRSessionManager.Instance.Profile.rollNumber = roll;
                }
            }

            isTransitioning = true;
            Debug.Log("[InterviewFlow] Start Interview clicked");
            Debug.Log($"[InterviewFlow] Student Roll Number: {roll}");
            Debug.Log($"[InterviewFlow] Selected interview type = {PREPVRSessionManager.Instance?.SelectedInterviewType ?? "Final HR Interview"}");
            Debug.Log("[InterviewFlow] Resume processing successful");
            string sceneToLoad = "InterviewRoom";
            if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
            {
                Debug.LogWarning($"[InterviewFlow] Scene '{sceneToLoad}' cannot be loaded. Checking build settings.");
            }
            Debug.Log($"[InterviewFlow] Loading {sceneToLoad} with PDF: {currentPdfPath}");
            Debug.Log($"[ResumeUploadController] Loading interview scene: {sceneToLoad} with PDF: {currentPdfPath}");
            SceneManager.LoadScene(sceneToLoad);
        }

        public void OnBackClicked()
        {
            if (autoProceedCoroutine != null)
            {
                StopCoroutine(autoProceedCoroutine);
                autoProceedCoroutine = null;
            }

            Debug.Log($"[ResumeUploadController] Returning to {backScene}");
            SceneManager.LoadScene(backScene);
        }
    }
}
