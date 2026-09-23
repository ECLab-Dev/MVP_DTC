using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using VRC.Udon;

[CustomEditor(typeof(SurveyManager))]
public class SurveyManagerEditor : Editor
{
    private bool showAdvancedData = false;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SurveyManager manager = (SurveyManager)target;

        AutoResizeArrays(manager);

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox("【配信対象モード】\n・リスト限定: JsonNamesString の名簿プレイヤーのみを対象\n・ワールド全員: RecordMaster を除くインスタンス全員を対象 (初回に同意確認画面を表示)", MessageType.Info);
        EditorGUILayout.Space(5);

        // 初期の配信対象モード設定ボックス
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🎯 初期の配信対象モード設定 (初期状態)", EditorStyles.boldLabel);
        string[] initialModeOptions = new string[] {
            "🎯 リスト限定 (JsonNamesString)",
            "🌐 ワールド全員 (同意確認画面・要同意)"
        };
        int newMode = EditorGUILayout.Popup("ワールド開始時の初期モード", manager.initialTargetMode, initialModeOptions);
        if (newMode != manager.initialTargetMode)
        {
            manager.initialTargetMode = newMode;
            EditorUtility.SetDirty(manager);
        }
        if (manager.initialTargetMode == 1)
        {
            EditorGUILayout.HelpBox("【ワールド全員】で開始します。\nワールド開始時から全員に同意確認画面が表示され、同意したプレイヤーのみデータが記録されます（リスト限定による未同意プレイヤーの誤記録を防止できます）。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("【リスト限定】で開始します。\nJsonNamesStringに含まれるプレイヤーのみがデフォルト記録対象となります。", MessageType.None);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);

        // 外部 UdonBehaviour / 変数設定ボックス
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🔒 外部 UdonBehaviour 変数連携設定", EditorStyles.boldLabel);
        manager.targetUdonBehaviour = (UdonBehaviour)EditorGUILayout.ObjectField("外部 UdonBehaviour 参照", manager.targetUdonBehaviour, typeof(UdonBehaviour), true);

        manager.jsonNamesVariableName = EditorGUILayout.TextField("アンケート出現対象の変数名", manager.jsonNamesVariableName);
        manager.recordMasterVariableName = EditorGUILayout.TextField("RecordMaster(開始権限&ログ)変数名", manager.recordMasterVariableName);

        EditorGUILayout.HelpBox("外部参照を使わずエディターで直接テストする場合は、下の「Allowed User Names」に DisplayName を指定してください。", MessageType.None);

        SerializedProperty allowedUsersProp = serializedObject.FindProperty("allowedUserNames");
        if (allowedUsersProp != null)
        {
            EditorGUILayout.PropertyField(allowedUsersProp, new GUIContent("直接指定のユーザー名リスト (DisplayName)"), true);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📜 アンケート ワールド全員モード時の同意確認文面", EditorStyles.boldLabel);
        manager.consentNoticeText = EditorGUILayout.TextArea(manager.consentNoticeText, GUILayout.Height(60));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📜 DTC ワールド全員モード時の同意確認文面", EditorStyles.boldLabel);
        manager.dtcConsentNoticeText = EditorGUILayout.TextArea(manager.dtcConsentNoticeText, GUILayout.Height(60));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // 質問数の設定
        int qCount = manager.questionTexts != null ? manager.questionTexts.Length : 0;
        int newQCount = EditorGUILayout.DelayedIntField("アンケートの質問数", qCount);
        if (newQCount != qCount && newQCount >= 0)
        {
            Array.Resize(ref manager.questionTexts, newQCount);
            AutoResizeArrays(manager);
            EditorUtility.SetDirty(manager);
        }

        EditorGUILayout.Space(10);

        // 質問ごとの統合設定表示
        if (manager.questionTexts != null)
        {
            for (int i = 0; i < manager.questionTexts.Length; i++)
            {
                EditorGUILayout.BeginVertical("box");

                string typeLabel = GetTypeLabel(manager.answerTypes[i]);
                EditorGUILayout.LabelField($"Q{i + 1}. {typeLabel}", EditorStyles.boldLabel);

                manager.questionTexts[i] = EditorGUILayout.TextField("質問文", manager.questionTexts[i]);

                string[] typeNames = new string[] { "選択式 (ボタン)", "スライダー評価式", "記述回答式 (自由入力)" };
                manager.answerTypes[i] = EditorGUILayout.Popup("回答形式", manager.answerTypes[i], typeNames);

                int type = manager.answerTypes[i];

                if (type == 0) // 選択式
                {
                    EditorGUI.indentLevel++;
                    int choiceCount = EditorGUILayout.IntSlider("選択肢の数 (択数)", manager.choiceCounts[i], 2, 6);
                    manager.choiceCounts[i] = choiceCount;

                    int labelOffset = i * 6;
                    for (int c = 0; c < choiceCount; c++)
                    {
                        int labelIdx = labelOffset + c;
                        if (labelIdx < manager.optionLabels.Length)
                        {
                            manager.optionLabels[labelIdx] = EditorGUILayout.TextField($"  選択肢 {c + 1} のラベル", manager.optionLabels[labelIdx]);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                else if (type == 1) // スライダー評価式
                {
                    EditorGUI.indentLevel++;
                    int maxVal = EditorGUILayout.IntSlider("最大評価値 (1 ~ N)", manager.sliderMaxValues[i], 2, 20);
                    manager.sliderMaxValues[i] = maxVal;
                    EditorGUI.indentLevel--;
                }
                else if (type == 2) // 記述回答式
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.HelpBox("ユーザーが自由記述入力欄 (InputField) から回答します。", MessageType.None);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }

        EditorGUILayout.Space(15);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("★ UIを自動生成・一括セットアップ (VRChatサイズ最適化)", GUILayout.Height(45)))
        {
            AutoSetupSurveyUI(manager);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
        if (GUILayout.Button("🛡️ コライダーの物理衝突を防止 (IsTrigger化)", GUILayout.Height(30)))
        {
            FixColliders(manager);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(15);
        showAdvancedData = EditorGUILayout.Foldout(showAdvancedData, "内部データの直接参照 (デバッグ・UI自動参照用)");
        if (showAdvancedData)
        {
            DrawDefaultInspector();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private string GetTypeLabel(int type)
    {
        switch (type)
        {
            case 0: return "[選択式]";
            case 1: return "[スライダー評価式]";
            case 2: return "[記述回答式]";
            default: return "[不明]";
        }
    }

    private Font GetSafeFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return font;
    }

    private void AutoSetupSurveyUI(SurveyManager manager)
    {
        AutoResizeArrays(manager);

        GameObject canvasObj = manager.gameObject;

        // 親Canvasの余分なImage（背景グレー）を除去して透明化
        Image parentImg = canvasObj.GetComponent<Image>();
        if (parentImg != null)
        {
            DestroyImmediate(parentImg);
        }

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
        }

        RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
        if (canvasRt != null)
        {
            canvasRt.sizeDelta = new Vector2(1000, 600);
            canvasRt.localScale = new Vector3(0.002f, 0.002f, 0.002f);
        }

        BoxCollider existingBox = canvasObj.GetComponent<BoxCollider>();
        if (existingBox != null) DestroyImmediate(existingBox);

        System.Type uiShapeType = null;
        Component existingUiShape = null;

        Component[] parentComps = canvasObj.GetComponents<Component>();
        foreach (var comp in parentComps)
        {
            if (comp != null)
            {
                string n = comp.GetType().Name.ToLower();
                if (n.Contains("uishape") || n.Contains("ui_shape"))
                {
                    existingUiShape = comp;
                    uiShapeType = comp.GetType();
                    break;
                }
            }
        }

        if (uiShapeType == null)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types = null;
                try
                {
                    types = asm.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }
                catch { continue; }

                if (types != null)
                {
                    foreach (var t in types)
                    {
                        if (t != null)
                        {
                            string tn = t.Name.ToLower();
                            if (tn == "vrcuishape" || tn == "vrc_ui_shape")
                            {
                                uiShapeType = t;
                                break;
                            }
                        }
                    }
                }
                if (uiShapeType != null) break;
            }
        }

        if (uiShapeType != null && canvasObj.GetComponent(uiShapeType) == null)
        {
            canvasObj.AddComponent(uiShapeType);
        }

        Transform oldPanel = canvasObj.transform.Find("SurveyPanel");
        if (oldPanel != null) DestroyImmediate(oldPanel.gameObject);
        Transform oldRes = canvasObj.transform.Find("ResultPanel");
        if (oldRes != null) DestroyImmediate(oldRes.gameObject);
        Transform oldMaster = canvasObj.transform.Find("MasterControlPanel");
        if (oldMaster != null) DestroyImmediate(oldMaster.gameObject);

        Vector3 savedDtcWorldPos = canvasObj.transform.position + canvasObj.transform.right * 2.3f;
        Quaternion savedDtcWorldRot = canvasObj.transform.rotation;
        Vector3 savedDtcScale = new Vector3(0.002f, 0.002f, 0.002f);
        bool hasSavedDtcTransform = false;

        Transform oldDtcConsent = canvasObj.transform.Find("Container_DtcConsent");
        if (oldDtcConsent == null && manager.containerDtcConsent != null)
        {
            oldDtcConsent = manager.containerDtcConsent.transform;
        }
        if (oldDtcConsent == null)
        {
            GameObject found = GameObject.Find("Container_DtcConsent");
            if (found != null) oldDtcConsent = found.transform;
        }

        if (oldDtcConsent != null)
        {
            savedDtcWorldPos = oldDtcConsent.position;
            savedDtcWorldRot = oldDtcConsent.rotation;
            savedDtcScale = oldDtcConsent.localScale;
            hasSavedDtcTransform = true;
            DestroyImmediate(oldDtcConsent.gameObject);
        }

        // 破棄前の残存参照による OdinSerializer エラーを防止するため一度参照クリア
        manager.titleText = null;
        manager.questionText = null;
        manager.containerChoice = null;
        manager.choiceButtons = new GameObject[6];
        manager.choiceButtonTexts = new Text[6];
        manager.containerRating = null;
        manager.ratingSlider = null;
        manager.ratingValueText = null;
        manager.containerTextInput = null;
        manager.inputFieldArea = null;
        manager.containerConsent = null;
        manager.consentNoticeDisplay = null;
        manager.consentAgreeButton = null;
        manager.consentDisagreeButton = null;
        manager.containerDtcConsent = null;
        manager.dtcConsentNoticeDisplay = null;
        manager.dtcConsentAgreeButton = null;
        manager.dtcConsentDisagreeButton = null;
        manager.surveyPanel = null;
        manager.resultPanel = null;
        manager.resultMessageText = null;
        manager.masterControlPanel = null;
        manager.masterStatusText = null;
        manager.masterWarningText = null;
        manager.startSurveyButton = null;
        manager.resetSurveyButton = null;
        manager.reopenSurveyButton = null;
        manager.targetModeListButton = null;
        manager.targetModeAllButton = null;

        Transform parentCanvas = canvasObj.transform;

        int qCount = (manager.questionTexts != null) ? manager.questionTexts.Length : 0;
        Font font = GetSafeFont();

        // 1. Survey Panel 作成 (回答用)
        GameObject pObj = new GameObject("SurveyPanel", typeof(RectTransform), typeof(Image));
        pObj.transform.SetParent(parentCanvas, false);
        Transform panelTr = pObj.transform;
        RectTransform rt = pObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        pObj.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 0.95f);
        manager.surveyPanel = pObj;

        // TitleText & QuestionText
        manager.titleText = CreateOrGetText(panelTr, "TitleText", $"質問 (1 / {qCount})", font, 36, TextAnchor.MiddleCenter, new Vector2(0, 220), new Vector2(900, 60));
        manager.questionText = CreateOrGetText(panelTr, "QuestionText", "質問文がここに入ります", font, 32, TextAnchor.MiddleCenter, new Vector2(0, 130), new Vector2(900, 100));

        // Container_Choice (選択式)
        GameObject cObj = new GameObject("Container_Choice", typeof(RectTransform));
        cObj.transform.SetParent(panelTr, false);
        Transform cTr = cObj.transform;
        RectTransform cRt = cObj.GetComponent<RectTransform>();
        cRt.anchoredPosition = new Vector2(0, -70);
        cRt.sizeDelta = new Vector2(850, 260);
        manager.containerChoice = cObj;

        manager.choiceButtons = new GameObject[6];
        manager.choiceButtonTexts = new Text[6];
        Vector2[] btnPositions = new Vector2[] {
            new Vector2(-220, 70),  new Vector2(220, 70),
            new Vector2(-220, 0),   new Vector2(220, 0),
            new Vector2(-220, -70), new Vector2(220, -70)
        };
        string[] choiceEvents = new string[] {
            "OnChoiceOption0", "OnChoiceOption1", "OnChoiceOption2",
            "OnChoiceOption3", "OnChoiceOption4", "OnChoiceOption5"
        };
        for (int i = 0; i < 6; i++)
        {
            GameObject bObj = CreateButtonWithEvent(cTr, $"Btn_Choice{i}", $"選択肢 {i + 1}", font, btnPositions[i], new Vector2(400, 60), 24, manager, choiceEvents[i]);
            manager.choiceButtons[i] = bObj;
            manager.choiceButtonTexts[i] = bObj.GetComponentInChildren<Text>();
        }

        // Container_Rating (スライダー評価式)
        GameObject rObj = new GameObject("Container_Rating", typeof(RectTransform));
        rObj.transform.SetParent(panelTr, false);
        Transform rTr = rObj.transform;
        RectTransform rRt = rObj.GetComponent<RectTransform>();
        rRt.anchoredPosition = new Vector2(0, -60);
        rRt.sizeDelta = new Vector2(800, 240);
        manager.containerRating = rObj;

        DefaultControls.Resources res = new DefaultControls.Resources();
        GameObject sliderObj = DefaultControls.CreateSlider(res);
        sliderObj.name = "Slider";
        sliderObj.transform.SetParent(rTr, false);
        RectTransform sRt = sliderObj.GetComponent<RectTransform>();
        sRt.anchoredPosition = new Vector2(0, 30);
        sRt.sizeDelta = new Vector2(600, 45);
        Slider s = sliderObj.GetComponent<Slider>();
        s.minValue = 1;
        s.maxValue = 10;
        s.wholeNumbers = true;
        s.value = 5;
        manager.ratingSlider = s;

        UdonBehaviour udon = manager.GetComponent<UdonBehaviour>();
        if (udon != null)
        {
            UnityEventTools.AddStringPersistentListener(s.onValueChanged, udon.SendCustomEvent, "OnRatingSliderChanged");
        }
        else
        {
            UnityEventTools.AddStringPersistentListener(s.onValueChanged, manager.SendCustomEvent, "OnRatingSliderChanged");
        }

        manager.ratingValueText = CreateOrGetText(rTr, "RatingValueText", "評価: 5 / 10", font, 44, TextAnchor.MiddleCenter, new Vector2(0, 110), new Vector2(400, 70));
        CreateButtonWithEvent(rTr, "Btn_SubmitRating", "決定", font, new Vector2(0, -70), new Vector2(240, 75), 28, manager, "OnSubmitRating");

        // Container_TextInput (記述回答式)
        GameObject txtObj = new GameObject("Container_TextInput", typeof(RectTransform));
        txtObj.transform.SetParent(panelTr, false);
        Transform txtTr = txtObj.transform;
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchoredPosition = new Vector2(0, -60);
        txtRt.sizeDelta = new Vector2(800, 240);
        manager.containerTextInput = txtObj;

        GameObject inputObj = DefaultControls.CreateInputField(res);
        inputObj.name = "InputField";
        inputObj.transform.SetParent(txtTr, false);
        RectTransform inRt = inputObj.GetComponent<RectTransform>();
        inRt.anchoredPosition = new Vector2(0, 35);
        inRt.sizeDelta = new Vector2(700, 90);
        InputField inputComp = inputObj.GetComponent<InputField>();
        Text inputFontText = inputObj.GetComponentInChildren<Text>();
        if (inputFontText != null)
        {
            inputFontText.font = font;
            inputFontText.fontSize = 28;
        }
        Text placeholderText = inputObj.transform.Find("Placeholder")?.GetComponent<Text>();
        if (placeholderText != null)
        {
            placeholderText.font = font;
            placeholderText.fontSize = 24;
            placeholderText.text = "ここにご意見・回答を入力してください...";
        }
        manager.inputFieldArea = inputComp;

        CreateButtonWithEvent(txtTr, "Btn_SubmitText", "送信する", font, new Vector2(0, -65), new Vector2(240, 75), 28, manager, "OnSubmitTextAnswer");

        // Container_Consent (同意確認画面)
        GameObject consentObj = new GameObject("Container_Consent", typeof(RectTransform));
        consentObj.transform.SetParent(panelTr, false);
        Transform consentTr = consentObj.transform;
        RectTransform consentRt = consentObj.GetComponent<RectTransform>();
        consentRt.anchoredPosition = new Vector2(0, -60);
        consentRt.sizeDelta = new Vector2(850, 260);
        manager.containerConsent = consentObj;

        Text consentNoticeText = CreateOrGetText(consentTr, "ConsentNoticeDisplay", manager.consentNoticeText, font, 22, TextAnchor.MiddleCenter, new Vector2(0, 45), new Vector2(820, 150));
        consentNoticeText.horizontalOverflow = HorizontalWrapMode.Wrap;
        consentNoticeText.verticalOverflow = VerticalWrapMode.Truncate;
        manager.consentNoticeDisplay = consentNoticeText;

        GameObject agreeBtnObj = CreateButtonWithEvent(consentTr, "Btn_ConsentAgree", "同意する", font, new Vector2(-180, -65), new Vector2(300, 65), 24, manager, "OnConsentAgree");
        manager.consentAgreeButton = agreeBtnObj.GetComponent<Button>();
        Image agreeImg = agreeBtnObj.GetComponent<Image>();
        if (agreeImg != null) agreeImg.color = new Color(0.2f, 0.75f, 0.35f, 1f);

        GameObject disagreeBtnObj = CreateButtonWithEvent(consentTr, "Btn_ConsentDisagree", "同意しない", font, new Vector2(180, -65), new Vector2(300, 65), 24, manager, "OnConsentDisagree");
        manager.consentDisagreeButton = disagreeBtnObj.GetComponent<Button>();
        Image disagreeImg = disagreeBtnObj.GetComponent<Image>();
        if (disagreeImg != null) disagreeImg.color = new Color(0.6f, 0.4f, 0.4f, 1f);

        consentObj.SetActive(false);

        // Container_DtcConsent (独立したDTC同意確認画面 World Space Canvas)
        GameObject dtcConsentObj = new GameObject("Container_DtcConsent", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        if (canvasObj.transform.parent != null)
        {
            dtcConsentObj.transform.SetParent(canvasObj.transform.parent, false);
        }
        else
        {
            dtcConsentObj.transform.SetParent(null, false);
        }

        dtcConsentObj.transform.position = savedDtcWorldPos;
        dtcConsentObj.transform.rotation = savedDtcWorldRot;
        dtcConsentObj.transform.localScale = savedDtcScale;

        Vector2 targetSize = (canvasRt != null) ? canvasRt.sizeDelta : new Vector2(1000, 600);
        RectTransform dtcConsentRt = dtcConsentObj.GetComponent<RectTransform>();
        dtcConsentRt.sizeDelta = targetSize;

        Canvas dtcCanvas = dtcConsentObj.GetComponent<Canvas>();
        dtcCanvas.renderMode = RenderMode.WorldSpace;

        bool uiShapeAdded = false;
        if (existingUiShape != null)
        {
            UnityEditorInternal.ComponentUtility.CopyComponent(existingUiShape);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(dtcConsentObj);
            uiShapeAdded = true;
            Debug.Log($"★ [SurveyManager] ComponentUtility で VRC UI Shape ({existingUiShape.GetType().FullName}) を Container_DtcConsent に完全にコピー＆ペーストしました！");
        }
        else if (uiShapeType != null)
        {
            if (dtcConsentObj.GetComponent(uiShapeType) == null)
            {
                dtcConsentObj.AddComponent(uiShapeType);
            }
            uiShapeAdded = true;
            Debug.Log($"★ [SurveyManager] VRC UI Shape ({uiShapeType.FullName}) を Container_DtcConsent に自動追加しました！");
        }

        if (!uiShapeAdded)
        {
            Debug.LogError("⚠️ [SurveyManager] VRC UI Shape の型を取得できませんでした。");
        }

        Image dtcBgImg = dtcConsentObj.GetComponent<Image>();
        dtcBgImg.color = new Color(0.12f, 0.16f, 0.22f, 0.98f);
        dtcBgImg.raycastTarget = false; // 背景Imageはボタン操作を遮らないようOFF
        manager.containerDtcConsent = dtcConsentObj;
        Transform dtcConsentTr = dtcConsentObj.transform;

        CreateOrGetText(dtcConsentTr, "DtcTitleText", "【位置・視線データ収集 (MVP_DTC) 同意確認】", font, 36, TextAnchor.MiddleCenter, new Vector2(0, 210), new Vector2(920, 60));

        Text dtcNoticeText = CreateOrGetText(dtcConsentTr, "DtcConsentNoticeDisplay", manager.dtcConsentNoticeText, font, 24, TextAnchor.MiddleCenter, new Vector2(0, 45), new Vector2(900, 220));
        dtcNoticeText.horizontalOverflow = HorizontalWrapMode.Wrap;
        dtcNoticeText.verticalOverflow = VerticalWrapMode.Truncate;
        manager.dtcConsentNoticeDisplay = dtcNoticeText;

        GameObject dtcAgreeBtnObj = CreateButtonWithEvent(dtcConsentTr, "Btn_DtcConsentAgree", "同意する (データ記録許可)", font, new Vector2(-220, -145), new Vector2(360, 80), 26, manager, "OnDtcConsentAgree");
        manager.dtcConsentAgreeButton = dtcAgreeBtnObj.GetComponent<Button>();
        Image dtcAgreeImg = dtcAgreeBtnObj.GetComponent<Image>();
        if (dtcAgreeImg != null) dtcAgreeImg.color = new Color(0.2f, 0.75f, 0.45f, 1f);

        GameObject dtcDisagreeBtnObj = CreateButtonWithEvent(dtcConsentTr, "Btn_DtcConsentDisagree", "同意しない (辞退)", font, new Vector2(220, -145), new Vector2(360, 80), 26, manager, "OnDtcConsentDisagree");
        manager.dtcConsentDisagreeButton = dtcDisagreeBtnObj.GetComponent<Button>();
        Image dtcDisagreeImg = dtcDisagreeBtnObj.GetComponent<Image>();
        if (dtcDisagreeImg != null) dtcDisagreeImg.color = new Color(0.65f, 0.35f, 0.35f, 1f);

        dtcConsentObj.SetActive(false);

        // ResultPanel
        GameObject resObj = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
        resObj.transform.SetParent(parentCanvas, false);
        Transform resTr = resObj.transform;
        RectTransform resRt = resObj.GetComponent<RectTransform>();
        resRt.anchorMin = Vector2.zero;
        resRt.anchorMax = Vector2.one;
        resRt.offsetMin = Vector2.zero;
        resRt.offsetMax = Vector2.zero;
        resObj.GetComponent<Image>().color = new Color(0.1f, 0.22f, 0.15f, 0.95f);
        manager.resultPanel = resObj;

        manager.resultMessageText = CreateOrGetText(resTr, "ResultMessageText", "ご回答ありがとうございました！", font, 40, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(800, 150));
        manager.resultPanel.SetActive(false);

        // 2. RecordMaster 専用コントロールパネル作成
        GameObject mObj = new GameObject("MasterControlPanel", typeof(RectTransform), typeof(Image));
        mObj.transform.SetParent(parentCanvas, false);
        Transform mTr = mObj.transform;
        RectTransform mRt = mObj.GetComponent<RectTransform>();
        mRt.anchorMin = Vector2.zero;
        mRt.anchorMax = Vector2.one;
        mRt.offsetMin = Vector2.zero;
        mRt.offsetMax = Vector2.zero;
        Image masterBgImg = mObj.GetComponent<Image>();
        masterBgImg.color = new Color(0.15f, 0.18f, 0.28f, 0.98f);
        masterBgImg.raycastTarget = false; // 管理者パネル背景のRaycastTargetをOFFにして他のUI操作を妨げない
        manager.masterControlPanel = mObj;

        CreateOrGetText(mTr, "MasterTitle", "【RecordMaster限定 コントロールパネル】", font, 36, TextAnchor.MiddleCenter, new Vector2(0, 230), new Vector2(900, 50));

        // 左側: ScrollView (進捗一覧)
        GameObject scrollObj = DefaultControls.CreateScrollView(res);
        scrollObj.name = "ScrollView";
        scrollObj.transform.SetParent(mTr, false);
        RectTransform scrollRt = scrollObj.GetComponent<RectTransform>();
        scrollRt.anchoredPosition = new Vector2(-180, -30);
        scrollRt.sizeDelta = new Vector2(540, 420);

        Transform contentTr = scrollObj.transform.Find("Viewport/Content");
        if (contentTr != null)
        {
            foreach (Transform child in contentTr)
            {
                DestroyImmediate(child.gameObject);
            }

            Text statusText = CreateOrGetText(contentTr, "MasterStatusText", "【進捗: 0 / 0 人 (0%)】\n・ 読込中...", font, 24, TextAnchor.UpperLeft, Vector2.zero, new Vector2(520, 400));
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            manager.masterStatusText = statusText;

            RectTransform statusRt = statusText.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0, 1);
            statusRt.anchorMax = new Vector2(1, 1);
            statusRt.pivot = new Vector2(0, 1);
            statusRt.anchoredPosition = Vector2.zero;
        }

        // 右側1: 一斉開始ボタン
        GameObject startBtnObj = CreateButtonWithEvent(mTr, "Btn_StartSurvey", "▶ アンケート一斉開始", font, new Vector2(260, 160), new Vector2(340, 55), 22, manager, "OnStartSurveyButton");
        Button startBtnComp = startBtnObj.GetComponent<Button>();
        manager.startSurveyButton = startBtnComp;

        Image btnImg = startBtnObj.GetComponent<Image>();
        if (btnImg != null)
        {
            btnImg.color = new Color(0.2f, 0.75f, 0.35f, 1f);
        }
        Text btnTxt = startBtnObj.GetComponentInChildren<Text>();
        if (btnTxt != null)
        {
            btnTxt.color = Color.white;
        }

        // 右側2: 配信対象ラジオボタン (2択)
        GameObject listBtnObj = CreateButtonWithEvent(mTr, "Btn_ModeList", "🔘 リスト限定", font, new Vector2(175, 85), new Vector2(165, 55), 18, manager, "OnSelectModeListButton");
        manager.targetModeListButton = listBtnObj.GetComponent<Button>();

        GameObject allBtnObj = CreateButtonWithEvent(mTr, "Btn_ModeAll", "⚪ ワールド全員", font, new Vector2(345, 85), new Vector2(165, 55), 18, manager, "OnSelectModeAllButton");
        manager.targetModeAllButton = allBtnObj.GetComponent<Button>();

        // 右側3: 管理者用 未回答者にアンケート再表示ボタン
        GameObject reopenBtnObj = CreateButtonWithEvent(mTr, "Btn_ReopenSurvey", "📋 未回答者に再表示", font, new Vector2(260, 20), new Vector2(340, 55), 20, manager, "OnReopenSurveyButton");
        manager.reopenSurveyButton = reopenBtnObj.GetComponent<Button>();
        Image reopenImg = reopenBtnObj.GetComponent<Image>();
        if (reopenImg != null)
        {
            reopenImg.color = new Color(0.2f, 0.45f, 0.85f, 1f);
        }
        Text reopenTxt = reopenBtnObj.GetComponentInChildren<Text>();
        if (reopenTxt != null)
        {
            reopenTxt.color = Color.white;
        }

        // 右側4: アンケートリセットボタン
        GameObject resetBtnObj = CreateButtonWithEvent(mTr, "Btn_ResetSurvey", "🔄 アンケートリセット", font, new Vector2(260, -45), new Vector2(340, 50), 18, manager, "OnResetSurveyButton");
        Button resetBtnComp = resetBtnObj.GetComponent<Button>();
        manager.resetSurveyButton = resetBtnComp;

        Image resetBtnImg = resetBtnObj.GetComponent<Image>();
        if (resetBtnImg != null)
        {
            resetBtnImg.color = new Color(0.85f, 0.4f, 0.2f, 1f);
        }
        Text resetBtnTxt = resetBtnObj.GetComponentInChildren<Text>();
        if (resetBtnTxt != null)
        {
            resetBtnTxt.color = Color.white;
        }

        // 右側5: MasterControlPanel の直下に MasterWarningText を作成
        Text warningText = CreateOrGetText(mTr, "MasterWarningText", "⚠️ 開始できません\n\n対象プレイヤーが\nワールド内に存在しません。", font, 18, TextAnchor.MiddleCenter, new Vector2(260, -145), new Vector2(340, 110));
        warningText.horizontalOverflow = HorizontalWrapMode.Wrap;
        warningText.verticalOverflow = VerticalWrapMode.Overflow;
        warningText.color = new Color(1.0f, 0.35f, 0.35f, 1.0f);
        manager.masterWarningText = warningText;

        manager.surveyPanel.SetActive(false);
        manager.masterControlPanel.SetActive(true);
        mObj.transform.SetAsLastSibling();

        FixColliders(manager);

        // 確実な参照のシリアライズ保存
        EditorUtility.SetDirty(manager);
        serializedObject.Update();
        serializedObject.ApplyModifiedProperties();

        Debug.Log($"★ [SurveyManager] 親Canvasの余分な背景Imageを除去し、完全シリアライズ保存されたUIセットアップが完了しました！");
    }

    [MenuItem("Tools/MVP/Fix All Colliders (IsTrigger)")]
    public static void FixAllCollidersMenu()
    {
        SurveyManager manager = UnityEngine.Object.FindObjectOfType<SurveyManager>();
        if (manager != null)
        {
            FixCollidersStatic(manager);
        }
        else
        {
            Debug.LogWarning("[SurveyManagerEditor] シーン内に SurveyManager が見つかりませんでした。");
        }
    }

    [InitializeOnLoadMethod]
    private static void OnEditorLoad()
    {
        EditorApplication.delayCall += () =>
        {
            SurveyManager manager = UnityEngine.Object.FindObjectOfType<SurveyManager>();
            if (manager != null)
            {
                FixCollidersStatic(manager);
            }
        };
    }

    private void FixColliders(SurveyManager manager)
    {
        FixCollidersStatic(manager);
    }

    public static void FixCollidersStatic(SurveyManager manager)
    {
        if (manager == null) return;

        int count = 0;
        Collider[] colliders = manager.GetComponentsInChildren<Collider>(true);
        if (colliders != null)
        {
            foreach (var c in colliders)
            {
                if (c != null)
                {
                    c.isTrigger = true;
                    EditorUtility.SetDirty(c);
                    count++;
                }
            }
        }

        GameObject dtcConsent = manager.containerDtcConsent;
        if (dtcConsent == null)
        {
            dtcConsent = GameObject.Find("Container_DtcConsent");
            if (dtcConsent != null)
            {
                manager.containerDtcConsent = dtcConsent;
                EditorUtility.SetDirty(manager);
            }
        }

        if (dtcConsent != null)
        {
            BoxCollider box = dtcConsent.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(dtcConsent);
                RectTransform rt = dtcConsent.GetComponent<RectTransform>();
                Vector2 size = (rt != null) ? rt.sizeDelta : new Vector2(1000, 600);
                box.size = new Vector3(size.x, size.y, 1f);
                box.center = Vector3.zero;
            }
            if (box != null)
            {
                box.isTrigger = true;
                EditorUtility.SetDirty(box);
                count++;
            }

            Collider[] dtcColliders = dtcConsent.GetComponentsInChildren<Collider>(true);
            if (dtcColliders != null)
            {
                foreach (var c in dtcColliders)
                {
                    if (c != null)
                    {
                        c.isTrigger = true;
                        EditorUtility.SetDirty(c);
                        count++;
                    }
                }
            }
            EditorUtility.SetDirty(dtcConsent);
        }

        EditorUtility.SetDirty(manager);
        if (manager.gameObject.scene.IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }
        Debug.Log($"[SurveyManagerEditor] {count} 個のコライダーを検出・IsTrigger化しました（Container_DtcConsent含む）。プレイヤーの物理衝突を防止しました。");
    }

    private void AutoResizeArrays(SurveyManager manager)
    {
        if (manager.questionTexts == null) return;
        int qCount = manager.questionTexts.Length;

        int oldAnsLen = (manager.answerTypes != null) ? manager.answerTypes.Length : 0;
        Array.Resize(ref manager.answerTypes, qCount);

        int oldChoiceLen = (manager.choiceCounts != null) ? manager.choiceCounts.Length : 0;
        Array.Resize(ref manager.choiceCounts, qCount);
        for (int i = oldChoiceLen; i < qCount; i++)
        {
            if (manager.choiceCounts[i] == 0) manager.choiceCounts[i] = 2;
        }

        int oldSliderLen = (manager.sliderMaxValues != null) ? manager.sliderMaxValues.Length : 0;
        Array.Resize(ref manager.sliderMaxValues, qCount);
        for (int i = oldSliderLen; i < qCount; i++)
        {
            if (manager.sliderMaxValues[i] == 0) manager.sliderMaxValues[i] = 5;
        }

        int targetOptionLen = qCount * 6;
        Array.Resize(ref manager.optionLabels, targetOptionLen);
    }

    private Text CreateOrGetText(Transform parent, string name, string defaultText, Font font, int fontSize, TextAnchor alignment, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text t = obj.GetComponent<Text>();
        t.text = defaultText;
        t.font = font;
        t.fontSize = fontSize;
        t.alignment = alignment;
        t.color = Color.white;
        t.raycastTarget = false; // テキスト自体はRaycast判定を受け取らず、後ろのボタン判定を通過させる

        return t;
    }

    private GameObject CreateButtonWithEvent(Transform parent, string name, string label, Font font, Vector2 pos, Vector2 size, int fontSize, SurveyManager manager, string eventName)
    {
        DefaultControls.Resources res = new DefaultControls.Resources();
        GameObject btnObj = DefaultControls.CreateButton(res);
        btnObj.name = name;
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image btnImg = btnObj.GetComponent<Image>();
        if (btnImg != null)
        {
            btnImg.raycastTarget = true; // ボタン本体ImageのRaycastTargetのみON
        }

        Text t = btnObj.GetComponentInChildren<Text>();
        if (t != null)
        {
            t.text = label;
            t.font = font;
            t.fontSize = fontSize;
            t.color = Color.black;
            t.raycastTarget = false; // ラベルテキストのRaycastTargetはOFF
        }

        Button b = btnObj.GetComponent<Button>();
        if (b != null && manager != null)
        {
            b.onClick = new Button.ButtonClickedEvent();
            UdonBehaviour udon = manager.GetComponent<UdonBehaviour>();
            if (udon != null)
            {
                UnityEventTools.AddStringPersistentListener(b.onClick, udon.SendCustomEvent, eventName);
            }
            else
            {
                UnityEventTools.AddStringPersistentListener(b.onClick, manager.SendCustomEvent, eventName);
            }
            EditorUtility.SetDirty(b);
        }

        return btnObj;
    }
}
