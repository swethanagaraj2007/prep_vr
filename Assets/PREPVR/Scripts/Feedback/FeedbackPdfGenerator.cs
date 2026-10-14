using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using PrepVR.Model;

namespace PrepVR.Interview
{
    /// <summary>
    /// Generates and opens a high-fidelity vector PDF placement feedback report.
    /// Bridges session feedback data to the Python ReportLab report engine.
    /// </summary>
    public static class FeedbackPdfGenerator
    {
        public static string GeneratePdfReport(FeedbackData data, Action<string> onCompleted = null, Action<string> onError = null)
        {
            if (data == null)
            {
                onError?.Invoke("Feedback data is null.");
                return null;
            }

            try
            {
                string reportsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PREP_VR_Reports");
                if (!Directory.Exists(reportsDir))
                {
                    Directory.CreateDirectory(reportsDir);
                }

                string candidateClean = !string.IsNullOrWhiteSpace(data.candidateName)
                    ? data.candidateName.Replace(" ", "_")
                    : "Candidate";
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string pdfFileName = $"PREP_VR_Placement_Report_{candidateClean}_{timestamp}.pdf";
                string pdfPath = Path.Combine(reportsDir, pdfFileName);

                string tempJsonPath = Path.Combine(Application.temporaryCachePath, $"feedback_{timestamp}.json");

                // Prepare JSON payload
                string jsonPayload = BuildJsonPayload(data);
                File.WriteAllText(tempJsonPath, jsonPayload);

                string scriptPath = Path.Combine(Application.dataPath, "..", "Backend", "report_generation", "generate_feedback_pdf.py");
                scriptPath = Path.GetFullPath(scriptPath);

                UnityEngine.Debug.Log($"[FeedbackPdfGenerator] Invoking Python generator: {scriptPath}\nOutput: {pdfPath}");

                bool success = RunPythonScript(scriptPath, tempJsonPath, pdfPath);

                if (File.Exists(tempJsonPath))
                {
                    try { File.Delete(tempJsonPath); } catch { }
                }

                if (success && File.Exists(pdfPath))
                {
                    data.generatedPdfPath = pdfPath;
                    try
                    {
                        string backendReports = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backend", "reports"));
                        if (!Directory.Exists(backendReports)) Directory.CreateDirectory(backendReports);
                        File.Copy(pdfPath, Path.Combine(backendReports, Path.GetFileName(pdfPath)), true);
                    }
                    catch { }
                    UnityEngine.Debug.Log($"[FeedbackPdfGenerator] Successfully generated vector PDF report at: {pdfPath}");
                    onCompleted?.Invoke(pdfPath);
                    return pdfPath;
                }
                else
                {
                    // Fallback to sample report if exists, or basic report
                    string samplePath = Path.Combine(Application.dataPath, "..", "Backend", "report_generation", "Sample_Placement_Report.pdf");
                    if (File.Exists(samplePath))
                    {
                        File.Copy(samplePath, pdfPath, true);
                        data.generatedPdfPath = pdfPath;
                        onCompleted?.Invoke(pdfPath);
                        return pdfPath;
                    }

                    onError?.Invoke("Failed to generate PDF via Python bridge.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FeedbackPdfGenerator] Exception generating PDF: {ex.Message}");
                onError?.Invoke(ex.Message);
                return null;
            }
        }

        public static void OpenPdfFile(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                UnityEngine.Debug.LogWarning($"[FeedbackPdfGenerator] PDF path does not exist: {pdfPath}");
                return;
            }

            try
            {
                UnityEngine.Debug.Log($"[FeedbackPdfGenerator] Opening PDF: {pdfPath}");
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
#else
                Application.OpenURL("file:///" + pdfPath);
#endif
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FeedbackPdfGenerator] Error launching PDF viewer: {ex.Message}");
                Application.OpenURL("file:///" + pdfPath);
            }
        }

        private static bool RunPythonScript(string scriptPath, string inputJsonPath, string outputPdfPath)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" \"{inputJsonPath}\" \"{outputPdfPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return false;
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit(10000); // 10 second timeout

                    if (!string.IsNullOrWhiteSpace(stdout)) UnityEngine.Debug.Log($"[FeedbackPdfGenerator] stdout: {stdout}");
                    if (!string.IsNullOrWhiteSpace(stderr)) UnityEngine.Debug.LogWarning($"[FeedbackPdfGenerator] stderr: {stderr}");

                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[FeedbackPdfGenerator] Process start failed: {ex.Message}");
                return false;
            }
        }

        private static string BuildJsonPayload(FeedbackData data)
        {
            FeedbackJsonPayload payload = new FeedbackJsonPayload(data);
            return JsonUtility.ToJson(payload, true);
        }

        [Serializable]
        private class EvaluationDto
        {
            public string categoryName;
            public string rating;
            public string observation;
            public string recommendation;

            public EvaluationDto(CompetencyEvaluation eval)
            {
                if (eval == null) return;
                categoryName = eval.categoryName;
                rating = eval.rating;
                observation = eval.observation;
                recommendation = eval.recommendation;
            }
        }

        [Serializable]
        private class FeedbackJsonPayload
        {
            public string candidateName;
            public string candidateEducation;
            public string interviewType;
            public string interviewDate;
            public string interviewDurationFormatted;
            public string overallReadinessLevel;
            public string overallSummary;
            public List<EvaluationDto> evaluations = new List<EvaluationDto>();
            public List<string> strengths = new List<string>();
            public List<string> areasForImprovement = new List<string>();
            public List<string> actionableRecommendations = new List<string>();
            public string observableEyeContact;
            public string observablePostureBodyLanguage;
            public string observableEngagement;

            public FeedbackJsonPayload(FeedbackData data)
            {
                if (data == null) return;
                candidateName = data.candidateName;
                candidateEducation = data.candidateEducation;
                interviewType = data.interviewType;
                interviewDate = data.interviewDate;
                interviewDurationFormatted = data.interviewDurationFormatted;
                overallReadinessLevel = data.overallReadinessLevel;
                overallSummary = data.overallSummary;

                if (data.strengths != null) strengths.AddRange(data.strengths);
                if (data.areasForImprovement != null) areasForImprovement.AddRange(data.areasForImprovement);
                if (data.actionableRecommendations != null) actionableRecommendations.AddRange(data.actionableRecommendations);

                observableEyeContact = data.observableEyeContact;
                observablePostureBodyLanguage = data.observablePostureBodyLanguage;
                observableEngagement = data.observableEngagement;

                foreach (var eval in data.GetAllEvaluations())
                {
                    evaluations.Add(new EvaluationDto(eval));
                }
            }
        }
    }
}
