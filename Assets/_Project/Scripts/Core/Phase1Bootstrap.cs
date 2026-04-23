using UnityEngine;
using UnityEngine.SceneManagement;

namespace JoyconBaseball.Phase1.Core
{
    public sealed class Phase1Bootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateBootstrap()
        {
            // Phase3 シーンは Phase3Bootstrap が管理するので干渉しない
            if (SceneManager.GetActiveScene().name == Phase3Bootstrap.SceneName) return;

            if (FindFirstObjectByType<Phase1Bootstrap>() != null)
            {
                return;
            }

            var bootstrapObject = new GameObject("Phase1Bootstrap");
            bootstrapObject.AddComponent<Phase1Bootstrap>();
        }

        private void Awake()
        {
            if (FindFirstObjectByType<Phase1GameController>() != null) return;
            if (FindFirstObjectByType<Phase2GameController>() != null) return;
            if (FindFirstObjectByType<Phase3GameController>() != null) return;

            gameObject.AddComponent<Phase1GameController>();
        }
    }
}
