using JoyconBaseball.Phase1.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JoyconBaseball.Phase1.Gameplay
{
    /// <summary>
    /// ピッチャー操作を担当するコンポーネント。
    ///
    /// 入力（常時受付、デフォルト = ストレート・中央）:
    ///   Left スティック  → 狙いゾーン (3x3)
    ///   ZL ホールド     → カーブ
    ///   L ホールド      → フォーク
    ///   ZL + L          → カーブ+フォーク
    ///   JoyCon 傾き     → カーブ方向 (左右)
    ///
    /// 投球トリガー:
    ///   JoyCon を上から下へ振り下ろす（下方向成分が支配的な場合のみ有効）
    ///   キーボード: Space キー（デバッグ用、中速固定）
    /// </summary>
    public sealed class PitcherController : MonoBehaviour
    {
        // ── Inspector パラメータ ─────────────────────────────────
        [Header("Throw Detection")]
        [Tooltip("ベースラインからの加速度差分の閾値（振り下ろし検出）")]
        public float throwThreshold = 1.5f;

        [Tooltip("下方向成分 / 水平成分 の比率閾値（横振り排除）")]
        public float directionRatioThreshold = 1.8f;

        [Tooltip("スイングウィンドウ秒数（誤検知防止クールダウン）")]
        public float throwCooldown = 0.8f;

        [Header("Speed Mapping")]
        [Tooltip("加速度デルタのこの値が minSpeedKmh に対応")]
        public float accelMin = 1.0f;

        [Tooltip("加速度デルタのこの値が maxSpeedKmh に対応")]
        public float accelMax = 5.0f;

        public float minSpeedKmh = 80f;
        public float maxSpeedKmh = 160f;

        [Header("Curve Direction")]
        [Tooltip("X 軸加速度オフセットのカーブ判定閾値")]
        public float rollThreshold = 0.3f;

        // ── 内部状態 ─────────────────────────────────────────────
        private Phase2GameController controller;
        private Joycon2Bridge joyconBridge;

        private Vector2Int currentZone = new Vector2Int(1, 1);
        private PitchType currentPitchType = PitchType.Straight;
        private int currentCurveDir = 0;

        // 振り下ろし検出
        private float downBaseline;
        private bool baselineInitialized;
        private float cooldownTimer;

        // カーブ方向（加速度 X 軸のキャリブレーションオフセット）
        private float calibAccelX;
        private bool isCalibrated;

        // ボタンマスク（球種選択: 上矢印=カーブ、下矢印=フォーク）
        private uint maskUp;
        private uint maskDown;
        private uint prevLeftMask;

        // キーボードフォールバック（デバッグ用）
        private bool useKeyboard;

        public PitchData CurrentPitchData => new PitchData
        {
            targetZone = currentZone,
            pitchType  = currentPitchType,
            curveDir   = currentCurveDir,
            speedKmh   = minSpeedKmh, // キーボード投球用デフォルト
        };

        public bool IsCalibrated => isCalibrated;

        // ─────────────────────────────────────────────────────────

        public void Initialize(Phase2GameController gameController, Joycon2Bridge bridge)
        {
            controller    = gameController;
            joyconBridge  = bridge;

            maskUp   = bridge.GetButtonMask("UP");
            maskDown = bridge.GetButtonMask("DOWN");

            useKeyboard = bridge == null || !bridge.IsAvailable || !bridge.LeftConnected;
        }

        /// <summary>
        /// キャリブレーション:
        /// JoyCon 正面を自分の体に向け、画面と平行に構えた状態でボタン押下。
        /// このときの X 軸加速度をカーブ判定のゼロ点として記録する。
        /// </summary>
        public void CalibrateThrowPose()
        {
            if (joyconBridge == null || !joyconBridge.IsAvailable || !joyconBridge.LeftConnected)
                return;

            var accel = joyconBridge.LeftAccel;
            calibAccelX       = accel.x;
            downBaseline      = accel.magnitude; // 静止時の合成加速度（≒ 1g）
            baselineInitialized = true;
            isCalibrated      = true;

            Debug.Log($"[Pitcher] キャリブレーション完了: accelX={calibAccelX:F3}, baseline={downBaseline:F3}");
        }

        private void Update()
        {
            if (controller == null) return;

            UpdateZoneFromStick();
            UpdatePitchTypeFromButtons();
            UpdateCurveDirFromAccel();
            HandleThrowDetection();
        }

        // ── ゾーン選択 ──────────────────────────────────────────

        private void UpdateZoneFromStick()
        {
            Vector2 stick = Vector2.zero;

            if (joyconBridge != null && joyconBridge.IsAvailable && joyconBridge.LeftConnected)
            {
                stick = joyconBridge.LeftStick;
            }
            else
            {
                // キーボードフォールバック（矢印キー）
                var kb = Keyboard.current;
                if (kb == null) return;
                if (kb.leftArrowKey.isPressed)  stick.x = -1f;
                if (kb.rightArrowKey.isPressed) stick.x =  1f;
                if (kb.downArrowKey.isPressed)  stick.y = -1f;
                if (kb.upArrowKey.isPressed)    stick.y =  1f;
            }

            currentZone = StickToZone(stick);
        }

        private static Vector2Int StickToZone(Vector2 stick)
        {
            var x = stick.x >  0.33f ? 2 : (stick.x < -0.33f ? 0 : 1);
            var y = stick.y >  0.33f ? 2 : (stick.y < -0.33f ? 0 : 1);
            return new Vector2Int(x, y);
        }

        // ── 球種選択 ─────────────────────────────────────────────

        private void UpdatePitchTypeFromButtons()
        {
            bool upHeld   = false;
            bool downHeld = false;

            if (joyconBridge != null && joyconBridge.IsAvailable && joyconBridge.LeftConnected)
            {
                var mask = joyconBridge.GetLeftButtonsMask();
                upHeld   = maskUp   != 0 && (mask & maskUp)   != 0;
                downHeld = maskDown != 0 && (mask & maskDown) != 0;
            }
            else
            {
                // キーボードフォールバック: Z = カーブ、X = フォーク
                var kb = Keyboard.current;
                if (kb != null)
                {
                    upHeld   = kb.zKey.isPressed;
                    downHeld = kb.xKey.isPressed;
                }
            }

            if (upHeld && downHeld) currentPitchType = PitchType.CurveFork;
            else if (upHeld)        currentPitchType = PitchType.Curve;
            else if (downHeld)      currentPitchType = PitchType.Fork;
            else                    currentPitchType = PitchType.Straight;
        }

        // ── カーブ方向（X 軸傾き） ───────────────────────────────

        private void UpdateCurveDirFromAccel()
        {
            if (!isCalibrated) { currentCurveDir = 0; return; }
            if (joyconBridge == null || !joyconBridge.LeftConnected) { currentCurveDir = 0; return; }

            var roll = joyconBridge.LeftAccel.x - calibAccelX;

            if      (roll >  rollThreshold) currentCurveDir =  1;
            else if (roll < -rollThreshold) currentCurveDir = -1;
            else                            currentCurveDir =  0;
        }

        // ── 振り下ろし検出 ───────────────────────────────────────

        private void HandleThrowDetection()
        {
            // クールダウン中はスキップ
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                return;
            }

            // キーボードフォールバック（Spaceキー）
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                FireThrow(minSpeedKmh + (minSpeedKmh + maxSpeedKmh) * 0.5f);
                return;
            }

            if (joyconBridge == null || !joyconBridge.IsAvailable || !joyconBridge.LeftConnected)
                return;

            var accel = joyconBridge.LeftAccel;
            var accelMag = accel.magnitude;

            // ベースライン初期化（未キャリブレーション時も動作させる）
            if (!baselineInitialized)
            {
                downBaseline        = accelMag;
                baselineInitialized = true;
                return;
            }

            // 静止中はゆっくりベースラインを追従
            downBaseline = Mathf.Lerp(downBaseline, accelMag, Time.deltaTime / 1.0f);

            var delta = accelMag - downBaseline;
            if (delta < throwThreshold) return;

            // ─ 方向フィルタ ─
            // 「下向き成分が支配的」かを確認する。
            // キャリブレーション済みの場合: 重力方向（= calibAccelX 周辺のY軸）を使う。
            // ここでは簡易版として「Y 軸加速度の絶対値 vs 水平（XZ）成分」で判断。
            var downAbs = Mathf.Abs(accel.y);           // Y = 上下方向（JoyCon 座標系）
            var horizMag = new Vector2(accel.x, accel.z).magnitude;

            var ratio = downAbs / Mathf.Max(horizMag, 0.01f);
            if (ratio < directionRatioThreshold) return;  // 横振り・斜め振りを排除

            // 下向きであること（正面を体に向けた状態での振り下ろし = Y が増加方向）
            if (accel.y < 0f) return;  // 上振りを排除

            var speedKmh = Mathf.Lerp(minSpeedKmh, maxSpeedKmh, Mathf.InverseLerp(accelMin, accelMax, delta));
            FireThrow(speedKmh);
        }

        private void FireThrow(float speedKmh)
        {
            cooldownTimer = throwCooldown;
            var pitch = new PitchData
            {
                targetZone = currentZone,
                pitchType  = currentPitchType,
                curveDir   = currentCurveDir,
                speedKmh   = speedKmh,
            };
            Debug.Log($"[Pitcher] 投球: zone={pitch.targetZone} type={pitch.pitchType} curveDir={pitch.curveDir} speed={speedKmh:F0}km/h");
            controller.NotifyPitchThrown(pitch);
        }
    }
}
