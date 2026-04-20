using System.Collections;
using System.Collections.Generic;
using JoyconBaseball.Phase1.Gameplay;
using JoyconBaseball.Phase1.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JoyconBaseball.Phase1.Core
{
    public sealed class Phase1GameController : MonoBehaviour
    {
        private const float PitchDistance = 18f;
        private const float BatReach = 1.4f;
        private const float MinPitchSpeedKmh = 40f;
        private const float MaxPitchSpeedKmh = 200f;

        private readonly List<string> atBatResults = new();

        private Phase1SceneReferences sceneReferences;
        private Joycon2Bridge joyconBridge;
        private Camera mainCamera;
        private Phase1UIController uiController;
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

        private float pitchSpeedKmh = 120f;
        private Phase1Ball activeBall;
        private uint previousRightButtonsMask;
        private uint buttonMaskA;
        private uint buttonMaskB;
        private uint buttonMaskR;
        private uint buttonMaskX;

        public float PitchSpeedKmh => pitchSpeedKmh;
        public bool PitchInProgress => pitchInProgress;

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

            Vector3? strikeZoneCenter = strikeZoneCollider != null
                ? strikeZoneCollider.transform.TransformPoint(strikeZoneCollider.center)
                : (Vector3?)null;
            activeBall = pitchingMachine.ThrowStraightBall(pitchSpeedKmh, sceneReferences != null ? sceneReferences.BallPrefab : null, strikeZoneCenter);
            activeBall.Initialize(this);
        }

        public void NotifyPitchFinishedWithoutHit(bool wasStrike)
        {
            pitchInProgress = false;
            activeBall = null;

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
            UpdateHud("Ball in play");
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
            if (joyconBridge == null || !joyconBridge.IsAvailable || !joyconBridge.RightConnected)
            {
                return;
            }

            // バットを真横（BatPivot Rotation ≈ 90,0,-90）に持った状態でCキーを押してキャリブレーション
            joyconBridge.CalibrateRight(batController.transform.rotation);
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
                CheckGameOver();
                uiController.ShowCenterPopup("STRIKE OUT!", new Color(0.98f, 0.42f, 0.32f));
                UpdateHud("Strike out");
                return;
            }

            if (label == "STRIKE")
            {
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
                    uiController.ShowCenterPopup("HOMERUN!", new Color(1f, 0.82f, 0.18f));
                    UpdateHud("HOMERUN!");
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
                if (runnerOnThird)
                {
                    score++;
                }

                if (runnerOnSecond)
                {
                    score++;
                }

                runnerOnThird = runnerOnFirst;
                runnerOnFirst = true;
                return;
            }

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

            runnerOnFirst = false;
            runnerOnSecond = true;
            runnerOnThird = false;
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
                pitchSpeedKmh,
                message);
        }
    }
}
