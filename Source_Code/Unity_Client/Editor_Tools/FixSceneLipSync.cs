using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using Convai.Modules.LipSync;
using Convai.Runtime.Components;
using PrepVR.Interview;

namespace PrepVR.EditorTesting
{
    public static class FixSceneLipSync
    {
        public static void ApplyFixes()
        {
            Debug.Log("[FixSceneLipSync] Starting scene lip-sync and avatar mouth repair...");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/InterviewRoom.unity");

            // 1. Locate ConvaiLipSyncDefaultMap_ARKitToCC4Extended asset
            string mapPath = "Packages/com.convai.convai-sdk-for-unity/SDK/Modules/LipSync/Resources/LipSync/DefaultMaps/ConvaiLipSyncDefaultMap_ARKitToCC4Extended.asset";
            var arkitMap = AssetDatabase.LoadAssetAtPath<ConvaiLipSyncMapAsset>(mapPath);
            if (arkitMap == null)
            {
                // Fallback by GUID
                string guidPath = AssetDatabase.GUIDToAssetPath("723dcc0a54572524cb370f44facc55e1");
                if (!string.IsNullOrEmpty(guidPath))
                {
                    arkitMap = AssetDatabase.LoadAssetAtPath<ConvaiLipSyncMapAsset>(guidPath);
                }
            }

            if (arkitMap == null)
            {
                Debug.LogError("[FixSceneLipSync] Could not locate ConvaiLipSyncDefaultMap_ARKitToCC4Extended.asset!");
                return;
            }
            Debug.Log($"[FixSceneLipSync] Loaded ARKit to CC4 Extended Map: {arkitMap.name}");

            var lipSyncComponents = UnityEngine.Object.FindObjectsByType<ConvaiLipSyncComponent>(FindObjectsSortMode.None);
            Debug.Log($"[FixSceneLipSync] Found {lipSyncComponents.Length} ConvaiLipSyncComponents.");

            var smoothingField = typeof(ConvaiLipSyncComponent).GetField("_smoothingFactor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var settleField = typeof(ConvaiLipSyncComponent).GetField("_settleDuration",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fadeOutField = typeof(ConvaiLipSyncComponent).GetField("_fadeOutDuration",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var targetsField = typeof(ConvaiLipSyncComponent).GetField("_targetMeshes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var mappingField = typeof(ConvaiLipSyncComponent).GetField("_mapping",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            foreach (var lsc in lipSyncComponents)
            {
                var go = lsc.gameObject;
                Debug.Log($"[FixSceneLipSync] Processing '{go.name}'...");

                // Assign the ARKit -> CC4 Extended mapping
                if (mappingField != null)
                {
                    mappingField.SetValue(lsc, arkitMap);
                }

                // Calibrate smoothing factor and settle durations
                if (smoothingField != null) smoothingField.SetValue(lsc, 0.35f);
                if (settleField != null) settleField.SetValue(lsc, 0.35f);
                if (fadeOutField != null) fadeOutField.SetValue(lsc, 0.2f);

                // Prune hair and clothing from target meshes
                if (targetsField != null)
                {
                    var currentList = targetsField.GetValue(lsc) as List<SkinnedMeshRenderer>;
                    if (currentList != null)
                    {
                        var pruned = new List<SkinnedMeshRenderer>();
                        foreach (var smr in currentList)
                        {
                            if (smr == null || smr.sharedMesh == null) continue;
                            string sName = smr.name.ToLowerInvariant();
                            if (sName.Contains("buzz_cut") || sName.Contains("hair") || sName.Contains("cloth") || sName.Contains("vest") || sName.Contains("shoe") || sName.Contains("pant"))
                            {
                                Debug.Log($"[FixSceneLipSync] Pruned non-facial mesh '{smr.name}' from {go.name}");
                                continue;
                            }
                            pruned.Add(smr);
                        }
                        targetsField.SetValue(lsc, pruned);
                    }
                }

                // Add or update InterviewAvatarMouthController
                var mouthCtrl = go.GetComponent<InterviewAvatarMouthController>();
                if (mouthCtrl == null)
                {
                    mouthCtrl = go.AddComponent<InterviewAvatarMouthController>();
                    Debug.Log($"[FixSceneLipSync] Added InterviewAvatarMouthController to {go.name}");
                }
                mouthCtrl.InitializeComponents();
                mouthCtrl.EnforceNeutralMouthPoseImmediate();

                // Ensure StandardRigBinding is configured on this character
                var rigBinding = go.GetComponent<Convai.Runtime.Animation.StandardRigBinding>();
                if (rigBinding == null)
                {
                    rigBinding = go.AddComponent<Convai.Runtime.Animation.StandardRigBinding>();
                    Debug.Log($"[FixSceneLipSync] Added StandardRigBinding to {go.name}");
                }

                // Explicitly assign clean facial meshes to StandardRigBinding
                var rigMeshesField = typeof(Convai.Runtime.Animation.StandardRigBinding).GetField("facialMeshes",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (rigMeshesField != null)
                {
                    var cleanFacialMeshes = new List<SkinnedMeshRenderer>();
                    var allSmrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    foreach (var smr in allSmrs)
                    {
                        if (smr == null || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) continue;
                        string sName = smr.name.ToLowerInvariant();
                        if (sName.Contains("buzz_cut") || sName.Contains("hair") || sName.Contains("cloth") || sName.Contains("vest") || sName.Contains("shoe") || sName.Contains("pant"))
                            continue;
                        cleanFacialMeshes.Add(smr);
                    }
                    rigMeshesField.SetValue(rigBinding, cleanFacialMeshes);
                    Debug.Log($"[FixSceneLipSync] Configured {cleanFacialMeshes.Count} facial meshes for StandardRigBinding on {go.name}");
                }

                rigBinding.Rebuild();
                EditorUtility.SetDirty(rigBinding);
                EditorUtility.SetDirty(lsc);
                EditorUtility.SetDirty(go);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[FixSceneLipSync] Fixes applied and scene saved: {saved}");
        }
    }
}
