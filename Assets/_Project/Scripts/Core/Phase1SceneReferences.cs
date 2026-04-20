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
    }
}
