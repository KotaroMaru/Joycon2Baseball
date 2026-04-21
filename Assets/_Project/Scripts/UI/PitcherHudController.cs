using JoyconBaseball.Phase1.Gameplay;
using UnityEngine;

namespace JoyconBaseball.Phase1.UI
{
    /// <summary>
    /// ピッチャー側のデバッグオーバーレイ（右画面に OnGUI で描画）。
    /// showDebugOverlay = false にするとリリース時には何も表示しない。
    /// </summary>
    public sealed class PitcherHudController : MonoBehaviour
    {
        [Header("Debug")]
        public bool showDebugOverlay = true;

        private PitcherController pitcherController;

        // 右半分の画面 X オフセット
        private float screenOffsetX;

        public void Initialize(PitcherController pitcher)
        {
            pitcherController = pitcher;
            screenOffsetX = Screen.width * 0.5f;
        }

        private void OnGUI()
        {
            if (!showDebugOverlay) return;
            if (pitcherController == null) return;

            screenOffsetX = Screen.width * 0.5f;

            var pitch = pitcherController.CurrentPitchData;

            DrawGrid(pitch.targetZone, pitch.FinalZone);
            DrawPitchInfo(pitch);
            DrawCalibrationStatus();
        }

        // ── 3x3 グリッド ────────────────────────────────────────

        private void DrawGrid(Vector2Int selected, Vector2Int final)
        {
            const float cellSize  = 60f;
            const float padding   = 8f;
            var gridX = screenOffsetX + 20f;
            var gridY = Screen.height * 0.5f - cellSize * 1.5f;

            for (var col = 0; col < 3; col++)
            {
                for (var row = 0; row < 3; row++)
                {
                    // zone.x=0(右) を HUD の右側に、zone.x=2(左) を HUD の左側に表示するために (2-col) を使用
                    var rx = gridX + (2 - col) * (cellSize + padding);
                    var ry = gridY + (2 - row) * (cellSize + padding); // row0=下、row2=上

                    var zone = new Vector2Int(col, row);
                    Color bg;
                    if (zone == final && zone != selected)
                        bg = new Color(0.9f, 0.5f, 0.1f, 0.8f);     // 最終到達ゾーン（濃いオレンジ）
                    else if (zone == selected)
                        bg = new Color(1.0f, 0.4f, 0.0f, 0.9f);     // 狙いゾーン（非常に鮮やかなオレンジ）
                    else
                        bg = new Color(0.1f, 0.1f, 0.1f, 0.7f);     // 通常（暗灰）

                    // 背景色を塗りつぶし描画
                    var rect = new Rect(rx, ry, cellSize, cellSize);
                    GUI.color = bg;
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    // 枠線を描画
                    GUI.Box(rect, "");

                    // 座標ラベル
                    GUI.Label(new Rect(rx + 4, ry + 4, cellSize, 20),
                        $"({col},{row})", new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.white } });
                }
            }

            GUI.backgroundColor = Color.white;
        }

        // ── 球種・方向・速度テキスト ─────────────────────────────

        private void DrawPitchInfo(PitchData pitch)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal   = { textColor = Color.white },
            };

            var x = screenOffsetX + 20f;
            var y = Screen.height * 0.5f + 120f;

            var typeLabel = pitch.pitchType switch
            {
                PitchType.Curve     => "CURVE",
                PitchType.Fork      => "FORK",
                PitchType.CurveFork => "CURVE+FORK",
                _                   => "STRAIGHT",
            };

            var curveLabel = pitch.curveDir switch
            {
                -1 => "← Left",
                 1 => "→ Right",
                _  => "",
            };

            GUI.Label(new Rect(x, y,      300, 24), $"Type : {typeLabel} {curveLabel}", style);
            GUI.Label(new Rect(x, y + 28, 300, 24), $"Zone : ({pitch.targetZone.x},{pitch.targetZone.y}) → ({pitch.FinalZone.x},{pitch.FinalZone.y})", style);
        }

        // ── キャリブレーション状態 ───────────────────────────────

        private void DrawCalibrationStatus()
        {
            var calibrated = pitcherController.IsCalibrated;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal   = { textColor = calibrated ? Color.green : Color.red },
            };
            var label = calibrated ? "Calibrated ✓" : "Not Calibrated (Press V)";
            GUI.Label(new Rect(screenOffsetX + 20f, 20f, 300, 24), label, style);
        }
    }
}
