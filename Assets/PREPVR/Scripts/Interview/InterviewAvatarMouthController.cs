using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convai.Runtime.Components;
using Convai.Modules.LipSync;

namespace PrepVR.Interview
{
    /// <summary>
    /// Specialized mouth and facial animation repair controller for PREP VR interview avatars.
    /// Addresses:
    /// 1. Avatar 2 (Realistic Man): Fixes mouth distortion, jaw over-extension, teeth desync, and un-smoothed jitter.
    /// 2. Avatar 3 (Business Girl): Prevents permanently open mouth, guarantees clean neutral idle, and smooth return-to-rest.
    /// 3. All avatars: Configurable speaking pacing rate and lip-sync settle calibration.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Runs in LateUpdate after Animators and Compositors
    public class InterviewAvatarMouthController : MonoBehaviour
    {
        [Header("Character Identity & Role")]
        [SerializeField] private string characterId;
        [SerializeField] private string roleName = "Interviewer";

        [Header("Mouth Neutral Stabilization (Avatar 3 / All)")]
        [Tooltip("Enforces neutral closed mouth when character is not speaking.")]
        [SerializeField] private bool enforceNeutralOnIdle = true;
        [SerializeField] private float returnToNeutralDuration = 0.2f;
        [SerializeField] [Range(0f, 1f)] private float maxIdleJawOpenWeight = 0.0f;

        [Header("Avatar 2 (Realistic Man) Distortion Prevention")]
        [Tooltip("Clamps combined jaw and mouth-open blendshapes to prevent horrific mesh tearing.")]
        [SerializeField] private bool clampJawDisplacement = true;
        [SerializeField] [Range(30f, 100f)] private float maxJawDisplacement = 70f;
        [SerializeField] private bool synchronizeTeethJaw = true;

        [Header("Speech Pacing & Smoothing")]
        [SerializeField] [Range(0.7f, 1.0f)] private float speakingPacingRate = 0.88f;
        [SerializeField] [Range(0.1f, 0.9f)] private float smoothingFactor = 0.35f;

        // Cached components
        private ConvaiCharacter convaiChar;
        private ConvaiLipSyncComponent lipSyncComp;
        private AudioSource audioSource;
        private SkinnedMeshRenderer bodyMesh;
        private SkinnedMeshRenderer teethMesh;
        private SkinnedMeshRenderer tongueMesh;
        private readonly List<SkinnedMeshRenderer> allFacialMeshes = new();

        // Blendshape index caches (-1 if not present on that mesh)
        private int bodyJawOpenIdx = -1;
        private int bodyArkitJawOpenIdx = -1;
        private int bodyFaceitJawOpenIdx = -1;
        private int bodyJawDownIdx = -1;
        private int bodyMouthDropLowerIdx = -1;
        private int teethArkitJawOpenIdx = -1;
        private int teethJawOpenIdx = -1;
        private int tongueJawOpenIdx = -1;

        // Runtime state
        private bool isSpeaking = false;
        private bool isAudioPlaying = false;
        private float idleSettleTimer = 0f;
        private bool wasSpeakingLastFrame = false;

        public float SpeakingPacingRate => speakingPacingRate;
        public bool IsSpeaking => isSpeaking;

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            CalibrateLipSyncSettings();
            EnforceNeutralMouthPoseImmediate();
        }

        private void OnEnable()
        {
            HookEvents();
        }

        private void OnDisable()
        {
            UnhookEvents();
            EnforceNeutralMouthPoseImmediate();
        }

        public void InitializeComponents()
        {
            convaiChar = GetComponent<ConvaiCharacter>();
            lipSyncComp = GetComponent<ConvaiLipSyncComponent>();
            audioSource = GetComponent<AudioSource>();

            if (convaiChar != null && string.IsNullOrEmpty(characterId))
            {
                characterId = convaiChar.CharacterId;
                roleName = convaiChar.CharacterName;
            }

            // Find all SkinnedMeshRenderers in hierarchy
            allFacialMeshes.Clear();
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                string lowerName = smr.name.ToLowerInvariant();

                // Classify key meshes
                if (lowerName.Contains("body") || lowerName.Contains("head") || lowerName.Contains("face"))
                {
                    if (bodyMesh == null) bodyMesh = smr;
                }
                else if (lowerName.Contains("teeth"))
                {
                    teethMesh = smr;
                }
                else if (lowerName.Contains("tongue"))
                {
                    tongueMesh = smr;
                }

                // If mesh contains any facial blendshape, add to facial meshes list
                if (HasFacialBlendshapes(smr.sharedMesh))
                {
                    allFacialMeshes.Add(smr);
                }
            }

