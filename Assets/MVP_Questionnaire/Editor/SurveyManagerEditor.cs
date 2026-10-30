using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEditor;
using UnityEditor.Events;
using VRC.Udon;
using UdonSharpEditor;

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

        // オンラインJSON設定ボックス (VRCStringDownloader)
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🌐 オンラインJSON読み込み設定 (VRCStringDownloader)", EditorStyles.boldLabel);

        SerializedProperty useOnlineProp = serializedObject.FindProperty("useOnlineSurveyJson");
        if (useOnlineProp != null)
        {
            EditorGUILayout.PropertyField(useOnlineProp, new GUIContent("オンラインJSONから質問を読み込む"));
        }
        else
        {
            manager.useOnlineSurveyJson = EditorGUILayout.Toggle("オンラインJSONから質問を読み込む", manager.useOnlineSurveyJson);
        }

        SerializedProperty urlProp = serializedObject.FindProperty("surveyJsonUrl");
        if (urlProp != null)
        {
            EditorGUILayout.PropertyField(urlProp, new GUIContent("アンケートJSONのURL (VRCUrl)"), true);
        }

        EditorGUILayout.HelpBox(
            "VRChatワールド開始時（およびリセット時）に、指定した URL (GitHub GistのRaw URL等) からJSONを自動取得して質問内容を反映します。\n" +
            "※ 読み込み失敗時やオフライン時は、下のInspectorで設定されている質問データがフォールバックとして使用されます。",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.25f, 0.7f, 0.95f);
        if (GUILayout.Button("📥 URLからJSONをテスト取得＆Inspectorに反映", GUILayout.Height(28)))
        {
            serializedObject.ApplyModifiedProperties();
            FetchAndApplyJsonInEditor(manager);
        }
        GUI.backgroundColor = new Color(0.85f, 0.65f, 0.25f);
        if (GUILayout.Button("💾 現在の質問設定をJSONに出力", GUILayout.Height(28)))
        {
            ExportCurrentSettingsToJsonFile(manager);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);

        // 出題順・ランダム並び替え設定ボックス
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("🔀 出題順ランダム並び替え設定", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "出題カテゴリの順番【選択式 → 選択式（グリッド） → スライダー評価 → 自由記述】は維持したまま、\n" +
            "各回答方式の質問項目ごとに一括でランダムな順番に並び替えて出題します。",
            MessageType.None);

        EditorGUI.BeginChangeCheck();
        bool newShuffle = EditorGUILayout.ToggleLeft(" 各回答方式内の質問をランダムな順番で出題する", manager.shuffleQuestionsWithinCategory);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(manager, "Update Question Shuffle Setting");
            manager.shuffleQuestionsWithinCategory = newShuffle;
            SaveAndSync(manager);
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
        // 参照が外れている場合は自動解決
        if (manager.consentNoticeDisplay == null)
        {
            GameObject cObj = manager.containerConsent;
            if (cObj == null && manager.surveyPanel != null)
            {
                Transform ct = manager.surveyPanel.transform.Find("Container_Consent");
                if (ct != null) cObj = ct.gameObject;
            }
            if (cObj == null) cObj = GameObject.Find("Container_Consent");
            if (cObj != null)
            {
                manager.containerConsent = cObj;
                Transform t = cObj.transform.Find("ConsentNoticeDisplay");
                if (t != null) manager.consentNoticeDisplay = t.GetComponent<Text>();
            }
        }
        if (manager.dtcConsentNoticeDisplay == null || manager.dtcConsentTitleDisplay == null)
        {
            GameObject dtcObj = manager.containerDtcConsent;
            if (dtcObj == null) dtcObj = GameObject.Find("Container_DtcConsent");
            if (dtcObj != null)
            {
                manager.containerDtcConsent = dtcObj;
                if (manager.dtcConsentTitleDisplay == null)
                {
                    Transform tt = dtcObj.transform.Find("DtcTitleText");
                    if (tt == null) tt = dtcObj.transform.Find("TitleText");
                    if (tt != null) manager.dtcConsentTitleDisplay = tt.GetComponent<Text>();
                }
                if (manager.dtcConsentNoticeDisplay == null)
                {
                    Transform t = dtcObj.transform.Find("DtcConsentNoticeDisplay");
                    if (t == null) t = dtcObj.transform.Find("ConsentNoticeDisplay");
                    if (t != null) manager.dtcConsentNoticeDisplay = t.GetComponent<Text>();
                }
            }
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📜 アンケート ワールド全員モード時の同意確認設定", EditorStyles.boldLabel);
        SerializedProperty consentTitleProp = serializedObject.FindProperty("consentTitleText");
        string currentConsentTitleVal = consentTitleProp != null ? consentTitleProp.stringValue : manager.consentTitleText;
        EditorGUI.BeginChangeCheck();
        string newConsentTitle = EditorGUILayout.TextField("UIパネル上部のタイトル", currentConsentTitleVal);
        if (EditorGUI.EndChangeCheck())
        {
            if (consentTitleProp != null) consentTitleProp.stringValue = newConsentTitle;
            manager.consentTitleText = newConsentTitle;
            SaveAndSync(manager);
        }

        SerializedProperty consentProp = serializedObject.FindProperty("consentNoticeText");
        string currentConsentVal = consentProp != null ? consentProp.stringValue : manager.consentNoticeText;

        EditorGUI.BeginChangeCheck();
        GUIStyle wrapTextAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        string newConsent = EditorGUILayout.TextArea(currentConsentVal, wrapTextAreaStyle, GUILayout.Height(70));
        if (EditorGUI.EndChangeCheck())
        {
            if (consentProp != null) consentProp.stringValue = newConsent;
            manager.consentNoticeText = newConsent;

            if (manager.consentNoticeDisplay != null)
            {
                Undo.RecordObject(manager.consentNoticeDisplay, "Update Consent Notice Display");
                manager.consentNoticeDisplay.text = newConsent;
                EditorUtility.SetDirty(manager.consentNoticeDisplay);
            }
            SaveAndSync(manager);
        }
        else if (manager.consentNoticeDisplay != null && manager.consentNoticeDisplay.text != currentConsentVal)
        {
            // 不一致がある場合は自動同期
            Undo.RecordObject(manager.consentNoticeDisplay, "Auto Sync Consent Notice Display");
            manager.consentNoticeDisplay.text = currentConsentVal;
            EditorUtility.SetDirty(manager.consentNoticeDisplay);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📜 DTC ワールド全員モード時の同意確認設定", EditorStyles.boldLabel);
        SerializedProperty dtcConsentTitleProp = serializedObject.FindProperty("dtcConsentTitleText");
        string currentDtcTitleVal = dtcConsentTitleProp != null ? dtcConsentTitleProp.stringValue : manager.dtcConsentTitleText;
        EditorGUI.BeginChangeCheck();
        string newDtcTitle = EditorGUILayout.TextField("UIパネル上部のタイトル", currentDtcTitleVal);
        if (EditorGUI.EndChangeCheck())
        {
            if (dtcConsentTitleProp != null) dtcConsentTitleProp.stringValue = newDtcTitle;
            manager.dtcConsentTitleText = newDtcTitle;
            if (manager.dtcConsentTitleDisplay != null)
            {
                Undo.RecordObject(manager.dtcConsentTitleDisplay, "Update DTC Consent Title Display");
                manager.dtcConsentTitleDisplay.text = newDtcTitle;
                EditorUtility.SetDirty(manager.dtcConsentTitleDisplay);
            }
            SaveAndSync(manager);
        }
        else if (manager.dtcConsentTitleDisplay != null && !string.IsNullOrEmpty(currentDtcTitleVal) && manager.dtcConsentTitleDisplay.text != currentDtcTitleVal)
        {
            Undo.RecordObject(manager.dtcConsentTitleDisplay, "Auto Sync DTC Consent Title Display");
            manager.dtcConsentTitleDisplay.text = currentDtcTitleVal;
            EditorUtility.SetDirty(manager.dtcConsentTitleDisplay);
        }

        SerializedProperty dtcConsentProp = serializedObject.FindProperty("dtcConsentNoticeText");
        string currentDtcConsentVal = dtcConsentProp != null ? dtcConsentProp.stringValue : manager.dtcConsentNoticeText;

        EditorGUI.BeginChangeCheck();
        string newDtcConsent = EditorGUILayout.TextArea(currentDtcConsentVal, wrapTextAreaStyle, GUILayout.Height(70));
        if (EditorGUI.EndChangeCheck())
        {
            if (dtcConsentProp != null) dtcConsentProp.stringValue = newDtcConsent;
            manager.dtcConsentNoticeText = newDtcConsent;

            if (manager.dtcConsentNoticeDisplay != null)
            {
                Undo.RecordObject(manager.dtcConsentNoticeDisplay, "Update DTC Consent Notice Display");
                manager.dtcConsentNoticeDisplay.text = newDtcConsent;
                EditorUtility.SetDirty(manager.dtcConsentNoticeDisplay);
            }
            SaveAndSync(manager);
        }
        else if (manager.dtcConsentNoticeDisplay != null && manager.dtcConsentNoticeDisplay.text != currentDtcConsentVal)
        {
            // 不一致がある場合は自動同期
            Undo.RecordObject(manager.dtcConsentNoticeDisplay, "Auto Sync DTC Consent Notice Display");
            manager.dtcConsentNoticeDisplay.text = currentDtcConsentVal;
            EditorUtility.SetDirty(manager.dtcConsentNoticeDisplay);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // 各カテゴリの共通説明文（VRChat回答画面の上部に表示）
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📝 各カテゴリの共通説明文 (VRChat回答画面の上部に表示)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("各カテゴリシートの1行目に記入した説明文がここに反映されます（空欄のカテゴリは説明文を表示しません）。", MessageType.None);
        EditorGUI.BeginChangeCheck();
        string newChoiceDesc = EditorGUILayout.TextField("選択式 の説明文", manager.choiceCategoryDescription ?? "");
        string newGridDesc = EditorGUILayout.TextField("選択式（グリッド） の説明文", manager.gridCategoryDescription ?? "");
        string newSliderDesc = EditorGUILayout.TextField("スライダー評価式 の説明文", manager.sliderCategoryDescription ?? "");
        string newTextDesc = EditorGUILayout.TextField("記述回答式 の説明文", manager.textCategoryDescription ?? "");
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(manager, "Update Category Descriptions");
            manager.choiceCategoryDescription = newChoiceDesc;
            manager.gridCategoryDescription = newGridDesc;
            manager.sliderCategoryDescription = newSliderDesc;
            manager.textCategoryDescription = newTextDesc;
            SaveAndSync(manager);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // 質問数の設定
        int qCount = manager.questionTexts != null ? manager.questionTexts.Length : 0;
        int newQCount = EditorGUILayout.DelayedIntField("アンケートの質問数 (エディタ設定 / フォールバック)", qCount);
        if (newQCount != qCount && newQCount >= 0)
        {
            Undo.RecordObject(manager, "Resize Questions");
            Array.Resize(ref manager.questionTexts, newQCount);
            AutoResizeArrays(manager);
            EditorUtility.SetDirty(manager);
            if (manager.gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
        }

        EditorGUILayout.Space(10);

        // 質問ごとの統合設定表示
        if (manager.questionTexts != null)
        {
            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < manager.questionTexts.Length; i++)
            {
                EditorGUILayout.BeginVertical("box");

                string typeLabel = GetTypeLabel(manager.answerTypes[i]);
                EditorGUILayout.LabelField($"Q{i + 1}. {typeLabel}", EditorStyles.boldLabel);

                manager.questionTexts[i] = EditorGUILayout.TextField("質問文", manager.questionTexts[i]);

                // 表示順: 0=選択式(0), 1=選択式（グリッド）(3), 2=スライダー評価式(1), 3=記述回答式(2)
                string[] typeDisplayNames = new string[] {
                    "選択式 (ボタン)",
                    "選択式（グリッド）",
                    "スライダー評価式",
                    "記述回答式 (自由入力)"
                };
                int[] typeValuesMap = new int[] { 0, 3, 1, 2 };
                int currentPopupIdx = 0;
                for (int tIdx = 0; tIdx < typeValuesMap.Length; tIdx++)
                {
                    if (typeValuesMap[tIdx] == manager.answerTypes[i])
                    {
                        currentPopupIdx = tIdx;
                        break;
                    }
                }
                int selectedPopupIdx = EditorGUILayout.Popup("回答形式", currentPopupIdx, typeDisplayNames);
                int newType = typeValuesMap[selectedPopupIdx];
                if (newType != manager.answerTypes[i])
                {
                    manager.answerTypes[i] = newType;
                    if (newType == 3)
                    {
                        if (manager.gridRowCounts != null && i < manager.gridRowCounts.Length && manager.gridRowCounts[i] < 1)
                            manager.gridRowCounts[i] = 3;
                        if (manager.gridColCounts != null && i < manager.gridColCounts.Length && manager.gridColCounts[i] < 2)
                            manager.gridColCounts[i] = 3;
                    }
                    AutoResizeArrays(manager);
                }

                int type = manager.answerTypes[i];

                if (type == 0) // 選択式
                {
                    EditorGUI.indentLevel++;
                    int currentCount = Mathf.Max(2, manager.choiceCounts[i]);
                    int maxSlider = Mathf.Max(24, currentCount);
                    int choiceCount = EditorGUILayout.IntSlider("選択肢の数 (択数)", currentCount, 2, maxSlider);
                    if (choiceCount != manager.choiceCounts[i])
                    {
                        manager.choiceCounts[i] = choiceCount;
                        AutoResizeArrays(manager);
                    }

                    int labelOffset = (manager.optionOffsets != null && i < manager.optionOffsets.Length) ? manager.optionOffsets[i] : i * 6;
                    for (int c = 0; c < choiceCount; c++)
                    {
                        int labelIdx = labelOffset + c;
                        if (manager.optionLabels != null && labelIdx < manager.optionLabels.Length)
                        {
                            manager.optionLabels[labelIdx] = EditorGUILayout.TextField($"  選択肢 {c + 1} のラベル", manager.optionLabels[labelIdx]);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                else if (type == 3) // 選択式（グリッド）
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.HelpBox("各行につき1つ・各列につき1つの選択肢を必須選択するマトリクス表を表示します（列の重複不可）。", MessageType.None);

                    int curRowCount = (manager.gridRowCounts != null && i < manager.gridRowCounts.Length) ? Mathf.Clamp(manager.gridRowCounts[i], 1, 10) : 3;
                    int newRowCount = EditorGUILayout.IntSlider("行の数 (評価項目数)", curRowCount, 1, 10);
                    if (manager.gridRowCounts != null && i < manager.gridRowCounts.Length && newRowCount != manager.gridRowCounts[i])
                    {
                        manager.gridRowCounts[i] = newRowCount;
                        if (manager.gridColCounts != null && i < manager.gridColCounts.Length && manager.gridColCounts[i] < newRowCount)
                        {
                            manager.gridColCounts[i] = Mathf.Clamp(newRowCount, 2, 10);
                        }
                        AutoResizeArrays(manager);
                    }

                    int rowOffset = (manager.gridRowOffsets != null && i < manager.gridRowOffsets.Length) ? manager.gridRowOffsets[i] : 0;
                    for (int r = 0; r < newRowCount; r++)
                    {
                        int rIdx = rowOffset + r;
                        if (manager.gridRowLabels != null && rIdx < manager.gridRowLabels.Length)
                        {
                            manager.gridRowLabels[rIdx] = EditorGUILayout.TextField($"  行 {r + 1} の項目名", manager.gridRowLabels[rIdx]);
                        }
                    }

                    EditorGUILayout.Space(4);

                    int curColCount = (manager.gridColCounts != null && i < manager.gridColCounts.Length) ? Mathf.Clamp(manager.gridColCounts[i], 2, 10) : Mathf.Max(3, newRowCount);
                    int newColCount = EditorGUILayout.IntSlider("列の数 (選択肢数)", curColCount, 2, 10);
                    if (manager.gridColCounts != null && i < manager.gridColCounts.Length && newColCount != manager.gridColCounts[i])
                    {
                        manager.gridColCounts[i] = newColCount;
                        AutoResizeArrays(manager);
                    }

                    if (newColCount < newRowCount)
                    {
                        EditorGUILayout.HelpBox($"1列につき1回答に制限されているため、全 {newRowCount} 行に回答させるには「列の数」を {newRowCount} 以上に設定してください。", MessageType.Warning);
                    }

                    int colOffset = (manager.gridColOffsets != null && i < manager.gridColOffsets.Length) ? manager.gridColOffsets[i] : 0;
                    for (int c = 0; c < newColCount; c++)
                    {
                        int cIdx = colOffset + c;
                        if (manager.gridColLabels != null && cIdx < manager.gridColLabels.Length)
                        {
                            manager.gridColLabels[cIdx] = EditorGUILayout.TextField($"  列 {c + 1} の選択肢", manager.gridColLabels[cIdx]);
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
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(manager);
                if (manager.gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
            }
        }

        EditorGUILayout.Space(15);
        if (manager.surveyPanel != null && manager.containerGrid == null)
        {
            EditorGUILayout.HelpBox("💡 「選択式（グリッド）」用のUIコンテナがまだシーン内に生成されていません。\n下の「★ UIを自動生成・一括セットアップ」ボタンを押してUIを更新してください。", MessageType.Warning);
        }
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

        if (serializedObject.ApplyModifiedProperties())
        {
            SaveAndSync(manager);
        }
    }

    private void FetchAndApplyJsonInEditor(SurveyManager manager)
    {
        if (manager.surveyJsonUrl == null || string.IsNullOrEmpty(manager.surveyJsonUrl.Get()))
        {
            EditorUtility.DisplayDialog("URL未設定", "「アンケートJSONのURL (VRCUrl)」にURLを入力してください。", "OK");
            return;
        }

        string url = manager.surveyJsonUrl.Get().Trim();

        // 1. GistのRaw URLにコミットハッシュ(/raw/<40文字ハッシュ>/file.json)が含まれている場合、
        //    古いバージョンに固定されてしまうため自動的に最新URL(/raw/file.json)へ正規化する
        string normalizedUrl = NormalizeGistRawUrl(url);
        if (normalizedUrl != url)
        {
            SerializedProperty urlProp = serializedObject.FindProperty("surveyJsonUrl");
            if (urlProp != null)
            {
                SerializedProperty strProp = urlProp.FindPropertyRelative("url");
                if (strProp != null)
                {
                    strProp.stringValue = normalizedUrl;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            url = normalizedUrl;
            Debug.Log($"[SurveyManagerEditor] GistのURLから固定リビジョンIDを除去し、常に最新版を取得するURLに自動修正しました: {url}");
        }

        try
        {
            string jsonText = null;

            // 2. GitHub Gist の場合は Raw CDNキャッシュ(最大5分遅延)を回避するため、まず GitHub Gist API から直接最新コンテンツを取得
            jsonText = TryFetchLatestGistContentViaApi(url);

            // 3. Gist以外、またはAPIから取得できなかった場合はキャッシュバスター付きで通常取得
            if (string.IsNullOrEmpty(jsonText))
            {
                string sep = url.Contains("?") ? "&" : "?";
                string requestUrl = $"{url}{sep}_t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

                using (UnityWebRequest req = UnityWebRequest.Get(requestUrl))
                {
                    req.SetRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate");
                    req.SetRequestHeader("Pragma", "no-cache");
                    var op = req.SendWebRequest();
                    while (!op.isDone) { }

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        EditorUtility.DisplayDialog("JSON取得エラー", $"URLからの取得に失敗しました:\n{req.error}\n\nURL: {url}", "OK");
                        return;
                    }

                    jsonText = req.downloadHandler.text;
                }
            }

            Undo.RecordObject(manager, "Import Survey JSON from URL");
            if (manager.ApplySurveyJson(jsonText))
            {
                if (manager.consentNoticeDisplay != null)
                {
                    Undo.RecordObject(manager.consentNoticeDisplay, "Sync Consent Notice Display");
                    manager.consentNoticeDisplay.text = manager.consentNoticeText;
                    EditorUtility.SetDirty(manager.consentNoticeDisplay);
                }
                if (manager.dtcConsentTitleDisplay != null && !string.IsNullOrEmpty(manager.dtcConsentTitleText))
                {
                    Undo.RecordObject(manager.dtcConsentTitleDisplay, "Sync DTC Consent Title Display");
                    manager.dtcConsentTitleDisplay.text = manager.dtcConsentTitleText;
                    EditorUtility.SetDirty(manager.dtcConsentTitleDisplay);
                }
                if (manager.dtcConsentNoticeDisplay != null)
                {
                    Undo.RecordObject(manager.dtcConsentNoticeDisplay, "Sync DTC Consent Notice Display");
                    manager.dtcConsentNoticeDisplay.text = manager.dtcConsentNoticeText;
                    EditorUtility.SetDirty(manager.dtcConsentNoticeDisplay);
                }

                serializedObject.Update();
                SaveAndSync(manager);
                int count = manager.questionTexts != null ? manager.questionTexts.Length : 0;
                EditorUtility.DisplayDialog("JSON反映成功", $"オンラインJSONから全 {count} 問のアンケート設定と同意確認文面を即時取得し、Inspectorに反映しました！", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("JSON解析エラー", "JSONの解析に失敗しました。フォーマットを確認してください。", "OK");
            }
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("例外発生", $"JSON取得中にエラーが発生しました:\n{ex.Message}", "OK");
        }
    }

    /// <summary>
    /// https://gist.githubusercontent.com/{user}/{gistId}/raw/{commitHash}/{fileName}
    /// の {commitHash}/ を除去して常に最新リビジョンを指すURLに変換する
    /// </summary>
    private string NormalizeGistRawUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        var match = System.Text.RegularExpressions.Regex.Match(
            url,
            @"^(https://gist\.githubusercontent\.com/[^/]+/[0-9a-fA-F]+/raw/)[0-9a-fA-F]{20,40}/(.+)$"
        );
        if (match.Success)
        {
            return match.Groups[1].Value + match.Groups[2].Value;
        }
        return url;
    }

    /// <summary>
    /// GitHub Gist の Raw URL から Gist ID とファイル名を抽出し、
    /// CDNキャッシュを経由しない GitHub API (https://api.github.com/gists/{gistId}) から最新のファイル内容を直接取得する
    /// </summary>
    private string TryFetchLatestGistContentViaApi(string rawUrl)
    {
        if (string.IsNullOrEmpty(rawUrl)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(
            rawUrl,
            @"^https://gist\.githubusercontent\.com/[^/]+/([0-9a-fA-F]+)/raw/(?:[0-9a-fA-F]+/)?([^?#]+)"
        );
        if (!match.Success) return null;

        string gistId = match.Groups[1].Value;
        string fileName = Uri.UnescapeDataString(match.Groups[2].Value);
        string apiUrl = $"https://api.github.com/gists/{gistId}?_t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        try
        {
            using (UnityWebRequest req = UnityWebRequest.Get(apiUrl))
            {
                req.SetRequestHeader("User-Agent", "Unity-SurveyManagerEditor");
                req.SetRequestHeader("Accept", "application/vnd.github.v3+json");
                req.SetRequestHeader("Cache-Control", "no-cache");
                var op = req.SendWebRequest();
                while (!op.isDone) { }

                if (req.result != UnityWebRequest.Result.Success) return null;

                string apiJson = req.downloadHandler.text;
                if (VRC.SDK3.Data.VRCJson.TryDeserializeFromJson(apiJson, out VRC.SDK3.Data.DataToken rootToken) &&
                    rootToken.TokenType == VRC.SDK3.Data.TokenType.DataDictionary)
                {
                    var rootDict = rootToken.DataDictionary;
                    if (rootDict.TryGetValue("files", out VRC.SDK3.Data.DataToken filesToken) &&
                        filesToken.TokenType == VRC.SDK3.Data.TokenType.DataDictionary)
                    {
                        var filesDict = filesToken.DataDictionary;
                        if (filesDict.TryGetValue(fileName, out VRC.SDK3.Data.DataToken fileObjToken) &&
                            fileObjToken.TokenType == VRC.SDK3.Data.TokenType.DataDictionary)
                        {
                            var fileObj = fileObjToken.DataDictionary;
                            if (fileObj.TryGetValue("content", out VRC.SDK3.Data.DataToken contentToken) &&
                                contentToken.TokenType == VRC.SDK3.Data.TokenType.String)
                            {
                                Debug.Log($"[SurveyManagerEditor] GitHub Gist API からキャッシュなしで最新の '{fileName}' を直接取得しました。");
                                return contentToken.String;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SurveyManagerEditor] Gist API 経由の取得に失敗したため通常取得へフォールバックします: {e.Message}");
        }
        return null;
    }

    private void ExportCurrentSettingsToJsonFile(SurveyManager manager)
    {
        AutoResizeArrays(manager);

        string path = EditorUtility.SaveFilePanel("アンケートJSONの保存", Application.dataPath, "survey_questions.json", "json");
        if (string.IsNullOrEmpty(path)) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"consentTitleText\": {EscapeJsonString(manager.consentTitleText)},");
        sb.AppendLine($"  \"consentNoticeText\": {EscapeJsonString(manager.consentNoticeText)},");
        sb.AppendLine($"  \"dtcConsentTitleText\": {EscapeJsonString(manager.dtcConsentTitleText)},");
        sb.AppendLine($"  \"dtcConsentNoticeText\": {EscapeJsonString(manager.dtcConsentNoticeText)},");
        if (!string.IsNullOrEmpty(manager.choiceCategoryDescription))
            sb.AppendLine($"  \"choiceCategoryDescription\": {EscapeJsonString(manager.choiceCategoryDescription)},");
        if (!string.IsNullOrEmpty(manager.gridCategoryDescription))
            sb.AppendLine($"  \"gridCategoryDescription\": {EscapeJsonString(manager.gridCategoryDescription)},");
        if (!string.IsNullOrEmpty(manager.sliderCategoryDescription))
            sb.AppendLine($"  \"sliderCategoryDescription\": {EscapeJsonString(manager.sliderCategoryDescription)},");
        if (!string.IsNullOrEmpty(manager.textCategoryDescription))
            sb.AppendLine($"  \"textCategoryDescription\": {EscapeJsonString(manager.textCategoryDescription)},");
        sb.AppendLine("  \"questions\": [");

        int qCount = manager.questionTexts != null ? manager.questionTexts.Length : 0;
        for (int i = 0; i < qCount; i++)
        {
            string qText = manager.questionTexts[i] ?? "";
            int type = (manager.answerTypes != null && i < manager.answerTypes.Length) ? manager.answerTypes[i] : 0;
            sb.AppendLine("    {");
            sb.AppendLine($"      \"question\": {EscapeJsonString(qText)},");

            if (type == 0)
            {
                sb.AppendLine("      \"type\": 0,");
                int cCount = (manager.choiceCounts != null && i < manager.choiceCounts.Length) ? Mathf.Max(2, manager.choiceCounts[i]) : 2;
                int labelOffset = (manager.optionOffsets != null && i < manager.optionOffsets.Length) ? manager.optionOffsets[i] : i * 6;
                sb.Append("      \"options\": [");
                for (int c = 0; c < cCount; c++)
                {
                    int idx = labelOffset + c;
                    string opt = (manager.optionLabels != null && idx < manager.optionLabels.Length && manager.optionLabels[idx] != null)
                        ? manager.optionLabels[idx]
                        : $"選択肢 {c + 1}";
                    if (c > 0) sb.Append(", ");
                    sb.Append(EscapeJsonString(opt));
                }
                sb.AppendLine("]");
            }
            else if (type == 3)
            {
                sb.AppendLine("      \"type\": 3,");
                int rCount = (manager.gridRowCounts != null && i < manager.gridRowCounts.Length) ? Mathf.Max(1, manager.gridRowCounts[i]) : 2;
                int rOffset = (manager.gridRowOffsets != null && i < manager.gridRowOffsets.Length) ? manager.gridRowOffsets[i] : 0;
                sb.Append("      \"rows\": [");
                for (int r = 0; r < rCount; r++)
                {
                    int idx = rOffset + r;
                    string rLbl = (manager.gridRowLabels != null && idx < manager.gridRowLabels.Length && !string.IsNullOrEmpty(manager.gridRowLabels[idx]))
                        ? manager.gridRowLabels[idx]
                        : $"項目 {r + 1}";
                    if (r > 0) sb.Append(", ");
                    sb.Append(EscapeJsonString(rLbl));
                }
                sb.AppendLine("],");

                int cCount = (manager.gridColCounts != null && i < manager.gridColCounts.Length) ? Mathf.Max(2, manager.gridColCounts[i]) : 3;
                int cOffset = (manager.gridColOffsets != null && i < manager.gridColOffsets.Length) ? manager.gridColOffsets[i] : 0;
                sb.Append("      \"columns\": [");
                for (int c = 0; c < cCount; c++)
                {
                    int idx = cOffset + c;
                    string cLbl = (manager.gridColLabels != null && idx < manager.gridColLabels.Length && !string.IsNullOrEmpty(manager.gridColLabels[idx]))
                        ? manager.gridColLabels[idx]
                        : $"選択肢 {c + 1}";
                    if (c > 0) sb.Append(", ");
                    sb.Append(EscapeJsonString(cLbl));
                }
                sb.AppendLine("]");
            }
            else if (type == 1)
            {
                int maxVal = (manager.sliderMaxValues != null && i < manager.sliderMaxValues.Length) ? manager.sliderMaxValues[i] : 5;
                sb.AppendLine("      \"type\": 1,");
                sb.AppendLine($"      \"sliderMax\": {maxVal}");
            }
            else
            {
                sb.AppendLine("      \"type\": 2");
            }

            sb.Append("    }");
            if (i < qCount - 1) sb.AppendLine(",");
            else sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"[SurveyManagerEditor] アンケート設定JSONを出力しました: {path}");
        EditorUtility.DisplayDialog("JSON出力完了", $"現在のアンケート設定を以下のファイルに出力しました:\n{path}", "OK");
    }

    private string EscapeJsonString(string str)
    {
        if (str == null) return "\"\"";
        return "\"" + str
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n")
            .Replace("\t", "\\t") + "\"";
    }

    private void SaveAndSync(SurveyManager manager)
    {
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        try
        {
            UdonSharpEditorUtility.CopyProxyToUdon(manager);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SurveyManagerEditor] CopyProxyToUdon error: {e.Message}");
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        if (manager.gameObject.scene.IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        }
    }

    private string GetTypeLabel(int type)
    {
        switch (type)
        {
            case 0: return "[選択式]";
            case 1: return "[スライダー評価式]";
            case 2: return "[記述回答式]";
            case 3: return "[選択式（グリッド）]";
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
        if (oldPanel != null)
        {
            Transform oldConsent = oldPanel.Find("Container_Consent/ConsentNoticeDisplay");
            if (oldConsent != null)
            {
                Text oldConsentText = oldConsent.GetComponent<Text>();
                if (oldConsentText != null && !string.IsNullOrEmpty(oldConsentText.text) && oldConsentText.text != manager.consentNoticeText)
                {
                    Undo.RecordObject(manager, "Preserve Consent Text");
                    manager.consentNoticeText = oldConsentText.text;
                    EditorUtility.SetDirty(manager);
                }
            }
            DestroyImmediate(oldPanel.gameObject);
        }
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
            Transform oldDtcTextTr = oldDtcConsent.Find("DtcConsentNoticeDisplay");
            if (oldDtcTextTr == null) oldDtcTextTr = oldDtcConsent.Find("ConsentNoticeDisplay");
            if (oldDtcTextTr != null)
            {
                Text oldDtcText = oldDtcTextTr.GetComponent<Text>();
                if (oldDtcText != null && !string.IsNullOrEmpty(oldDtcText.text) && oldDtcText.text != manager.dtcConsentNoticeText)
                {
                    Undo.RecordObject(manager, "Preserve DTC Consent Text");
                    manager.dtcConsentNoticeText = oldDtcText.text;
                    EditorUtility.SetDirty(manager);
                }
            }
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
        manager.choiceButtons = new GameObject[24];
        manager.choiceButtonTexts = new Text[24];
        manager.containerRating = null;
        manager.ratingSlider = null;
        manager.ratingValueText = null;
        manager.containerTextInput = null;
        manager.inputFieldArea = null;
        manager.containerGrid = null;
        manager.gridScrollRect = null;
        manager.gridScrollContent = null;
        manager.gridCornerHeaderText = null;
        manager.gridColHeaderTexts = new Text[10];
        manager.gridRowObjects = new GameObject[20];
        manager.gridRowBgImages = new Image[20];
        manager.gridRowLabelTexts = new Text[20];
        manager.gridCellButtons = new GameObject[120];
        manager.gridCellButtonImages = new Image[120];
        manager.gridCellButtonTexts = new Text[120];
        manager.gridSubmitButton = null;
        manager.gridSubmitButtonImage = null;
        manager.gridSubmitButtonText = null;
        manager.containerConsent = null;
        manager.consentNoticeDisplay = null;
        manager.consentAgreeButton = null;
        manager.consentDisagreeButton = null;
        manager.containerDtcConsent = null;
        manager.dtcConsentTitleDisplay = null;
        manager.dtcConsentNoticeDisplay = null;
        manager.dtcConsentAgreeButton = null;
        manager.dtcConsentDisagreeButton = null;
        manager.surveyPanel = null;
        manager.categoryDescriptionText = null;
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

        // TitleText, CategoryDescriptionText & QuestionText
        manager.titleText = CreateOrGetText(panelTr, "TitleText", $"質問 (1 / {qCount})", font, 36, TextAnchor.MiddleCenter, new Vector2(0, 224), new Vector2(900, 56));
        manager.categoryDescriptionText = CreateOrGetText(panelTr, "CategoryDescriptionText", "", font, 21, TextAnchor.MiddleCenter, new Vector2(0, 196), new Vector2(900, 56));
        manager.categoryDescriptionText.color = new Color(0.68f, 0.85f, 1.0f, 1f);
        manager.categoryDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        manager.categoryDescriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        manager.categoryDescriptionText.gameObject.SetActive(false);
        manager.questionText = CreateOrGetText(panelTr, "QuestionText", "質問文がここに入ります", font, 32, TextAnchor.MiddleCenter, new Vector2(0, 130), new Vector2(900, 100));

        // Container_Choice (選択式)
        GameObject cObj = new GameObject("Container_Choice", typeof(RectTransform));
        cObj.transform.SetParent(panelTr, false);
        Transform cTr = cObj.transform;
        RectTransform cRt = cObj.GetComponent<RectTransform>();
        cRt.anchoredPosition = new Vector2(0, -70);
        cRt.sizeDelta = new Vector2(920, 300);
        manager.containerChoice = cObj;

        int maxChoiceButtons = 24;
        manager.choiceButtons = new GameObject[maxChoiceButtons];
        manager.choiceButtonTexts = new Text[maxChoiceButtons];
        for (int i = 0; i < maxChoiceButtons; i++)
        {
            int col = i % 2;
            int row = i / 2;
            Vector2 defaultPos = new Vector2(col == 0 ? -220f : 220f, 70f - row * 70f);
            string eventName = $"OnChoiceOption{i}";
            GameObject bObj = CreateButtonWithEvent(cTr, $"Btn_Choice{i}", $"選択肢 {i + 1}", font, defaultPos, new Vector2(400, 60), 24, manager, eventName);
            if (i >= 6) bObj.SetActive(false);
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

        // Container_Grid (選択式（グリッド）)
        GameObject gridObj = new GameObject("Container_Grid", typeof(RectTransform));
        gridObj.transform.SetParent(panelTr, false);
        Transform gridTr = gridObj.transform;
        RectTransform gridRt = gridObj.GetComponent<RectTransform>();
        gridRt.anchoredPosition = new Vector2(0, -85);
        gridRt.sizeDelta = new Vector2(940, 410);
        manager.containerGrid = gridObj;

        // 上部: 列見出しバー
        GameObject headerBarObj = new GameObject("GridHeaderBar", typeof(RectTransform), typeof(Image));
        headerBarObj.transform.SetParent(gridTr, false);
        RectTransform hbRt = headerBarObj.GetComponent<RectTransform>();
        hbRt.anchoredPosition = new Vector2(0, 178);
        hbRt.sizeDelta = new Vector2(900, 46);
        Image hbImg = headerBarObj.GetComponent<Image>();
        hbImg.color = new Color(0.18f, 0.22f, 0.32f, 0.92f);
        hbImg.raycastTarget = false;

        Text cornerTxt = CreateOrGetText(headerBarObj.transform, "CornerHeaderText", "項目 \\ 選択肢", font, 17, TextAnchor.MiddleLeft, new Vector2(-300, 0), new Vector2(280, 42));
        cornerTxt.color = new Color(0.72f, 0.82f, 0.94f, 1f);
        manager.gridCornerHeaderText = cornerTxt;

        int maxGridCols = 10;
        manager.gridColHeaderTexts = new Text[maxGridCols];
        for (int c = 0; c < maxGridCols; c++)
        {
            Text chTxt = CreateOrGetText(headerBarObj.transform, $"ColHeader{c}", $"選択肢 {c + 1}", font, 18, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(100, 44));
            chTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            chTxt.verticalOverflow = VerticalWrapMode.Truncate;
            chTxt.color = new Color(0.92f, 0.96f, 1f, 1f);
            if (c >= 3) chTxt.gameObject.SetActive(false);
            manager.gridColHeaderTexts[c] = chTxt;
        }

        // 中央: スクロール対応マトリクス領域
        GameObject gScrollObj = new GameObject("GridScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        gScrollObj.transform.SetParent(gridTr, false);
        RectTransform gScrollRt = gScrollObj.GetComponent<RectTransform>();
        gScrollRt.anchoredPosition = new Vector2(0, 8);
        gScrollRt.sizeDelta = new Vector2(900, 284);
        Image gScrollBg = gScrollObj.GetComponent<Image>();
        gScrollBg.color = new Color(0.08f, 0.09f, 0.13f, 0.65f);
        gScrollBg.raycastTarget = true;

        GameObject gViewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        gViewportObj.transform.SetParent(gScrollObj.transform, false);
        RectTransform gViewportRt = gViewportObj.GetComponent<RectTransform>();
        gViewportRt.anchorMin = Vector2.zero;
        gViewportRt.anchorMax = Vector2.one;
        gViewportRt.offsetMin = Vector2.zero;
        gViewportRt.offsetMax = Vector2.zero;
        Image gViewportImg = gViewportObj.GetComponent<Image>();
        gViewportImg.color = new Color(0f, 0f, 0f, 0.01f);
        gViewportImg.raycastTarget = true;

        GameObject gContentObj = new GameObject("Content", typeof(RectTransform));
        gContentObj.transform.SetParent(gViewportObj.transform, false);
        RectTransform gContentRt = gContentObj.GetComponent<RectTransform>();
        gContentRt.anchorMin = new Vector2(0.5f, 1f);
        gContentRt.anchorMax = new Vector2(0.5f, 1f);
        gContentRt.pivot = new Vector2(0.5f, 1f);
        gContentRt.anchoredPosition = Vector2.zero;
        gContentRt.sizeDelta = new Vector2(900, 284);

        GameObject vScrollbarObj = DefaultControls.CreateScrollbar(res);
        vScrollbarObj.name = "Scrollbar Vertical";
        vScrollbarObj.transform.SetParent(gScrollObj.transform, false);
        Scrollbar vSb = vScrollbarObj.GetComponent<Scrollbar>();
        vSb.SetDirection(Scrollbar.Direction.BottomToTop, true);
        RectTransform vSbRt = vScrollbarObj.GetComponent<RectTransform>();
        vSbRt.anchorMin = new Vector2(1f, 0f);
        vSbRt.anchorMax = new Vector2(1f, 1f);
        vSbRt.pivot = new Vector2(1f, 0.5f);
        vSbRt.anchoredPosition = new Vector2(-2f, 0f);
        vSbRt.sizeDelta = new Vector2(14f, -8f);
        Image vSbBg = vScrollbarObj.GetComponent<Image>();
        if (vSbBg != null) vSbBg.color = new Color(0.14f, 0.16f, 0.22f, 0.85f);
        if (vSb.targetGraphic != null) vSb.targetGraphic.color = new Color(0.45f, 0.72f, 0.95f, 0.9f);

        ScrollRect gSr = gScrollObj.GetComponent<ScrollRect>();
        gSr.content = gContentRt;
        gSr.viewport = gViewportRt;
        gSr.horizontal = false;
        gSr.vertical = true;
        gSr.movementType = ScrollRect.MovementType.Clamped;
        gSr.scrollSensitivity = 25f;
        gSr.verticalScrollbar = vSb;
        gSr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        manager.gridScrollRect = gSr;
        manager.gridScrollContent = gContentRt;

        int maxGridRows = 20;
        manager.gridRowObjects = new GameObject[maxGridRows];
        manager.gridRowBgImages = new Image[maxGridRows];
        manager.gridRowLabelTexts = new Text[maxGridRows];
        for (int r = 0; r < maxGridRows; r++)
        {
            GameObject rowObj = new GameObject($"GridRow{r}", typeof(RectTransform), typeof(Image));
            rowObj.transform.SetParent(gContentRt, false);
            RectTransform rowRt = rowObj.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = new Vector2(-10f, -38f - r * 76f);
            rowRt.sizeDelta = new Vector2(880f, 68f);

            Image rowBg = rowObj.GetComponent<Image>();
            rowBg.color = (r % 2 == 0) ? new Color(1f, 1f, 1f, 0.05f) : new Color(1f, 1f, 1f, 0.10f);
            rowBg.raycastTarget = false;

            Text rLabel = CreateOrGetText(rowObj.transform, "RowLabel", $"項目 {r + 1}", font, 20, TextAnchor.MiddleLeft, new Vector2(12f, 0f), new Vector2(280f, 60f));
            rLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            rLabel.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform rlRt = rLabel.GetComponent<RectTransform>();
            rlRt.anchorMin = new Vector2(0f, 0.5f);
            rlRt.anchorMax = new Vector2(0f, 0.5f);
            rlRt.pivot = new Vector2(0f, 0.5f);
            rlRt.anchoredPosition = new Vector2(12f, 0f);

            if (r >= 3) rowObj.SetActive(false);
            manager.gridRowObjects[r] = rowObj;
            manager.gridRowBgImages[r] = rowBg;
            manager.gridRowLabelTexts[r] = rLabel;
        }

        int maxGridCells = 120;
        manager.gridCellButtons = new GameObject[maxGridCells];
        manager.gridCellButtonImages = new Image[maxGridCells];
        manager.gridCellButtonTexts = new Text[maxGridCells];
        for (int k = 0; k < maxGridCells; k++)
        {
            GameObject cellBtn = CreateButtonWithEvent(gContentRt, $"Btn_GridCell{k}", "○", font, Vector2.zero, new Vector2(72f, 44f), 24, manager, $"OnGridCell{k}");
            RectTransform cbRt = cellBtn.GetComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(0.5f, 1f);
            cbRt.anchorMax = new Vector2(0.5f, 1f);
            cbRt.pivot = new Vector2(0.5f, 0.5f);

            Image cbImg = cellBtn.GetComponent<Image>();
            if (cbImg != null) cbImg.color = new Color(0.22f, 0.25f, 0.34f, 1f);
            Text cbTxt = cellBtn.GetComponentInChildren<Text>();
            if (cbTxt != null) cbTxt.color = new Color(0.78f, 0.82f, 0.88f, 1f);

            if (k >= 9) cellBtn.SetActive(false);
            manager.gridCellButtons[k] = cellBtn;
            manager.gridCellButtonImages[k] = cbImg;
            manager.gridCellButtonTexts[k] = cbTxt;
        }

        GameObject submitGridBtnObj = CreateButtonWithEvent(gridTr, "Btn_SubmitGrid", "各行・各列1つずつ選択してください", font, new Vector2(0, -172), new Vector2(480, 56), 21, manager, "OnSubmitGrid");
        manager.gridSubmitButton = submitGridBtnObj.GetComponent<Button>();
        manager.gridSubmitButtonImage = submitGridBtnObj.GetComponent<Image>();
        manager.gridSubmitButtonText = submitGridBtnObj.GetComponentInChildren<Text>();
        if (manager.gridSubmitButtonImage != null) manager.gridSubmitButtonImage.color = new Color(0.32f, 0.34f, 0.40f, 1f);
        if (manager.gridSubmitButtonText != null) manager.gridSubmitButtonText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        if (manager.gridSubmitButton != null) manager.gridSubmitButton.interactable = false;

        gridObj.SetActive(false);

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

        string dtcTitleVal = !string.IsNullOrEmpty(manager.dtcConsentTitleText) ? manager.dtcConsentTitleText : "【位置・視線データ収集に関する同意確認】";
        manager.dtcConsentTitleDisplay = CreateOrGetText(dtcConsentTr, "DtcTitleText", dtcTitleVal, font, 36, TextAnchor.MiddleCenter, new Vector2(0, 210), new Vector2(920, 60));

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

        // 各質問の開始オフセットを計算し、既存のラベルデータを保持しつつ optionLabels を再構築
        int[] oldOffsets = manager.optionOffsets;
        string[] oldLabels = manager.optionLabels;

        int[] newOffsets = new int[qCount];
        int totalNeeded = 0;
        for (int i = 0; i < qCount; i++)
        {
            newOffsets[i] = totalNeeded;
            int slotCount = Mathf.Max(6, manager.choiceCounts[i]);
            totalNeeded += slotCount;
        }

        string[] newLabels = new string[totalNeeded];
        for (int i = 0; i < qCount; i++)
        {
            int slotCount = Mathf.Max(6, manager.choiceCounts[i]);
            int oldBase = (oldOffsets != null && i < oldOffsets.Length) ? oldOffsets[i] : (i * 6);
            int nextOldBase = (oldOffsets != null && i + 1 < oldOffsets.Length) ? oldOffsets[i + 1] : ((i + 1) * 6);
            int oldSlotCount = Mathf.Max(0, nextOldBase - oldBase);

            for (int c = 0; c < slotCount; c++)
            {
                int newIdx = newOffsets[i] + c;
                int oldIdx = oldBase + c;
                if (oldLabels != null && c < oldSlotCount && oldIdx >= 0 && oldIdx < oldLabels.Length)
                {
                    newLabels[newIdx] = oldLabels[oldIdx];
                }
            }
        }

        manager.optionOffsets = newOffsets;
        manager.optionLabels = newLabels;

        // 選択式（グリッド）用の行・列配列をリサイズ＆既存ラベル保持
        int oldGridRowLen = (manager.gridRowCounts != null) ? manager.gridRowCounts.Length : 0;
        Array.Resize(ref manager.gridRowCounts, qCount);
        for (int i = oldGridRowLen; i < qCount; i++)
        {
            if (manager.answerTypes[i] == 3 && manager.gridRowCounts[i] < 1) manager.gridRowCounts[i] = 3;
        }

        int oldGridColLen = (manager.gridColCounts != null) ? manager.gridColCounts.Length : 0;
        Array.Resize(ref manager.gridColCounts, qCount);
        for (int i = oldGridColLen; i < qCount; i++)
        {
            if (manager.answerTypes[i] == 3 && manager.gridColCounts[i] < 2) manager.gridColCounts[i] = 3;
        }

        int[] oldRowOffsets = manager.gridRowOffsets;
        string[] oldRowLabels = manager.gridRowLabels;
        int[] newRowOffsets = new int[qCount];
        int totalRowsNeeded = 0;
        for (int i = 0; i < qCount; i++)
        {
            newRowOffsets[i] = totalRowsNeeded;
            int rSlots = Mathf.Max(4, manager.gridRowCounts[i]);
            totalRowsNeeded += rSlots;
        }
        string[] newRowLabels = new string[totalRowsNeeded];
        for (int i = 0; i < qCount; i++)
        {
            int rSlots = Mathf.Max(4, manager.gridRowCounts[i]);
            int oldBase = (oldRowOffsets != null && i < oldRowOffsets.Length) ? oldRowOffsets[i] : (i * 4);
            int nextOldBase = (oldRowOffsets != null && i + 1 < oldRowOffsets.Length)
                ? oldRowOffsets[i + 1]
                : (oldRowLabels != null ? oldRowLabels.Length : 0);
            int oldSlotCount = Mathf.Max(0, nextOldBase - oldBase);
            for (int r = 0; r < rSlots; r++)
            {
                int newIdx = newRowOffsets[i] + r;
                int oldIdx = oldBase + r;
                if (oldRowLabels != null && r < oldSlotCount && oldIdx >= 0 && oldIdx < oldRowLabels.Length)
                {
                    newRowLabels[newIdx] = oldRowLabels[oldIdx];
                }
            }
        }
        manager.gridRowOffsets = newRowOffsets;
        manager.gridRowLabels = newRowLabels;

        int[] oldColOffsets = manager.gridColOffsets;
        string[] oldColLabels = manager.gridColLabels;
        int[] newColOffsets = new int[qCount];
        int totalColsNeeded = 0;
        for (int i = 0; i < qCount; i++)
        {
            newColOffsets[i] = totalColsNeeded;
            int cSlots = Mathf.Max(6, manager.gridColCounts[i]);
            totalColsNeeded += cSlots;
        }
        string[] newColLabels = new string[totalColsNeeded];
        for (int i = 0; i < qCount; i++)
        {
            int cSlots = Mathf.Max(6, manager.gridColCounts[i]);
            int oldBase = (oldColOffsets != null && i < oldColOffsets.Length) ? oldColOffsets[i] : (i * 6);
            int nextOldBase = (oldColOffsets != null && i + 1 < oldColOffsets.Length)
                ? oldColOffsets[i + 1]
                : (oldColLabels != null ? oldColLabels.Length : 0);
            int oldSlotCount = Mathf.Max(0, nextOldBase - oldBase);
            for (int c = 0; c < cSlots; c++)
            {
                int newIdx = newColOffsets[i] + c;
                int oldIdx = oldBase + c;
                if (oldColLabels != null && c < oldSlotCount && oldIdx >= 0 && oldIdx < oldColLabels.Length)
                {
                    newColLabels[newIdx] = oldColLabels[oldIdx];
                }
            }
        }
        manager.gridColOffsets = newColOffsets;
        manager.gridColLabels = newColLabels;
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
