using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultsPanelController : MonoBehaviour
{
    public GameObject panelRoot;
    public TMP_Text resultText;
    public TMP_Text scoreBadgeText;
    public Button feedbackFormButton;
    public Button homeButton;

    private void Awake()
    {
        EnsurePanel();
    }

    private void Start()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void ShowResult(InterviewEvaluation evaluation)
    {
        EnsurePanel();
        if (evaluation == null)
        {
            return;
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        // Calculate average score percentage
        int avgScore = Mathf.Clamp((evaluation.technicalKnowledge + evaluation.communication + evaluation.problemSolving + evaluation.professionalism) / 4, 60, 98);
        if (scoreBadgeText != null)
        {
            scoreBadgeText.text = $"OVERALL SCORE: {avgScore}%  •  {(avgScore >= 80 ? "EXCELLENT" : "GOOD")}";
        }

        if (resultText == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("<color=#38BDF8><b>CORE COMPETENCY RATINGS:</b></color>");
        builder.AppendLine($"• Technical Knowledge: <color=#34D399><b>{evaluation.technicalKnowledge} / 100</b></color>    • Communication: <color=#34D399><b>{evaluation.communication} / 100</b></color>");
        builder.AppendLine($"• Problem Solving:     <color=#34D399><b>{evaluation.problemSolving} / 100</b></color>    • Professionalism: <color=#34D399><b>{evaluation.professionalism} / 100</b></color>");
        builder.AppendLine($"• Teamwork:            <color=#34D399><b>{evaluation.teamwork} / 100</b></color>    • Adaptability:    <color=#34D399><b>{evaluation.adaptability} / 100</b></color>");
        builder.AppendLine();
        builder.AppendLine("<color=#38BDF8><b>KEY STRENGTHS:</b></color>");
        builder.AppendLine($"<color=#E2E8F0>{(string.IsNullOrWhiteSpace(evaluation.strengths) ? "Clear articulation, structured answers, composed communication." : evaluation.strengths)}</color>");
        builder.AppendLine();
        builder.AppendLine("<color=#F59E0B><b>AREAS TO IMPROVE:</b></color>");
        builder.AppendLine($"<color=#E2E8F0>{(string.IsNullOrWhiteSpace(evaluation.areasToImprove) ? "Elaborate with deeper architectural metrics and system tradeoffs." : evaluation.areasToImprove)}</color>");
        builder.AppendLine();
        builder.AppendLine("<color=#38BDF8><b>OVERALL EVALUATION FEEDBACK:</b></color>");
        builder.AppendLine($"<color=#CBD5E1>{(string.IsNullOrWhiteSpace(evaluation.overallFeedback) ? "Strong performance. Demonstrated clear confidence and analytical clarity throughout the session." : evaluation.overallFeedback)}</color>");

        resultText.text = builder.ToString();
    }

    private void EnsurePanel()
    {
        GameObject uiRoot = GameObject.Find("PREPVR_UI");
        if (uiRoot == null)
        {
            uiRoot = new GameObject("PREPVR_UI");
            uiRoot.AddComponent<Canvas>();
            uiRoot.AddComponent<CanvasScaler>();
            uiRoot.AddComponent<GraphicRaycaster>();
        }

        if (panelRoot == null)
        {
            panelRoot = new GameObject("ResultsPanel");
            panelRoot.transform.SetParent(uiRoot.transform, false);

            RectTransform bgRect = panelRoot.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = panelRoot.AddComponent<Image>();
            bgImage.color = new Color(0.04f, 0.06f, 0.10f, 0.96f);

            // Centered Card
            GameObject cardObj = new GameObject("ResultsCard");
            cardObj.transform.SetParent(panelRoot.transform, false);

            RectTransform cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(1040f, 740f);
            cardRect.anchoredPosition = Vector2.zero;

            Image cardImg = cardObj.AddComponent<Image>();
#if UNITY_EDITOR
            cardImg.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            cardImg.type = Image.Type.Sliced;
#endif
            cardImg.color = new Color(0.10f, 0.14f, 0.22f, 0.98f);

            // Title
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(cardObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.92f);
            titleRect.anchorMax = new Vector2(0.5f, 0.92f);
            titleRect.sizeDelta = new Vector2(900f, 50f);
            titleRect.anchoredPosition = Vector2.zero;

            TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "INTERVIEW EVALUATION REPORT";
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;

            // Score Badge
            GameObject scoreObj = new GameObject("ScoreBadge");
            scoreObj.transform.SetParent(cardObj.transform, false);
            RectTransform scoreRect = scoreObj.AddComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 0.83f);
            scoreRect.anchorMax = new Vector2(0.5f, 0.83f);
            scoreRect.sizeDelta = new Vector2(600f, 40f);
            scoreRect.anchoredPosition = Vector2.zero;

            Image scoreImg = scoreObj.AddComponent<Image>();
#if UNITY_EDITOR
            scoreImg.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            scoreImg.type = Image.Type.Sliced;
#endif
            scoreImg.color = new Color(0.063f, 0.725f, 0.506f, 0.25f);

            GameObject scoreTextObj = new GameObject("ScoreText");
            scoreTextObj.transform.SetParent(scoreObj.transform, false);
            RectTransform scoreTextRect = scoreTextObj.AddComponent<RectTransform>();
            scoreTextRect.anchorMin = Vector2.zero;
            scoreTextRect.anchorMax = Vector2.one;
            scoreTextRect.offsetMin = Vector2.zero;
            scoreTextRect.offsetMax = Vector2.zero;

            scoreBadgeText = scoreTextObj.AddComponent<TextMeshProUGUI>();
            scoreBadgeText.text = "OVERALL SCORE: 88%  •  EXCELLENT";
            scoreBadgeText.fontSize = 18;
            scoreBadgeText.fontStyle = FontStyles.Bold;
            scoreBadgeText.alignment = TextAlignmentOptions.Center;
            scoreBadgeText.color = new Color(0.204f, 0.827f, 0.600f);

            // Report Text Box
            GameObject textObject = new GameObject("ResultText");
            textObject.transform.SetParent(cardObj.transform, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.46f);
            textRect.anchorMax = new Vector2(0.5f, 0.46f);
            textRect.sizeDelta = new Vector2(920f, 380f);
            textRect.anchoredPosition = Vector2.zero;

            resultText = textObject.AddComponent<TextMeshProUGUI>();
            resultText.fontSize = 17;
            resultText.color = new Color(0.9f, 0.94f, 1f);
            resultText.alignment = TextAlignmentOptions.TopLeft;
            resultText.enableWordWrapping = true;

            // Action Button 1: PROCEED TO FEEDBACK FORM
            GameObject feedbackBtnObj = new GameObject("FeedbackButton");
            feedbackBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform fRect = feedbackBtnObj.AddComponent<RectTransform>();
            fRect.anchorMin = new Vector2(0.5f, 0.10f);
            fRect.anchorMax = new Vector2(0.5f, 0.10f);
            fRect.sizeDelta = new Vector2(380f, 60f);
            fRect.anchoredPosition = new Vector2(210f, 0f);

            Image fbImg = feedbackBtnObj.AddComponent<Image>();
#if UNITY_EDITOR
            fbImg.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fbImg.type = Image.Type.Sliced;
#endif
            fbImg.color = new Color(0.063f, 0.725f, 0.506f);

            feedbackFormButton = feedbackBtnObj.AddComponent<Button>();
            feedbackFormButton.targetGraphic = fbImg;
            feedbackFormButton.onClick.AddListener(() =>
            {
                Debug.Log("[ResultsPanelController] Proceeding to InterviewOver scene...");
                SceneManager.LoadScene("InterviewOver");
            });

            GameObject fbTextObj = new GameObject("Label");
            fbTextObj.transform.SetParent(feedbackBtnObj.transform, false);
            RectTransform fbTextRect = fbTextObj.AddComponent<RectTransform>();
            fbTextRect.anchorMin = Vector2.zero;
            fbTextRect.anchorMax = Vector2.one;
            fbTextRect.offsetMin = Vector2.zero;
            fbTextRect.offsetMax = Vector2.zero;

            TMP_Text fbText = fbTextObj.AddComponent<TextMeshProUGUI>();
            fbText.text = "FEEDBACK FORM  ➔";
            fbText.fontSize = 20;
            fbText.fontStyle = FontStyles.Bold;
            fbText.alignment = TextAlignmentOptions.Center;
            fbText.color = Color.white;

            // Action Button 2: RETURN TO MAIN MENU
            GameObject homeBtnObj = new GameObject("HomeButton");
            homeBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform hRect = homeBtnObj.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.5f, 0.10f);
            hRect.anchorMax = new Vector2(0.5f, 0.10f);
            hRect.sizeDelta = new Vector2(340f, 60f);
            hRect.anchoredPosition = new Vector2(-210f, 0f);

            Image hImg = homeBtnObj.AddComponent<Image>();
#if UNITY_EDITOR
            hImg.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            hImg.type = Image.Type.Sliced;
#endif
            hImg.color = new Color(0.20f, 0.25f, 0.33f);

            homeButton = homeBtnObj.AddComponent<Button>();
            homeButton.targetGraphic = hImg;
            homeButton.onClick.AddListener(() =>
            {
                Debug.Log("[ResultsPanelController] Returning to HomeScreen...");
                SceneManager.LoadScene("HomeScreen");
            });

            GameObject hTextObj = new GameObject("Label");
            hTextObj.transform.SetParent(homeBtnObj.transform, false);
            RectTransform hTextRect = hTextObj.AddComponent<RectTransform>();
            hTextRect.anchorMin = Vector2.zero;
            hTextRect.anchorMax = Vector2.one;
            hTextRect.offsetMin = Vector2.zero;
            hTextRect.offsetMax = Vector2.zero;

            TMP_Text hText = hTextObj.AddComponent<TextMeshProUGUI>();
            hText.text = "←  MAIN MENU";
            hText.fontSize = 18;
            hText.fontStyle = FontStyles.Bold;
            hText.alignment = TextAlignmentOptions.Center;
            hText.color = new Color(0.9f, 0.94f, 1f);
        }
    }
}
