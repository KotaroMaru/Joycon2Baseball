using JoyconBaseball.Phase1.Core;
using UnityEngine;
using UnityEngine.InputSystem;
// IPitchReceiver は JoyconBaseball.Phase1.Core に定義

namespace JoyconBaseball.Phase1.Gameplay
{
    /// <summary>
    /// ピッチャー操作を担当するコンポーネント。
    ///
    /// 入力（常時受付、デフォルト = ストレート・中央）:
    ///   Left スティック傾け → 狙いゾーン (3x3) を更新して固定（離しても維持）
    ///   UP ボタン押す       → カーブに変更（離しても維持）
    ///   DOWN ボタン押す     → フォークに変更（離しても維持）
    ///   UP + DOWN 同時押し  → カーブ+フォーク
    ///   LS（スティック押し込み） → ゾーン・球種をリセット（中央・ストレート）
    ///
    /// 投球トリガー:
    ///   JoyCon を上から下へ振り下ろす
    ///   キーボード: Space キー（デバッグ用）
    /// </summary>
    public sealed class PitcherController : MonoBehaviour
    {
        // ── Inspector パラメータ ─────────────────────────────────
        [Header("Debug")]
        [Tooltip("ON にするとコンソールに加速度・判定値をリアルタイム出力する")]
        public bool debugLog = false;

        [Header("Throw Detection")]
        [Tooltip("ベースラインからの加速度差分の閾値（振り下ろし検出）")]
        public float throwThreshold = 1.5f;

        [Tooltip("下方向成分 / 水平成分 の比率閾値（横振り排除）")]
        public float directionRatioThreshold = 0.3f;

        [Tooltip("投球後のクールダウン秒数（誤検知防止）")]
        public float throwCooldown = 0.8f;

        [Header("Speed Mapping")]
        [Tooltip("加速度デルタのこの値が minSpeedKmh に対応")]
        public float accelMin = 1.0f;

        [Tooltip("加速度デルタのこの値が maxSpeedKmh に対応")]
        public float accelMax = 12.0f;

        public float minSpeedKmh = 80f;
        public float maxSpeedKmh = 160f;

        [Header("Curve (Gyro Twist)")]
        [Tooltip("Y 軸ジャイロデルタのカーブ方向判定閾値（これ未満のひねりは無視）")]
        public float twistThreshold = 0.1f;

        [Tooltip("このひねり量で curveAmount = 1.0 になる（rad）")]
        public float maxTwist = 1.5f;

        [Header("Input")]
        [Tooltip("スティックのデッドゾーン（これ以上傾けるとゾーン更新）")]
        public float stickDeadzone = 0.3f;

        // ── 内部状態 ─────────────────────────────────────────────
        private IPitchReceiver controller;
        private Joycon2Bridge joyconBridge;

        private Vector2Int currentZone = new Vector2Int(1, 1);
        private PitchType currentPitchType = PitchType.Straight;
        private int currentCurveDir = 0;

        // 振り下ろし検出
        private float downBaseline;
        private bool baselineInitialized;
        private float cooldownTimer;

        // ジャイロひねり（カーブ用）
        // ConsumeGyroDelta はシーン内の Joycon2ControllerModel が消費するため、
        // 消費なしの LeftGyro.y の瞬間値をサンプリングしてピーク値を追跡する。
        private float peakTwist;         // 投球間での LeftGyro.y のピーク（符号付き最大値）
        private float peakTwistAbs;      // |peakTwist| のキャッシュ

        // ボタンマスク
        private uint maskUp;
        private uint maskDown;
        private uint maskStickL;   // LS = 左スティック押し込み（リセット）
        private uint prevLeftMask;

        public PitchData CurrentPitchData => new PitchData
        {
            targetZone  = currentZone,
            pitchType   = currentPitchType,
            curveDir    = currentCurveDir,
            curveAmount = 0f,
            speedKmh    = minSpeedKmh,
        };

        // キャリブレーション不要になったため常に true（UI 互換のために残す）
        public bool IsCalibrated => true;

        /// <summary>直前の投球速度 (km/h)。まだ一度も投げていない場合は 0。</summary>
        public float LastThrownSpeedKmh { get; private set; }

