using System.Collections;
using System.Collections.Generic;
using JoyconBaseball.Phase1.Audio;
using JoyconBaseball.Phase1.Gameplay;
using JoyconBaseball.Phase1.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JoyconBaseball.Phase1.Core
{
    /// <summary>
    /// Phase2 マルチプレイ対戦コントローラー。
    ///
    /// 画面分割: 左 = バッター視点、右 = ピッチャー視点
    /// ピッチャーは JoyCon Left を振り下ろすことで投球する。
    /// バッターは既存の JoyCon Right スイングで打つ。
    /// </summary>
    public sealed class Phase2GameController : MonoBehaviour, IBallGameController
    {
        private const float BatReach     = 1.4f;
        private const float PitchDistance = 17.4f;

        // ── 共通 ─────────────────────────────────────────────────
        private Phase1SceneReferences sceneReferences;
        private Joycon2Bridge         joyconBridge;
        private Camera                batterCamera;
        private Camera                pitcherCamera;
        private Phase1UIController    uiController;
        private Phase1AudioManager    audioManager;
        private PitchingMachine       pitchingMachine;
        private BatController         batController;
        private BoxCollider           strikeZoneCollider;
        private PitcherController     pitcherController;
        private PitcherHudController  pitcherHud;

        // ── ゲーム状態 ────────────────────────────────────────────
        private int balls;
        private int strikes;
        private int outs;
        private int score;
        private bool runnerOnFirst;
        private bool runnerOnSecond;
        private bool runnerOnThird;
        private bool pitchInProgress;
        private bool gameOver;
        private readonly List<string> atBatResults = new();

        private Phase1Ball activeBall;

        // ── JoyCon ボタンマスク ────────────────────────────────
        private uint previousRightButtonsMask;
        private uint previousLeftButtonsMask;
        private uint buttonMaskA;
        private uint buttonMaskR;
        private uint buttonMaskX;
        private uint buttonMaskB;
        private uint buttonMaskL_left;   // Left JoyCon の L ボタン

        public Vector3 BatterPosition =>
            batController != null ? batController.transform.position : Vector3.zero;

        public bool PitchInProgress => pitchInProgress;

        // ─────────────────────────────────────────────────────────

        private void Awake()
        {
            sceneReferences = FindFirstObjectByType<Phase1SceneReferences>();
            joyconBridge    = new Joycon2Bridge();
            buttonMaskA      = joyconBridge.GetButtonMask("A");
            buttonMaskB      = joyconBridge.GetButtonMask("B");
            buttonMaskR      = joyconBridge.GetButtonMask("R");
            buttonMaskX      = joyconBridge.GetButtonMask("X");
            buttonMaskL_left = joyconBridge.GetButtonMask("L");

            BuildCameras();
            BuildWorld();
            BuildUi();
            BuildAudio();
            BuildPitcher();
            ShowTitle();
        }

        // ── セットアップ ──────────────────────────────────────────

        private void BuildCameras()
        {
            // バッターカメラ（左半分）
            var hasAuthored = sceneReferences != null && sceneReferences.GameplayCamera != null;
            batterCamera = hasAuthored ? sceneReferences.GameplayCamera : Camera.main;

            if (batterCamera == null)
            {
                var go = new GameObject("BatterCamera");
                batterCamera     = go.AddComponent<Camera>();
                batterCamera.tag = "MainCamera";
            }

            if (!hasAuthored)
            {
                batterCamera.transform.position = new Vector3(18.55f, 1.6f, -17f);
                batterCamera.transform.LookAt(new Vector3(6.29f, 1.1f, -3.16f));
            }

            batterCamera.rect        = new Rect(0f, 0f, 0.5f, 1f);
            batterCamera.clearFlags  = CameraClearFlags.Skybox;
            batterCamera.nearClipPlane = 0.01f;

            // ピッチャーカメラ（右半分）
            var pitcherCamGo = new GameObject("PitcherCamera");
            pitcherCamera = pitcherCamGo.AddComponent<Camera>();
            pitcherCamera.rect        = new Rect(0.5f, 0f, 0.5f, 1f);
            pitcherCamera.clearFlags  = CameraClearFlags.Skybox;
            pitcherCamera.nearClipPlane = 0.01f;

            // マウンド後方・やや高め → ホームプレート方向
            pitcherCamera.transform.position = new Vector3(1.5f, 3.0f, 2.0f);
            pitcherCamera.transform.LookAt(new Vector3(18.55f, 1.0f, -15.49f));
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

            batController    = RuntimeSceneFactory.GetOrCreateBat(sceneReferences, batterCamera.transform, this, BatReach);
            pitchingMachine  = RuntimeSceneFactory.GetOrCreatePitchingMachine(sceneReferences, PitchDistance);
            RuntimeSceneFactory.CreateCatcher(pitchingMachine.transform.position, strikeZoneCollider);
        }

        private void BuildUi()
        {
            uiController = RuntimeSceneFactory.GetUi(sceneReferences);
            if (uiController == null)
            {
                Debug.LogError("Phase2GameController: Phase1UIController が見つかりません。");
                enabled = false;
            }
        }

        private void BuildAudio()
        {
            var audioGo = new GameObject("Phase1AudioManager");
            audioGo.transform.SetParent(transform);
            audioManager = audioGo.AddComponent<Phase1AudioManager>();

            if (sceneReferences != null)
            {
                audioManager.Initialize(
                    sceneReferences.BgmClip,
                    sceneReferences.StartClip,
                    sceneReferences.HitStrongClip,
                    sceneReferences.HitNormalClip,
                    sceneReferences.HitWeakClip,
                    sceneReferences.SwingClip,
                    sceneReferences.CatcherCatchClip,
                    sceneReferences.StrikeClip,
                    sceneReferences.BallClip,
                    sceneReferences.OutClip,
                    sceneReferences.CheeringClip);
            }
            else
            {
                audioManager.Initialize(null, null, null, null, null, null, null, null, null, null, null);
            }
        }

        private void BuildPitcher()
        {
            var pitcherGo  = new GameObject("PitcherController");
            pitcherController = pitcherGo.AddComponent<PitcherController>();
            pitcherController.Initialize(this, joyconBridge);

            var hudGo = new GameObject("PitcherHud");
            pitcherHud = hudGo.AddComponent<PitcherHudController>();
            pitcherHud.Initialize(pitcherController);
        }

        // ── Update ────────────────────────────────────────────────

        private void Update()
        {
            var kb = Keyboard.current;
            var rightMask = joyconBridge != null && joyconBridge.IsAvailable && joyconBridge.RightConnected
                ? joyconBridge.GetRightButtonsMask()
                : 0;
            var leftMask = joyconBridge != null && joyconBridge.IsAvailable && joyconBridge.LeftConnected
                ? joyconBridge.GetLeftButtonsMask()
                : 0;

            if (gameOver)
            {
                if ((kb != null && kb.rKey.wasPressedThisFrame) || WasJoyconButtonPressed(rightMask, buttonMaskA))
                    ResetGame();

                previousRightButtonsMask = rightMask;
                previousLeftButtonsMask  = leftMask;
                return;
            }

            // ゲーム開始（Enter）
            if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ||
                WasJoyconButtonPressed(rightMask, buttonMaskA))
            {
                StartGameplay();
            }

            // バッター（右JoyCon）キャリブレーション: R ボタン / C キー
            if ((kb != null && kb.cKey.wasPressedThisFrame) ||
                WasJoyconButtonPressed(rightMask, buttonMaskR))
            {
                CalibrateJoycon();
            }

            // ピッチャー（左JoyCon）キャリブレーション: L ボタン / V キー
            if ((kb != null && kb.vKey.wasPressedThisFrame) ||
                WasLeftJoyconButtonPressed(leftMask, buttonMaskL_left))
            {
                pitcherController.CalibrateThrowPose();
            }

            // ピッチ速度調整
            if ((kb != null && (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame)) ||
                WasJoyconButtonPressed(rightMask, buttonMaskX))
            {
                pitcherController.minSpeedKmh = Mathf.Clamp(pitcherController.minSpeedKmh + 5f, 40f, 200f);
            }

            if ((kb != null && (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame)) ||
                WasJoyconButtonPressed(rightMask, buttonMaskB))
            {
                pitcherController.minSpeedKmh = Mathf.Clamp(pitcherController.minSpeedKmh - 5f, 40f, 200f);
            }

            previousRightButtonsMask = rightMask;
            previousLeftButtonsMask  = leftMask;
        }

        // ── ゲームフロー ─────────────────────────────────────────

        private void ShowTitle()
        {
            uiController.ShowTitle();
            uiController.SetHudVisible(false);
            uiController.SetResultVisible(false);
        }

        private void StartGameplay()
        {
            if (pitchInProgress || gameOver) return;

            uiController.SetHudVisible(true);
            uiController.HideTitle();
            UpdateHud("Pitcher: swing JoyCon to throw");
            audioManager.PlayStartSound();
            audioManager.StartBgm();
        }

        /// <summary>PitcherController から呼ばれる（JoyCon 振り下ろし or Space キー）</summary>
        public void NotifyPitchThrown(PitchData pitch)
        {
            if (pitchInProgress || gameOver || uiController.TitleVisible) return;

            pitchInProgress = true;
            StartCoroutine(BeginPitchRoutine(pitch));
        }

        private IEnumerator BeginPitchRoutine(PitchData pitch)
        {
            UpdateHud("Pitching...");
            yield return new WaitForSeconds(0.35f);

            activeBall = pitchingMachine.ThrowBall(pitch, sceneReferences != null ? sceneReferences.BallPrefab : null, strikeZoneCollider);
            activeBall.Initialize(this);
        }

        // ── フィールドコールバック ────────────────────────────────

        public void NotifyBallHit(Vector3 hitVelocity)
        {
            if (!pitchInProgress || activeBall == null) return;

            activeBall.ApplyHit(hitVelocity);
            audioManager.PlayHitSound(hitVelocity.magnitude);
            UpdateHud("Ball in play");
        }

        public void NotifySwingStarted()
        {
            audioManager.PlaySwingSound();
        }

        public void NotifyPitchFinishedWithoutHit(bool wasStrike)
        {
            pitchInProgress = false;
            activeBall = null;
            audioManager.PlayCatcherCatchSound();

            if (wasStrike) RegisterStrike("STRIKE");
            else           RegisterBall();
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
                var local    = strikeZoneCollider.transform.InverseTransformPoint(worldPosition) - strikeZoneCollider.center;
                var halfSize = strikeZoneCollider.size * 0.5f;
                return Mathf.Abs(local.x) <= halfSize.x &&
                       Mathf.Abs(local.y) <= halfSize.y &&
                       Mathf.Abs(local.z) <= halfSize.z;
            }
            return Mathf.Abs(worldPosition.x) <= 0.35f &&
                   worldPosition.y >= 0.65f && worldPosition.y <= 1.55f;
        }

        public Vector3 BuildHitVelocity(float contactPower, float verticalInput, float horizontalInput)
        {
            var forward = 8f + contactPower * 18f;
            var upward  = 3f + verticalInput * 5f + contactPower * 3f;
            var side    = horizontalInput * 8f;
            return new Vector3(side, upward, forward);
        }

        // ── スコア・カウント処理（Phase1 と同一） ─────────────────

        private void RegisterStrike(string label)
        {
            strikes++;
            if (strikes >= 3)
            {
                outs++;
                atBatResults.Add("K");
                balls = strikes = 0;
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
                balls = strikes = 0;
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
                if (strikes < 2) strikes++;
                uiController.ShowCenterPopup("FOUL!", new Color(1f, 0.55f, 0.1f));
                UpdateHud("FOUL");
                return;
            }

            balls = strikes = 0;

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

        // ── 進塁（Phase1 と同一） ─────────────────────────────────

        private void AdvanceRunnersOnWalk()
        {
            var next1 = true;
            var next2 = runnerOnSecond;
            var next3 = runnerOnThird;

            if (runnerOnFirst && runnerOnSecond && runnerOnThird) score++;
            if (runnerOnFirst && runnerOnSecond)                  next3 = true;
            if (runnerOnFirst)                                    next2 = true;

            runnerOnFirst = next1; runnerOnSecond = next2; runnerOnThird = next3;
        }

        private void AdvanceRunnersOnHit(int bases)
        {
            if (bases == 1)
            {
                if (runnerOnThird) score++;
                var n3 = runnerOnSecond;
                var n2 = runnerOnFirst;
                runnerOnFirst = true; runnerOnSecond = n2; runnerOnThird = n3;
                return;
            }
            if (bases == 2)
            {
                if (runnerOnThird)  score++;
                if (runnerOnSecond) score++;
                var n3 = runnerOnFirst;
                runnerOnFirst = false; runnerOnSecond = true; runnerOnThird = n3;
                return;
            }
            // Triple
            if (runnerOnFirst)  score++;
            if (runnerOnSecond) score++;
            if (runnerOnThird)  score++;
            runnerOnFirst = false; runnerOnSecond = false; runnerOnThird = true;
        }

        private void ScoreAllRunnersAndBatter()
        {
            if (runnerOnFirst)  score++;
            if (runnerOnSecond) score++;
            if (runnerOnThird)  score++;
            score++;
            runnerOnFirst = runnerOnSecond = runnerOnThird = false;
        }

        // ── ゲームオーバー・リセット ──────────────────────────────

        private void CheckGameOver()
        {
            if (outs < 3) return;

            gameOver = true;
            pitchInProgress = false;
            audioManager.StopBgm();
            uiController.ShowResults(atBatResults, score);
        }

        private void ResetGame()
        {
            balls = strikes = outs = score = 0;
            runnerOnFirst = runnerOnSecond = runnerOnThird = false;
            pitchInProgress = gameOver = false;
            atBatResults.Clear();
            audioManager.StopBgm();

            if (activeBall != null) { Destroy(activeBall.gameObject); activeBall = null; }

            uiController.SetResultVisible(false);
            ShowTitle();
        }

        // ── ユーティリティ ────────────────────────────────────────

        private void CalibrateJoycon()
        {
            if (joyconBridge == null || !joyconBridge.IsAvailable || !joyconBridge.RightConnected) return;
            joyconBridge.CalibrateRight(batController.transform.rotation);
        }

        private bool WasJoyconButtonPressed(uint currentMask, uint targetMask) =>
            targetMask != 0 &&
            (currentMask & targetMask) != 0 &&
            (previousRightButtonsMask & targetMask) == 0;

        private bool WasLeftJoyconButtonPressed(uint currentMask, uint targetMask) =>
            targetMask != 0 &&
            (currentMask & targetMask) != 0 &&
            (previousLeftButtonsMask & targetMask) == 0;

        private void UpdateHud(string message)
        {
            uiController.UpdateHud(balls, strikes, outs, score,
                runnerOnFirst, runnerOnSecond, runnerOnThird,
                pitcherController.minSpeedKmh, message);
        }
    }
}
