using UnityEngine;
using UnityEngine.SceneManagement;

namespace JoyconBaseball.Phase1.Core
{
    /// <summary>
    /// "Game_SoloPitcher" シーンの起動ブートストラップ。
    ///
    /// Phase3GameController がシーンに事前配置されている場合:
    ///   → disabled になっていても FindObjectsInactive.Include で検出し enabled に戻す
    /// 事前配置されていない場合:
    ///   → 新規 GameObject を生成して Phase3GameController を追加する
    /// </summary>
    public sealed class Phase3Bootstrap : MonoBehaviour
    {
        public const string SceneName = "Game_SoloPitcher";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateBootstrap()
        {
            if (SceneManager.GetActiveScene().name != SceneName) return;
            if (FindFirstObjectByType<Phase3Bootstrap>(FindObjectsInactive.Include) != null) return;

            var obj = new GameObject("Phase3Bootstrap");
            obj.AddComponent<Phase3Bootstrap>();
        }

        private void Awake()
        {
            if (FindFirstObjectByType<Phase3GameController>() != null) return;

            // シーンに存在しない場合は動的生成
            Debug.Log("[Phase3Bootstrap] Phase3GameController が見つからないため動的生成します。");
            gameObject.AddComponent<Phase3GameController>();
        }
    }
}
