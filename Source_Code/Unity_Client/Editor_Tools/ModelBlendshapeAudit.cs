using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using Convai.Modules.LipSync;
using Convai.Runtime.Components;
using Convai.Runtime;

namespace PrepVR.EditorTesting
{
    public static class ModelBlendshapeAudit
    {
        public static void RunAudit()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MODEL BLENDSHAPE AUDIT START ===");

            EditorSceneManager.OpenScene("Assets/Scenes/InterviewRoom.unity");
            var lipSyncComponents = UnityEngine.Object.FindObjectsByType<ConvaiLipSyncComponent>(FindObjectsSortMode.None);
            sb.AppendLine($"Found {lipSyncComponents.Length} ConvaiLipSyncComponents in InterviewRoom.");

            foreach (var lsc in lipSyncComponents)
            {
                sb.AppendLine($"\n--- GameObject: '{lsc.gameObject.name}', Active: {lsc.gameObject.activeInHierarchy} ---");
                sb.AppendLine($"LockedProfile: {lsc.LockedProfile.Value}, Mapping: {(lsc.Mapping != null ? lsc.Mapping.name : "null")}");
                
                var charComp = lsc.GetComponent<ConvaiCharacter>();
                if (charComp != null)
                {
                    sb.AppendLine($"ConvaiCharacter ID: {charComp.CharacterId}, Name: {charComp.CharacterName}");
                }

                var targetMeshes = lsc.TargetMeshes;
                sb.AppendLine($"Target Meshes count: {targetMeshes.Count}");
                for (int m = 0; m < targetMeshes.Count; m++)
                {
                    var smr = targetMeshes[m];
                    if (smr == null)
                    {
                        sb.AppendLine($"  Mesh [{m}]: NULL!");
                        continue;
                    }
                    var mesh = smr.sharedMesh;
                    if (mesh == null)
                    {
                        sb.AppendLine($"  Mesh [{m}]: '{smr.name}' has NULL sharedMesh!");
                        continue;
                    }

                    sb.AppendLine($"  Mesh [{m}]: '{smr.gameObject.name}' (Mesh: '{mesh.name}'), Blendshapes: {mesh.blendShapeCount}");
                    for (int b = 0; b < mesh.blendShapeCount; b++)
                    {
                        string bName = mesh.GetBlendShapeName(b);
                        float currentWeight = smr.GetBlendShapeWeight(b);
                        sb.AppendLine($"    [{b}] '{bName}' = {currentWeight}");
                    }
                }

                // Also check Animator
                var anim = lsc.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    sb.AppendLine($"  Animator: {(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "None")}, avatar: {(anim.avatar != null ? anim.avatar.name : "None")}");
                }

                // Check StandardRigBinding
                var rig = lsc.GetComponent<Convai.Runtime.Animation.StandardRigBinding>();
                if (rig != null)
                {
                    rig.Rebuild();
                    sb.AppendLine($"  [StandardRigBinding] DetectedConvention: {rig.DetectedConvention}, Confidence: {rig.DetectionConfidence:F2}");
                    sb.AppendLine($"  [StandardRigBinding] FacialMeshes ({rig.FacialMeshes.Count}):");
                    foreach (var fm in rig.FacialMeshes)
                    {
                        sb.AppendLine($"    -> {(fm != null ? fm.name : "null")}");
                    }

                    int resolvedCount = 0;
                    int missingCount = 0;
                    var missingList = new System.Collections.Generic.List<string>();

                    foreach (Convai.Domain.Embodiment.Semantics.StandardBlendshape sbEnum in System.Enum.GetValues(typeof(Convai.Domain.Embodiment.Semantics.StandardBlendshape)))
                    {
                        if (rig.TryGetBlendshape(sbEnum, out SkinnedMeshRenderer smrMesh, out int bsIdx))
                        {
                            resolvedCount++;
                        }
                        else
                        {
                            missingCount++;
                            missingList.Add(sbEnum.ToString());
                        }
                    }

                    sb.AppendLine($"  [StandardRigBinding] Blendshape Resolution: {resolvedCount} resolved, {missingCount} unresolved.");
                    if (missingList.Count > 0)
                    {
                        sb.AppendLine($"  [StandardRigBinding] Unresolved semantics: {string.Join(", ", missingList)}");
                    }
                }
                else
                {
                    sb.AppendLine("  [StandardRigBinding] NOT ATTACHED!");
                }
            }

            sb.AppendLine("\n=== CONVAI CHARACTER API DETAILS ===");
            string apiKey = ConvaiSettings.Instance != null ? ConvaiSettings.Instance.ApiKey : "";
            sb.AppendLine($"API Key present: {!string.IsNullOrEmpty(apiKey)}");
            string[] charIds = new string[]
            {
                "39e56af2-5a98-406f-90e6-ddb52728b542",
                "4ebdede2-8ec8-4254-8f03-40047800c032",
                "04b5c5e7-998c-4731-ac35-ef176ac44707",
                "c493d354-3215-4c62-8906-ed3ae499004b",
                "d8ab4f7e-c75b-4e13-a684-f61ee2452006",
                "84d5ff7d-03e3-44b0-9365-62387127b413"
            };

            foreach (var cid in charIds)
            {
                try
                {
                    using (var req = new UnityEngine.Networking.UnityWebRequest("https://api.convai.com/character/get", "POST"))
                    {
                        byte[] bodyRaw = Encoding.UTF8.GetBytes("{\"charID\":\"" + cid + "\"}");
                        req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                        req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                        req.SetRequestHeader("Content-Type", "application/json");
                        req.SetRequestHeader("CONVAI-API-KEY", apiKey);
                        var op = req.SendWebRequest();
                        while (!op.isDone) { }
                        sb.AppendLine($"Char {cid}: {(req.result == UnityEngine.Networking.UnityWebRequest.Result.Success ? req.downloadHandler.text : req.error)}");
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Char {cid} exception: {ex.Message}");
                }
            }

            sb.AppendLine("\n=== FBX PREFAB DEFAULT BLENDSHAPES ===");
            var bgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/BusinesssGirl/Business girl.fbx");
            if (bgPrefab != null)
            {
                var smrs = bgPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in smrs)
                {
                    if (smr.sharedMesh != null)
                    {
                        for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                        {
                            float w = smr.GetBlendShapeWeight(i);
                            if (w > 0.01f)
                            {
                                sb.AppendLine($"Business girl FBX default: '{smr.name}' -> [{i}] '{smr.sharedMesh.GetBlendShapeName(i)}' = {w}");
                            }
                        }
                    }
                }
            }

            var rmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Male/Realistic man.fbx");
            if (rmPrefab != null)
            {
                var smrs = rmPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in smrs)
                {
                    if (smr.sharedMesh != null)
                    {
                        for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                        {
                            float w = smr.GetBlendShapeWeight(i);
                            if (w > 0.01f)
                            {
                                sb.AppendLine($"Realistic man FBX default: '{smr.name}' -> [{i}] '{smr.sharedMesh.GetBlendShapeName(i)}' = {w}");
                            }
                        }
                    }
                }
            }

            sb.AppendLine("\n=== MODEL BLENDSHAPE AUDIT END ===");
            File.WriteAllText("blendshape_audit_output.txt", sb.ToString());
            Debug.Log("Blendshape Audit Written successfully to blendshape_audit_output.txt");
        }
    }
}
