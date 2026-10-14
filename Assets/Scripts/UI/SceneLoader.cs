using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrepVR.UI
{
    /// <summary>
    /// Reusable scene loader component for UI buttons.
    /// Provides simple methods to navigate between scenes by name or index.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        [Header("Default Target Scene (Optional)")]
        [Tooltip("Optional scene name to load when calling LoadDefaultScene()")]
        [SerializeField] private string defaultSceneName;

        /// <summary>
        /// Loads the specified scene by name.
        /// </summary>
        /// <param name="sceneName">The exact name of the scene to load.</param>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[SceneLoader] Scene name is null or empty!");
                return;
            }

            Debug.Log($"[SceneLoader] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }

        public void SelectDesktopModeAndLoad(string sceneName)
        {
            PREPVRSessionManager.EnsureInstance().SelectedMode = "DESK";
            Debug.Log("[SceneLoader] Selected Mode: DESK");
            LoadScene(sceneName);
        }

        public void SelectVRModeAndLoad(string sceneName)
        {
            PREPVRSessionManager.EnsureInstance().SelectedMode = "VR";
            Debug.Log("[SceneLoader] Selected Mode: VR");
            LoadScene(sceneName);
        }

        /// <summary>
        /// Loads the specified scene asynchronously by name.
        /// </summary>
        /// <param name="sceneName">The exact name of the scene to load.</param>
        public void LoadSceneAsync(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[SceneLoader] Scene name is null or empty!");
                return;
            }

            Debug.Log($"[SceneLoader] Loading scene asynchronously: {sceneName}");
            SceneManager.LoadSceneAsync(sceneName);
        }

        /// <summary>
        /// Loads the default scene set in the inspector.
        /// </summary>
        public void LoadDefaultScene()
        {
            if (!string.IsNullOrEmpty(defaultSceneName))
            {
                LoadScene(defaultSceneName);
            }
            else
            {
                Debug.LogWarning("[SceneLoader] No default scene name configured!");
            }
        }

        /// <summary>
        /// Loads a scene by its index in Build Settings.
        /// </summary>
        /// <param name="sceneIndex">Build index of the target scene.</param>
        public void LoadSceneByIndex(int sceneIndex)
        {
            Debug.Log($"[SceneLoader] Loading scene index: {sceneIndex}");
            SceneManager.LoadScene(sceneIndex);
        }

        /// <summary>
        /// Reloads the currently active scene.
        /// </summary>
        public void ReloadCurrentScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            Debug.Log($"[SceneLoader] Reloading active scene: {currentScene}");
            SceneManager.LoadScene(currentScene);
        }

        /// <summary>
        /// Quits the application or stops play mode in editor.
        /// </summary>
        public void QuitApplication()
        {
            Debug.Log("[SceneLoader] Quitting application...");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
