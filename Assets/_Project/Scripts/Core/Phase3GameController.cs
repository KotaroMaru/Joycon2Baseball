using System.Collections.Generic;
using JoyconBaseball.Phase1.Audio;
using JoyconBaseball.Phase1.Gameplay;
using JoyconBaseball.Phase1.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JoyconBaseball.Phase1.Core
{
    /// <summary>
    /// Phase3 一人プレイ（ピッチャーモード）コントローラー。
    ///
    /// 画面: ピッチャー視点のみ（フルスクリーン）
    /// プレイヤー: JoyCon Left を振り下ろして投球
    /// バッター: AIBatterController が BatPivot を回転させてスイング（人間と同一機構）
    ///
    /// シーンに Phase3SceneReferences を持つ GameObject を配置し、
    /// Inspector で全フィールドを設定してください。
    /// </summary>
    public sealed class Phase3GameController : MonoBehaviour, IBallGameController, IPitchReceiver
    {
        // ── シーン参照 ────────────────────────────────────────────
        private Phase3SceneReferences sceneRefs;
        private Joycon2Bridge         joyconBridge;
        private Camera                pitcherCamera;
        private Phase1UIController    uiController;
        private Phase1AudioManager    audioManager;
        private PitchingMachine       pitchingMachine;
        private BatController         batController;
        private BoxCollider           strikeZoneCollider;
        private PitcherController     pitcherController;
        private PitcherHudController  pitcherHud;
        private AIBatterController    aiBatter;

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

        // ── JoyCon ボタンマスク（Left JoyCon のみ） ─────────────
        private uint previousLeftButtonsMask;
        private uint buttonMaskMinus;   // ゲーム開始 / リセット（Left JoyCon の - ボタン = SELECT）
        private uint buttonMaskL_left;  // ピッチャーモデルのキャリブレーション

        public Vector3 BatterPosition =>
            batController != null ? batController.transform.position : Vector3.zero;

        public bool PitchInProgress => pitchInProgress;

        // ─────────────────────────────────────────────────────────

        private void Awake()
        {
            sceneRefs = FindFirstObjectByType<Phase3SceneReferences>();
            if (sceneRefs == null)
            {
                Debug.LogError("Phase3GameController: Phase3SceneReferences がシーンに見つかりません。" +
                               "GameObject に Phase3SceneReferences を追加して全フィールドを設定してください。");
                enabled = false;
                return;
            }

            joyconBridge     = new Joycon2Bridge();
            buttonMaskMinus  = joyconBridge.GetButtonMask("SELECT"); // Left JoyCon の - ボタン
            buttonMaskL_left = joyconBridge.GetButtonMask("L");

            InitializeComponents();
            ShowTitle();
        }

        // ── セットアップ ──────────────────────────────────────────

        private void InitializeComponents()
        {
            // カメラ（フルスクリーン）
            pitcherCamera = sceneRefs.PitcherCamera;
            if (pitcherCamera != null)
                pitcherCamera.rect = new Rect(0f, 0f, 1f, 1f);

            // ゲームプレイ
            strikeZoneCollider = sceneRefs.StrikeZoneCollider;
            if (strikeZoneCollider != null)
            {
                strikeZoneCollider.gameObject.name = "StrikeZoneTrigger";
                var sz = strikeZoneCollider.size;
                sz.z = 2.0f;
                strikeZoneCollider.size = sz;
            }

            pitchingMachine = sceneRefs.PitchingMachine;

            // バッター（JoyCon 入力なし、AI が BatPivot を回転させる）
            batController = sceneRefs.BatController;
            if (batController != null)
            {
                batController.Initialize(this, sceneRefs.BatPivot);
                batController.ConfigureInput(false, 2.0f);
            }

            // AI バッター
            aiBatter = sceneRefs.AIBatterController;
            if (aiBatter != null)
                aiBatter.Initialize(sceneRefs.BatPivot, batController);

            // UI
            uiController = sceneRefs.UiController;
            if (uiController != null)
            {
                uiController.Initialize();
                uiController.SetPersistentHint("Swing JoyCon-L to pitch  ↑↓ pitch type  ←→ zone  V/L to recalibrate");
            }
            else
            {
                Debug.LogError("Phase3GameController: Phase1UIController が Phase3SceneReferences に設定されていません。");
                return;
            }

            // オーディオ
            audioManager = sceneRefs.AudioManager;
            if (audioManager != null)
            {
                audioManager.Initialize(
                    sceneRefs.BgmClip,
                    sceneRefs.StartClip,
                    sceneRefs.HitStrongClip,
                    sceneRefs.HitNormalClip,
                    sceneRefs.HitWeakClip,
                    sceneRefs.SwingClip,
                    sceneRefs.CatcherCatchClip,
                    sceneRefs.StrikeClip,
                    sceneRefs.BallClip,
                    sceneRefs.OutClip,
                    sceneRefs.CheeringClip);
            }

            // ピッチャー
            pitcherController = sceneRefs.PitcherController;
            if (pitcherController != null)
                pitcherController.Initialize(this, joyconBridge);

            pitcherHud = sceneRefs.PitcherHudController;
            if (pitcherHud != null && pitcherController != null)
                pitcherHud.Initialize(pitcherController, leftSide: true);
        }

        // ── Update ────────────────────────────────────────────────

        private void Update()
        {
            var kb       = Keyboard.current;
            var leftMask = joyconBridge != null && joyconBridge.IsAvailable && joyconBridge.LeftConnected
                ? joyconBridge.GetLeftButtonsMask() : 0;

            if (gameOver)
            {
                if ((kb != null && kb.rKey.wasPressedThisFrame) ||
                    WasLeftButtonPressed(leftMask, buttonMaskMinus))
                    ResetGame();

                previousLeftButtonsMask = leftMask;
                return;
            }

            // ゲーム開始: Enter キー / Left JoyCon MINUS ボタン
            if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ||
                WasLeftButtonPressed(leftMask, buttonMaskMinus))
            {
                StartGameplay();
            }

            // キャリブレーション: V キー / Left JoyCon L ボタン
            if ((kb != null && kb.vKey.wasPressedThisFrame) ||
                WasLeftButtonPressed(leftMask, buttonMaskL_left))
            {
                sceneRefs.PitcherJoyconModel?.ResetToCalibrationPose();
            }

            previousLeftButtonsMask = leftMask;
        }

        // ── ゲームフロー ─────────────────────────────────────────

        private void ShowTitle()
        {
            uiController.ShowTitleWithText(
                "SOLO PITCHER MODE\n\n" +
                "Enter / Minus : start game\n" +
                "Swing JoyCon-L down : throw\n" +
                "Left stick : aim zone\n" +
                "↑ : Curve  ↓ : Fork  ↑+↓ : CurveFork\n" +
                "LS push / Tab : reset zone & type\n" +
                "V / L : recalibrate JoyCon\n\n" +
                "Strike out the CPU batter!");
            uiController.SetHudVisible(false);
            uiController.SetResultVisible(false);
        }

        private void StartGameplay()
        {
            if (pitchInProgress || gameOver) return;

            uiController.SetHudVisible(true);
            uiController.HideTitle();
            UpdateHud("Swing JoyCon-L to throw!");
            audioManager?.PlayStartSound();
            audioManager?.StartBgm();
        }

        /// <summary>PitcherController から呼ばれる（IPitchReceiver 実装）。</summary>
        public void NotifyPitchThrown(PitchData pitch)
        {
            if (pitchInProgress || gameOver || uiController.TitleVisible) return;

            pitchInProgress = true;
            BeginPitch(pitch);
        }

        private void BeginPitch(PitchData pitch)
        {
            UpdateHud("Pitching...");

            var armBall = sceneRefs.PitchArmBall;
            if (armBall != null)
            {
                var spawnPos = armBall.transform.position;
                armBall.SetActive(false);
                activeBall = pitchingMachine.ThrowBallFrom(spawnPos, pitch, sceneRefs.BallPrefab, strikeZoneCollider);
            }
            else
            {
                activeBall = pitchingMachine.ThrowBall(pitch, sceneRefs.BallPrefab, strikeZoneCollider);
            }

            activeBall.Initialize(this);
            aiBatter?.OnPitchThrown(pitch, activeBall);
        }

        // ── フィールドコールバック（IBallGameController 実装） ────

        public void NotifyBallHit(Vector3 hitVelocity)
        {
            if (!pitchInProgress || activeBall == null) return;
            activeBall.ApplyHit(hitVelocity);
            audioManager?.PlayHitSound(hitVelocity.magnitude);
            UpdateHud("Ball in play");
        }

        public void NotifySwingStarted()
        {
            audioManager?.PlaySwingSound();
        }

        public void NotifyPitchFinishedWithoutHit(bool wasStrike)
        {
            pitchInProgress = false;
            activeBall = null;
            sceneRefs.PitchArmBall?.SetActive(true);
            aiBatter?.ResetForNextPitch();
            audioManager?.PlayCatcherCatchSound();

            if (wasStrike) RegisterStrike("LOOKING STRIKE");
            else           RegisterBall();
        }

        public void NotifyBallLanded(HitResult result)
        {
            pitchInProgress = false;
            activeBall = null;
            sceneRefs.PitchArmBall?.SetActive(true);
            aiBatter?.ResetForNextPitch();
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

        // ── スコア・カウント処理 ──────────────────────────────────

        private void RegisterStrike(string label)
        {
            strikes++;
            if (strikes >= 3)
            {
                outs++;
                atBatResults.Add("K");
                balls = strikes = 0;
                audioManager?.PlayOutSound();
                CheckGameOver();
                var msg = (label == "LOOKING STRIKE") ? "LOOKING STRIKE OUT!" : "STRIKE OUT!";
                uiController.ShowCenterPopup(msg, new Color(0.98f, 0.42f, 0.32f));
                UpdateHud("Strike out");
                return;
            }

            if (label == "LOOKING STRIKE" || label == "STRIKE")
            {
                audioManager?.PlayStrikeSound();
                uiController.ShowCenterPopup("STRIKE!", new Color(0.98f, 0.42f, 0.32f));
            }
            UpdateHud(label == "LOOKING STRIKE" ? "STRIKE" : label);
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
            audioManager?.PlayBallSound();
            uiController.ShowCenterPopup("BALL", new Color(0.4f, 0.86f, 0.54f));
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
                    audioManager?.PlayCheeringSound();
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
                    audioManager?.PlayOutSound();
                    CheckGameOver();
                    uiController.ShowCenterPopup("OUT!", new Color(0.95f, 0.3f, 0.3f));
                    UpdateHud("OUT!");
                    break;
            }
        }

        // ── 進塁 ──────────────────────────────────────────────────

        private void AdvanceRunnersOnWalk()
        {
            var next1 = true;
            var next2 = runnerOnSecond;
            var next3 = runnerOnThird;

            if (runnerOnFirst && runnerOnSecond && runnerOnThird) score++;
            if (runnerOnFirst && runnerOnSecond) next3 = true;
            if (runnerOnFirst)                   next2 = true;

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
            audioManager?.StopBgm();
            uiController.ShowPitcherResults(atBatResults, score);
        }

        private void ResetGame()
        {
            balls = strikes = outs = score = 0;
            runnerOnFirst = runnerOnSecond = runnerOnThird = false;
            pitchInProgress = gameOver = false;
            atBatResults.Clear();
            audioManager?.StopBgm();

            if (activeBall != null) { Destroy(activeBall.gameObject); activeBall = null; }

            aiBatter?.ResetForNextPitch();
            uiController.SetResultVisible(false);
            ShowTitle();
        }

        // ── ユーティリティ ────────────────────────────────────────

        private bool WasLeftButtonPressed(uint currentMask, uint targetMask) =>
            targetMask != 0 &&
            (currentMask & targetMask) != 0 &&
            (previousLeftButtonsMask & targetMask) == 0;

        private void UpdateHud(string message)
        {
            var speedKmh = pitcherController != null ? pitcherController.LastThrownSpeedKmh : 0f;
            uiController.UpdateHud(balls, strikes, outs, score,
                runnerOnFirst, runnerOnSecond, runnerOnThird,
                speedKmh, message);
        }
    }
}