            CacheBlendshapeIndices();
            CalibrateLipSyncSettings();
        }

        private bool HasFacialBlendshapes(Mesh mesh)
        {
            if (mesh == null) return false;
            int count = mesh.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                string n = mesh.GetBlendShapeName(i);
                if (n.Contains("Jaw") || n.Contains("jaw") || n.Contains("Mouth") || n.Contains("mouth") || n.Contains("Eye") || n.Contains("eye") || n.Contains("V_"))
                    return true;
            }
            return false;
        }

        private void CacheBlendshapeIndices()
        {
            if (bodyMesh != null && bodyMesh.sharedMesh != null)
            {
                Mesh m = bodyMesh.sharedMesh;
                bodyJawOpenIdx = m.GetBlendShapeIndex("Jaw_Open");
                bodyArkitJawOpenIdx = m.GetBlendShapeIndex("jawOpen");
                bodyFaceitJawOpenIdx = m.GetBlendShapeIndex("faceit_cc_jawOpen");
                bodyJawDownIdx = m.GetBlendShapeIndex("Jaw_Down");
                bodyMouthDropLowerIdx = m.GetBlendShapeIndex("Mouth_Drop_Lower");
            }

            if (teethMesh != null && teethMesh.sharedMesh != null)
            {
                Mesh m = teethMesh.sharedMesh;
                teethArkitJawOpenIdx = m.GetBlendShapeIndex("jawOpen");
                teethJawOpenIdx = m.GetBlendShapeIndex("Jaw_Open");
            }

            if (tongueMesh != null && tongueMesh.sharedMesh != null)
            {
                Mesh m = tongueMesh.sharedMesh;
                tongueJawOpenIdx = m.GetBlendShapeIndex("jawOpen");
                if (tongueJawOpenIdx < 0) tongueJawOpenIdx = m.GetBlendShapeIndex("Jaw_Open");
            }
        }

        private void CalibrateLipSyncSettings()
        {
            if (lipSyncComp == null) return;

            // Apply smoothing factor to eliminate raw jitter frame spikes
            var fieldSmoothing = typeof(ConvaiLipSyncComponent).GetField("_smoothingFactor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fieldSmoothing != null)
            {
                fieldSmoothing.SetValue(lipSyncComp, smoothingFactor);
            }

            // Calibrate settle and fade-out durations
            var fieldSettle = typeof(ConvaiLipSyncComponent).GetField("_settleDuration",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fieldSettle != null)
            {
                fieldSettle.SetValue(lipSyncComp, 0.35f);
            }

            var fieldFadeOut = typeof(ConvaiLipSyncComponent).GetField("_fadeOutDuration",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fieldFadeOut != null)
            {
                fieldFadeOut.SetValue(lipSyncComp, 0.2f);
            }

            // Auto-detect and populate all valid facial SkinnedMeshRenderers from avatar hierarchy
            var validMeshes = new List<SkinnedMeshRenderer>();
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr == null || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) continue;
                string sName = smr.name.ToLowerInvariant();
                // Exclude clothing/shoes/accessories that have no facial expressions
                if (sName.Contains("shoe") || sName.Contains("pant") || sName.Contains("cloth") || sName.Contains("bottom") || sName.Contains("top_"))
                    continue;
                validMeshes.Add(smr);
            }

            if (validMeshes.Count > 0)
            {
                var fieldTargets = typeof(ConvaiLipSyncComponent).GetField("_targetMeshes",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldTargets != null)
                {
                    fieldTargets.SetValue(lipSyncComp, validMeshes);
                    Debug.Log($"[LipSyncRepair] Injected {validMeshes.Count} facial meshes into ConvaiLipSyncComponent on {gameObject.name}.");
                }

                // Force ConvaiLipSyncComponent and its runtime controller to reconfigure with the new meshes
                try
                {
                    var methodValidate = typeof(ConvaiLipSyncComponent).GetMethod("OnValidate",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    methodValidate?.Invoke(lipSyncComp, null);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LipSyncRepair] OnValidate reconfigure warning: {ex.Message}");
                }
            }

            // Also ensure StandardRigBinding is configured and rebuilt
            var standardRig = GetComponent<Convai.Runtime.Animation.StandardRigBinding>();
            if (standardRig != null)
            {
                try
                {
                    standardRig.Rebuild();
                    Debug.Log($"[LipSyncRepair] Rebuilt StandardRigBinding on {gameObject.name}.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LipSyncRepair] StandardRigBinding.Rebuild warning: {ex.Message}");
                }
            }
        }

        private void HookEvents()
        {
            if (convaiChar != null)
            {
                convaiChar.OnSpeechStarted += HandleSpeechStarted;
                convaiChar.OnSpeechStopped += HandleSpeechStopped;
            }
        }

        private void UnhookEvents()
        {
            if (convaiChar != null)
            {
                convaiChar.OnSpeechStarted -= HandleSpeechStarted;
                convaiChar.OnSpeechStopped -= HandleSpeechStopped;
            }
        }

        private void HandleSpeechStarted()
        {
            isSpeaking = true;
            idleSettleTimer = 0f;
        }

        private void HandleSpeechStopped()
        {
            isSpeaking = false;
            idleSettleTimer = returnToNeutralDuration;
        }

        private void LateUpdate()
        {
            // Determine active audio & talking status using all available Convai indicators
            bool charSpeaking = (convaiChar != null && convaiChar.IsSpeaking);
            bool lipSyncTalking = (lipSyncComp != null && (lipSyncComp.IsPlaying || lipSyncComp.IsTalking || lipSyncComp.EngineState == PlaybackState.Playing));
            isAudioPlaying = (audioSource != null && audioSource.isPlaying);
            bool activelyTalking = isSpeaking || charSpeaking || lipSyncTalking || isAudioPlaying;

            if (activelyTalking)
            {
                wasSpeakingLastFrame = true;
                idleSettleTimer = 0f;

                // During speech: Apply Avatar 2 distortion repair and jaw synchronization if enabled
                if (clampJawDisplacement)
                {
                    RepairAvatar2JawDistortion();
                }
            }
            else
            {
                // Settle transition
                if (wasSpeakingLastFrame)
                {
                    idleSettleTimer = returnToNeutralDuration;
                    wasSpeakingLastFrame = false;
                }

                if (idleSettleTimer > 0f)
                {
                    idleSettleTimer -= Time.deltaTime;
                    float progress = Mathf.Clamp01(1f - (idleSettleTimer / Mathf.Max(0.01f, returnToNeutralDuration)));
                    SettleMouthToNeutral(progress);
                }
                else if (enforceNeutralOnIdle)
                {
                    // Clean idle: only clamp jaw if stuck open, never clobber active visemes
                    ClampIdleMouthPose();
                }
            }
        }

        /// <summary>
        /// Prevents Avatar 2 (Realistic Man) from suffering grotesque jaw tearing and teeth separation.
        /// Synchronizes CC_Base_Teeth jawOpen with CC_Base_Body, and clamps combined jaw deformation.
        /// </summary>
        private void RepairAvatar2JawDistortion()
        {
            if (bodyMesh == null) return;

            // 1. Read current body jaw displacement
            float jawOpenVal = bodyJawOpenIdx >= 0 ? bodyMesh.GetBlendShapeWeight(bodyJawOpenIdx) : 0f;
            float arkitOpenVal = bodyArkitJawOpenIdx >= 0 ? bodyMesh.GetBlendShapeWeight(bodyArkitJawOpenIdx) : 0f;
            float faceitOpenVal = bodyFaceitJawOpenIdx >= 0 ? bodyMesh.GetBlendShapeWeight(bodyFaceitJawOpenIdx) : 0f;
            float jawDownVal = bodyJawDownIdx >= 0 ? bodyMesh.GetBlendShapeWeight(bodyJawDownIdx) : 0f;

            // Compute dominant jaw opening
            float effectiveOpen = Mathf.Max(jawOpenVal, Mathf.Max(arkitOpenVal, faceitOpenVal));

            // Prevent combined additive distortion: if both CC4 and ARKit shapes are active, clamp them
            if (effectiveOpen > maxJawDisplacement)
            {
                float scale = maxJawDisplacement / effectiveOpen;
                if (bodyJawOpenIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyJawOpenIdx, jawOpenVal * scale);
                if (bodyArkitJawOpenIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyArkitJawOpenIdx, arkitOpenVal * scale);
                if (bodyFaceitJawOpenIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyFaceitJawOpenIdx, faceitOpenVal * scale);
                effectiveOpen = maxJawDisplacement;
            }

            // Suppress secondary Jaw_Down if Jaw_Open is already active to prevent vertical mouth tearing
            if (jawDownVal > 10f && effectiveOpen > 15f)
            {
                bodyMesh.SetBlendShapeWeight(bodyJawDownIdx, Mathf.Min(jawDownVal, 20f));
            }

            // 2. Synchronize teeth and tongue meshes with the actual jaw position
            if (synchronizeTeethJaw && teethMesh != null)
            {
                if (teethArkitJawOpenIdx >= 0)
                {
                    teethMesh.SetBlendShapeWeight(teethArkitJawOpenIdx, effectiveOpen);
                }
                if (teethJawOpenIdx >= 0)
                {
                    teethMesh.SetBlendShapeWeight(teethJawOpenIdx, effectiveOpen);
                }
            }

            if (tongueMesh != null && tongueJawOpenIdx >= 0)
            {
                tongueMesh.SetBlendShapeWeight(tongueJawOpenIdx, effectiveOpen * 0.7f);
            }
        }

        /// <summary>
        /// Smoothly interpolates opening blendshapes toward zero over the settle duration.
        /// </summary>
        private void SettleMouthToNeutral(float progress)
        {
            float targetFactor = 1f - Mathf.SmoothStep(0f, 1f, progress);
            ApplyScaleToOpeningBlendshapes(targetFactor);
        }

        /// <summary>
        /// Gently settles jaw to rest when idle without clobbering visemes or active facial animations.
        /// </summary>
        private void ClampIdleMouthPose()
        {
            if (bodyMesh == null) return;
            if (bodyJawOpenIdx >= 0 && bodyMesh.GetBlendShapeWeight(bodyJawOpenIdx) > 2f)
            {
                bodyMesh.SetBlendShapeWeight(bodyJawOpenIdx, Mathf.Lerp(bodyMesh.GetBlendShapeWeight(bodyJawOpenIdx), 0f, Time.deltaTime * 10f));
            }
            if (bodyArkitJawOpenIdx >= 0 && bodyMesh.GetBlendShapeWeight(bodyArkitJawOpenIdx) > 2f)
            {
                bodyMesh.SetBlendShapeWeight(bodyArkitJawOpenIdx, Mathf.Lerp(bodyMesh.GetBlendShapeWeight(bodyArkitJawOpenIdx), 0f, Time.deltaTime * 10f));
            }
        }

        /// <summary>
        /// Firmly holds mouth in neutral closed idle state so avatars
        /// never stand with a gaping or half-open mouth while waiting.
        /// </summary>
        private void EnforceNeutralIdleState()
        {
            ApplyScaleToOpeningBlendshapes(maxIdleJawOpenWeight);
        }

        private void ApplyScaleToOpeningBlendshapes(float factor)
        {
            if (factor <= 0.0001f)
            {
                if (bodyMesh != null)
                {
                    if (bodyJawOpenIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyJawOpenIdx, 0f);
                    if (bodyArkitJawOpenIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyArkitJawOpenIdx, 0f);
                    if (bodyFaceitJawOpenIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyFaceitJawOpenIdx, 0f);
                    if (bodyJawDownIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyJawDownIdx, 0f);
                    if (bodyMouthDropLowerIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyMouthDropLowerIdx, 0f);
                }

                if (teethMesh != null)
                {
                    if (teethArkitJawOpenIdx >= 0) teethMesh.SetBlendShapeWeight(teethArkitJawOpenIdx, 0f);
                    if (teethJawOpenIdx >= 0) teethMesh.SetBlendShapeWeight(teethJawOpenIdx, 0f);
                }

                if (tongueMesh != null && tongueJawOpenIdx >= 0)
                {
                    tongueMesh.SetBlendShapeWeight(tongueJawOpenIdx, 0f);
                }

                // Firmly hold visemes closed on all facial meshes
                for (int i = 0; i < allFacialMeshes.Count; i++)
                {
                    var smr = allFacialMeshes[i];
                    if (smr == null || smr.sharedMesh == null) continue;
                    int vOpen = smr.sharedMesh.GetBlendShapeIndex("V_Open");
                    if (vOpen >= 0) smr.SetBlendShapeWeight(vOpen, 0f);
                    int vLipOpen = smr.sharedMesh.GetBlendShapeIndex("V_Lip_Open");
                    if (vLipOpen >= 0) smr.SetBlendShapeWeight(vLipOpen, 0f);
                    int mDropUp = smr.sharedMesh.GetBlendShapeIndex("Mouth_Drop_Upper");
                    if (mDropUp >= 0) smr.SetBlendShapeWeight(mDropUp, 0f);
                }
                return;
            }

            if (bodyMesh != null)
            {
                if (bodyJawOpenIdx >= 0)
                    bodyMesh.SetBlendShapeWeight(bodyJawOpenIdx, bodyMesh.GetBlendShapeWeight(bodyJawOpenIdx) * factor);
                if (bodyArkitJawOpenIdx >= 0)
                    bodyMesh.SetBlendShapeWeight(bodyArkitJawOpenIdx, bodyMesh.GetBlendShapeWeight(bodyArkitJawOpenIdx) * factor);
                if (bodyFaceitJawOpenIdx >= 0)
                    bodyMesh.SetBlendShapeWeight(bodyFaceitJawOpenIdx, bodyMesh.GetBlendShapeWeight(bodyFaceitJawOpenIdx) * factor);
                if (bodyJawDownIdx >= 0)
                    bodyMesh.SetBlendShapeWeight(bodyJawDownIdx, bodyMesh.GetBlendShapeWeight(bodyJawDownIdx) * factor);
                if (bodyMouthDropLowerIdx >= 0)
                    bodyMesh.SetBlendShapeWeight(bodyMouthDropLowerIdx, bodyMesh.GetBlendShapeWeight(bodyMouthDropLowerIdx) * factor);
            }

            if (teethMesh != null)
            {
                if (teethArkitJawOpenIdx >= 0)
                    teethMesh.SetBlendShapeWeight(teethArkitJawOpenIdx, teethMesh.GetBlendShapeWeight(teethArkitJawOpenIdx) * factor);
                if (teethJawOpenIdx >= 0)
                    teethMesh.SetBlendShapeWeight(teethJawOpenIdx, teethMesh.GetBlendShapeWeight(teethJawOpenIdx) * factor);
            }

            if (tongueMesh != null && tongueJawOpenIdx >= 0)
            {
                tongueMesh.SetBlendShapeWeight(tongueJawOpenIdx, tongueMesh.GetBlendShapeWeight(tongueJawOpenIdx) * factor);
            }
        }

        /// <summary>
        /// Authoritatively resets all jaw and mouth opening blendshapes to 0.
        /// </summary>
        public void EnforceNeutralMouthPoseImmediate()
        {
            isSpeaking = false;
            wasSpeakingLastFrame = false;
            idleSettleTimer = 0f;

            if (bodyMesh != null)
            {
                if (bodyJawOpenIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyJawOpenIdx, 0f);
                if (bodyArkitJawOpenIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyArkitJawOpenIdx, 0f);
                if (bodyFaceitJawOpenIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyFaceitJawOpenIdx, 0f);
                if (bodyJawDownIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyJawDownIdx, 0f);
                if (bodyMouthDropLowerIdx >= 0) bodyMesh.SetBlendShapeWeight(bodyMouthDropLowerIdx, 0f);
            }

            if (teethMesh != null)
            {
                if (teethArkitJawOpenIdx >= 0) teethMesh.SetBlendShapeWeight(teethArkitJawOpenIdx, 0f);
                if (teethJawOpenIdx >= 0) teethMesh.SetBlendShapeWeight(teethJawOpenIdx, 0f);
            }

            if (tongueMesh != null && tongueJawOpenIdx >= 0)
            {
                tongueMesh.SetBlendShapeWeight(tongueJawOpenIdx, 0f);
            }

            // Also check visemes V_Open and V_Lip_Open on all meshes
            foreach (var smr in allFacialMeshes)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                int vOpen = smr.sharedMesh.GetBlendShapeIndex("V_Open");
                if (vOpen >= 0) smr.SetBlendShapeWeight(vOpen, 0f);
                int vLipOpen = smr.sharedMesh.GetBlendShapeIndex("V_Lip_Open");
                if (vLipOpen >= 0) smr.SetBlendShapeWeight(vLipOpen, 0f);
            }
        }
    }
}