        // ─────────────────────────────────────────────────────────

        public void Initialize(IPitchReceiver gameController, Joycon2Bridge bridge)
        {
            controller   = gameController;
            joyconBridge = bridge;

            maskUp      = bridge.GetButtonMask("UP");
            maskDown    = bridge.GetButtonMask("DOWN");
            maskStickL  = bridge.GetButtonMask("LS");

            bridge?.LogReflectionStatus();
            Debug.Log($"[Pitcher] Initialize: IsLeftAvailable={bridge?.IsLeftAvailable}, LeftConnected={bridge?.LeftConnected}");
        }

        private void Update()
        {
            if (controller == null) return;

            var leftMask = (joyconBridge != null && joyconBridge.IsLeftAvailable && joyconBridge.LeftConnected)
                ? joyconBridge.GetLeftButtonsMask()
                : 0;

            // LS 押し込み / Tab キー → ゾーン・球種をリセット
            var kb = Keyboard.current;
            if (WasPressed(leftMask, maskStickL) || (kb != null && kb.tabKey.wasPressedThisFrame))
            {
                currentZone      = new Vector2Int(1, 1);
                currentPitchType = PitchType.Straight;
            }
            else
            {
                UpdateZoneFromStick();
                UpdatePitchTypeFromButtons(leftMask);
            }

            AccumulateGyroDelta();
            HandleThrowDetection();

            prevLeftMask = leftMask;
        }

        // ── ゾーン選択（固定式） ─────────────────────────────────

        private void UpdateZoneFromStick()
        {
            Vector2 stick = Vector2.zero;

            if (joyconBridge != null && joyconBridge.IsLeftAvailable && joyconBridge.LeftConnected)
            {
                stick = joyconBridge.LeftStick;
            }
            else
            {
                var kb = Keyboard.current;
                if (kb == null) return;
                if (kb.leftArrowKey.isPressed)  stick.x = -1f;
                if (kb.rightArrowKey.isPressed) stick.x =  1f;
                if (kb.downArrowKey.isPressed)  stick.y = -1f;
                if (kb.upArrowKey.isPressed)    stick.y =  1f;
            }

            // デッドゾーンを超えて傾いているときだけゾーンを更新（離してもそのまま固定）
            if (stick.magnitude > stickDeadzone)
            {
                currentZone = StickToZone(stick);
            }
        }

        private static Vector2Int StickToZone(Vector2 stick)
        {
            // ピッチャーから見て、スティック右(stick.x > 0)で右(zone.x=0)、左(stick.x < 0)で左(zone.x=2)に投げるように反転
            var x = stick.x >  0.33f ? 0 : (stick.x < -0.33f ? 2 : 1);
            var y = stick.y >  0.33f ? 2 : (stick.y < -0.33f ? 0 : 1);
            return new Vector2Int(x, y);
        }

        // ── 球種選択（押した瞬間に切り替え・固定） ───────────────

        private void UpdatePitchTypeFromButtons(uint leftMask)
        {
            var upPressed   = false;
            var downPressed = false;

            if (joyconBridge != null && joyconBridge.IsLeftAvailable && joyconBridge.LeftConnected)
            {
                upPressed   = WasPressed(leftMask, maskUp);
                downPressed = WasPressed(leftMask, maskDown);
            }
            else
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    upPressed   = kb.zKey.wasPressedThisFrame;
                    downPressed = kb.xKey.wasPressedThisFrame;
                }
            }

