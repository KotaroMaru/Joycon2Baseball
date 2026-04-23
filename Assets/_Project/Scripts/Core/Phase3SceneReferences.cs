using JoyconBaseball.Phase1.Audio;
using JoyconBaseball.Phase1.Gameplay;
using JoyconBaseball.Phase1.UI;
using UnityEngine;

namespace JoyconBaseball.Phase1.Core
{
    /// <summary>
    /// Phase3 (一人プレイ・ピッチャーモード) シーンの参照コンテナ。
    /// Inspector で全フィールドを設定してください。
    /// </summary>
    public sealed class Phase3SceneReferences : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("ピッチャー視点カメラ（フルスクリーン）")]
        [SerializeField] private Camera pitcherCamera;

        [Header("Gameplay")]
        [SerializeField] private BatController batController;
        [Tooltip("バットの手元にある回転ピボット (= Joycon2ControllerModel がついているオブジェクト)")]
        [SerializeField] private Transform batPivot;
        [SerializeField] private AIBatterController aiBatterController;
        [SerializeField] private PitchingMachine pitchingMachine;
        [SerializeField] private BoxCollider strikeZoneCollider;
        [SerializeField] private GameObject ballPrefab;

        [Header("Pitcher")]
        [SerializeField] private PitcherController pitcherController;
        [SerializeField] private PitcherHudController pitcherHudController;
        [SerializeField] private Joycon2ControllerModel pitcherJoyconModel;
        [Tooltip("ピッチャーアームの手元ボール（投球時にここから飛ばす）")]
        [SerializeField] private GameObject pitchArmBall;

        [Header("UI")]
        [SerializeField] private Phase1UIController uiController;

        [Header("Audio")]
        [SerializeField] private Phase1AudioManager audioManager;
        [SerializeField] private AudioClip bgmClip;
        [SerializeField] private AudioClip startClip;
        [SerializeField] private AudioClip hitStrongClip;
        [SerializeField] private AudioClip hitNormalClip;
        [SerializeField] private AudioClip hitWeakClip;
        [SerializeField] private AudioClip swingClip;
        [SerializeField] private AudioClip catcherCatchClip;
        [SerializeField] private AudioClip strikeClip;
        [SerializeField] private AudioClip ballClip;
        [SerializeField] private AudioClip outClip;
        [SerializeField] private AudioClip cheeringClip;

        public Camera                PitcherCamera        => pitcherCamera;
        public BatController         BatController        => batController;
        public Transform             BatPivot             => batPivot;
        public AIBatterController    AIBatterController   => aiBatterController;
        public PitchingMachine       PitchingMachine      => pitchingMachine;
        public BoxCollider           StrikeZoneCollider   => strikeZoneCollider;
        public GameObject            BallPrefab           => ballPrefab;
        public PitcherController     PitcherController    => pitcherController;
        public PitcherHudController  PitcherHudController => pitcherHudController;
        public Joycon2ControllerModel PitcherJoyconModel  => pitcherJoyconModel;
        public GameObject            PitchArmBall         => pitchArmBall;
        public Phase1UIController    UiController         => uiController;
        public Phase1AudioManager    AudioManager         => audioManager;
        public AudioClip BgmClip          => bgmClip;
        public AudioClip StartClip        => startClip;
        public AudioClip HitStrongClip    => hitStrongClip;
        public AudioClip HitNormalClip    => hitNormalClip;
        public AudioClip HitWeakClip      => hitWeakClip;
        public AudioClip SwingClip        => swingClip;
        public AudioClip CatcherCatchClip => catcherCatchClip;
        public AudioClip StrikeClip       => strikeClip;
        public AudioClip BallClip         => ballClip;
        public AudioClip OutClip          => outClip;
        public AudioClip CheeringClip     => cheeringClip;
    }
}
