using System.Collections;
using System.Collections.Generic;
using JoyconBaseball.Phase1.Audio;
using JoyconBaseball.Phase1.Gameplay;
using JoyconBaseball.Phase1.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JoyconBaseball.Phase1.Core
{
    public sealed class Phase1GameController : MonoBehaviour, IBallGameController
    {
        private const float PitchDistance = 17.4f;
        private const float BatReach = 1.4f;
        private const float MinPitchSpeedKmh = 40f;
        private const float MaxPitchSpeedKmh = 200f;

        [Header("Random Pitch Settings")]
        [Tooltip("ランダム球速の最小値 (km/h)")]
        public float randomSpeedMin = 100f;
        [Tooltip("ランダム球速の最大値 (km/h)")]
        public float randomSpeedMax = 150f;
        [Tooltip("ストレートの出現確率 (0〜1)")]
        [Range(0f, 1f)] public float straightWeight = 0.5f;
        [Tooltip("カーブの出現確率 (0〜1)")]
        [Range(0f, 1f)] public float curveWeight = 0.25f;
        // フォークの出現確率 = 1 - straightWeight - curveWeight

        private readonly List<string> atBatResults = new();

        private Phase1SceneReferences sceneReferences;
        private Joycon2Bridge joyconBridge;
        private Camera mainCamera;
        private Phase1UIController uiController;
        private Phase1AudioManager audioManager;
        private PitchingMachine pitchingMachine;
        private BatController batController;
        private BoxCollider strikeZoneCollider;

        private int balls;
        private int strikes;
        private int outs;
        private int score;
        private bool runnerOnFirst;
        private bool runnerOnSecond;
        private bool runnerOnThird;
        private bool pitchInProgress;
        private bool gameOver;

        private float pitchSpeedKmh = 120f;    // 手動調整用（AdjustPitchSpeed で変更）
        private float lastThrownSpeedKmh = 0f; // 直前の投球速度（HUD 表示用）
        private Phase1Ball activeBall;
        private uint previousRightButtonsMask;
        private uint buttonMaskA;
        private uint buttonMaskB;
        private uint buttonMaskR;
        private uint buttonMaskX;

        public float PitchSpeedKmh => pitchSpeedKmh;
        public bool PitchInProgress => pitchInProgress;
        public Vector3 BatterPosition => batController != null ? batController.transform.position : Vector3.zero;

        private void Awake()
        {
            sceneReferences = FindFirstObjectByType<Phase1SceneReferences>();
            joyconBridge = new Joycon2Bridge();
            buttonMaskA = joyconBridge.GetButtonMask("A");
            buttonMaskB = joyconBridge.GetButtonMask("B");
            buttonMaskR = joyconBridge.GetButtonMask("R");
            buttonMaskX = joyconBridge.GetButtonMask("X");
            BuildCamera();
            BuildWorld();
            BuildUi();
            BuildAudio();
            ShowTitle();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var rightButtonsMask = joyconBridge != null && joyconBridge.IsAvailable && joyconBridge.RightConnected
                ? joyconBridge.GetRightButtonsMask()
                : 0;

            if (gameOver)
            {
                if ((keyboard != null && keyboard.rKey.wasPressedThisFrame) || WasJoyconButtonPressed(rightButtonsMask, buttonMaskA))
                {
                    ResetGame();
                }

                previousRightButtonsMask = rightButtonsMask;
                return;
            }

            if ((keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) ||
                WasJoyconButtonPressed(rightButtonsMask, buttonMaskA))
            {
                StartGameplay();
            }

            if ((keyboard != null && keyboard.spaceKey.wasPressedThisFrame) ||
                WasJoyconButtonPressed(rightButtonsMask, buttonMaskR))
            {
                TryStartPitch();
            }

            if ((keyboard != null && (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)) ||
                WasJoyconButtonPressed(rightButtonsMask, buttonMaskX))
            {
                AdjustPitchSpeed(5f);
            }

            if ((keyboard != null && (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)) ||
                WasJoyconButtonPressed(rightButtonsMask, buttonMaskB))
            {
                AdjustPitchSpeed(-5f);
            }

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                CalibrateJoycon();
            }

            previousRightButtonsMask = rightButtonsMask;
        }

        private void BuildCamera()
        {
            var hasAuthoredGameplayCamera = sceneReferences != null && sceneReferences.GameplayCamera != null;
            if (sceneReferences != null && sceneReferences.GameplayCamera != null)
            {
                mainCamera = sceneReferences.GameplayCamera;
            }
            else
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            if (!hasAuthoredGameplayCamera)
            {
                mainCamera.transform.position = new Vector3(0f, 1.6f, -1.6f);
                mainCamera.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            }

            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.nearClipPlane = 0.01f;
        }

        private void BuildWorld()
        {
            if (sceneReferences == null || sceneReferences.CreateDirectionalLightIfMissing)
            {
                RuntimeSceneFactory.CreateLightingIfMissing();
            }

            strikeZoneCollider = RuntimeSceneFactory.GetOrCreateStrikeZone(sceneReferences);
            if (strikeZoneCollider != null)
            {
                strikeZoneCollider.gameObject.name = "StrikeZoneTrigger";
                var size = strikeZoneCollider.size;
                size.z = 2f;
                strikeZoneCollider.size = size;
            }

            batController = RuntimeSceneFactory.GetOrCreateBat(sceneReferences, mainCamera.transform, this, BatReach);
            pitchingMachine = RuntimeSceneFactory.GetOrCreatePitchingMachine(sceneReferences, PitchDistance);
            RuntimeSceneFactory.CreateCatcher(pitchingMachine.transform.position, strikeZoneCollider);
        }

        private void BuildUi()
        {
            uiController = RuntimeSceneFactory.GetUi(sceneReferences);
            if (uiController == null)
            {
                Debug.LogError("Phase1GameController could not find an authored UI. Add Phase1UIController to the scene and wire its references.");
                enabled = false;
            }
        }

        private void BuildAudio()
        {
            var audioObject = new GameObject("Phase1AudioManager");
            audioObject.transform.SetParent(transform);
            audioManager = audioObject.AddComponent<Phase1AudioManager>();
            audioManager.Initialize(
                sceneReferences != null ? sceneReferences.BgmClip : null,
                sceneReferences != null ? sceneReferences.StartClip : null,
                sceneReferences != null ? sceneReferences.HitStrongClip : null,
                sceneReferences != null ? sceneReferences.HitNormalClip : null,
                sceneReferences != null ? sceneReferences.HitWeakClip : null,
                sceneReferences != null ? sceneReferences.SwingClip : null,
                sceneReferences != null ? sceneReferences.CatcherCatchClip : null,
                sceneReferences != null ? sceneReferences.StrikeClip : null,
                sceneReferences != null ? sceneReferences.BallClip : null,
                sceneReferences != null ? sceneReferences.OutClip : null,
                sceneReferences != null ? sceneReferences.CheeringClip : null);
        }

        private void ShowTitle()
        {
            uiController.ShowTitle();
            uiController.SetHudVisible(false);
            uiController.SetResultVisible(false);
        }

        private void StartGameplay()
        {
            if (pitchInProgress || gameOver)
            {
                return;
            }

            uiController.SetHudVisible(true);
            uiController.HideTitle();
            UpdateHud("Press Space to pitch");
            audioManager.PlayStartSound();
            audioManager.StartBgm();
        }

        private void TryStartPitch()
        {
            if (pitchInProgress || gameOver || uiController.TitleVisible)
            {
                return;
            }

            StartCoroutine(BeginPitchRoutine());
        }

        private IEnumerator BeginPitchRoutine()
        {
            pitchInProgress = true;
            UpdateHud("Pitching...");
            yield return new WaitForSeconds(0.35f);

            var pitch = GenerateRandomPitch();
            lastThrownSpeedKmh = pitch.speedKmh;

            activeBall = pitchingMachine.ThrowBall(pitch, sceneReferences != null ? sceneReferences.BallPrefab : null, strikeZoneCollider);
            activeBall.Initialize(this);
            UpdateHud("Pitching...");
        }

        private PitchData GenerateRandomPitch()
        {
            // 球速：randomSpeedMin〜randomSpeedMax のランダム
            var speed = Random.Range(randomSpeedMin, randomSpeedMax);

            // 球種：重みに基づいてランダム選択
            var r = Random.value;
            PitchType pitchType;
            if (r < straightWeight)
                pitchType = PitchType.Straight;
            else if (r < straightWeight + curveWeight)
                pitchType = PitchType.Curve;
            else
                pitchType = PitchType.Fork;

            // コース：3x3 グリッドからランダム（ボール球も含む -1〜3 の範囲）
            var zone = new Vector2Int(Random.Range(0, 3), Random.Range(0, 3));

            // カーブ方向・変化量：ランダム
            var curveDir    = Random.value < 0.5f ? -1 : 1;
            var curveAmount = Random.Range(0.6f, 1.0f);

            return new PitchData
            {
                targetZone  = zone,
                pitchType   = pitchType,
                curveDir    = pitchType == PitchType.Straight ? 0 : curveDir,
                curveAmount = pitchType == PitchType.Straight ? 0f : curveAmount,
                speedKmh    = speed,
            };
        }

        public void NotifyPitchFinishedWithoutHit(bool wasStrike)
        {
            pitchInProgress = false;
            activeBall = null;
            audioManager.PlayCatcherCatchSound();

            if (wasStrike)
            {
                RegisterStrike("STRIKE");
            }
            else
            {
                RegisterBall();
            }
        }

        public void NotifyBallHit(Vector3 hitVelocity)
        {
            if (!pitchInProgress || activeBall == null)
            {
                return;
            }

            activeBall.ApplyHit(hitVelocity);
            audioManager.PlayHitSound(hitVelocity.magnitude);
            UpdateHud("Ball in play");
        }

        public void NotifySwingStarted()
        {
            audioManager.PlaySwingSound();
        }

        public void NotifyBallLanded(HitResult result)
        {
            pitchInProgress = false;
            activeBall = null;
            RegisterBattedBallResult(result);
        }

        public bool IsInsideStrikeZone(Vector3 worldPosition)
        {
            if (strikeZoneCollider != null)
            {
                var local = strikeZoneCollider.transform.InverseTransformPoint(worldPosition) - strikeZoneCollider.center;
                var halfSize = strikeZoneCollider.size * 0.5f;

                return Mathf.Abs(local.x) <= halfSize.x &&
                       Mathf.Abs(local.y) <= halfSize.y &&
                       Mathf.Abs(local.z) <= halfSize.z;
            }

            return Mathf.Abs(worldPosition.x) <= 0.35f &&
                   worldPosition.y >= 0.65f &&
                   worldPosition.y <= 1.55f;
        }

        public Vector3 BuildHitVelocity(float contactPower, float verticalInput, float horizontalInput)
        {
            var forward = 8f + contactPower * 18f;   // 最弱: 8 m/s、フルスイング: 26 m/s
            var upward = 3f + (verticalInput * 5f) + (contactPower * 3f);
            var side = horizontalInput * 8f;
            return new Vector3(side, upward, forward);
        }

        private void CalibrateJoycon()
        {
            // バットをデフォルト位置にリセット（Home/Capture ボタンと同じ挙動）
            batController?.GetComponentInChildren<Joycon2ControllerModel>()?.ResetToCalibrationPose();
        }

        public void AdjustPitchSpeed(float delta)
        {
            pitchSpeedKmh = Mathf.Clamp(pitchSpeedKmh + delta, MinPitchSpeedKmh, MaxPitchSpeedKmh);
            UpdateHud("Pitch speed changed");
        }

        private bool WasJoyconButtonPressed(uint currentMask, uint targetMask)
        {
            return targetMask != 0 &&
                   (currentMask & targetMask) != 0 &&
                   (previousRightButtonsMask & targetMask) == 0;
        }

        private void RegisterStrike(string label)
        {
            strikes++;
            if (strikes >= 3)
            {
                outs++;
                atBatResults.Add("K");
                balls = 0;
                strikes = 0;
                audioManager.PlayOutSound();
                CheckGameOver();
                uiController.ShowCenterPopup("STRIKE OUT!", new Color(0.98f, 0.42f, 0.32f));
                UpdateHud("Strike out");
                return;
            }

            if (label == "STRIKE")
            {
                audioManager.PlayStrikeSound();
                uiController.ShowCenterPopup("STRIKE!", new Color(0.98f, 0.42f, 0.32f));
            }

            UpdateHud(label);
        }

        private void RegisterBall()
        {
            balls++;
            if (balls >= 4)
            {
                atBatResults.Add("BB");
                AdvanceRunnersOnWalk();
                balls = 0;
                strikes = 0;
                uiController.ShowCenterPopup("BALL FOUR!", new Color(0.4f, 0.86f, 0.54f));
                UpdateHud("Walk");
                return;
            }

            audioManager.PlayBallSound();
            UpdateHud("BALL");
        }

        private void RegisterBattedBallResult(HitResult result)
        {
            if (result == HitResult.Foul)
            {
                // ファール：2ストライク未満なら+1、2ストライクなら変化なし（三振にならない）
                if (strikes < 2)
                {
                    strikes++;
                }
                uiController.ShowCenterPopup("FOUL!", new Color(1f, 0.55f, 0.1f));
                UpdateHud("FOUL");
                return;
            }

            balls = 0;
            strikes = 0;

            switch (result)
            {
                case HitResult.HomeRun:
                    atBatResults.Add("HR");
                    ScoreAllRunnersAndBatter();
                    audioManager.PlayCheeringSound();
                    uiController.ShowCenterPopup("HOMERUN!", new Color(1f, 0.82f, 0.18f));
                    UpdateHud("HOMERUN!");
                    break;
                case HitResult.Triple:
                    atBatResults.Add("3B");
                    AdvanceRunnersOnHit(3);
                    uiController.ShowCenterPopup("3 BASE HIT!", new Color(0.9f, 0.55f, 1f));
                    UpdateHud("TRIPLE!");
                    break;
                case HitResult.Double:
                    atBatResults.Add("2B");
                    AdvanceRunnersOnHit(2);
                    uiController.ShowCenterPopup("2 BASE HIT!", new Color(0.36f, 0.8f, 1f));
                    UpdateHud("DOUBLE!");
                    break;
                case HitResult.Single:
                    atBatResults.Add("1B");
                    AdvanceRunnersOnHit(1);
                    uiController.ShowCenterPopup("HIT!", new Color(0.4f, 0.86f, 0.54f));
                    UpdateHud("HIT!");
                    break;
                default:
                    atBatResults.Add("OUT");
                    outs++;
                    audioManager.PlayOutSound();
                    CheckGameOver();
                    uiController.ShowCenterPopup("OUT!", new Color(0.95f, 0.3f, 0.3f));
                    UpdateHud("OUT!");
                    break;
            }
        }

        private void AdvanceRunnersOnWalk()
        {
            var nextFirst = true;
            var nextSecond = runnerOnSecond;
            var nextThird = runnerOnThird;

            if (runnerOnFirst && runnerOnSecond && runnerOnThird)
            {
                score++;
            }

            if (runnerOnFirst && runnerOnSecond)
            {
                nextThird = true;
            }

            if (runnerOnFirst)
            {
                nextSecond = true;
            }

            runnerOnFirst = nextFirst;
            runnerOnSecond = nextSecond;
            runnerOnThird = nextThird;
        }

        private void AdvanceRunnersOnHit(int bases)
        {
            if (bases == 1)
            {
                if (runnerOnThird) score++;
                var newThird = runnerOnSecond;
                var newSecond = runnerOnFirst;
                runnerOnFirst = true;
                runnerOnSecond = newSecond;
                runnerOnThird = newThird;
                return;
            }

            if (bases == 2)
            {
                if (runnerOnThird) score++;
                if (runnerOnSecond) score++;
                var newThird = runnerOnFirst;
                runnerOnFirst = false;
                runnerOnSecond = true;
                runnerOnThird = newThird;
                return;
            }

            // bases == 3 (Triple)
            if (runnerOnFirst) score++;
            if (runnerOnSecond) score++;
            if (runnerOnThird) score++;
            runnerOnFirst = false;
            runnerOnSecond = false;
            runnerOnThird = true;
        }

        private void ScoreAllRunnersAndBatter()
        {
            if (runnerOnFirst)
            {
                score++;
            }

            if (runnerOnSecond)
            {
                score++;
            }

            if (runnerOnThird)
            {
                score++;
            }

            score++;
            runnerOnFirst = false;
            runnerOnSecond = false;
            runnerOnThird = false;
        }

        private void CheckGameOver()
        {
            if (outs < 3)
            {
                return;
            }

            gameOver = true;
            pitchInProgress = false;
            audioManager.StopBgm();
            uiController.ShowResults(atBatResults, score);
        }

        private void ResetGame()
        {
            balls = 0;
            strikes = 0;
            outs = 0;
            score = 0;
            runnerOnFirst = false;
            runnerOnSecond = false;
            runnerOnThird = false;
            pitchInProgress = false;
            gameOver = false;
            atBatResults.Clear();
            audioManager.StopBgm();

            if (activeBall != null)
            {
                Destroy(activeBall.gameObject);
                activeBall = null;
            }

            uiController.SetResultVisible(false);
            ShowTitle();
        }

        private void UpdateHud(string message)
        {
            uiController.UpdateHud(
                balls,
                strikes,
                outs,
                score,
                runnerOnFirst,
                runnerOnSecond,
                runnerOnThird,
                lastThrownSpeedKmh,
                message);
        }
    }
}
