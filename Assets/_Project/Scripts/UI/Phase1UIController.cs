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
            hintText.text = $"STATUS  {message}\nR/Space pitch  X/+ up  B/- down";
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
