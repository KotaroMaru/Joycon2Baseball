using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace JoyconBaseball.Phase1.UI
{
    public sealed class Phase1UIController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private GameObject popupPanel;

        [Header("Title")]
        [SerializeField] private Text titleText;

        [Header("HUD")]
        [SerializeField] private Text scoreValueText;
        [SerializeField] private Text countValueText;
        [SerializeField] private Text pitchSpeedValueText;
        [SerializeField] private Text hintText;
        [SerializeField] private Image firstBaseLamp;
        [SerializeField] private Image secondBaseLamp;
        [SerializeField] private Image thirdBaseLamp;

        [Header("Popup")]
        [SerializeField] private Text popupText;
        [SerializeField] private Image popupBackground;

        [Header("Results")]
        [SerializeField] private Text resultText;

        private bool initialized;
        private Coroutine popupCoroutine;
        private string persistentHint = "";

        public bool TitleVisible => titlePanel != null && titlePanel.activeSelf;

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            if (!HasRequiredReferences())
            {
                Debug.LogError("Phase1UIController is missing UI references. Assign the authored scene UI objects in the inspector.", this);
                enabled = false;
                return;
            }

            SetResultVisible(false);
            popupPanel.SetActive(false);
        }

        public void ShowTitle()
        {
            titlePanel.SetActive(true);
            titleText.text =
                "VS OHTANI Phase1 MVP\n\n" +
                "Enter / Joy-Con A: start game\n" +
                "Space / Joy-Con R: pitch\n" +
                "Joy-Con gyro: swing bat\n" +
                "+ / Joy-Con X : speed up\n" +
                "- / Joy-Con B : speed down\n" +
                "C: Joy-Con recalibrate";
        }

        /// <summary>任意のテキストでタイトル画面を表示する（Phase3 など用）。</summary>
        public void ShowTitleWithText(string text)
        {
            titlePanel.SetActive(true);
            titleText.text = text;
        }

        /// <summary>HUD 下部のヒント行を固定文字列で上書きする。空文字でリセット。</summary>
        public void SetPersistentHint(string hint)
        {
            persistentHint = hint ?? "";
        }

        public void HideTitle()
        {
            titlePanel.SetActive(false);
        }

        public void SetHudVisible(bool visible)
        {
            hudPanel.SetActive(visible);
            if (!visible)
            {
                popupPanel.SetActive(false);
            }
        }

        public void SetResultVisible(bool visible)
        {
            resultPanel.SetActive(visible);
        }

        public void UpdateHud(
            int balls,
            int strikes,
            int outs,
            int score,
            bool runnerOnFirst,
            bool runnerOnSecond,
            bool runnerOnThird,
            float pitchSpeed,
            string message)
        {
            scoreValueText.text = score.ToString();
            countValueText.text = $"B {balls}   S {strikes}   O {outs}";
            pitchSpeedValueText.text = $"{pitchSpeed:0} km/h";
            var hint = string.IsNullOrEmpty(persistentHint)
                ? "R/Space pitch  X/+ up  B/- down"
                : persistentHint;
            hintText.text = $"STATUS  {message}\n{hint}";
            SetBaseLamp(firstBaseLamp, runnerOnFirst);
            SetBaseLamp(secondBaseLamp, runnerOnSecond);
            SetBaseLamp(thirdBaseLamp, runnerOnThird);
        }

        public void ShowCenterPopup(string message, Color color)
        {
            if (!hudPanel.activeSelf)
            {
                return;
            }

            if (popupCoroutine != null)
            {
                StopCoroutine(popupCoroutine);
            }

            popupCoroutine = StartCoroutine(ShowPopupRoutine(message, color));
        }

        /// <summary>
        /// ピッチャー視点のリザルト画面を表示する。
        /// atBatResults の内容（"K", "BB", "1B", "2B", "3B", "HR", "OUT"）を集計して表示する。
        /// </summary>
        public void ShowPitcherResults(List<string> atBatResults, int runsAllowed)
        {
            titlePanel.SetActive(false);
            hudPanel.SetActive(false);
            resultPanel.SetActive(true);

            var strikeouts  = 0;
            var walks       = 0;
            var hitsAllowed = 0;
            var groundOuts  = 0;

            foreach (var r in atBatResults)
            {
                switch (r)
                {
                    case "K":   strikeouts++;  break;
                    case "BB":  walks++;       break;
                    case "1B":
                    case "2B":
                    case "3B":
                    case "HR":  hitsAllowed++; break;
                    case "OUT": groundOuts++;  break;
                }
            }

            var builder = new StringBuilder();
            builder.AppendLine("GAME OVER  - PITCHER RESULTS -");
            builder.AppendLine();
            builder.AppendLine($"Batters faced : {atBatResults.Count}");
            builder.AppendLine($"Strikeouts    : {strikeouts}");
            builder.AppendLine($"Walks         : {walks}");
            builder.AppendLine($"Hits allowed  : {hitsAllowed}");
            builder.AppendLine($"Outs (ground) : {groundOuts}");
            builder.AppendLine($"Runs allowed  : {runsAllowed}");
            builder.AppendLine();

            for (var i = 0; i < atBatResults.Count; i++)
                builder.AppendLine($"  {i + 1,2}. {atBatResults[i]}");

            builder.AppendLine();
            builder.AppendLine("Press R / Minus to return to title");
            resultText.text = builder.ToString();
        }

        public void ShowResults(List<string> atBatResults, int score)
        {
            titlePanel.SetActive(false);
            hudPanel.SetActive(false);
            resultPanel.SetActive(true);

            var builder = new StringBuilder();
            builder.AppendLine("GAME OVER");
            builder.AppendLine();
            builder.AppendLine($"Score: {score}");
            builder.AppendLine($"At-bats: {atBatResults.Count}");
            builder.AppendLine();

            for (var i = 0; i < atBatResults.Count; i++)
            {
                builder.AppendLine($"{i + 1}. {atBatResults[i]}");
            }

            builder.AppendLine();
            builder.AppendLine("Press R to return to title");
            resultText.text = builder.ToString();
        }

        private bool HasRequiredReferences()
        {
            return titlePanel != null &&
                   hudPanel != null &&
                   resultPanel != null &&
                   popupPanel != null &&
                   titleText != null &&
                   scoreValueText != null &&
                   countValueText != null &&
                   pitchSpeedValueText != null &&
                   hintText != null &&
                   firstBaseLamp != null &&
                   secondBaseLamp != null &&
                   thirdBaseLamp != null &&
                   popupText != null &&
                   popupBackground != null &&
                   resultText != null;
        }

        private void SetBaseLamp(Image lamp, bool occupied)
        {
            lamp.color = occupied
                ? new Color(1f, 0.84f, 0.24f, 1f)
                : new Color(0.22f, 0.24f, 0.28f, 1f);
        }

        private IEnumerator ShowPopupRoutine(string message, Color color)
        {
            popupPanel.SetActive(true);
            popupText.text = message;
            popupText.color = color;

            var panelColor = popupBackground.color;
            panelColor.a = 0.72f;
            popupBackground.color = panelColor;

            yield return new WaitForSeconds(1.1f);

            popupPanel.SetActive(false);
            popupCoroutine = null;
        }
    }
}
