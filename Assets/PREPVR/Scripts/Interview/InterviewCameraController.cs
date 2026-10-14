using System;
using System.Collections;
using UnityEngine;

namespace PrepVR.Interview
{
    /// <summary>
    /// Smoothly frames and focuses the candidate camera on the active interviewer.
    /// Provides professional broadcast/video-call framing transitions between:
    /// - Stage 1: HR Interviewer (sitting left)
    /// - Stage 2: Behavioral Interviewer (sitting center)
    /// - Stage 3: Hiring Manager / Team Lead (sitting right)
    /// Transitions are smooth (e.g. 1.5s damped rotation/framing) with zero teleportation.
    /// </summary>
    public class InterviewCameraController : MonoBehaviour
    {
        [Header("Targeting & Framing")]
        [SerializeField] private Transform targetInterviewer;
        [SerializeField] private float focusHeightOffset = 1.35f; // Upper chest / face level of seated interviewer
        [SerializeField] private float transitionDuration = 1.5f;

        private Camera targetCamera;
        private Coroutine activeTransitionCoroutine;
        private Quaternion targetRotation;

        public static InterviewCameraController Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            ResolveCamera();
        }

        private void Start()
        {
            ResolveCamera();
        }

        private void ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = FindFirstObjectByType<Camera>();
                }
            }
        }

        /// <summary>
        /// Smoothly pans and frames the camera toward the active interviewer.
        /// </summary>
        public void FocusOn(Transform interviewerTransform)
        {
            if (interviewerTransform == null) return;
            ResolveCamera();
            if (targetCamera == null) return;

            targetInterviewer = interviewerTransform;

            Vector3 cameraPos = targetCamera.transform.position;
            Vector3 targetLookPoint = interviewerTransform.position + Vector3.up * focusHeightOffset;
            Vector3 direction = (targetLookPoint - cameraPos).normalized;

            if (direction.sqrMagnitude > 0.001f)
            {
                targetRotation = Quaternion.LookRotation(direction);
                if (activeTransitionCoroutine != null)
                {
                    StopCoroutine(activeTransitionCoroutine);
                }
                activeTransitionCoroutine = StartCoroutine(SmoothRotateCameraCoroutine(targetRotation, transitionDuration));
            }
        }

        private IEnumerator SmoothRotateCameraCoroutine(Quaternion desiredRot, float duration)
        {
            if (targetCamera == null) yield break;

            Quaternion startRot = targetCamera.transform.rotation;
            float elapsed = 0f;

            Debug.Log($"[InterviewCamera] Smoothly framing camera toward {targetInterviewer?.name} over {duration:F1}s.");

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                targetCamera.transform.rotation = Quaternion.Slerp(startRot, desiredRot, t);
                yield return null;
            }

            targetCamera.transform.rotation = desiredRot;
            activeTransitionCoroutine = null;
        }
    }
}
