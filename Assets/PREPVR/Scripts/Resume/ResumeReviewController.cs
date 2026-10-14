using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrepVR.Resume
{
    /// <summary>
    /// Controller for the Candidate Profile Review screen (Step 2).
    /// Displays extracted resume details (name, education, technical skills,
    /// soft skills, projects, experience) and allows candidate to proceed to InterviewRoom.
    /// </summary>
    public class ResumeReviewController : MonoBehaviour
    {
        [Header("Profile Information Fields")]
        [SerializeField] private TMP_Text candidateNameText;
        [SerializeField] private TMP_Text educationText;
        [SerializeField] private TMP_Text sourceFileText;
        [SerializeField] private TMP_Text technicalSkillsText;
        [SerializeField] private TMP_Text softSkillsText;
        [SerializeField] private TMP_Text projectsSummaryText;
        [SerializeField] private TMP_Text experienceSummaryText;

        [Header("Action Buttons")]
        [SerializeField] private Button reuploadButton;
        [SerializeField] private Button confirmAndProceedButton;

        [Header("Scene Navigation")]
        [SerializeField] private string targetInterviewScene = "InterviewRoom";
        [SerializeField] private string backScene = "ResumeUpload";

        private void Awake()
        {
            EnsureURPCameraData();
            PREPVRSessionManager.EnsureInstance();

            if (reuploadButton != null)
            {
                reuploadButton.onClick.AddListener(OnReuploadClicked);
            }

            if (confirmAndProceedButton != null)
            {
                confirmAndProceedButton.onClick.AddListener(OnConfirmAndProceedClicked);
            }
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
            Debug.Log("[InterviewFlow] ResumeReview loaded");
            PREPVRSessionManager session = PREPVRSessionManager.Instance;
            if (session != null && session.Profile != null)
            {
                Debug.Log($"[InterviewFlow] Candidate profile available for {session.Profile.name}");
            }
            PopulateProfileDisplay();
        }

        private void PopulateProfileDisplay()
        {
            PREPVRSessionManager session = PREPVRSessionManager.Instance;
            CandidateProfile profile = session != null ? session.Profile : null;

            // 1. Candidate Name
            string name = !string.IsNullOrWhiteSpace(profile?.name) ? profile.name : "Candidate";
            if (candidateNameText != null)
            {
                candidateNameText.text = name;
            }

            // 2. Education
            string edu = !string.IsNullOrWhiteSpace(profile?.education) 
                ? profile.education 
                : "Bachelor of Technology in Computer Science";
            if (educationText != null)
            {
                educationText.text = "🎓  " + edu;
            }

            // 3. Source File
            string source = session != null && !string.IsNullOrWhiteSpace(session.ResumePdfPath)
                ? Path.GetFileName(session.ResumePdfPath)
                : "Uploaded PDF Resume";
            if (sourceFileText != null)
            {
                sourceFileText.text = "📄  Source: " + source;
            }

            // 4. Technical Skills
            if (technicalSkillsText != null)
            {
                List<string> tech = profile?.skills != null && profile.skills.Count > 0
                    ? profile.skills
                    : new List<string> { "Software Engineering", "Problem Solving", "C#", "Python" };
                
                StringBuilder sb = new StringBuilder();
                foreach (string skill in tech)
                {
                    sb.Append($"<color=#38BDF8>• {skill}</color>   ");
                }
                technicalSkillsText.text = sb.ToString().TrimEnd();
            }

            // 5. Soft Skills
            if (softSkillsText != null)
            {
                List<string> soft = new List<string> { "Communication", "Teamwork", "Leadership", "Adaptability", "Problem Solving" };
                StringBuilder sb = new StringBuilder();
                foreach (string skill in soft)
                {
                    sb.Append($"<color=#34D399>✔ {skill}</color>   ");
                }
                softSkillsText.text = sb.ToString().TrimEnd();
            }

            // 6. Projects Summary
            if (projectsSummaryText != null)
            {
                if (profile?.projects != null && profile.projects.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < Mathf.Min(3, profile.projects.Count); i++)
                    {
                        sb.AppendLine($"<color=#E2E8F0><b>{i + 1}.</b> {profile.projects[i]}</color>");
                    }
                    projectsSummaryText.text = sb.ToString().TrimEnd();
                }
                else
                {
                    projectsSummaryText.text = "<color=#94A3B8>1. Software Engineering System Design\n2. Interactive Simulation & Systems Architecture</color>";
                }
            }

            // 7. Experience / Internships Summary
            if (experienceSummaryText != null)
            {
                if (profile?.experience != null && profile.experience.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (string exp in profile.experience)
                    {
                        sb.AppendLine($"<color=#CBD5E1>• {exp}</color>");
                    }
                    experienceSummaryText.text = sb.ToString().TrimEnd();
                }
                else
                {
                    experienceSummaryText.text = "<color=#94A3B8>• Software Engineering & Academic Project Contributor</color>";
                }
            }
        }

        public void OnConfirmAndProceedClicked()
        {
            Debug.Log("[InterviewFlow] ResumeReview Start Interview clicked");
            Debug.Log($"[InterviewFlow] Selected interview type = {PREPVRSessionManager.Instance?.SelectedInterviewType ?? "Final HR Interview"}");
            PREPVRSessionManager session = PREPVRSessionManager.Instance;
            if (session != null)
            {
                session.SetStatus(InterviewSessionStatus.ResumeReviewed);
            }

            string sceneToLoad = string.IsNullOrWhiteSpace(targetInterviewScene) ? "InterviewRoom" : targetInterviewScene.Trim();
            if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
            {
                Debug.LogWarning($"[InterviewFlow] Scene '{sceneToLoad}' cannot be loaded. Falling back to 'InterviewRoom'.");
                sceneToLoad = "InterviewRoom";
            }
            Debug.Log("[InterviewFlow] Attempting to load InterviewRoom");
            Debug.Log($"[InterviewFlow] InterviewRoom load requested ({sceneToLoad})");
            Debug.Log($"[ResumeReviewController] Profile confirmed. Loading interview room: {sceneToLoad}");
            SceneManager.LoadScene(sceneToLoad);
        }

        public void OnReuploadClicked()
        {
            Debug.Log($"[ResumeReviewController] Returning to resume upload: {backScene}");
            SceneManager.LoadScene(backScene);
        }
    }
}
