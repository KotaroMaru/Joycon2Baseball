using JoyconBaseball.Phase1.Gameplay;
using JoyconBaseball.Phase1.UI;
using UnityEngine;

namespace JoyconBaseball.Phase1.Core
{
    public sealed class Phase1SceneReferences : MonoBehaviour
    {
        [Header("Fallbacks")]
        [SerializeField] private bool createDirectionalLightIfMissing = true;

        [Header("Core")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Phase1UIController uiController;

        [Header("Gameplay")]
        [SerializeField] private BatController batController;
        [SerializeField] private Transform batPivot;
        [SerializeField] private PitchingMachine pitchingMachine;
        [SerializeField] private BoxCollider strikeZoneCollider;

        [Header("Optional Prefabs")]
        [SerializeField] private GameObject ballPrefab;

        [Header("Joy-Con 2")]
        [SerializeField] private bool useJoyconGyroBatControl;
        [SerializeField] private float joyconSwingThreshold = 1.35f;

        [Header("Camera Tracking")]
        [SerializeField] private bool enableBallTracking = true;
        [SerializeField] private Vector3 fieldForwardDirection = new Vector3(-0.707f, 0f, 0.707f);
        [SerializeField] private float viewportExitMargin = 0.1f;
        [SerializeField] private float directionAngleThreshold = 25f;

        [Header("Audio")]
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

        public Camera GameplayCamera => gameplayCamera;
        public Phase1UIController UiController => uiController;
        public BatController BatController => batController;
        public Transform BatPivot => batPivot;
        public PitchingMachine PitchingMachine => pitchingMachine;
        public BoxCollider StrikeZoneCollider => strikeZoneCollider;
        public GameObject BallPrefab => ballPrefab;
        public bool CreateDirectionalLightIfMissing => createDirectionalLightIfMissing;
        public bool UseJoyconGyroBatControl => useJoyconGyroBatControl;
        public float JoyconSwingThreshold => joyconSwingThreshold;
        public bool EnableBallTracking => enableBallTracking;
        public Vector3 FieldForwardDirection => fieldForwardDirection;
        public float ViewportExitMargin => viewportExitMargin;
        public float DirectionAngleThreshold => directionAngleThreshold;
        public AudioClip BgmClip => bgmClip;
        public AudioClip StartClip => startClip;
        public AudioClip HitStrongClip => hitStrongClip;
        public AudioClip HitNormalClip => hitNormalClip;
        public AudioClip HitWeakClip => hitWeakClip;
        public AudioClip SwingClip => swingClip;
        public AudioClip CatcherCatchClip => catcherCatchClip;
        public AudioClip StrikeClip => strikeClip;
        public AudioClip BallClip => ballClip;
        public AudioClip OutClip => outClip;
        public AudioClip CheeringClip => cheeringClip;
    }
}
