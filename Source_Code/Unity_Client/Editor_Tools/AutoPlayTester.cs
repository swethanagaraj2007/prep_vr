using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Convai.Runtime.Components;
using Convai.Runtime;

namespace PrepVR.EditorTesting
{
    public static class AutoPlayTester
    {
        public static string CheckAllCharacterIDs()
        {
            var sb = new StringBuilder();
            string apiKey = ConvaiSettings.Instance.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                return "ERROR: ConvaiSettings.Instance.ApiKey is null or empty!";
            }

            string[] testIds = new string[]
            {
                "96d2f7ff-c33b-4b1f-8c58-b5798da695e2", // User Prompt: HR
                "67cdd4cc-8124-4301-8117-1355734b7faa", // User Prompt: Behavioral
                "7b1aedff-1322-4c5e-8fe5-3d7ed2e8f200", // User Prompt: Hiring Manager
                "1a49ba1e-4a24-4040-bab0-3e1c6623c85a", // Scene: HR
                "f10ec8e4-998d-4f3c-bb4d-260f55427e9e", // Scene: Behaviour analyst
                "8f23bb25-483d-4890-907b-36de8c20d331"  // Scene: Technical Interviewer
            };

            foreach (var id in testIds)
            {
                sb.AppendLine($"=== Character ID: {id} ===");
                try
                {
                    using (UnityWebRequest req = new UnityWebRequest("https://api.convai.com/character/get", "POST"))
                    {
                        string body = "{\"charID\":\"" + id + "\"}";
                        byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                        req.downloadHandler = new DownloadHandlerBuffer();
                        req.SetRequestHeader("Content-Type", "application/json");
                        req.SetRequestHeader("CONVAI-API-KEY", apiKey);

                        var op = req.SendWebRequest();
                        while (!op.isDone) { }

                        if (req.result == UnityWebRequest.Result.Success)
                        {
                            sb.AppendLine("  SUCCESS Response: " + req.downloadHandler.text);
                        }
                        else
                        {
                            sb.AppendLine($"  FAILED ({req.responseCode}): {req.error} - {req.downloadHandler.text}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  EXCEPTION: {ex.Message}");
                }
            }

            return sb.ToString();
        }

        public static string CheckCharacterTriggers(string id)
        {
            var sb = new StringBuilder();
            string apiKey = ConvaiSettings.Instance.ApiKey;
            try
            {
                using (UnityWebRequest req = new UnityWebRequest("https://api.convai.com/character/narrative/list-triggers", "POST"))
                {
                    string body = "{\"character_id\":\"" + id + "\"}";
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                    req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("CONVAI-API-KEY", apiKey);

                    var op = req.SendWebRequest();
                    while (!op.isDone) { }

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        sb.AppendLine($"Triggers for {id}: " + req.downloadHandler.text);
                    }
                    else
                    {
                        sb.AppendLine($"Triggers failed for {id} ({req.responseCode}): {req.error} - {req.downloadHandler.text}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"EXCEPTION: {ex.Message}");
            }
            return sb.ToString();
        }

        public static string CheckCharacterSections(string id)
        {
            var sb = new StringBuilder();
            string apiKey = ConvaiSettings.Instance.ApiKey;
            try
            {
                using (UnityWebRequest req = new UnityWebRequest("https://api.convai.com/character/narrative/list-sections", "POST"))
                {
                    string body = "{\"character_id\":\"" + id + "\"}";
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                    req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("CONVAI-API-KEY", apiKey);

                    var op = req.SendWebRequest();
                    while (!op.isDone) { }

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        sb.AppendLine($"Sections for {id}: " + req.downloadHandler.text);
                    }
                    else
                    {
                        sb.AppendLine($"Sections failed for {id} ({req.responseCode}): {req.error} - {req.downloadHandler.text}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"EXCEPTION: {ex.Message}");
            }
            return sb.ToString();
        }

        public static string DiagnoseCharacters()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Diagnose] isPlaying: {EditorApplication.isPlaying}");
            sb.AppendLine($"[Diagnose] Active scene: {SceneManager.GetActiveScene().name} ({SceneManager.GetActiveScene().path})");

            var characters = UnityEngine.Object.FindObjectsByType<ConvaiCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"[Diagnose] Total ConvaiCharacters in memory/scene: {characters.Length}");
            foreach (var ch in characters)
            {
                sb.AppendLine($"  - GameObject: '{ch.gameObject.name}', Active: {ch.gameObject.activeInHierarchy}, ID: '{ch.CharacterId}', Name: '{ch.CharacterName}'");
            }

            var manager = ConvaiManager.ActiveManager;
            sb.AppendLine($"[Diagnose] ConvaiManager Active: {manager != null}");
            if (manager != null)
            {
                sb.AppendLine($"  IsConnected: {manager.IsConnected}");
                sb.AppendLine($"  AddressedCharacter: {manager.AddressedCharacter?.CharacterName} ({manager.AddressedCharacter?.CharacterId})");
                sb.AppendLine($"  ConversationAvailability: {manager.ConversationAvailability}");
            }

            return sb.ToString();
        }
    }
}
