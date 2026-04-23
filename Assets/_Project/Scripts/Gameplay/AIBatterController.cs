using System.Collections;
using UnityEngine;

namespace JoyconBaseball.Phase1.Gameplay
{
    public sealed class AIBatterController : MonoBehaviour
    {
        // ── ハードコード定数 ──────────────────────────────────────
        private const float TimingJitter        = 0.05f;
        private const float ReturnDuration      = 0.50f;
        private const float SwingPowerMin       = 11.0f;
        private const float SwingPowerMax       = 12.0f;
        private const float ContactCenterHeight = 1.1f;
        private const float MaxVerticalShift    = 35f;
        private const float MaxHorizontalShift  = 50f;

        // ── Inspector パラメーター ────────────────────────────────

        [Header("AI 難易度")]
        [Tooltip("ストライク球に対してスイングする確率")]
        [Range(0f, 1f)] public float strikeSwingRate = 0.85f;
        [Tooltip("ボール球に対してスイングしてしまう確率")]
        [Range(0f, 1f)] public float ballSwingRate = 0.12f;

        [Header("スイング姿勢（中央球の基準）")]
        [Tooltip("スイング開始時の BatPivot ローカル Euler 角\n参考: 中央球コンタクト≈(85,-40,-135) / 高め≈X110 / 低め≈X60〜72")]
        public Vector3 swingStartEuler = new Vector3(85f,  20f, -135f);
        [Tooltip("フォロースルー時の BatPivot ローカル Euler 角")]
        public Vector3 swingEndEuler   = new Vector3(85f, -100f, -135f);
        [Tooltip("振り抜きにかかる時間 (秒)")]
        public float swingDuration = 0.18f;

        [Header("タイミング")]
        [Tooltip("ピッチャー〜ホームベース間のゲーム内距離 (m)。" +
                 "空振りが多い場合は実際のシーン距離に合わせて調整する")]
        public float pitchDistanceM = 17.4f;

        [Header("エイム感度")]
        [Tooltip("垂直補正: ボールが 1m 高いごとに加算する X Euler 度数。" +
                 "正 = 高め → X 増加（参考値 65）。逆方向なら負値にする")]
        public float degreesPerMeterVertical   = 65f;
        [Tooltip("水平補正: ボールが 1m 横にずれるごとに加算する Y Euler 度数")]
        public float degreesPerMeterHorizontal = 25f;

        // ── private ──────────────────────────────────────────────
        private Transform     batPivot;
        private BatController batController;
        private Quaternion    readyRotation;
        private bool          swingPending;
        private Phase1Ball    activeBall;

        // ─────────────────────────────────────────────────────────

        public void Initialize(Transform pivot, BatController bat)
        {
            batPivot      = pivot;
            batController = bat;
            readyRotation = pivot != null ? pivot.localRotation : Quaternion.identity;
        }

        public void OnPitchThrown(PitchData pitch, Phase1Ball ball)
        {
            if (swingPending) return;

            activeBall = ball;

            var fz = pitch.FinalZone;
            var isEstimatedStrike = fz.x >= 0 && fz.x <= 2 && fz.y >= 0 && fz.y <= 2;
            var swingProb = isEstimatedStrike ? strikeSwingRate : ballSwingRate;
            if (Random.value > swingProb) return;

            var speedMs     = pitch.speedKmh / 3.6f;
            var arrivalTime = pitchDistanceM / Mathf.Max(speedMs, 1f);
            var jitter      = Random.Range(-TimingJitter, TimingJitter);

            // スイング中間点（コンタクト）が到達時刻に合うよう delay を計算
            // 旧式: arrivalTime - swingDuration - leadTime → スイング終端が到達時刻（早すぎる）
            // 新式: arrivalTime - swingDuration * 0.5  → スイング中間点が到達時刻
            var delay = Mathf.Max(0f, arrivalTime - swingDuration * 0.5f + jitter);

            swingPending = true;
            StartCoroutine(SwingRoutine(delay));
        }

        public void ResetForNextPitch()
        {
            StopAllCoroutines();
            swingPending = false;
            activeBall   = null;
            if (batPivot != null)
                batPivot.localRotation = readyRotation;
        }

        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// スイング中間点（コンタクト時刻）のボール位置を velocity から予測する。
        /// スイング開始時点ではボールがまだ高い位置にあるため、
        /// 予測位置を使うことで低めの角度が正確になる。
        /// </summary>
        private Vector3 PredictBallPosAtContact()
        {
            if (activeBall == null) return Vector3.zero;

            var rb = activeBall.GetComponent<Rigidbody>();
            if (rb == null) return activeBall.transform.position;

            // コンタクトまでの残り時間 = スイング半分（中間点が到達時刻）
            var timeToContact = swingDuration * 0.5f;
            return activeBall.transform.position + rb.linearVelocity * timeToContact;
        }

        private Vector2 ComputeSwingShift()
        {
            if (activeBall == null || batPivot == null) return Vector2.zero;

            var predicted = PredictBallPosAtContact();

            var xShift = Mathf.Clamp((predicted.y - ContactCenterHeight) * degreesPerMeterVertical,
                                     -MaxVerticalShift, MaxVerticalShift);
            var yShift = Mathf.Clamp((predicted.x - batPivot.position.x) * degreesPerMeterHorizontal,
                                     -MaxHorizontalShift, MaxHorizontalShift);

            return new Vector2(xShift, yShift);
        }

        private IEnumerator SwingRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);

            batController?.SetAISwing(Random.Range(SwingPowerMin, SwingPowerMax));

            var shift   = ComputeSwingShift();
            var shift3  = new Vector3(shift.x, shift.y, 0f);
            var fromRot = Quaternion.Euler(swingStartEuler + shift3);
            var toRot   = Quaternion.Euler(swingEndEuler   + shift3);

            var elapsed = 0f;
            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                if (batPivot != null)
                    batPivot.localRotation = Quaternion.Lerp(fromRot, toRot, elapsed / swingDuration);
                yield return null;
            }
            if (batPivot != null) batPivot.localRotation = toRot;

            elapsed = 0f;
            while (elapsed < ReturnDuration)
            {
                elapsed += Time.deltaTime;
                if (batPivot != null)
                    batPivot.localRotation = Quaternion.Lerp(toRot, readyRotation, elapsed / ReturnDuration);
                yield return null;
            }
            if (batPivot != null) batPivot.localRotation = readyRotation;

            swingPending = false;
        }
    }
}
