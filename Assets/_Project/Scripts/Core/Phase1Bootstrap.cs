using UnityEngine;

namespace JoyconBaseball.Phase1.Core
{
    public sealed class Phase1Bootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateBootstrap()
        {
            if (FindFirstObjectByType<Phase1Bootstrap>() != null)
            {
                return;
            }

            var bootstrapObject = new GameObject("Phase1Bootstrap");
            bootstrapObject.AddComponent<Phase1Bootstrap>();
        }

        private void Awake()
        {
            if (FindFirstObjectByType<Phase1GameController>() != null)
            {
                return;
            }

            gameObject.AddComponent<Phase1GameController>();
        }
    }
}