            if      (upPressed && downPressed) currentPitchType = PitchType.CurveFork;
            else if (upPressed)                currentPitchType = PitchType.Curve;
            else if (downPressed)              currentPitchType = PitchType.Fork;
            // どちらも押されていない → currentPitchType を維持
        }

        // ── ジャイロひねりピーク追跡 ─────────────────────────────

        private void AccumulateGyroDelta()
        {
            if (joyconBridge == null || !joyconBridge.IsLeftAvailable || !joyconBridge.LeftConnected) return;

            // クールダウン中は投球スイング由来のジャイロが混入するためスキップ
            if (cooldownTimer > 0f) return;

            // LeftGyro は消費なし（Joycon2ControllerModel の ConsumeGyroDelta と競合しない）
            var gyroY = joyconBridge.LeftGyro.y;
            var absY  = Mathf.Abs(gyroY);
            if (absY > peakTwistAbs)
            {
                peakTwistAbs = absY;
                peakTwist    = gyroY;
            }

            if (debugLog && absY > twistThreshold)
                Debug.Log($"[Pitcher] gyroY={gyroY:F3} peak={peakTwist:F3}");
        }

        // ── 振り下ろし検出 ───────────────────────────────────────

        private void HandleThrowDetection()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;

                // クールダウン終了の瞬間にピークをリセット（スイング由来のジャイロを捨てる）
                if (cooldownTimer <= 0f)
                {
                    peakTwist    = 0f;
                    peakTwistAbs = 0f;
                }
                return;
            }

            // キーボードフォールバック（Space キー）
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                FireThrow((minSpeedKmh + maxSpeedKmh) * 0.5f);
                return;
            }

            if (joyconBridge == null || !joyconBridge.IsLeftAvailable || !joyconBridge.LeftConnected)
            {
                if (debugLog)
                    Debug.Log($"[Pitcher] JoyCon Left 未接続: IsLeftAvailable={joyconBridge?.IsLeftAvailable}, LeftConnected={joyconBridge?.LeftConnected}");
                return;
            }

            var accel    = joyconBridge.LeftAccel;
            var accelMag = accel.magnitude;

            if (!baselineInitialized)
            {
                downBaseline        = accelMag;
                baselineInitialized = true;
                if (debugLog) Debug.Log($"[Pitcher] ベースライン初期化: {downBaseline:F3}");
                return;
            }

            downBaseline = Mathf.Lerp(downBaseline, accelMag, Time.deltaTime / 1.0f);
            var delta = accelMag - downBaseline;

            if (delta < throwThreshold) return;

            var downAbs  = Mathf.Abs(accel.y);
            var horizMag = new Vector2(accel.x, accel.z).magnitude;
            var ratio    = downAbs / Mathf.Max(horizMag, 0.01f);

            if (ratio < directionRatioThreshold)
            {
                if (debugLog) Debug.Log($"[Pitcher] ratio 不足で排除 delta={delta:F3} ratio={ratio:F3}");
                return;
            }

            var speedKmh = Mathf.Lerp(minSpeedKmh, maxSpeedKmh, Mathf.InverseLerp(accelMin, accelMax, delta));
            Debug.Log($"[Pitcher] 投球検出: delta={delta:F3} (min={accelMin} max={accelMax}) → {speedKmh:F0}km/h");
            FireThrow(speedKmh);
        }

        private void FireThrow(float speedKmh)
        {
            cooldownTimer = throwCooldown;

            // ジャイロひねり（Y 軸ピーク）からカーブ方向・変化量を決定
            var twist = peakTwist;
            peakTwist    = 0f;  // 次の投球のためリセット
            peakTwistAbs = 0f;

            int   curveDir    = 0;
            float curveAmount = 0f;
            if (Mathf.Abs(twist) >= twistThreshold)
            {
                curveDir    = twist > 0f ? 1 : -1;
                curveAmount = Mathf.Clamp01(Mathf.Abs(twist) / maxTwist);
            }

            currentCurveDir     = curveDir;
            LastThrownSpeedKmh  = speedKmh;

            var pitch = new PitchData
            {
                targetZone  = currentZone,
                pitchType   = currentPitchType,
                curveDir    = curveDir,
                curveAmount = curveAmount,
                speedKmh    = speedKmh,
            };
            Debug.Log($"[Pitcher] 投球: type={pitch.pitchType} curveDir={curveDir} amount={curveAmount:F2} twist={twist:F3} speed={speedKmh:F0}km/h zone={pitch.targetZone}");
            controller.NotifyPitchThrown(pitch);
        }

        // ── ユーティリティ ────────────────────────────────────────

        /// <summary>前フレームは押されておらず、今フレームで押されたか（エッジ検出）</summary>
        private bool WasPressed(uint currentMask, uint targetMask) =>
            targetMask != 0 &&
            (currentMask  & targetMask) != 0 &&
            (prevLeftMask & targetMask) == 0;
    }
}
