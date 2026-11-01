using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.StringLoading;
using VRC.SDK3.Data;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class SurveyManager : UdonSharpBehaviour
{
    [Header("オンラインJSON設定 (VRCStringDownloader)")]
    [Tooltip("有効にすると、ワールド開始時に指定URLのJSONからアンケート質問データを読み込んで反映します")]
    public bool useOnlineSurveyJson = true;

    [Tooltip("アンケートデータJSONのURL (GitHub GistのRaw URL等)")]
    public VRCUrl surveyJsonUrl;

    [Header("アンケートデータ設定 (Inspector設定 / オフライン・フォールバック用)")]
    [Tooltip("質問文のリスト")]
    public string[] questionTexts = new string[] {
        "イベントは楽しめましたか？",
        "使用しているプラットフォームは？",
        "このワールドの満足度を教えてください",
        "ご意見・ご感想を自由にご記述ください"
    };

    [Tooltip("回答形式のタイプ (0: 選択式, 1: スライダー評価式, 2: 記述回答式, 3: 選択式（グリッド）)")]
    public int[] answerTypes = new int[] { 0, 0, 1, 2 };

    [Tooltip("選択式の場合の択数 (例: 2択なら2, 3択なら3, 4択なら4)")]
    public int[] choiceCounts = new int[] { 2, 4, 0, 0 };

    [Tooltip("選択式の各質問の開始インデックス (自動計算・任意択数対応)")]
    public int[] optionOffsets;

    [Tooltip("選択式のラベル名リスト")]
    public string[] optionLabels = new string[] {
        "いいえ", "はい", "", "", "", "",
        "PC (VR)", "PC (Desktop)", "Quest", "iOS", "", "",
        "", "", "", "", "", "",
        "", "", "", "", "", ""
    };

    [Tooltip("スライダー評価式の場合の最大評価値 (例: 5なら1~5段階, 10なら1~10段階)")]
    public int[] sliderMaxValues = new int[] { 5, 5, 5, 5 };

    [Tooltip("選択式（グリッド）の場合の行数（評価項目数）")]
    public int[] gridRowCounts = new int[] { 0, 0, 0, 0 };

    [Tooltip("選択式（グリッド）の各質問の行ラベル開始インデックス")]
    public int[] gridRowOffsets;

    [Tooltip("選択式（グリッド）の行ラベル名リスト")]
    public string[] gridRowLabels;

    [Tooltip("選択式（グリッド）の場合の列数（選択肢数）")]
    public int[] gridColCounts = new int[] { 0, 0, 0, 0 };

    [Tooltip("選択式（グリッド）の各質問の列ラベル開始インデックス")]
    public int[] gridColOffsets;

    [Tooltip("選択式（グリッド）の列ラベル名リスト")]
    public string[] gridColLabels;

    [Header("カテゴリ別の共通説明文 (VRChat画面上部に表示)")]
    [Tooltip("選択式 (type=0) の質問画面上部に表示する説明文")]
    public string choiceCategoryDescription = "";

    [Tooltip("選択式（グリッド） (type=3) の質問画面上部に表示する説明文")]
    public string gridCategoryDescription = "";

    [Tooltip("スライダー評価式 (type=1) の質問画面上部に表示する説明文")]
    public string sliderCategoryDescription = "";

    [Tooltip("記述回答式 (type=2) の質問画面上部に表示する説明文")]
    public string textCategoryDescription = "";

    [Header("外部 UdonBehaviour 参照 & 変数設定")]
    [Tooltip("RecordMasterNames 及び JsonNamesString が格納されている外部 UdonBehaviour")]
    public UdonBehaviour targetUdonBehaviour;

    [Tooltip("アンケート出現対象の string[] 変数名 (デフォルト: JsonNamesString)")]
    public string jsonNamesVariableName = "JsonNamesString";

    [Tooltip("RecordMaster(開始権限&ログ出力) の string[] 変数名 (デフォルト: RecordMasterNames)")]
    public string recordMasterVariableName = "RecordMasterNames";

    [Tooltip("直接指定する場合の対象ユーザー名リスト (DisplayName)")]
    public string[] allowedUserNames;

    [Header("UI要素参照")]
    public Text titleText;
    public Text categoryDescriptionText;
    public Text questionText;

    [Header("回答コンテナ")]
    public GameObject containerChoice;
    public GameObject[] choiceButtons;
    public Text[] choiceButtonTexts;

    public GameObject containerRating;
    public Slider ratingSlider;
    public Text ratingValueText;

    public GameObject containerTextInput;
    public InputField inputFieldArea;

    [Header("選択式（グリッド）コンテナ UI参照")]
    public GameObject containerGrid;
    public ScrollRect gridScrollRect;
    public RectTransform gridScrollContent;
    public Text gridCornerHeaderText;
    public Text[] gridColHeaderTexts;
    public GameObject[] gridRowObjects;
    public Image[] gridRowBgImages;
    public Text[] gridRowLabelTexts;
    public GameObject[] gridCellButtons;
    public Image[] gridCellButtonImages;
    public Text[] gridCellButtonTexts;
    public Button gridSubmitButton;
    public Image gridSubmitButtonImage;
    public Text gridSubmitButtonText;

    [Header("アンケート同意確認画面 UIコンテナ参照")]
    public GameObject containerConsent;
    public Text consentNoticeDisplay;
    public Button consentAgreeButton;
    public Button consentDisagreeButton;
    public string consentTitleText = "アンケート参加の同意確認";
    [TextArea(3, 8)]
    public string consentNoticeText = "【アンケート参加・データ収集に関する同意確認】\n\n本アンケートの回答データは統計・報告目的のみに使用されます。\n内容に同意いただける方は「同意する」を押してアンケートへお進みください。\n「同意しない」を押した場合は、アンケートに回答せず終了します。";

    [Header("DTC (位置・視線データ収集) 同意確認画面 UI参照")]
    public GameObject containerDtcConsent;
    public Text dtcConsentTitleDisplay;
    public Text dtcConsentNoticeDisplay;
    public Button dtcConsentAgreeButton;
    public Button dtcConsentDisagreeButton;
    public string dtcConsentTitleText = "【位置・視線データ収集に関する同意確認】";
    [TextArea(3, 8)]
    public string dtcConsentNoticeText = "【位置・視線データ収集に関する同意確認】\n\n本ワールドでは研究・統計分析を目的として、プレイヤーの位置および頭部姿勢データを記録する機能があります。\nデータ収集にご同意いただける方は「同意する」を押してください。";

    [Header("ナビゲーション & 主催者用コントロール")]
    public GameObject surveyPanel;
    public GameObject resultPanel;
    public Text resultMessageText;

    [Tooltip("RecordMaster専用のアンケート開始コントロールパネル")]
    public GameObject masterControlPanel;
    [Tooltip("RecordMaster専用のリアルタイム回答状況表示テキスト")]
    public Text masterStatusText;
    [Tooltip("RecordMaster同一UIパネル内のメッセージ/警告表示テキスト")]
    public Text masterWarningText;
    [Tooltip("一斉開始ボタン")]
    public Button startSurveyButton;
    [Tooltip("アンケートリセットボタン")]
    public Button resetSurveyButton;
    [Tooltip("一般回答者用: アンケート再表示ボタン")]
    public Button reopenSurveyButton;
    [Tooltip("配信対象モード切替ボタン (トグル用)")]
    public Button toggleTargetModeButton;
    [Tooltip("配信対象ラジオボタン: リスト限定モード")]
    public Button targetModeListButton;
    [Tooltip("配信対象ラジオボタン: ワールド全員モード")]
    public Button targetModeAllButton;

    [Header("配信対象モードの初期設定 (Inspector設定)")]
    [Tooltip("ワールド開始時の初期配信対象モード (0: リスト限定 [JsonNames], 1: ワールド全員 [要同意])")]
    public int initialTargetMode = 0; // 0: リスト限定, 1: ワールド全員

    [Header("出題順ランダム並び替え設定")]
    [Tooltip("有効にすると、カテゴリ順(選択式→選択式（グリッド）→スライダー評価→自由記述)を維持したまま、各カテゴリ内の質問順を一括でランダムに並び替えて出題します")]
    public bool shuffleQuestionsWithinCategory = false;

    // ネットワーク同期変数
    [UdonSynced] private string syncedAnsweredPlayers = ""; 
    [UdonSynced] private string syncedLatestResultLog = ""; 
    [UdonSynced] private bool syncedSurveyStarted = false; 
    [UdonSynced] private bool syncedTargetAllMode = false; // false: リスト限定 (JsonNames), true: ワールド全員 (同意画面あり)

    // DTC用 ネットワーク同期変数
    [UdonSynced] private bool syncedDtcTargetAllMode = false; // false: リスト限定, true: ワールド全員 (ワールド全員ボタンに連動)
    [UdonSynced] private string syncedDtcConsentedPlayers = ""; // DTC同意したプレイヤー名 (,Player1,Player2,)
    [UdonSynced] private string syncedDtcDisagreedPlayers = ""; // DTC辞退したプレイヤー名

    // 内部状態変数
    private int currentIndex = 0;
    private int[] questionOrder; // 出題順の質問インデックス配列 (選択式 -> 選択式（グリッド） -> スライダー -> 自由記述)
    private int[] userAnswers;
    private string[] userTextAnswers;
    private int currentGridRowCount = 0;
    private int currentGridColCount = 0;
    private int[] currentGridRowSelections;
    private bool hasAnswered = false;
    private string lastProcessedLog = "";

    // 主催者(RecordMaster)ローカル専用: 一度確定した回答完了・辞退プレイヤー名の永続蓄積リスト (,name1,name2,)
    // ネットワーク同期ではなくマスターのメモリ上に保持されるため、他クライアントの同期変数が巻き戻っても影響を受けない
    private string masterRecordedAnsweredPlayers = "";

    // シリアライズ送信待機フラグ (回答完了・同意辞退のシリアライズを確実に成功させるための制御)
    private bool pendingSerialization = false;
    private int serializationRetryCount = 0;

    // ローカルでのDTC同意・辞退回答済みフラグ (アンケート一斉開始時などの不要な再出現を完全に防止)
    private bool hasDtcResponded = false;

    // DTC同意・辞退プレイヤーのローカル蓄積リスト（オーナー権移譲時の同期巻き戻り防止用）
    private string localRecordedDtcConsentedPlayers = "";
    private string localRecordedDtcDisagreedPlayers = "";

    // 外部 UdonBehaviour (MVP_DTC_Online) の元の JsonNamesString キャッシュと上書き追跡用
    private string[] cachedOriginalJsonNames = null;
    private string[] lastAppliedDtcNamesArray = null;
    private string lastBuiltDtcConsentedSource = null;
    private int lastAppliedModeState = -1; // -1: 未適用, 0: リスト限定, 1: ワールド全員
    private string[] lastBuiltListModePlayerNames = null;
    private string[] lastBuiltListModeMasterNames = null;
    private string[] lastObservedMasterNamesForEvents = null;
    private string[] lastObservedPlayerNamesForEvents = null;
    private bool hasRevivedDtcLoopEvent = false;

    // オンラインJSON読み込み状態管理
    private bool isOnlineJsonLoaded = false;
    private string onlineJsonStatusMessage = "";
    private int jsonLoadRetryCount = 0;

    void Start()
    {
        EnsureUIReferences();

        bool isAllInitial = (initialTargetMode == 1);
        syncedTargetAllMode = isAllInitial;
        syncedDtcTargetAllMode = isAllInitial;

        bool isOwnerOrLocal = (Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid() || Networking.IsOwner(gameObject));
        if (isOwnerOrLocal)
        {
            RequestSerialization();
        }

        // ワールド開始直後から外部 UdonBehaviour (MVP_DTC_Online) の対象リストをモードに合わせて即時制御
        SyncDtcTargetsToUdonBehaviour();

        // ワールド開始時にオンラインJSONからアンケート設定を読み込む
        // ※ MVP_DTC_Online 側の VRCStringDownloader.LoadUrl (VRCanswers.json) と
        //   VRChat の 5秒レート制限キューで競合しないよう、Hierarchy 内の Prefab 配置順に関わらず
        //   アンケート設定JSONの初回読み込みを 5.5 秒後に遅延実行する
        jsonLoadRetryCount = 0;
        if (useOnlineSurveyJson && surveyJsonUrl != null && !string.IsNullOrEmpty(surveyJsonUrl.Get()))
        {
            onlineJsonStatusMessage = "<color=#70D6FF>⏳ オンラインJSON読込待機中...</color>";
            UpdateMasterStatusText();
            SendCustomEventDelayedSeconds(nameof(LoadSurveyJsonFromUrl), 5.5f);
        }
        else
        {
            LoadSurveyJsonFromUrl();
        }

        CheckIfAlreadyAnswered();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer != null && localPlayer.IsValid())
        {
            if (CheckIfDtcResponded(localPlayer.displayName))
            {
                hasDtcResponded = true;
            }
        }

        if (surveyPanel != null) surveyPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (containerDtcConsent != null) containerDtcConsent.SetActive(false);

        if (masterControlPanel != null)
        {
            masterControlPanel.SetActive(false);
        }

        Image selfImage = GetComponent<Image>();
        if (selfImage != null)
        {
            selfImage.enabled = false;
        }

        DisablePhysicalCollisions();
        SendCustomEventDelayedFrames(nameof(DisablePhysicalCollisions), 1);
        SendCustomEventDelayedSeconds(nameof(DisablePhysicalCollisions), 0.5f);
        SendCustomEventDelayedSeconds(nameof(DisablePhysicalCollisions), 1.5f);
        SendCustomEventDelayedSeconds(nameof(DisablePhysicalCollisions), 3.0f);

        RefreshMasterPanelVisibility();
        CheckAndRestoreSurveyForLocalUser();
        CheckAndRestoreDtcConsentForLocalUser();

        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 0.2f);
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 1.0f);
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 3.0f);
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 5.0f);
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 6.0f);
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 10.0f);
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 15.0f);

        SendCustomEventDelayedSeconds(nameof(EnsureDtcLoopEventRunning), 1.8f);
        SendCustomEventDelayedSeconds(nameof(EnsureDtcLoopEventRunning), 3.5f);
        SendCustomEventDelayedSeconds(nameof(EnsureDtcLoopEventRunning), 6.0f);
        SendCustomEventDelayedSeconds(nameof(EnsureDtcLoopEventRunning), 10.5f);

        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreSurveyForLocalUser), 0.5f);
        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreSurveyForLocalUser), 1.5f);
        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreSurveyForLocalUser), 3.5f);
        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreSurveyForLocalUser), 6.0f);

        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreDtcConsentForLocalUser), 0.6f);
        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreDtcConsentForLocalUser), 1.6f);
        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreDtcConsentForLocalUser), 6.0f);
    }

    void Update()
    {
        SyncDtcTargetsToUdonBehaviour();
    }

    public override void PostLateUpdate()
    {
        SyncDtcTargetsToUdonBehaviour();
    }

    // ==========================================
    // オンラインJSON読み込み & パース処理 (VRCStringDownloader / VRCJson)
    // ==========================================

    /// <summary>
    /// 指定された surveyJsonUrl からアンケート質問データのJSONを非同期ダウンロードする
    /// </summary>
    public void LoadSurveyJsonFromUrl()
    {
        if (!useOnlineSurveyJson)
        {
            int fallbackLen = (questionTexts != null) ? questionTexts.Length : 0;
            onlineJsonStatusMessage = $"💻 エディタ設定を使用中 (全{fallbackLen}問)";
            UpdateMasterStatusText();
            return;
        }

        if (surveyJsonUrl == null || string.IsNullOrEmpty(surveyJsonUrl.Get()))
        {
            int fallbackLen = (questionTexts != null) ? questionTexts.Length : 0;
            onlineJsonStatusMessage = $"<color=#FFB300>⚠️ URL未設定 (エディタ設定 全{fallbackLen}問を使用中)</color>";
            UpdateMasterStatusText();
            return;
        }

        onlineJsonStatusMessage = "<color=#70D6FF>⏳ オンラインJSON読込中...</color>";
        UpdateMasterStatusText();
        VRCStringDownloader.LoadUrl(surveyJsonUrl, (IUdonEventReceiver)this);
    }

    public void RetryLoadSurveyJson()
    {
        if (!isOnlineJsonLoaded && useOnlineSurveyJson)
        {
            LoadSurveyJsonFromUrl();
        }
    }

    /// <summary>
    /// RecordMaster専用: 全員のクライアントにオンラインJSONの再読み込みを指示する
    /// </summary>
    public void OnReloadSurveyJsonButton()
    {
        EnsureUIReferences();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string localName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        if (!CheckIfRecordMaster(localName))
        {
            Debug.LogWarning("[SurveyManager] JSON再読込の指示権限がありません。");
            return;
        }

        if (masterWarningText != null)
        {
            masterWarningText.text = "<color=#70D6FF><b>🌐 オンラインJSONの再読込を開始しました</b></color>";
        }

        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReloadSurveyJsonForEveryone));
    }

    public void ReloadSurveyJsonForEveryone()
    {
        isOnlineJsonLoaded = false;
        jsonLoadRetryCount = 0;
        LoadSurveyJsonFromUrl();
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        if (result == null || string.IsNullOrEmpty(result.Result))
        {
            int fallbackLen = (questionTexts != null) ? questionTexts.Length : 0;
            Debug.LogWarning("[SurveyManager] オンラインJSONのレスポンスが空です。Inspectorの設定値を使用します。");
            onlineJsonStatusMessage = $"<color=#FFB300>⚠️ JSONが空です (エディタ設定 全{fallbackLen}問を使用中)</color>";
            UpdateMasterStatusText();
            return;
        }

        if (ApplySurveyJson(result.Result))
        {
            isOnlineJsonLoaded = true;
            jsonLoadRetryCount = 0;
            int qLen = (questionTexts != null) ? questionTexts.Length : 0;
            onlineJsonStatusMessage = $"<color=#4FE369>🌐 オンラインJSON反映済 (全{qLen}問)</color>";
            Debug.Log($"[SurveyManager] オンラインJSONからアンケート設定を正常に反映しました (全{qLen}問)");
        }
        else
        {
            int fallbackLen = (questionTexts != null) ? questionTexts.Length : 0;
            onlineJsonStatusMessage = $"<color=#FFB300>⚠️ JSON解析失敗 (エディタ設定 全{fallbackLen}問を使用中)</color>";
        }

        UpdateMasterStatusText();

        // 既にアンケート画面が開いていて未回答かつ最初の画面にいる場合は、最新の質問内容を即時反映
        if (surveyPanel != null && surveyPanel.activeSelf && !hasAnswered && currentIndex == 0)
        {
            InitializeSurvey();
        }
        // DTC同意確認パネルが表示中であればタイトルと本文も即時更新
        if (containerDtcConsent != null && containerDtcConsent.activeSelf)
        {
            if (dtcConsentTitleDisplay != null && !string.IsNullOrEmpty(dtcConsentTitleText))
            {
                dtcConsentTitleDisplay.text = dtcConsentTitleText;
            }
            if (dtcConsentNoticeDisplay != null)
            {
                dtcConsentNoticeDisplay.text = dtcConsentNoticeText;
            }
        }
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        int errCode = (result != null) ? result.ErrorCode : -1;
        string errMsg = (result != null) ? result.Error : "Unknown";
        Debug.LogWarning($"[SurveyManager] オンラインJSONの読み込みに失敗しました (Code {errCode}): {errMsg}");

        if (jsonLoadRetryCount < 2)
        {
            jsonLoadRetryCount++;
            onlineJsonStatusMessage = $"<color=#FFB300>⏳ JSON読込リトライ待機中 ({jsonLoadRetryCount}/2)...</color>";
            SendCustomEventDelayedSeconds(nameof(RetryLoadSurveyJson), 5.0f);
        }
        else
        {
            int fallbackLen = (questionTexts != null) ? questionTexts.Length : 0;
            onlineJsonStatusMessage = $"<color=#FF6B6B>⚠️ JSON読込エラー({errCode}) - エディタ設定(全{fallbackLen}問)を使用中</color>";
        }

        UpdateMasterStatusText();
    }

    /// <summary>
    /// JSON文字列を VRCJson でパースして SurveyManager の配列・設定フィールドに反映する
    /// </summary>
    public bool ApplySurveyJson(string rawJson)
    {
        if (string.IsNullOrEmpty(rawJson)) return false;

        string json = rawJson.Trim();
        if (json.Length > 0 && json[0] == '\uFEFF')
        {
            json = json.Substring(1).Trim();
        }

        DataToken rootToken;
        if (!VRCJson.TryDeserializeFromJson(json, out rootToken))
        {
            Debug.LogError("[SurveyManager] VRCJson.TryDeserializeFromJson によるJSONのデシリアライズに失敗しました。JSONの文法を確認してください。");
            return false;
        }

        DataList questionsList = null;

        if (rootToken.TokenType == TokenType.DataDictionary)
        {
            DataDictionary rootDict = rootToken.DataDictionary;

            // アンケート同意確認タイトル (オプション)
            DataToken consentTitleToken;
            if (rootDict.TryGetValue("consentTitleText", out consentTitleToken) && consentTitleToken.TokenType == TokenType.String && !string.IsNullOrEmpty(consentTitleToken.String))
            {
                consentTitleText = consentTitleToken.String;
            }
            else if (rootDict.TryGetValue("consentTitle", out consentTitleToken) && consentTitleToken.TokenType == TokenType.String && !string.IsNullOrEmpty(consentTitleToken.String))
            {
                consentTitleText = consentTitleToken.String;
            }

            // 同意確認文面 (オプション)
            DataToken consentToken;
            if (rootDict.TryGetValue("consentNoticeText", out consentToken) && consentToken.TokenType == TokenType.String && !string.IsNullOrEmpty(consentToken.String))
            {
                consentNoticeText = consentToken.String;
                if (consentNoticeDisplay != null) consentNoticeDisplay.text = consentNoticeText;
            }
            else if (rootDict.TryGetValue("consentText", out consentToken) && consentToken.TokenType == TokenType.String && !string.IsNullOrEmpty(consentToken.String))
            {
                consentNoticeText = consentToken.String;
                if (consentNoticeDisplay != null) consentNoticeDisplay.text = consentNoticeText;
            }

            // DTC同意確認タイトル (オプション)
            DataToken dtcTitleToken;
            if (rootDict.TryGetValue("dtcConsentTitleText", out dtcTitleToken) && dtcTitleToken.TokenType == TokenType.String && !string.IsNullOrEmpty(dtcTitleToken.String))
            {
                dtcConsentTitleText = dtcTitleToken.String;
                if (dtcConsentTitleDisplay != null) dtcConsentTitleDisplay.text = dtcConsentTitleText;
            }
            else if (rootDict.TryGetValue("dtcConsentTitle", out dtcTitleToken) && dtcTitleToken.TokenType == TokenType.String && !string.IsNullOrEmpty(dtcTitleToken.String))
            {
                dtcConsentTitleText = dtcTitleToken.String;
                if (dtcConsentTitleDisplay != null) dtcConsentTitleDisplay.text = dtcConsentTitleText;
            }

            // DTC同意確認文面 (オプション)
            DataToken dtcConsentToken;
            if (rootDict.TryGetValue("dtcConsentNoticeText", out dtcConsentToken) && dtcConsentToken.TokenType == TokenType.String && !string.IsNullOrEmpty(dtcConsentToken.String))
            {
                dtcConsentNoticeText = dtcConsentToken.String;
                if (dtcConsentNoticeDisplay != null) dtcConsentNoticeDisplay.text = dtcConsentNoticeText;
            }
            else if (rootDict.TryGetValue("dtcConsentText", out dtcConsentToken) && dtcConsentToken.TokenType == TokenType.String && !string.IsNullOrEmpty(dtcConsentToken.String))
            {
                dtcConsentNoticeText = dtcConsentToken.String;
                if (dtcConsentNoticeDisplay != null) dtcConsentNoticeDisplay.text = dtcConsentNoticeText;
            }

            // 各カテゴリの共通説明文の取得（空欄の場合はリセット）
            choiceCategoryDescription = "";
            gridCategoryDescription = "";
            sliderCategoryDescription = "";
            textCategoryDescription = "";

            DataToken catDescToken;
            if (rootDict.TryGetValue("choiceCategoryDescription", out catDescToken) && catDescToken.TokenType == TokenType.String)
            {
                choiceCategoryDescription = catDescToken.String;
            }
            if (rootDict.TryGetValue("gridCategoryDescription", out catDescToken) && catDescToken.TokenType == TokenType.String)
            {
                gridCategoryDescription = catDescToken.String;
            }
            if (rootDict.TryGetValue("sliderCategoryDescription", out catDescToken) && catDescToken.TokenType == TokenType.String)
            {
                sliderCategoryDescription = catDescToken.String;
            }
            if (rootDict.TryGetValue("textCategoryDescription", out catDescToken) && catDescToken.TokenType == TokenType.String)
            {
                textCategoryDescription = catDescToken.String;
            }

            // 質問リストの取得 ("questions", "items", "survey" のいずれかに対応)
            DataToken qListToken;
            if (rootDict.TryGetValue("questions", out qListToken) && qListToken.TokenType == TokenType.DataList)
            {
                questionsList = qListToken.DataList;
            }
            else if (rootDict.TryGetValue("items", out qListToken) && qListToken.TokenType == TokenType.DataList)
            {
                questionsList = qListToken.DataList;
            }
            else if (rootDict.TryGetValue("survey", out qListToken) && qListToken.TokenType == TokenType.DataList)
            {
                questionsList = qListToken.DataList;
            }
        }
        else if (rootToken.TokenType == TokenType.DataList)
        {
            questionsList = rootToken.DataList;
        }

        if (questionsList == null || questionsList.Count == 0)
        {
            Debug.LogWarning("[SurveyManager] JSON内に有効な questions 配列が見つかりませんでした。");
            return false;
        }

        int qCount = questionsList.Count;
        string[] newQuestionTexts = new string[qCount];
        int[] newAnswerTypes = new int[qCount];
        int[] newChoiceCounts = new int[qCount];
        int[] newOptionOffsets = new int[qCount];
        int[] newSliderMaxValues = new int[qCount];
        int[] newGridRowCounts = new int[qCount];
        int[] newGridRowOffsets = new int[qCount];
        int[] newGridColCounts = new int[qCount];
        int[] newGridColOffsets = new int[qCount];

        // 最初に全質問の選択肢・グリッド行・グリッド列の総数を計算（任意の択数・行列数に対応）
        int totalOptionsAllocated = 0;
        int totalGridRowsAllocated = 0;
        int totalGridColsAllocated = 0;
        for (int i = 0; i < qCount; i++)
        {
            newOptionOffsets[i] = totalOptionsAllocated;
            newGridRowOffsets[i] = totalGridRowsAllocated;
            newGridColOffsets[i] = totalGridColsAllocated;

            int countForQ = 6;
            int rowsForQ = 4;
            int colsForQ = 6;
            DataToken itemToken;
            if (questionsList.TryGetValue(i, out itemToken) && itemToken.TokenType == TokenType.DataDictionary)
            {
                DataDictionary qDict = itemToken.DataDictionary;
                DataToken optsToken;
                if ((qDict.TryGetValue("options", out optsToken) ||
                     qDict.TryGetValue("choices", out optsToken) ||
                     qDict.TryGetValue("labels", out optsToken) ||
                     qDict.TryGetValue("optionLabels", out optsToken)) && optsToken.TokenType == TokenType.DataList)
                {
                    int listLen = optsToken.DataList.Count;
                    if (listLen > countForQ) countForQ = listLen;
                    if (listLen > colsForQ) colsForQ = listLen;
                }

                DataToken gRowsToken;
                if ((qDict.TryGetValue("rows", out gRowsToken) ||
                     qDict.TryGetValue("gridRows", out gRowsToken) ||
                     qDict.TryGetValue("rowLabels", out gRowsToken)) && gRowsToken.TokenType == TokenType.DataList)
                {
                    int rLen = gRowsToken.DataList.Count;
                    if (rLen > rowsForQ) rowsForQ = rLen;
                }

                DataToken gColsToken;
                if ((qDict.TryGetValue("columns", out gColsToken) ||
                     qDict.TryGetValue("cols", out gColsToken) ||
                     qDict.TryGetValue("gridColumns", out gColsToken) ||
                     qDict.TryGetValue("gridCols", out gColsToken) ||
                     qDict.TryGetValue("colLabels", out gColsToken)) && gColsToken.TokenType == TokenType.DataList)
                {
                    int cLen = gColsToken.DataList.Count;
                    if (cLen > colsForQ) colsForQ = cLen;
                }
            }
            totalOptionsAllocated += countForQ;
            totalGridRowsAllocated += rowsForQ;
            totalGridColsAllocated += colsForQ;
        }

        string[] newOptionLabels = new string[totalOptionsAllocated];
        for (int idx = 0; idx < newOptionLabels.Length; idx++)
        {
            newOptionLabels[idx] = "";
        }

        string[] newGridRowLabels = new string[totalGridRowsAllocated];
        for (int idx = 0; idx < newGridRowLabels.Length; idx++)
        {
            newGridRowLabels[idx] = "";
        }

        string[] newGridColLabels = new string[totalGridColsAllocated];
        for (int idx = 0; idx < newGridColLabels.Length; idx++)
        {
            newGridColLabels[idx] = "";
        }

        for (int i = 0; i < qCount; i++)
        {
            newQuestionTexts[i] = $"質問 {i + 1}";
            newAnswerTypes[i] = 0;
            newChoiceCounts[i] = 2;
            newSliderMaxValues[i] = 5;
            newGridRowCounts[i] = 0;
            newGridColCounts[i] = 0;

            DataToken itemToken;
            if (!questionsList.TryGetValue(i, out itemToken) || itemToken.TokenType != TokenType.DataDictionary)
            {
                continue;
            }

            DataDictionary qDict = itemToken.DataDictionary;

            // 1. 質問文 ("question", "questionText", "text", "title")
            DataToken qTextToken;
            if (qDict.TryGetValue("question", out qTextToken) && qTextToken.TokenType == TokenType.String)
            {
                newQuestionTexts[i] = qTextToken.String;
            }
            else if (qDict.TryGetValue("questionText", out qTextToken) && qTextToken.TokenType == TokenType.String)
            {
                newQuestionTexts[i] = qTextToken.String;
            }
            else if (qDict.TryGetValue("text", out qTextToken) && qTextToken.TokenType == TokenType.String)
            {
                newQuestionTexts[i] = qTextToken.String;
            }
            else if (qDict.TryGetValue("title", out qTextToken) && qTextToken.TokenType == TokenType.String)
            {
                newQuestionTexts[i] = qTextToken.String;
            }

            // 2. 選択肢リスト ("options", "choices", "labels", "optionLabels")
            DataList optsList = null;
            DataToken optsToken;
            if (qDict.TryGetValue("options", out optsToken) && optsToken.TokenType == TokenType.DataList)
            {
                optsList = optsToken.DataList;
            }
            else if (qDict.TryGetValue("choices", out optsToken) && optsToken.TokenType == TokenType.DataList)
            {
                optsList = optsToken.DataList;
            }
            else if (qDict.TryGetValue("labels", out optsToken) && optsToken.TokenType == TokenType.DataList)
            {
                optsList = optsToken.DataList;
            }
            else if (qDict.TryGetValue("optionLabels", out optsToken) && optsToken.TokenType == TokenType.DataList)
            {
                optsList = optsToken.DataList;
            }

            // 2b. グリッド行・列リスト ("rows", "columns")
            DataList rowsList = null;
            DataToken rowsToken;
            if (qDict.TryGetValue("rows", out rowsToken) && rowsToken.TokenType == TokenType.DataList)
            {
                rowsList = rowsToken.DataList;
            }
            else if (qDict.TryGetValue("gridRows", out rowsToken) && rowsToken.TokenType == TokenType.DataList)
            {
                rowsList = rowsToken.DataList;
            }
            else if (qDict.TryGetValue("rowLabels", out rowsToken) && rowsToken.TokenType == TokenType.DataList)
            {
                rowsList = rowsToken.DataList;
            }

            DataList colsList = null;
            DataToken colsToken;
            if (qDict.TryGetValue("columns", out colsToken) && colsToken.TokenType == TokenType.DataList)
            {
                colsList = colsToken.DataList;
            }
            else if (qDict.TryGetValue("cols", out colsToken) && colsToken.TokenType == TokenType.DataList)
            {
                colsList = colsToken.DataList;
            }
            else if (qDict.TryGetValue("gridColumns", out colsToken) && colsToken.TokenType == TokenType.DataList)
            {
                colsList = colsToken.DataList;
            }
            else if (qDict.TryGetValue("gridCols", out colsToken) && colsToken.TokenType == TokenType.DataList)
            {
                colsList = colsToken.DataList;
            }
            else if (qDict.TryGetValue("colLabels", out colsToken) && colsToken.TokenType == TokenType.DataList)
            {
                colsList = colsToken.DataList;
            }
            else if (optsList != null)
            {
                colsList = optsList;
            }

            // 3. 回答形式 ("type", "answerType")
            int parsedType = -1;
            DataToken typeToken;
            if (!qDict.TryGetValue("type", out typeToken))
            {
                qDict.TryGetValue("answerType", out typeToken);
            }

            if (typeToken.TokenType == TokenType.Double)
            {
                parsedType = Mathf.RoundToInt((float)typeToken.Double);
            }
            else if (typeToken.TokenType == TokenType.String)
            {
                string ts = typeToken.String.Trim().ToLower();
                if (ts == "3" || ts == "grid" || ts == "choice_grid" || ts == "multiple_choice_grid" || ts.Contains("グリッド"))
                {
                    parsedType = 3;
                }
                else if (ts == "0" || ts == "choice" || ts == "select" || ts == "button" || ts == "選択式" || ts == "選択")
                {
                    parsedType = 0;
                }
                else if (ts == "1" || ts == "slider" || ts == "rating" || ts == "スライダー" || ts == "スライダー評価式" || ts == "評価")
                {
                    parsedType = 1;
                }
                else if (ts == "2" || ts == "text" || ts == "input" || ts == "free" || ts == "記述" || ts == "記述回答式" || ts == "自由記述")
                {
                    parsedType = 2;
                }
            }

            if (parsedType < 0 || parsedType > 3)
            {
                if (rowsList != null && rowsList.Count > 0 && colsList != null && colsList.Count > 0)
                {
                    parsedType = 3;
                }
                else if (optsList != null && optsList.Count > 0)
                {
                    parsedType = 0;
                }
                else if (qDict.ContainsKey("sliderMax") || qDict.ContainsKey("sliderMaxValue") || qDict.ContainsKey("max") || qDict.ContainsKey("maxValue"))
                {
                    parsedType = 1;
                }
                else
                {
                    parsedType = 2;
                }
            }
            newAnswerTypes[i] = parsedType;

            // 4. 選択肢ラベルと択数の設定（上限6の制限を撤廃し、任意の択数に対応）
            int validOptionCount = 0;
            int baseOffset = newOptionOffsets[i];
            if (optsList != null)
            {
                for (int c = 0; c < optsList.Count; c++)
                {
                    DataToken optItem;
                    if (optsList.TryGetValue(c, out optItem))
                    {
                        if (optItem.TokenType == TokenType.String)
                        {
                            newOptionLabels[baseOffset + c] = optItem.String;
                            if (!string.IsNullOrEmpty(optItem.String)) validOptionCount++;
                        }
                        else if (optItem.TokenType == TokenType.Double)
                        {
                            newOptionLabels[baseOffset + c] = optItem.Double.ToString();
                            validOptionCount++;
                        }
                    }
                }
            }

            DataToken ccToken;
            if (qDict.TryGetValue("choiceCount", out ccToken) && ccToken.TokenType == TokenType.Double)
            {
                int ccVal = Mathf.RoundToInt((float)ccToken.Double);
                newChoiceCounts[i] = ccVal < 2 ? 2 : ccVal;
            }
            else if (validOptionCount > 0)
            {
                newChoiceCounts[i] = validOptionCount < 2 ? 2 : validOptionCount;
            }
            else if (optsList != null && optsList.Count > 0)
            {
                newChoiceCounts[i] = optsList.Count < 2 ? 2 : optsList.Count;
            }
            else
            {
                newChoiceCounts[i] = (parsedType == 0) ? 2 : 0;
            }

            // 4b. 選択式（グリッド）の行・列ラベルと数の設定
            int validGridRowCount = 0;
            int rowBaseOffset = newGridRowOffsets[i];
            if (rowsList != null)
            {
                for (int r = 0; r < rowsList.Count; r++)
                {
                    DataToken rItem;
                    if (rowsList.TryGetValue(r, out rItem))
                    {
                        if (rItem.TokenType == TokenType.String)
                        {
                            newGridRowLabels[rowBaseOffset + r] = rItem.String;
                            if (!string.IsNullOrEmpty(rItem.String)) validGridRowCount++;
                        }
                        else if (rItem.TokenType == TokenType.Double)
                        {
                            newGridRowLabels[rowBaseOffset + r] = rItem.Double.ToString();
                            validGridRowCount++;
                        }
                    }
                }
            }

            int validGridColCount = 0;
            int colBaseOffset = newGridColOffsets[i];
            if (colsList != null)
            {
                for (int c = 0; c < colsList.Count; c++)
                {
                    DataToken cItem;
                    if (colsList.TryGetValue(c, out cItem))
                    {
                        if (cItem.TokenType == TokenType.String)
                        {
                            newGridColLabels[colBaseOffset + c] = cItem.String;
                            if (!string.IsNullOrEmpty(cItem.String)) validGridColCount++;
                        }
                        else if (cItem.TokenType == TokenType.Double)
                        {
                            newGridColLabels[colBaseOffset + c] = cItem.Double.ToString();
                            validGridColCount++;
                        }
                    }
                }
            }

            if (parsedType == 3)
            {
                newGridRowCounts[i] = validGridRowCount > 0 ? validGridRowCount : 1;
                newGridColCounts[i] = validGridColCount >= 2 ? validGridColCount : 2;
            }
            else
            {
                newGridRowCounts[i] = validGridRowCount;
                newGridColCounts[i] = validGridColCount;
            }

            // 5. スライダー最大評価値 ("sliderMax", "sliderMaxValue", "max", "maxValue")
            DataToken smToken;
            if (!qDict.TryGetValue("sliderMax", out smToken))
            {
                if (!qDict.TryGetValue("sliderMaxValue", out smToken))
                {
                    if (!qDict.TryGetValue("max", out smToken))
                    {
                        qDict.TryGetValue("maxValue", out smToken);
                    }
                }
            }

            if (smToken.TokenType == TokenType.Double)
            {
                newSliderMaxValues[i] = Mathf.Clamp(Mathf.RoundToInt((float)smToken.Double), 2, 20);
            }
            else
            {
                newSliderMaxValues[i] = 5;
            }
        }

        questionTexts = newQuestionTexts;
        answerTypes = newAnswerTypes;
        choiceCounts = newChoiceCounts;
        optionOffsets = newOptionOffsets;
        optionLabels = newOptionLabels;
        sliderMaxValues = newSliderMaxValues;
        gridRowCounts = newGridRowCounts;
        gridRowOffsets = newGridRowOffsets;
        gridRowLabels = newGridRowLabels;
        gridColCounts = newGridColCounts;
        gridColOffsets = newGridColOffsets;
        gridColLabels = newGridColLabels;

        return true;
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        EnsureUIReferences();
        lastAppliedModeState = -1;
        SyncDtcTargetsToUdonBehaviour();
        RefreshMasterPanelVisibility();
        CheckAndRestoreSurveyForLocalUser();
        CheckAndRestoreDtcConsentForLocalUser();
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 0.5f);
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 2.0f);
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 5.0f);
        SendCustomEventDelayedSeconds(nameof(EnsureDtcLoopEventRunning), 1.8f);
        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreSurveyForLocalUser), 1.0f);
        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreSurveyForLocalUser), 3.0f);
        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreDtcConsentForLocalUser), 1.2f);
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        EnsureUIReferences();
        RefreshMasterPanelVisibility();
        CheckAndRestoreSurveyForLocalUser();
        CheckAndRestoreDtcConsentForLocalUser();
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 0.5f);
    }

    public void RefreshMasterPanelVisibility()
    {
        EnsureUIReferences();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string playerName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";
        bool isMaster = CheckIfRecordMaster(playerName);

        if (masterControlPanel != null)
        {
            masterControlPanel.SetActive(isMaster);
        }

        if (isMaster)
        {
            // 主催者(RecordMaster)にはアンケート回答パネルおよび結果パネルを絶対に表示しない（非表示を強制）
            if (surveyPanel != null && surveyPanel.activeSelf) surveyPanel.SetActive(false);
            if (resultPanel != null && resultPanel.activeSelf) resultPanel.SetActive(false);

            UpdateMasterStatusText();
        }

        if (syncedDtcTargetAllMode && containerDtcConsent != null && containerDtcConsent.activeSelf)
        {
            containerDtcConsent.transform.SetAsLastSibling();
        }
    }

    private void EnsureUIReferences()
    {
        if (masterControlPanel == null)
        {
            Transform t = transform.Find("MasterControlPanel");
            if (t != null) masterControlPanel = t.gameObject;
        }

        if (masterControlPanel != null)
        {
            if (startSurveyButton == null)
            {
                Transform t = masterControlPanel.transform.Find("Btn_StartSurvey");
                if (t != null) startSurveyButton = t.GetComponent<Button>();
            }

            if (reopenSurveyButton == null)
            {
                Transform t = masterControlPanel.transform.Find("Btn_ReopenSurvey");
                if (t == null) t = masterControlPanel.transform.Find("Btn_Reopen");
                if (t != null) reopenSurveyButton = t.GetComponent<Button>();
            }

            if (resetSurveyButton == null)
            {
                Transform t = masterControlPanel.transform.Find("Btn_ResetSurvey");
                if (t == null) t = masterControlPanel.transform.Find("Btn_Reset");
                if (t != null) resetSurveyButton = t.GetComponent<Button>();
            }

            if (masterWarningText == null)
            {
                Transform t = masterControlPanel.transform.Find("MasterWarningText");
                if (t == null) t = masterControlPanel.transform.Find("WarningMsgBox/MasterWarningText");
                if (t != null) masterWarningText = t.GetComponent<Text>();
            }

            if (masterStatusText == null)
            {
                Transform t = masterControlPanel.transform.Find("ScrollView/Viewport/Content/MasterStatusText");
                if (t != null) masterStatusText = t.GetComponent<Text>();
            }

            if (toggleTargetModeButton == null)
            {
                Transform t = masterControlPanel.transform.Find("Btn_ToggleTargetMode");
                if (t != null) toggleTargetModeButton = t.GetComponent<Button>();
            }

            if (targetModeListButton == null)
            {
                Transform t = masterControlPanel.transform.Find("Btn_ModeList");
                if (t != null) targetModeListButton = t.GetComponent<Button>();
            }

            if (targetModeAllButton == null)
            {
                Transform t = masterControlPanel.transform.Find("Btn_ModeAll");
                if (t != null) targetModeAllButton = t.GetComponent<Button>();
            }
        }

        if (surveyPanel == null)
        {
            Transform t = transform.Find("SurveyPanel");
            if (t != null) surveyPanel = t.gameObject;
        }

        if (surveyPanel != null)
        {
            if (categoryDescriptionText == null)
            {
                Transform t = surveyPanel.transform.Find("CategoryDescriptionText");
                if (t != null) categoryDescriptionText = t.GetComponent<Text>();
            }

            if (containerConsent == null)
            {
                Transform t = surveyPanel.transform.Find("Container_Consent");
                if (t != null) containerConsent = t.gameObject;
            }

            if (containerConsent != null)
            {
                if (consentNoticeDisplay == null)
                {
                    Transform t = containerConsent.transform.Find("ConsentNoticeDisplay");
                    if (t != null) consentNoticeDisplay = t.GetComponent<Text>();
                }
                if (consentAgreeButton == null)
                {
                    Transform t = containerConsent.transform.Find("Btn_ConsentAgree");
                    if (t != null) consentAgreeButton = t.GetComponent<Button>();
                }
                if (consentDisagreeButton == null)
                {
                    Transform t = containerConsent.transform.Find("Btn_ConsentDisagree");
                    if (t != null) consentDisagreeButton = t.GetComponent<Button>();
                }
            }

            if (containerGrid == null)
            {
                Transform t = surveyPanel.transform.Find("Container_Grid");
                if (t != null) containerGrid = t.gameObject;
            }

            if (containerGrid != null)
            {
                if (gridCornerHeaderText == null)
                {
                    Transform t = containerGrid.transform.Find("GridHeaderBar/CornerHeaderText");
                    if (t != null) gridCornerHeaderText = t.GetComponent<Text>();
                }
                if (gridColHeaderTexts == null || gridColHeaderTexts.Length == 0)
                {
                    Transform hb = containerGrid.transform.Find("GridHeaderBar");
                    if (hb != null)
                    {
                        gridColHeaderTexts = new Text[10];
                        for (int c = 0; c < 10; c++)
                        {
                            Transform ct = hb.Find($"ColHeader{c}");
                            if (ct != null) gridColHeaderTexts[c] = ct.GetComponent<Text>();
                        }
                    }
                }
                if (gridScrollRect == null)
                {
                    Transform sv = containerGrid.transform.Find("GridScrollView");
                    if (sv != null) gridScrollRect = sv.GetComponent<ScrollRect>();
                }
                if (gridScrollContent == null)
                {
                    Transform cnt = containerGrid.transform.Find("GridScrollView/Viewport/Content");
                    if (cnt != null) gridScrollContent = cnt.GetComponent<RectTransform>();
                }
                if (gridScrollContent != null)
                {
                    if (gridRowObjects == null || gridRowObjects.Length == 0)
                    {
                        gridRowObjects = new GameObject[20];
                        gridRowBgImages = new Image[20];
                        gridRowLabelTexts = new Text[20];
                        for (int r = 0; r < 20; r++)
                        {
                            Transform rt = gridScrollContent.Find($"GridRow{r}");
                            if (rt != null)
                            {
                                gridRowObjects[r] = rt.gameObject;
                                gridRowBgImages[r] = rt.GetComponent<Image>();
                                Transform lt = rt.Find("RowLabel");
                                if (lt != null) gridRowLabelTexts[r] = lt.GetComponent<Text>();
                            }
                        }
                    }
                    if (gridCellButtons == null || gridCellButtons.Length == 0)
                    {
                        gridCellButtons = new GameObject[120];
                        gridCellButtonImages = new Image[120];
                        gridCellButtonTexts = new Text[120];
                        for (int k = 0; k < 120; k++)
                        {
                            Transform bt = gridScrollContent.Find($"Btn_GridCell{k}");
                            if (bt != null)
                            {
                                gridCellButtons[k] = bt.gameObject;
                                gridCellButtonImages[k] = bt.GetComponent<Image>();
                                gridCellButtonTexts[k] = bt.GetComponentInChildren<Text>();
                            }
                        }
                    }
                }
                if (gridSubmitButton == null)
                {
                    Transform sb = containerGrid.transform.Find("Btn_SubmitGrid");
                    if (sb != null)
                    {
                        gridSubmitButton = sb.GetComponent<Button>();
                        gridSubmitButtonImage = sb.GetComponent<Image>();
                        gridSubmitButtonText = sb.GetComponentInChildren<Text>();
                    }
                }
            }
        }

        if (containerDtcConsent == null)
        {
            Transform t = transform.Find("Container_DtcConsent");
            if (t == null && surveyPanel != null) t = surveyPanel.transform.Find("Container_DtcConsent");
            if (t == null && transform.parent != null) t = transform.parent.Find("Container_DtcConsent");
            if (t == null)
            {
                GameObject found = GameObject.Find("Container_DtcConsent");
                if (found != null) t = found.transform;
            }
            if (t != null) containerDtcConsent = t.gameObject;
        }

        if (containerDtcConsent != null)
        {
            if (dtcConsentTitleDisplay == null)
            {
                Transform t = containerDtcConsent.transform.Find("DtcTitleText");
                if (t == null) t = containerDtcConsent.transform.Find("TitleText");
                if (t != null) dtcConsentTitleDisplay = t.GetComponent<Text>();
            }
            if (dtcConsentNoticeDisplay == null)
            {
                Transform t = containerDtcConsent.transform.Find("DtcConsentNoticeDisplay");
                if (t == null) t = containerDtcConsent.transform.Find("ConsentNoticeDisplay");
                if (t != null) dtcConsentNoticeDisplay = t.GetComponent<Text>();
            }
            if (dtcConsentAgreeButton == null)
            {
                Transform t = containerDtcConsent.transform.Find("Btn_DtcConsentAgree");
                if (t == null) t = containerDtcConsent.transform.Find("Btn_ConsentAgree");
                if (t != null) dtcConsentAgreeButton = t.GetComponent<Button>();
            }
            if (dtcConsentDisagreeButton == null)
            {
                Transform t = containerDtcConsent.transform.Find("Btn_DtcConsentDisagree");
                if (t == null) t = containerDtcConsent.transform.Find("Btn_ConsentDisagree");
                if (t != null) dtcConsentDisagreeButton = t.GetComponent<Button>();
            }
        }
    }

    /// <summary>
    /// オーナー権が自身に移譲された際のコールバック (シリアライズの確実な送出と同期修復)
    /// </summary>
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        EnsureUIReferences();
        RefreshMasterPanelVisibility();
        SyncDtcTargetsToUdonBehaviour();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer != null && localPlayer.IsValid() && player != null && player.IsValid())
        {
            if (player.isLocal)
            {
                MergeLocalDtcRecordsIntoSynced();

                // 自分が新しくオーナーになった場合、未送信のシリアライズデータがあれば即座に送信
                if (pendingSerialization)
                {
                    RequestSerialization();
                    pendingSerialization = false;
                }

                // 主催者(RecordMaster)であれば、手元の完全な確定リストを syncedAnsweredPlayers にマージして同期を自動修復
                string localName = localPlayer.displayName;
                if (CheckIfRecordMaster(localName))
                {
                    RepairAndBroadcastAnsweredList();
                }
            }
        }
    }

    /// <summary>
    /// オーナー権移譲待ち・遅延シリアライズリトライ処理
    /// </summary>
    public void RetrySerialization()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer != null && localPlayer.IsValid())
        {
            if (Networking.IsOwner(gameObject))
            {
                MergeLocalDtcRecordsIntoSynced();
                RequestSerialization();
                serializationRetryCount++;
                if (serializationRetryCount >= 3)
                {
                    pendingSerialization = false;
                }
            }
            else
            {
                Networking.SetOwner(localPlayer, gameObject);
            }
        }
    }

    /// <summary>
    /// 他のプレイヤーからネットワーク同期データを受信したタイミングの処理
    /// </summary>
    public override void OnDeserialization()
    {
        SyncDtcPlayersToLocalList();
        SyncDtcTargetsToUdonBehaviour();
        CheckIfAlreadyAnswered();
        RefreshMasterPanelVisibility();
        CheckAndRestoreSurveyForLocalUser();
        CheckAndRestoreDtcConsentForLocalUser();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string localName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        // RecordMaster 権限のあるプレイヤーの処理
        if (CheckIfRecordMaster(localName))
        {
            // 届いた最新ログから回答者名を抽出し、マスターローカルの確定リストに登録
            string extractedName = ExtractPlayerNameFromLog(syncedLatestResultLog);
            if (!string.IsNullOrEmpty(extractedName))
            {
                RegisterPlayerAnsweredByMaster(extractedName);
            }

            // syncedAnsweredPlayers に含まれるプレイヤーもマスター確定リストに取り込む
            SyncAnsweredPlayersToMasterList();

            if (!string.IsNullOrEmpty(syncedLatestResultLog) && syncedLatestResultLog != lastProcessedLog)
            {
                lastProcessedLog = syncedLatestResultLog;
                Debug.Log(syncedLatestResultLog);
            }

            UpdateMasterStatusText();

            // マスターがオーナーであれば最新確定リストで同期文字列を自動修復
            if (Networking.IsOwner(gameObject))
            {
                RepairAndBroadcastAnsweredList();
            }
        }

        if (!syncedSurveyStarted)
        {
            hasAnswered = false;
            if (surveyPanel != null && surveyPanel.activeSelf) surveyPanel.SetActive(false);
            if (resultPanel != null && resultPanel.activeSelf) resultPanel.SetActive(false);
        }

        if (!CheckIfRecordMaster(localName) && hasAnswered && surveyPanel != null && surveyPanel.activeSelf)
        {
            InitializeSurvey();
        }
    }

    /// <summary>
    /// 途中入室(再イン)した未回答者に対する自動復帰処理
    /// </summary>
    public void CheckAndRestoreSurveyForLocalUser()
    {
        EnsureUIReferences();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string localName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        if (CheckIfRecordMaster(localName))
        {
            // 主催者(RecordMaster)には回答画面・結果画面を表示せず、必ず閉じる
            if (surveyPanel != null && surveyPanel.activeSelf) surveyPanel.SetActive(false);
            if (resultPanel != null && resultPanel.activeSelf) resultPanel.SetActive(false);
            return;
        }

        if (!syncedTargetAllMode && !CheckIfUserInVariable(localName, jsonNamesVariableName)) return;

        CheckIfAlreadyAnswered();

        // 配信中でかつ未回答の場合、自動的に画面前に復帰表示
        if (syncedSurveyStarted && !hasAnswered)
        {
            if (surveyPanel != null && !surveyPanel.activeSelf)
            {
                Debug.Log($"[SurveyManager] {localName} が再入室したためアンケート画面を自動復帰表示します。");
                PositionSurveyInFrontOfPlayer();
                InitializeSurvey();
            }
        }
    }

    /// <summary>
    /// RecordMaster専用: 未回答の一般参加者へアンケート画面の再表示を指示するボタンイベント
    /// </summary>
    public void OnReopenSurveyButton()
    {
        EnsureUIReferences();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string localName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        if (!CheckIfRecordMaster(localName))
        {
            Debug.LogWarning("[SurveyManager] アンケート再表示の指示権限がありません。");
            return;
        }

        if (!syncedSurveyStarted)
        {
            if (masterWarningText != null)
            {
                masterWarningText.text = "<color=#FFB300><b>⚠️ アンケート開始前は再表示できません</b></color>";
            }
            return;
        }

        if (masterWarningText != null)
        {
            masterWarningText.text = "<color=#4FE369><b>📋 未回答者にアンケートを再表示しました</b></color>";
        }

        if (masterControlPanel != null) masterControlPanel.SetActive(true);

        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReopenSurveyForUnansweredPlayers));
    }

    public void ReopenSurveyForUnansweredPlayers()
    {
        EnsureUIReferences();

        if (!syncedSurveyStarted) return;

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string localName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        // 主催者(RecordMaster)には表示しない
        if (CheckIfRecordMaster(localName)) return;

        // モード別の対象者判定
        bool isTarget = false;
        if (syncedTargetAllMode)
        {
            isTarget = true;
        }
        else
        {
            isTarget = CheckIfUserInVariable(localName, jsonNamesVariableName);
        }
        if (!isTarget) return;

        CheckIfAlreadyAnswered();

        if (hasAnswered)
        {
            // すでにローカルで回答済みだがマスター側で未回答判定になっていた場合の救済再送
            SubmitAnswerAndSerialize(localName);
            return;
        }

        // 未回答の場合、目の前にアンケートを表示・復帰！
        Debug.Log($"[SurveyManager] 管理者の指示により、{localName} のアンケート画面を再表示します。");
        PositionSurveyInFrontOfPlayer();
        InitializeSurvey();
    }

    private void CheckIfAlreadyAnswered()
    {
        if (hasAnswered) return;

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer != null && localPlayer.IsValid())
        {
            string pName = localPlayer.displayName;
            if (IsPlayerAnswered(pName))
            {
                hasAnswered = true;
            }
        }
    }

    /// <summary>
    /// 指定されたプレイヤーが回答済み（または同意辞退）かを判定する
    /// masterRecordedAnsweredPlayers と syncedAnsweredPlayers の双方を大文字小文字無視で照合
    /// </summary>
    private bool IsPlayerAnswered(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return false;
        string cleanName = playerName.Trim();
        if (string.IsNullOrEmpty(cleanName)) return false;

        string key = "," + cleanName.ToLower() + ",";

        // 1. マスターローカルの確定リストにあれば true
        if (!string.IsNullOrEmpty(masterRecordedAnsweredPlayers) && masterRecordedAnsweredPlayers.ToLower().Contains(key))
        {
            return true;
        }

        // 2. ネットワーク同期文字列にあれば true
        if (!string.IsNullOrEmpty(syncedAnsweredPlayers) && syncedAnsweredPlayers.ToLower().Contains(key))
        {
            // マスター側ならローカル確定リストにも取り込んでおく
            RegisterPlayerAnsweredByMaster(cleanName);
            return true;
        }

        return false;
    }

    /// <summary>
    /// RecordMasterローカル専用: プレイヤーを回答完了としてマスターのメモリに永続登録する
    /// </summary>
    private void RegisterPlayerAnsweredByMaster(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return;
        string cleanName = playerName.Trim();
        if (string.IsNullOrEmpty(cleanName)) return;

        if (string.IsNullOrEmpty(masterRecordedAnsweredPlayers))
        {
            masterRecordedAnsweredPlayers = ",";
        }

        string key = cleanName.ToLower() + ",";
        if (!masterRecordedAnsweredPlayers.ToLower().Contains("," + key))
        {
            masterRecordedAnsweredPlayers += cleanName + ",";
        }
    }

    /// <summary>
    /// 回答ログ文字列 "[MVP_Q] PlayerName: ..." からプレイヤー名を抽出する
    /// </summary>
    private string ExtractPlayerNameFromLog(string log)
    {
        if (string.IsNullOrEmpty(log)) return "";
        string prefix = "[MVP_Q] ";
        int startIdx = log.IndexOf(prefix);
        if (startIdx < 0) return "";
        startIdx += prefix.Length;

        int colonIdx = log.IndexOf(":", startIdx);
        if (colonIdx < 0) return "";

        string name = log.Substring(startIdx, colonIdx - startIdx).Trim();
        return name;
    }

    /// <summary>
    /// syncedAnsweredPlayers 内の全プレイヤーをマスター確定リストに一括登録する
    /// </summary>
    private void SyncAnsweredPlayersToMasterList()
    {
        if (string.IsNullOrEmpty(syncedAnsweredPlayers)) return;
        string[] arr = syncedAnsweredPlayers.Split(',');
        for (int i = 0; i < arr.Length; i++)
        {
            string p = arr[i].Trim();
            if (!string.IsNullOrEmpty(p))
            {
                RegisterPlayerAnsweredByMaster(p);
            }
        }
    }

    /// <summary>
    /// RecordMasterがオーナー権を持っている時、マスターが保持する全員分の確定リストを
    /// syncedAnsweredPlayers および DTC同意リストにマージして全クライアントへ再配信（自動修復）する
    /// </summary>
    private void RepairAndBroadcastAnsweredList()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null || !localPlayer.IsValid()) return;
        if (!CheckIfRecordMaster(localPlayer.displayName)) return;
        if (!Networking.IsOwner(gameObject)) return;

        bool changed = MergeLocalDtcRecordsIntoSynced();

        if (!string.IsNullOrEmpty(masterRecordedAnsweredPlayers))
        {
            if (string.IsNullOrEmpty(syncedAnsweredPlayers))
            {
                syncedAnsweredPlayers = ",";
                changed = true;
            }

            string[] recorded = masterRecordedAnsweredPlayers.Split(',');
            for (int i = 0; i < recorded.Length; i++)
            {
                string p = recorded[i].Trim();
                if (!string.IsNullOrEmpty(p))
                {
                    string key = "," + p.ToLower() + ",";
                    if (!syncedAnsweredPlayers.ToLower().Contains(key))
                    {
                        syncedAnsweredPlayers += p + ",";
                        changed = true;
                    }
                }
            }
        }

        if (changed)
        {
            RequestSerialization();
        }
    }

    private void SyncDtcPlayersToLocalList()
    {
        if (!string.IsNullOrEmpty(syncedDtcConsentedPlayers))
        {
            if (string.IsNullOrEmpty(localRecordedDtcConsentedPlayers))
            {
                localRecordedDtcConsentedPlayers = ",";
            }
            string[] arr = syncedDtcConsentedPlayers.Split(',');
            for (int i = 0; i < arr.Length; i++)
            {
                string p = arr[i].Trim();
                if (!string.IsNullOrEmpty(p))
                {
                    string key = "," + p + ",";
                    if (!localRecordedDtcConsentedPlayers.Contains(key))
                    {
                        localRecordedDtcConsentedPlayers += p + ",";
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(syncedDtcDisagreedPlayers))
        {
            if (string.IsNullOrEmpty(localRecordedDtcDisagreedPlayers))
            {
                localRecordedDtcDisagreedPlayers = ",";
            }
            string[] arr = syncedDtcDisagreedPlayers.Split(',');
            for (int i = 0; i < arr.Length; i++)
            {
                string p = arr[i].Trim();
                if (!string.IsNullOrEmpty(p))
                {
                    string key = "," + p + ",";
                    if (!localRecordedDtcDisagreedPlayers.Contains(key))
                    {
                        localRecordedDtcDisagreedPlayers += p + ",";
                    }
                }
            }
        }
    }

    private bool MergeLocalDtcRecordsIntoSynced()
    {
        bool changed = false;

        if (!string.IsNullOrEmpty(localRecordedDtcConsentedPlayers))
        {
            if (string.IsNullOrEmpty(syncedDtcConsentedPlayers))
            {
                syncedDtcConsentedPlayers = ",";
                changed = true;
            }
            string[] arr = localRecordedDtcConsentedPlayers.Split(',');
            for (int i = 0; i < arr.Length; i++)
            {
                string p = arr[i].Trim();
                if (!string.IsNullOrEmpty(p))
                {
                    string key = "," + p + ",";
                    if (!syncedDtcConsentedPlayers.Contains(key))
                    {
                        syncedDtcConsentedPlayers += p + ",";
                        changed = true;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(localRecordedDtcDisagreedPlayers))
        {
            if (string.IsNullOrEmpty(syncedDtcDisagreedPlayers))
            {
                syncedDtcDisagreedPlayers = ",";
                changed = true;
            }
            string[] arr = localRecordedDtcDisagreedPlayers.Split(',');
            for (int i = 0; i < arr.Length; i++)
            {
                string p = arr[i].Trim();
                if (!string.IsNullOrEmpty(p))
                {
                    string key = "," + p + ",";
                    if (!syncedDtcDisagreedPlayers.Contains(key))
                    {
                        syncedDtcDisagreedPlayers += p + ",";
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// リスト限定モード用の対象プレイヤー配列 (VRCanswers.json の PlayerNames) を取得する
    /// ワールド全員モード中に上書きされている場合はキャッシュされた元の配列を返す
    /// </summary>
    private string[] GetListModeJsonNames()
    {
        if (targetUdonBehaviour != null && !string.IsNullOrEmpty(jsonNamesVariableName))
        {
            object val = targetUdonBehaviour.GetProgramVariable(jsonNamesVariableName);
            if (val != null)
            {
                string[] currentArr = (string[])val;
                if (currentArr != null && currentArr != lastAppliedDtcNamesArray)
                {
                    cachedOriginalJsonNames = currentArr;
                }
            }
        }
        return cachedOriginalJsonNames;
    }

    /// <summary>
    /// DTC同意済みプレイヤー文字列 (,Player1,Player2,) から string[] 配列を構築する
    /// </summary>
    private string[] BuildDtcConsentedPlayersArray(string sourceStr)
    {
        if (string.IsNullOrEmpty(sourceStr))
        {
            return new string[0];
        }

        string[] parts = sourceStr.Split(',');
        int validCount = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (!string.IsNullOrEmpty(parts[i]) && !string.IsNullOrEmpty(parts[i].Trim()))
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return new string[0];
        }

        string[] result = new string[validCount];
        int idx = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (!string.IsNullOrEmpty(parts[i]))
            {
                string trimmed = parts[i].Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    result[idx] = trimmed;
                    idx++;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// 全角/半角の括弧・スペース・大文字小文字の違いを吸収してプレイヤー名を比較用に正規化する
    /// </summary>
    private string NormalizePlayerNameForMatch(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        string s = name.Trim()
            .Replace('（', '(')
            .Replace('）', ')')
            .Replace('　', ' ');
        while (s.IndexOf(" (") >= 0) s = s.Replace(" (", "(");
        while (s.IndexOf("( ") >= 0) s = s.Replace("( ", "(");
        while (s.IndexOf(" )") >= 0) s = s.Replace(" )", ")");
        while (s.IndexOf(") ") >= 0) s = s.Replace(") ", ")");
        return s.Trim().ToLower();
    }

    /// <summary>
    /// JSON上のプレイヤー名がインスタンス内の実プレイヤーと括弧・大文字小文字違い等で一致する場合、
    /// MVP_DTC_Online 側の完全一致 (==) でも一致するよう実プレイヤーの displayName に解決する
    /// </summary>
    private string ResolveActualPlayerDisplayName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return "";
        string trimmed = rawName.Trim();
        string normTarget = NormalizePlayerNameForMatch(trimmed);
        if (string.IsNullOrEmpty(normTarget)) return trimmed;

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer != null && localPlayer.IsValid() && !string.IsNullOrEmpty(localPlayer.displayName))
        {
            if (NormalizePlayerNameForMatch(localPlayer.displayName) == normTarget)
            {
                return localPlayer.displayName;
            }

            int pCount = VRCPlayerApi.GetPlayerCount();
            if (pCount > 0)
            {
                VRCPlayerApi[] players = new VRCPlayerApi[pCount];
                VRCPlayerApi.GetPlayers(players);
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null && players[i].IsValid() && !string.IsNullOrEmpty(players[i].displayName))
                    {
                        if (NormalizePlayerNameForMatch(players[i].displayName) == normTarget)
                        {
                            return players[i].displayName;
                        }
                    }
                }
            }
        }
        return trimmed;
    }

    /// <summary>
    /// リスト限定モード時に [MVP_DTC] の出力対象とする配列 (StaffNames + PlayerNames の重複なし結合配列) を構築する
    /// </summary>
    private string[] BuildListModeDtcTargetArray(string[] playerNames, string[] masterNames)
    {
        int maxLen = 0;
        if (masterNames != null) maxLen += masterNames.Length;
        if (allowedUserNames != null) maxLen += allowedUserNames.Length;
        if (playerNames != null) maxLen += playerNames.Length;

        if (maxLen == 0)
        {
            return new string[0];
        }

        string[] temp = new string[maxLen];
        int count = 0;

        // 1. StaffNames (RecordMasterNames) を追加
        if (masterNames != null)
        {
            for (int i = 0; i < masterNames.Length; i++)
            {
                if (!string.IsNullOrEmpty(masterNames[i]))
                {
                    string name = ResolveActualPlayerDisplayName(masterNames[i]);
                    if (!string.IsNullOrEmpty(name) && !ContainsNameIgnoreCase(temp, count, name))
                    {
                        temp[count] = name;
                        count++;
                    }
                }
            }
        }

        // 2. Inspector直指定の allowedUserNames があれば追加
        if (allowedUserNames != null)
        {
            for (int i = 0; i < allowedUserNames.Length; i++)
            {
                if (!string.IsNullOrEmpty(allowedUserNames[i]))
                {
                    string name = ResolveActualPlayerDisplayName(allowedUserNames[i]);
                    if (!string.IsNullOrEmpty(name) && !ContainsNameIgnoreCase(temp, count, name))
                    {
                        temp[count] = name;
                        count++;
                    }
                }
            }
        }

        // 3. PlayerNames (JsonNamesString) を追加
        if (playerNames != null)
        {
            for (int i = 0; i < playerNames.Length; i++)
            {
                if (!string.IsNullOrEmpty(playerNames[i]))
                {
                    string name = ResolveActualPlayerDisplayName(playerNames[i]);
                    if (!string.IsNullOrEmpty(name) && !ContainsNameIgnoreCase(temp, count, name))
                    {
                        temp[count] = name;
                        count++;
                    }
                }
            }
        }

        string[] result = new string[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = temp[i];
        }
        return result;
    }

    private bool ContainsNameIgnoreCase(string[] arr, int currentCount, string target)
    {
        string targetNorm = NormalizePlayerNameForMatch(target);
        for (int i = 0; i < currentCount; i++)
        {
            if (arr[i] != null && NormalizePlayerNameForMatch(arr[i]) == targetNorm)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// MVP_DTC_Online 側の JSON 読み込みが 1.0 秒より遅れた場合や、途中参加・括弧表記揺れ等で
    /// MVP_DTC_Online の初回 LoopEvent (1.0s) が停止してしまった場合に、安全に LoopEvent を再開させる
    /// </summary>
    public void EnsureDtcLoopEventRunning()
    {
        if (targetUdonBehaviour == null) return;

        // MVP_DTC_Online 自身の _start で予約された初回 LoopEvent (1.0秒後) が実行し終わるまでは待機し、二重起動を防止する
        if (Time.timeSinceLevelLoad < 1.5f)
        {
            SendCustomEventDelayedSeconds(nameof(EnsureDtcLoopEventRunning), 1.6f - Time.timeSinceLevelLoad);
            return;
        }

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null || !localPlayer.IsValid()) return;

        string localName = localPlayer.displayName;
        if (!CheckIfRecordMaster(localName)) return;

        object staffBoolObj = targetUdonBehaviour.GetProgramVariable("StaffBool");
        bool currentStaffBool = (staffBoolObj != null) ? (bool)staffBoolObj : false;

        if (!currentStaffBool && !hasRevivedDtcLoopEvent)
        {
            hasRevivedDtcLoopEvent = true;
            targetUdonBehaviour.SetProgramVariable("StaffBool", true);
            targetUdonBehaviour.SendCustomEvent("LoopEvent");
            Debug.Log("[SurveyManager] MVP_DTC_Online の LoopEvent を自動開始・再開しました。");
        }
    }

    /// <summary>
    /// 外部 UdonBehaviour (MVP_DTC_Online / DatacollectionJson) の JsonNamesString を
    /// 現在のモードおよび DTC 同意状態 (syncedDtcConsentedPlayers) に合わせて同期・制御する
    /// ・ワールド全員モード時: 同意確認で「同意する」を押したプレイヤーのみを [MVP_DTC] ログに出力
    /// ・リスト限定モード時: StaffNames (RecordMasterNames) と PlayerNames (JsonNamesString) の両方を [MVP_DTC] ログに出力
    /// </summary>
    public void SyncDtcTargetsToUdonBehaviour()
    {
        if (targetUdonBehaviour == null || string.IsNullOrEmpty(jsonNamesVariableName)) return;

        object currentObj = targetUdonBehaviour.GetProgramVariable(jsonNamesVariableName);
        string[] currentArr = (currentObj != null) ? (string[])currentObj : null;

        // 外部 UdonBehaviour 側 (VRCStringDownloader 等) が新しい配列をセットした場合は元の PlayerNames リストとしてキャッシュする
        if (currentArr != null && currentArr != lastAppliedDtcNamesArray)
        {
            cachedOriginalJsonNames = currentArr;
        }

        // RecordMasterNames の変化を常時監視し、何秒後に Gist (VRCanswers.json) が読み込まれても即座にUIとLoopEventへ反映する
        string[] currentMasterNames = null;
        if (!string.IsNullOrEmpty(recordMasterVariableName))
        {
            object masterObj = targetUdonBehaviour.GetProgramVariable(recordMasterVariableName);
            if (masterObj != null)
            {
                currentMasterNames = (string[])masterObj;
            }
        }

        bool masterListChanged = (currentMasterNames != lastObservedMasterNamesForEvents);
        bool playerListChanged = (cachedOriginalJsonNames != lastObservedPlayerNamesForEvents);

        if (masterListChanged || playerListChanged)
        {
            lastObservedMasterNamesForEvents = currentMasterNames;
            lastObservedPlayerNamesForEvents = cachedOriginalJsonNames;

            // もし RecordMasterNames 内にローカルプレイヤーと括弧違い等で一致する名前があれば、
            // MVP_DTC_Online 側の == 判定でも通るよう実 displayName に補正しておく
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (currentMasterNames != null && lp != null && lp.IsValid() && !string.IsNullOrEmpty(lp.displayName))
            {
                string lpNorm = NormalizePlayerNameForMatch(lp.displayName);
                for (int i = 0; i < currentMasterNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(currentMasterNames[i]) &&
                        NormalizePlayerNameForMatch(currentMasterNames[i]) == lpNorm &&
                        currentMasterNames[i] != lp.displayName)
                    {
                        currentMasterNames[i] = lp.displayName;
                    }
                }
            }
        }

        if (syncedDtcTargetAllMode)
        {
            // ワールド全員モード: 同意確認パネルで「同意する」を押したプレイヤーのみを出力
            string combinedSource = !string.IsNullOrEmpty(localRecordedDtcConsentedPlayers)
                ? localRecordedDtcConsentedPlayers
                : syncedDtcConsentedPlayers;
            if (combinedSource == null) combinedSource = "";

            if (lastAppliedModeState != 1 || lastAppliedDtcNamesArray == null || lastBuiltDtcConsentedSource != combinedSource)
            {
                lastAppliedModeState = 1;
                lastBuiltDtcConsentedSource = combinedSource;
                lastAppliedDtcNamesArray = BuildDtcConsentedPlayersArray(combinedSource);
            }

            if (currentArr != lastAppliedDtcNamesArray)
            {
                targetUdonBehaviour.SetProgramVariable(jsonNamesVariableName, lastAppliedDtcNamesArray);
            }
        }
        else
        {
            // リスト限定モード: StaffNames (RecordMasterNames) と PlayerNames (cachedOriginalJsonNames) の両方を出力対象にする
            if (lastAppliedModeState != 0 ||
                lastAppliedDtcNamesArray == null ||
                lastBuiltListModePlayerNames != cachedOriginalJsonNames ||
                lastBuiltListModeMasterNames != currentMasterNames)
            {
                lastAppliedModeState = 0;
                lastBuiltListModePlayerNames = cachedOriginalJsonNames;
                lastBuiltListModeMasterNames = currentMasterNames;
                lastBuiltDtcConsentedSource = null;
                lastAppliedDtcNamesArray = BuildListModeDtcTargetArray(cachedOriginalJsonNames, currentMasterNames);
            }

            if (currentArr != lastAppliedDtcNamesArray)
            {
                targetUdonBehaviour.SetProgramVariable(jsonNamesVariableName, lastAppliedDtcNamesArray);
            }
        }

        // Gist のダウンロード完了等でリストが更新された瞬間に、主催者パネル表示・アンケート対象判定・DTC LoopEvent を即時更新
        if (masterListChanged || playerListChanged)
        {
            RefreshMasterPanelVisibility();
            CheckAndRestoreSurveyForLocalUser();
            CheckAndRestoreDtcConsentForLocalUser();
            EnsureDtcLoopEventRunning();
        }
    }

    /// <summary>
    /// 回答完了・同意辞退を安全に記録し、段階的リトライを伴ってシリアライズ送信する
    /// </summary>
    private void SubmitAnswerAndSerialize(string playerName)
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;

        // 安全に自身の名前をローカルの syncedAnsweredPlayers に追加
        AppendAnsweredPlayer(playerName);

        if (localPlayer != null && localPlayer.IsValid())
        {
            pendingSerialization = true;
            serializationRetryCount = 0;

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(localPlayer, gameObject);
            }
            else
            {
                RequestSerialization();
            }

            // オーナー権移譲中・ネットワーク遅延を考慮し、段階的にリトライ実行して確実にシリアライズを送出
            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 0.2f);
            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 0.5f);
            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 1.0f);
            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 2.0f);
        }

        // 回答完了のトリガーを全体通知
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnReceiveSurveyResultLogNet));
        SendCustomEventDelayedSeconds(nameof(TriggerDelayedLogBroadcast), 0.3f);
    }

    private void AppendAnsweredPlayer(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return;
        string cleanName = playerName.Trim();

        if (string.IsNullOrEmpty(syncedAnsweredPlayers))
        {
            syncedAnsweredPlayers = ",";
        }
        string key = cleanName.ToLower() + ",";
        if (!syncedAnsweredPlayers.ToLower().Contains("," + key))
        {
            syncedAnsweredPlayers += cleanName + ",";
        }
    }

    private bool IsPlayerPresentInInstance(string pName)
    {
        if (string.IsNullOrEmpty(pName)) return false;
        string cleanPNameNorm = NormalizePlayerNameForMatch(pName);

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null || !localPlayer.IsValid())
        {
            if (cleanPNameNorm == "localuser") return true;
            if (allowedUserNames != null)
            {
                for (int i = 0; i < allowedUserNames.Length; i++)
                {
                    if (allowedUserNames[i] != null && NormalizePlayerNameForMatch(allowedUserNames[i]) == cleanPNameNorm) return true;
                }
            }
            return false;
        }

        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].IsValid())
            {
                if (players[i].displayName != null && NormalizePlayerNameForMatch(players[i].displayName) == cleanPNameNorm) return true;
            }
        }
        return false;
    }

    private int GetPresentTargetUserCount()
    {
        if (syncedTargetAllMode)
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer == null || !localPlayer.IsValid()) return 1;

            VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            VRCPlayerApi.GetPlayers(players);

            int count = 0;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsValid())
                {
                    string pName = players[i].displayName;
                    if (!string.IsNullOrEmpty(pName) && !CheckIfRecordMaster(pName))
                    {
                        count++;
                    }
                }
            }
            return count;
        }
        else
        {
            return GetPresentJsonNamesUserCount();
        }
    }

    private int GetPresentJsonNamesUserCount()
    {
        string[] targetNames = GetListModeJsonNames();
        if (targetNames == null || targetNames.Length == 0)
        {
            targetNames = allowedUserNames;
        }

        if (targetNames == null || targetNames.Length == 0) return 0;

        int presentCount = 0;
        for (int i = 0; i < targetNames.Length; i++)
        {
            string pName = targetNames[i];
            if (!string.IsNullOrEmpty(pName) && !CheckIfRecordMaster(pName) && IsPlayerPresentInInstance(pName))
            {
                presentCount++;
            }
        }
        return presentCount;
    }

    public void OnSelectModeListButton()
    {
        SetTargetMode(false);
    }

    public void OnSelectModeAllButton()
    {
        SetTargetMode(true);
    }

    private void SetTargetMode(bool isAllMode)
    {
        EnsureUIReferences();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string playerName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        if (!CheckIfRecordMaster(playerName))
        {
            Debug.LogWarning("[SurveyManager] 配信モード変更権限がありません。");
            return;
        }

        if (syncedSurveyStarted)
        {
            if (masterWarningText != null)
            {
                masterWarningText.text = "<color=#FFB300><b>⚠️ アンケート配信中はモード変更できません</b></color>";
            }
            return;
        }

        if (syncedTargetAllMode == isAllMode) return;

        if (localPlayer != null && localPlayer.IsValid())
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(localPlayer, gameObject);
            }
        }

        syncedTargetAllMode = isAllMode;
        syncedDtcTargetAllMode = isAllMode;
        SyncDtcTargetsToUdonBehaviour();
        RequestSerialization();

        string modeLabel = syncedTargetAllMode ? "🌐 ワールド全員 (同意画面・固定表示)" : "🎯 リスト限定 (JsonNames)";
        if (masterWarningText != null)
        {
            masterWarningText.text = $"<color=#4FE369><b>配信対象モードを 【{modeLabel}】 に変更しました</b></color>";
        }

        RefreshMasterPanelVisibility();
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(RefreshMasterPanelVisibility));

        if (isAllMode)
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ShowFixedConsentPanelForEveryone));
        }
        else
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(HideFixedConsentPanelForEveryone));
        }
    }

    public void OnToggleTargetModeButton()
    {
        SetTargetMode(!syncedTargetAllMode);
    }

    // ==========================================
    // ワールド固定位置 同意確認パネル (MVP_DTC) 制御
    // ==========================================

    public void ShowFixedConsentPanelForEveryone()
    {
        EnsureUIReferences();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string localName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        // すでに同意または辞退している場合は絶対に再表示しない
        if (hasDtcResponded || CheckIfDtcResponded(localName))
        {
            if (containerDtcConsent != null && containerDtcConsent.activeSelf) containerDtcConsent.SetActive(false);
            return;
        }

        if (containerDtcConsent != null)
        {
            // 最前面に表示
            containerDtcConsent.transform.SetAsLastSibling();
            containerDtcConsent.SetActive(true);

            DisablePhysicalCollisions();
            SendCustomEventDelayedFrames(nameof(DisablePhysicalCollisions), 1);
            SendCustomEventDelayedSeconds(nameof(DisablePhysicalCollisions), 0.2f);
            SendCustomEventDelayedSeconds(nameof(DisablePhysicalCollisions), 0.5f);

            Image bgImg = containerDtcConsent.GetComponent<Image>();
            if (bgImg != null) bgImg.raycastTarget = false;

            if (dtcConsentAgreeButton != null)
            {
                dtcConsentAgreeButton.interactable = true;
                Image agreeImg = dtcConsentAgreeButton.GetComponent<Image>();
                if (agreeImg != null) agreeImg.raycastTarget = true;
            }

            if (dtcConsentDisagreeButton != null)
            {
                dtcConsentDisagreeButton.interactable = true;
                Image disagreeImg = dtcConsentDisagreeButton.GetComponent<Image>();
                if (disagreeImg != null) disagreeImg.raycastTarget = true;
            }

            if (dtcConsentTitleDisplay != null)
            {
                dtcConsentTitleDisplay.text = !string.IsNullOrEmpty(dtcConsentTitleText) ? dtcConsentTitleText : "【位置・視線データ収集に関する同意確認】";
            }

            if (dtcConsentNoticeDisplay != null)
            {
                dtcConsentNoticeDisplay.text = dtcConsentNoticeText;
            }
        }
    }

    public void HideFixedConsentPanelForEveryone()
    {
        EnsureUIReferences();

        if (containerDtcConsent != null)
        {
            containerDtcConsent.SetActive(false);
        }
    }

    public void OnDtcConsentAgree()
    {
        EnsureUIReferences();

        hasDtcResponded = true;

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string playerName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        if (string.IsNullOrEmpty(localRecordedDtcConsentedPlayers))
        {
            localRecordedDtcConsentedPlayers = ",";
        }
        string nameKey = playerName + ",";
        if (!localRecordedDtcConsentedPlayers.Contains("," + nameKey))
        {
            localRecordedDtcConsentedPlayers += nameKey;
        }

        if (string.IsNullOrEmpty(syncedDtcConsentedPlayers))
        {
            syncedDtcConsentedPlayers = ",";
        }
        if (!syncedDtcConsentedPlayers.Contains("," + nameKey))
        {
            syncedDtcConsentedPlayers += nameKey;
        }

        MergeLocalDtcRecordsIntoSynced();
        SyncDtcTargetsToUdonBehaviour();

        if (localPlayer != null && localPlayer.IsValid())
        {
            pendingSerialization = true;
            serializationRetryCount = 0;

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(localPlayer, gameObject);
            }
            else
            {
                RequestSerialization();
            }

            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 0.2f);
            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 0.5f);
            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 1.0f);
            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 2.0f);
        }

        Debug.LogWarning($"★ [MVP_DTC] {playerName}: 同意（データ記録対象に登録されました）");

        if (containerDtcConsent != null) containerDtcConsent.SetActive(false);

        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnReceiveDtcStatusUpdateNet));
        SendCustomEventDelayedSeconds(nameof(TriggerDelayedDtcBroadcast), 0.3f);
        SendCustomEventDelayedSeconds(nameof(TriggerDelayedDtcBroadcast), 1.0f);
    }

    public void OnDtcConsentDisagree()
    {
        EnsureUIReferences();

        hasDtcResponded = true;

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string playerName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        if (string.IsNullOrEmpty(localRecordedDtcDisagreedPlayers))
        {
            localRecordedDtcDisagreedPlayers = ",";
        }
        string nameKey = playerName + ",";
        if (!localRecordedDtcDisagreedPlayers.Contains("," + nameKey))
        {
            localRecordedDtcDisagreedPlayers += nameKey;
        }

        if (string.IsNullOrEmpty(syncedDtcDisagreedPlayers))
        {
            syncedDtcDisagreedPlayers = ",";
        }
        if (!syncedDtcDisagreedPlayers.Contains("," + nameKey))
        {
            syncedDtcDisagreedPlayers += nameKey;
        }

        MergeLocalDtcRecordsIntoSynced();
        SyncDtcTargetsToUdonBehaviour();

        if (localPlayer != null && localPlayer.IsValid())
        {
            pendingSerialization = true;
            serializationRetryCount = 0;

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(localPlayer, gameObject);
            }
            else
            {
                RequestSerialization();
            }

            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 0.2f);
            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 0.5f);
            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 1.0f);
            SendCustomEventDelayedSeconds(nameof(RetrySerialization), 2.0f);
        }

        Debug.LogWarning($"★ [MVP_DTC] {playerName}: 同意辞退（データ記録対象外）");

        if (containerDtcConsent != null) containerDtcConsent.SetActive(false);

        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnReceiveDtcStatusUpdateNet));
        SendCustomEventDelayedSeconds(nameof(TriggerDelayedDtcBroadcast), 0.3f);
        SendCustomEventDelayedSeconds(nameof(TriggerDelayedDtcBroadcast), 1.0f);
    }

    public void TriggerDelayedDtcBroadcast()
    {
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnReceiveDtcStatusUpdateNet));
    }

    public void OnReceiveDtcStatusUpdateNet()
    {
        SyncDtcPlayersToLocalList();
        SyncDtcTargetsToUdonBehaviour();
        RefreshMasterPanelVisibility();
    }

    public bool CheckIfDtcResponded(string playerName)
    {
        if (hasDtcResponded) return true;
        if (string.IsNullOrEmpty(playerName)) return false;
        string nameKey = "," + playerName.Trim().ToLower() + ",";

        bool isAgreed = (!string.IsNullOrEmpty(syncedDtcConsentedPlayers) && syncedDtcConsentedPlayers.ToLower().Contains(nameKey)) ||
                        (!string.IsNullOrEmpty(localRecordedDtcConsentedPlayers) && localRecordedDtcConsentedPlayers.ToLower().Contains(nameKey));
        bool isDisagreed = (!string.IsNullOrEmpty(syncedDtcDisagreedPlayers) && syncedDtcDisagreedPlayers.ToLower().Contains(nameKey)) ||
                           (!string.IsNullOrEmpty(localRecordedDtcDisagreedPlayers) && localRecordedDtcDisagreedPlayers.ToLower().Contains(nameKey));

        return isAgreed || isDisagreed;
    }

    public bool IsUserDtcTarget(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return false;

        if (syncedDtcTargetAllMode)
        {
            string nameKey = "," + playerName.Trim().ToLower() + ",";
            return (!string.IsNullOrEmpty(syncedDtcConsentedPlayers) && syncedDtcConsentedPlayers.ToLower().Contains(nameKey)) ||
                   (!string.IsNullOrEmpty(localRecordedDtcConsentedPlayers) && localRecordedDtcConsentedPlayers.ToLower().Contains(nameKey));
        }
        else
        {
            // リスト限定モード時は StaffNames (RecordMaster) または PlayerNames (JsonNamesString) の両方を出力対象とする
            if (CheckIfRecordMaster(playerName)) return true;
            return CheckIfUserInVariable(playerName, jsonNamesVariableName);
        }
    }

    public void CheckAndRestoreDtcConsentForLocalUser()
    {
        EnsureUIReferences();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string localName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        // すでに同意または辞退している場合は絶対に再表示しない
        if (hasDtcResponded || CheckIfDtcResponded(localName))
        {
            if (containerDtcConsent != null && containerDtcConsent.activeSelf)
            {
                containerDtcConsent.SetActive(false);
            }
            return;
        }

        if (syncedDtcTargetAllMode)
        {
            if (containerDtcConsent != null)
            {
                containerDtcConsent.transform.SetAsLastSibling();
                containerDtcConsent.SetActive(true);

                DisablePhysicalCollisions();
                SendCustomEventDelayedFrames(nameof(DisablePhysicalCollisions), 1);
                SendCustomEventDelayedSeconds(nameof(DisablePhysicalCollisions), 0.2f);
                SendCustomEventDelayedSeconds(nameof(DisablePhysicalCollisions), 0.5f);

                Image bgImg = containerDtcConsent.GetComponent<Image>();
                if (bgImg != null) bgImg.raycastTarget = false;

                if (dtcConsentAgreeButton != null)
                {
                    dtcConsentAgreeButton.interactable = true;
                    Image agreeImg = dtcConsentAgreeButton.GetComponent<Image>();
                    if (agreeImg != null) agreeImg.raycastTarget = true;
                }

                if (dtcConsentDisagreeButton != null)
                {
                    dtcConsentDisagreeButton.interactable = true;
                    Image disagreeImg = dtcConsentDisagreeButton.GetComponent<Image>();
                    if (disagreeImg != null) disagreeImg.raycastTarget = true;
                }

                if (dtcConsentTitleDisplay != null)
                {
                    dtcConsentTitleDisplay.text = !string.IsNullOrEmpty(dtcConsentTitleText) ? dtcConsentTitleText : "【位置・視線データ収集に関する同意確認】";
                }

                if (dtcConsentNoticeDisplay != null)
                {
                    dtcConsentNoticeDisplay.text = dtcConsentNoticeText;
                }
            }
        }
        else
        {
            if (containerDtcConsent != null && containerDtcConsent.activeSelf)
            {
                containerDtcConsent.SetActive(false);
            }
        }
    }

    public void UpdateMasterStatusText()
    {
        EnsureUIReferences();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string localName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";
        bool isMaster = CheckIfRecordMaster(localName);

        if (masterControlPanel != null)
        {
            masterControlPanel.SetActive(isMaster);
        }

        if (!isMaster) return;

        string[] targetNames = null;
        if (syncedTargetAllMode)
        {
            VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            VRCPlayerApi.GetPlayers(players);

            string namesCombined = "";
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsValid())
                {
                    string pName = players[i].displayName;
                    if (!string.IsNullOrEmpty(pName) && !CheckIfRecordMaster(pName))
                    {
                        if (string.IsNullOrEmpty(namesCombined)) namesCombined = pName;
                        else namesCombined += ";" + pName;
                    }
                }
            }

            if (!string.IsNullOrEmpty(syncedAnsweredPlayers))
            {
                string[] answeredArr = syncedAnsweredPlayers.Split(',');
                for (int i = 0; i < answeredArr.Length; i++)
                {
                    string aName = answeredArr[i].Trim();
                    if (!string.IsNullOrEmpty(aName) && !CheckIfRecordMaster(aName))
                    {
                        if (!namesCombined.Contains(aName))
                        {
                            if (string.IsNullOrEmpty(namesCombined)) namesCombined = aName;
                            else namesCombined += ";" + aName;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(masterRecordedAnsweredPlayers))
            {
                string[] masterArr = masterRecordedAnsweredPlayers.Split(',');
                for (int i = 0; i < masterArr.Length; i++)
                {
                    string mName = masterArr[i].Trim();
                    if (!string.IsNullOrEmpty(mName) && !CheckIfRecordMaster(mName))
                    {
                        if (!namesCombined.Contains(mName))
                        {
                            if (string.IsNullOrEmpty(namesCombined)) namesCombined = mName;
                            else namesCombined += ";" + mName;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(namesCombined))
            {
                targetNames = namesCombined.Split(';');
            }
        }
        else
        {
            targetNames = GetListModeJsonNames();
            if (targetNames == null || targetNames.Length == 0)
            {
                targetNames = allowedUserNames;
            }
        }

        int totalTargetCount = 0;
        int presentTargetCount = 0;
        int answeredCount = 0;
        int unAnsweredCount = 0;

        string unAnsweredList = "";
        string answeredList = "";

        if (targetNames != null && targetNames.Length > 0)
        {
            for (int i = 0; i < targetNames.Length; i++)
            {
                string pName = targetNames[i];
                if (string.IsNullOrEmpty(pName)) continue;

                // RecordMaster(主催者・進行役)は回答対象外のためカウントおよび未回答リストから完全に除外
                if (CheckIfRecordMaster(pName)) continue;

                totalTargetCount++;
                bool isPresent = IsPlayerPresentInInstance(pName);

                bool isDone = IsPlayerAnswered(pName);

                if (isPresent)
                {
                    presentTargetCount++;
                    if (isDone)
                    {
                        answeredCount++;
                        answeredList += $"・ {pName} <color=#4FE369>【済】</color>\n";
                    }
                    else
                    {
                        unAnsweredCount++;
                        unAnsweredList += $"・ {pName} <color=#FFB300>【未回答】</color>\n";
                    }
                }
                else
                {
                    if (isDone)
                    {
                        answeredList += $"・ {pName} <color=#4FE369>【済 (退室)】</color>\n";
                    }
                }
            }
        }

        if (masterStatusText != null)
        {
            string modeTitle = syncedTargetAllMode ? "🌐 ワールド全員モード (同意画面あり)" : "🎯 リスト限定モード (JsonNames)";
            string jsonInfo = !string.IsNullOrEmpty(onlineJsonStatusMessage)
                ? $"<b>【質問データ: {onlineJsonStatusMessage}】</b>\n"
                : "";

            if (totalTargetCount == 0 && !syncedTargetAllMode)
            {
                masterStatusText.text = jsonInfo + $"<b>【配信対象: {modeTitle}】</b>\n対象ユーザーリスト (JsonNamesString) が空です。";
            }
            else
            {
                int percent = (presentTargetCount > 0) ? Mathf.RoundToInt((float)answeredCount / presentTargetCount * 100f) : 0;
                string header = jsonInfo + $"<b>【配信対象: {modeTitle}】</b>\n<b>【滞在中回答進捗: {answeredCount} / {presentTargetCount} 人 ({percent}%)】</b>\n\n";

                string body = "";
                if (unAnsweredCount > 0)
                {
                    body += $"<b>▼ 未回答のプレイヤー (滞在中: {unAnsweredCount}名):</b>\n" + unAnsweredList;
                    if (answeredCount > 0)
                    {
                        body += "\n<b>▼ 回答済みのプレイヤー:</b>\n" + answeredList;
                    }
                }
                else if (presentTargetCount > 0)
                {
                    body += "<b>🎉 滞在中の対象者の回答が完了しました！</b>\n\n" + answeredList;
                }
                else
                {
                    body += "<b>⚠️ ワールド内に対象プレイヤーがいません。</b>\n";
                }

                masterStatusText.text = header + body;
            }
        }

        SetWarningDisplay(totalTargetCount, presentTargetCount, answeredCount, unAnsweredCount);
    }

    private void SetWarningDisplay(int totalTargetCount, int presentCount, int answeredCount, int unAnsweredCount)
    {
        EnsureUIReferences();

        string msg = "";
        Color textColor = Color.white;

        if (totalTargetCount == 0 && !syncedTargetAllMode)
        {
            msg = "⚠️ JsonNamesStringのリストが空です";
            textColor = new Color(1.0f, 0.7f, 0.0f, 1.0f);
        }
        else if (presentCount == 0)
        {
            msg = "⚠️ 開始できません\n\n回答対象のプレイヤーが\nワールド内に存在しません。";
            textColor = new Color(1.0f, 0.35f, 0.35f, 1.0f);
        }
        else
        {
            string modeName = syncedTargetAllMode ? "ワールド全員" : "リスト限定";
            msg = $"▶ [{modeName}] 対象 {presentCount}名 滞在中\n\n（準備完了）";
            textColor = new Color(0.3f, 0.9f, 0.4f, 1.0f);
        }

        if (masterWarningText != null)
        {
            masterWarningText.gameObject.SetActive(true);
            if (masterWarningText.transform.parent != null)
            {
                masterWarningText.transform.parent.gameObject.SetActive(true);
            }
            masterWarningText.color = textColor;
            masterWarningText.text = msg;
        }

        if (toggleTargetModeButton != null)
        {
            Text toggleTxt = toggleTargetModeButton.GetComponentInChildren<Text>();
            toggleTargetModeButton.interactable = !syncedSurveyStarted;

            if (syncedSurveyStarted)
            {
                if (syncedTargetAllMode)
                {
                    if (toggleTxt != null)
                    {
                        toggleTxt.text = "🌐 ワールド全員 (配信中・固定)";
                        toggleTxt.color = new Color(0.65f, 0.65f, 0.65f, 1.0f);
                    }
                }
                else
                {
                    if (toggleTxt != null)
                    {
                        toggleTxt.text = "🎯 リスト限定 (配信中・固定)";
                        toggleTxt.color = new Color(0.65f, 0.65f, 0.65f, 1.0f);
                    }
                }
            }
            else
            {
                if (syncedTargetAllMode)
                {
                    if (toggleTxt != null)
                    {
                        toggleTxt.text = "🌐 対象: ワールド全員 (同意画面)";
                        toggleTxt.color = Color.cyan;
                    }
                }
                else
                {
                    if (toggleTxt != null)
                    {
                        toggleTxt.text = "🎯 対象: リスト限定 (JsonNames)";
                        toggleTxt.color = Color.white;
                    }
                }
            }
        }

        if (targetModeListButton != null)
        {
            Text listTxt = targetModeListButton.GetComponentInChildren<Text>();
            targetModeListButton.interactable = !syncedSurveyStarted;
            Image listImg = targetModeListButton.GetComponent<Image>();

            if (syncedTargetAllMode)
            {
                if (listTxt != null)
                {
                    listTxt.text = "⚪ リスト限定";
                    listTxt.color = syncedSurveyStarted ? new Color(0.5f, 0.5f, 0.5f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f);
                }
                if (listImg != null) listImg.color = new Color(0.2f, 0.22f, 0.28f, 1f);
            }
            else
            {
                if (listTxt != null)
                {
                    listTxt.text = "🔘 🎯 リスト限定";
                    listTxt.color = syncedSurveyStarted ? new Color(0.7f, 0.9f, 0.7f, 1f) : Color.white;
                }
                if (listImg != null) listImg.color = syncedSurveyStarted ? new Color(0.15f, 0.45f, 0.25f, 1f) : new Color(0.2f, 0.65f, 0.35f, 1f);
            }
        }

        if (targetModeAllButton != null)
        {
            Text allTxt = targetModeAllButton.GetComponentInChildren<Text>();
            targetModeAllButton.interactable = !syncedSurveyStarted;
            Image allImg = targetModeAllButton.GetComponent<Image>();

            if (syncedTargetAllMode)
            {
                if (allTxt != null)
                {
                    allTxt.text = "🔘 🌐 ワールド全員";
                    allTxt.color = syncedSurveyStarted ? new Color(0.7f, 0.9f, 0.9f, 1f) : Color.white;
                }
                if (allImg != null) allImg.color = syncedSurveyStarted ? new Color(0.15f, 0.45f, 0.55f, 1f) : new Color(0.2f, 0.55f, 0.75f, 1f);
            }
            else
            {
                if (allTxt != null)
                {
                    allTxt.text = "⚪ ワールド全員";
                    allTxt.color = syncedSurveyStarted ? new Color(0.5f, 0.5f, 0.5f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f);
                }
                if (allImg != null) allImg.color = new Color(0.2f, 0.22f, 0.28f, 1f);
            }
        }

        if (startSurveyButton != null)
        {
            Text btnTxt = startSurveyButton.GetComponentInChildren<Text>();
            
            // ワールド内に対象の一般プレイヤーが0名の場合は一斉開始ボタンを確実に非活性化
            if (presentCount == 0)
            {
                startSurveyButton.interactable = false;
                if (btnTxt != null)
                {
                    btnTxt.text = "⚠️ 対象プレイヤー不在 (開始不可)";
                    btnTxt.color = new Color(0.65f, 0.65f, 0.65f, 1.0f);
                }
            }
            else
            {
                // 滞在中の回答対象者が1名以上存在し、かつ全員の回答が完了していない時のみ非活性化
                bool isWaitingForAnswers = syncedSurveyStarted && answeredCount < presentCount;

                if (isWaitingForAnswers)
                {
                    startSurveyButton.interactable = false;
                    if (btnTxt != null)
                    {
                        btnTxt.text = $"⏳ 回答受付中 ({answeredCount}/{presentCount})";
                        btnTxt.color = new Color(0.85f, 0.85f, 0.85f, 1.0f);
                    }
                }
                else
                {
                    // 全員回答完了、またはリセットされた場合は活性化
                    startSurveyButton.interactable = true;
                    if (btnTxt != null)
                    {
                        if (syncedSurveyStarted && answeredCount >= presentCount)
                        {
                            btnTxt.text = "🎉 回答完了 (再開始可能)";
                            btnTxt.color = Color.cyan;
                        }
                        else
                        {
                            btnTxt.text = "▶ アンケート一斉開始";
                            btnTxt.color = Color.white;
                        }
                    }
                }
            }
        }

        if (reopenSurveyButton != null)
        {
            Text reopenTxt = reopenSurveyButton.GetComponentInChildren<Text>();
            if (syncedSurveyStarted)
            {
                // アンケート一斉開始後のみ「未回答者に再表示」ボタンを有効化
                reopenSurveyButton.interactable = true;
                if (reopenTxt != null)
                {
                    reopenTxt.text = "📋 未回答者に再表示";
                    reopenTxt.color = Color.white;
                }
            }
            else
            {
                // 開始前は非無効化 (非アクティブ化)
                reopenSurveyButton.interactable = false;
                if (reopenTxt != null)
                {
                    reopenTxt.text = "📋 未回答者に再表示 (開始前)";
                    reopenTxt.color = new Color(0.6f, 0.6f, 0.6f, 1.0f);
                }
            }
        }
    }

    public void OnStartSurveyButton()
    {
        EnsureUIReferences();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string playerName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        if (!CheckIfRecordMaster(playerName))
        {
            Debug.LogWarning("[SurveyManager] アンケート開始権限がありません。");
            return;
        }

        int presentCount = GetPresentTargetUserCount();

        if (presentCount == 0)
        {
            Debug.LogWarning("[SurveyManager] ボタン押下時: 対象プレイヤーがワールド内に存在しません。");
            if (masterControlPanel != null) masterControlPanel.SetActive(true);
            return; 
        }

        // 一斉開始フラグをセットして全員に同期
        if (localPlayer != null && localPlayer.IsValid())
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(localPlayer, gameObject);
            }
        }
        syncedSurveyStarted = true;
        RequestSerialization();

        if (startSurveyButton != null)
        {
            startSurveyButton.interactable = false;
            Text btnTxt = startSurveyButton.GetComponentInChildren<Text>();
            if (btnTxt != null)
            {
                btnTxt.text = "⏳ 回答配信中...";
                btnTxt.color = new Color(0.85f, 0.85f, 0.85f, 1.0f);
            }
        }

        if (masterWarningText != null)
        {
            masterWarningText.text = "<color=#4FE369><b>▶ アンケートを一斉配信しました</b></color>";
        }

        if (masterControlPanel != null) masterControlPanel.SetActive(true);

        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(StartSurveyForEveryone));
    }

    /// <summary>
    /// RecordMaster専用: アンケート状態の一括リセットボタンイベント
    /// </summary>
    public void OnResetSurveyButton()
    {
        EnsureUIReferences();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string playerName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        if (!CheckIfRecordMaster(playerName))
        {
            Debug.LogWarning("[SurveyManager] アンケートリセット権限がありません。");
            return;
        }

        if (localPlayer != null && localPlayer.IsValid())
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(localPlayer, gameObject);
            }
        }

        syncedSurveyStarted = false;
        syncedAnsweredPlayers = "";
        syncedLatestResultLog = "";
        hasAnswered = false;
        masterRecordedAnsweredPlayers = "";
        pendingSerialization = false;
        syncedDtcConsentedPlayers = "";
        syncedDtcDisagreedPlayers = "";
        localRecordedDtcConsentedPlayers = "";
        localRecordedDtcDisagreedPlayers = "";
        hasDtcResponded = false;
        SyncDtcTargetsToUdonBehaviour();
        RequestSerialization();

        if (masterWarningText != null)
        {
            masterWarningText.text = "<color=#FFB300><b>🔄 アンケート状態を初期リセットしました</b></color>";
        }

        if (masterControlPanel != null) masterControlPanel.SetActive(true);

        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ResetSurveyForEveryone));
    }

    public void ResetSurveyForEveryone()
    {
        EnsureUIReferences();

        hasAnswered = false;
        hasDtcResponded = false;
        syncedDtcConsentedPlayers = "";
        syncedDtcDisagreedPlayers = "";
        localRecordedDtcConsentedPlayers = "";
        localRecordedDtcDisagreedPlayers = "";
        pendingSerialization = false;
        currentIndex = 0;

        SyncDtcTargetsToUdonBehaviour();
        CheckAndRestoreDtcConsentForLocalUser();

        // 全回答者の画面からアンケートパネルおよび完了結果パネルを即座に消す（非表示）
        if (surveyPanel != null) surveyPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);

        // リセット時にオンラインJSONも再取得して最新の質問を反映
        isOnlineJsonLoaded = false;
        jsonLoadRetryCount = 0;
        LoadSurveyJsonFromUrl();

        RefreshMasterPanelVisibility();
    }

    public void StartSurveyForEveryone()
    {
        EnsureUIReferences();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string playerName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        // 1. RecordMasterNames に含まれているプレイヤー(主催者)にはアンケート画面を出さず、管理パネルを表示維持
        bool isRecordMaster = CheckIfRecordMaster(playerName);
        if (isRecordMaster)
        {
            Debug.Log($"[SurveyManager] {playerName} は RecordMaster のためアンケート画面は出さず、管理パネルを表示維持します。");
            if (surveyPanel != null) surveyPanel.SetActive(false);
            if (masterControlPanel != null) masterControlPanel.SetActive(true);
            UpdateMasterStatusText();
            return;
        }

        // 2. 一般参加者(RecordMasterでない人)のローカルでは管理パネルを必ず非表示にする
        if (masterControlPanel != null) masterControlPanel.SetActive(false);

        // 3. モード別対象者チェック
        bool isTargetForSurvey = false;
        if (syncedTargetAllMode)
        {
            isTargetForSurvey = !isRecordMaster;
        }
        else
        {
            isTargetForSurvey = CheckIfUserInVariable(playerName, jsonNamesVariableName);
        }

        if (!isTargetForSurvey)
        {
            Debug.Log($"[SurveyManager] {playerName} はアンケート配信対象外のためアンケートを表示しません。");
            if (surveyPanel != null) surveyPanel.SetActive(false);
            return;
        }

        CheckIfAlreadyAnswered();

        if (hasAnswered)
        {
            if (surveyPanel != null) surveyPanel.SetActive(false);
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
                if (resultMessageText != null)
                {
                    resultMessageText.text = "ご回答は既に送信されています。\n（1回のみ回答可能です）";
                }
            }
            return;
        }

        PositionSurveyInFrontOfPlayer();
        InitializeSurvey();
    }

    public void PositionPanelInFrontOfPlayer(GameObject targetPanel)
    {
        if (targetPanel == null) return;

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer != null && localPlayer.IsValid())
        {
            VRCPlayerApi.TrackingData headData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 forwardDir = headData.rotation * Vector3.forward;

            forwardDir.y = 0;
            if (forwardDir.sqrMagnitude > 0.001f)
            {
                forwardDir.Normalize();
            }
            else
            {
                forwardDir = Vector3.forward;
            }

            targetPanel.transform.position = headData.position + forwardDir * 1.5f;
            targetPanel.transform.rotation = Quaternion.LookRotation(forwardDir);

            // プレイヤーの目の前に出現したパネルによる物理的な引っかかりを防止
            DisablePhysicalCollisions();
        }
    }

    /// <summary>
    /// アンケートパネルおよび子要素のコライダーによる物理的な引っかかりを完全に防止する
    /// レイヤーは変更せず (Defaultのまま)、全てのコライダーを isTrigger = true に設定して物理衝突を無効化
    /// </summary>
    public void DisablePhysicalCollisions()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].isTrigger = true;
                }
            }
        }

        if (containerDtcConsent == null)
        {
            GameObject dtcObj = GameObject.Find("Container_DtcConsent");
            if (dtcObj != null) containerDtcConsent = dtcObj;
        }

        if (containerDtcConsent != null)
        {
            Collider[] dtcColliders = containerDtcConsent.GetComponentsInChildren<Collider>(true);
            if (dtcColliders != null)
            {
                for (int i = 0; i < dtcColliders.Length; i++)
                {
                    if (dtcColliders[i] != null)
                    {
                        dtcColliders[i].isTrigger = true;
                    }
                }
            }
        }
    }

    private void PositionSurveyInFrontOfPlayer()
    {
        PositionPanelInFrontOfPlayer(gameObject);
    }

    public void InitializeSurvey()
    {
        EnsureUIReferences();
        currentIndex = 0;
        DisablePhysicalCollisions();

        if (syncedTargetAllMode)
        {
            // インスタンス全員モードの場合、最初に同意確認画面を表示
            if (surveyPanel != null) surveyPanel.SetActive(true);
            if (resultPanel != null) resultPanel.SetActive(false);

            if (containerChoice != null) containerChoice.SetActive(false);
            if (containerRating != null) containerRating.SetActive(false);
            if (containerTextInput != null) containerTextInput.SetActive(false);
            if (containerGrid != null) containerGrid.SetActive(false);

            if (containerConsent != null)
            {
                containerConsent.SetActive(true);
                if (consentNoticeDisplay != null)
                {
                    consentNoticeDisplay.text = consentNoticeText;
                }
            }

            if (titleText != null)
            {
                RectTransform tRt = titleText.GetComponent<RectTransform>();
                if (tRt != null)
                {
                    tRt.anchoredPosition = new Vector2(0f, 220f);
                    tRt.sizeDelta = new Vector2(900f, 60f);
                }
                titleText.fontSize = 36;
                titleText.text = !string.IsNullOrEmpty(consentTitleText) ? consentTitleText : "アンケート参加の同意確認";
            }
            if (categoryDescriptionText != null)
            {
                categoryDescriptionText.gameObject.SetActive(false);
            }
            if (questionText != null)
            {
                RectTransform qRt = questionText.GetComponent<RectTransform>();
                if (qRt != null)
                {
                    qRt.anchoredPosition = new Vector2(0f, 130f);
                    qRt.sizeDelta = new Vector2(900f, 100f);
                }
                questionText.fontSize = 32;
                questionText.text = "以下の項目をご確認の上、同意ボタンを選択してください。";
            }
        }
        else
        {
            // リスト限定モードの場合、同意画面をスキップして質問1から開始
            if (containerConsent != null) containerConsent.SetActive(false);

            if (questionTexts != null && questionTexts.Length > 0)
            {
                userAnswers = new int[questionTexts.Length];
                userTextAnswers = new string[questionTexts.Length];
                BuildQuestionOrder();

                if (resultPanel != null) resultPanel.SetActive(false);
                if (surveyPanel != null) surveyPanel.SetActive(true);

                ShowQuestion(currentIndex);
            }
            else
            {
                Debug.LogWarning("[SurveyManager] 質問データが設定されていません。");
            }
        }
    }

    /// <summary>
    /// 出題順配列 questionOrder を構築する（選択式 -> 選択式（グリッド） -> スライダー評価 -> 自由記述 の順、各項目内でランダム並び替え設定を反映）
    /// </summary>
    private void BuildQuestionOrder()
    {
        int qCount = (questionTexts != null) ? questionTexts.Length : 0;
        questionOrder = new int[qCount];
        if (qCount == 0) return;

        int[] choiceIndices = new int[qCount];
        int choiceLen = 0;
        int[] gridIndices = new int[qCount];
        int gridLen = 0;
        int[] sliderIndices = new int[qCount];
        int sliderLen = 0;
        int[] textIndices = new int[qCount];
        int textLen = 0;

        for (int i = 0; i < qCount; i++)
        {
            int t = (answerTypes != null && i < answerTypes.Length) ? answerTypes[i] : 0;
            if (t == 0)
            {
                choiceIndices[choiceLen] = i;
                choiceLen++;
            }
            else if (t == 3)
            {
                gridIndices[gridLen] = i;
                gridLen++;
            }
            else if (t == 1)
            {
                sliderIndices[sliderLen] = i;
                sliderLen++;
            }
            else
            {
                textIndices[textLen] = i;
                textLen++;
            }
        }

        if (shuffleQuestionsWithinCategory)
        {
            if (choiceLen > 1)
            {
                for (int i = choiceLen - 1; i > 0; i--)
                {
                    int r = Random.Range(0, i + 1);
                    int tmp = choiceIndices[i];
                    choiceIndices[i] = choiceIndices[r];
                    choiceIndices[r] = tmp;
                }
            }

            if (gridLen > 1)
            {
                for (int i = gridLen - 1; i > 0; i--)
                {
                    int r = Random.Range(0, i + 1);
                    int tmp = gridIndices[i];
                    gridIndices[i] = gridIndices[r];
                    gridIndices[r] = tmp;
                }
            }

            if (sliderLen > 1)
            {
                for (int i = sliderLen - 1; i > 0; i--)
                {
                    int r = Random.Range(0, i + 1);
                    int tmp = sliderIndices[i];
                    sliderIndices[i] = sliderIndices[r];
                    sliderIndices[r] = tmp;
                }
            }

            if (textLen > 1)
            {
                for (int i = textLen - 1; i > 0; i--)
                {
                    int r = Random.Range(0, i + 1);
                    int tmp = textIndices[i];
                    textIndices[i] = textIndices[r];
                    textIndices[r] = tmp;
                }
            }
        }

        int pos = 0;
        for (int i = 0; i < choiceLen; i++)
        {
            questionOrder[pos] = choiceIndices[i];
            pos++;
        }
        for (int i = 0; i < gridLen; i++)
        {
            questionOrder[pos] = gridIndices[i];
            pos++;
        }
        for (int i = 0; i < sliderLen; i++)
        {
            questionOrder[pos] = sliderIndices[i];
            pos++;
        }
        for (int i = 0; i < textLen; i++)
        {
            questionOrder[pos] = textIndices[i];
            pos++;
        }
    }

    private int GetActualQuestionIndex(int stepIndex)
    {
        if (questionOrder != null && stepIndex >= 0 && stepIndex < questionOrder.Length)
        {
            return questionOrder[stepIndex];
        }
        return stepIndex;
    }

    public void OnConsentAgree()
    {
        EnsureUIReferences();

        if (containerConsent != null) containerConsent.SetActive(false);

        if (questionTexts != null && questionTexts.Length > 0)
        {
            userAnswers = new int[questionTexts.Length];
            userTextAnswers = new string[questionTexts.Length];
            BuildQuestionOrder();

            ShowQuestion(currentIndex);
        }
        else
        {
            Debug.LogWarning("[SurveyManager] 質問データが設定されていません。");
        }
    }

    public void OnConsentDisagree()
    {
        EnsureUIReferences();

        hasAnswered = true;

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string playerName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        syncedLatestResultLog = $"[MVP_Q] {playerName}: (同意辞退)";

        if (surveyPanel != null) surveyPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(true);

        if (resultMessageText != null)
        {
            resultMessageText.text = "アンケート回答をご辞退されました。\nご協力ありがとうございました。";
        }

        SubmitAnswerAndSerialize(playerName);

        // 辞退後、回答完了パネルを5秒後に自動的に閉じる
        SendCustomEventDelayedSeconds(nameof(HideResultPanel), 5.0f);
    }

    public string GetOptionLabel(int questionIndex, int choiceIndex)
    {
        if (optionLabels == null || optionLabels.Length == 0) return $"選択肢 {choiceIndex + 1}";

        int baseIdx = 0;
        if (optionOffsets != null && questionIndex >= 0 && questionIndex < optionOffsets.Length)
        {
            baseIdx = optionOffsets[questionIndex];
        }
        else
        {
            int qLen = (questionTexts != null && questionTexts.Length > 0) ? questionTexts.Length : 1;
            int stride = optionLabels.Length / qLen;
            if (stride < 1) stride = 6;
            baseIdx = questionIndex * stride;
        }

        int labelIdx = baseIdx + choiceIndex;
        if (labelIdx >= 0 && labelIdx < optionLabels.Length && !string.IsNullOrEmpty(optionLabels[labelIdx]))
        {
            return optionLabels[labelIdx];
        }
        return $"選択肢 {choiceIndex + 1}";
    }

    /// <summary>
    /// 選択肢の数(count)に応じてボタンのサイズ・フォント・グリッド配置を自動調整する
    /// </summary>
    private void LayoutChoiceButtons(int count)
    {
        if (choiceButtons == null || count <= 0) return;

        int activeCount = count < choiceButtons.Length ? count : choiceButtons.Length;

        int cols = 2;
        if (activeCount <= 6) cols = 2;
        else if (activeCount <= 12) cols = 3;
        else cols = 4;

        int rows = (activeCount + cols - 1) / cols;
        if (rows < 1) rows = 1;

        float totalW = 860f;
        float totalH = 260f;
        float gapX = 16f;
        float gapY = rows <= 3 ? 14f : 8f;

        float btnW = (totalW - gapX * (cols - 1)) / cols;
        if (btnW > 400f) btnW = 400f;

        float btnH = (totalH - gapY * (rows - 1)) / rows;
        if (btnH > 65f) btnH = 65f;
        if (btnH < 32f) btnH = 32f;

        int fontSize = 24;
        if (rows == 4) fontSize = 20;
        else if (rows >= 5) fontSize = 16;

        float gridW = cols * btnW + (cols - 1) * gapX;
        float gridH = rows * btnH + (rows - 1) * gapY;
        float startX = -gridW * 0.5f + btnW * 0.5f;
        float startY = gridH * 0.5f - btnH * 0.5f;

        for (int i = 0; i < activeCount; i++)
        {
            if (choiceButtons[i] != null)
            {
                RectTransform rt = choiceButtons[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    int r = i / cols;
                    int c = i % cols;
                    float x = startX + c * (btnW + gapX);
                    float y = startY - r * (btnH + gapY);
                    rt.anchoredPosition = new Vector2(x, y);
                    rt.sizeDelta = new Vector2(btnW, btnH);
                }

                if (choiceButtonTexts != null && i < choiceButtonTexts.Length && choiceButtonTexts[i] != null)
                {
                    choiceButtonTexts[i].fontSize = fontSize;
                }
            }
        }
    }

    public string GetGridRowLabel(int questionIndex, int rowIndex)
    {
        if (gridRowLabels != null && gridRowLabels.Length > 0)
        {
            int baseIdx = (gridRowOffsets != null && questionIndex >= 0 && questionIndex < gridRowOffsets.Length)
                ? gridRowOffsets[questionIndex]
                : 0;
            int labelIdx = baseIdx + rowIndex;
            if (labelIdx >= 0 && labelIdx < gridRowLabels.Length && !string.IsNullOrEmpty(gridRowLabels[labelIdx]))
            {
                return gridRowLabels[labelIdx];
            }
        }
        return $"項目 {rowIndex + 1}";
    }

    public string GetGridColLabel(int questionIndex, int colIndex)
    {
        if (gridColLabels != null && gridColLabels.Length > 0)
        {
            int baseIdx = (gridColOffsets != null && questionIndex >= 0 && questionIndex < gridColOffsets.Length)
                ? gridColOffsets[questionIndex]
                : 0;
            int labelIdx = baseIdx + colIndex;
            if (labelIdx >= 0 && labelIdx < gridColLabels.Length && !string.IsNullOrEmpty(gridColLabels[labelIdx]))
            {
                return gridColLabels[labelIdx];
            }
        }
        return $"選択肢 {colIndex + 1}";
    }

    private void LayoutGridMatrix(int actualQuestionIndex, int rowCount, int colCount)
    {
        if (containerGrid == null) return;

        int maxCols = (gridColHeaderTexts != null && gridColHeaderTexts.Length > 0) ? gridColHeaderTexts.Length : 10;
        int maxRows = (gridRowObjects != null && gridRowObjects.Length > 0) ? gridRowObjects.Length : 20;
        int maxCells = (gridCellButtons != null && gridCellButtons.Length > 0) ? gridCellButtons.Length : 120;

        if (colCount < 2) colCount = 2;
        if (colCount > maxCols) colCount = maxCols;
        if (rowCount < 1) rowCount = 1;
        if (rowCount > maxRows) rowCount = maxRows;
        if (rowCount * colCount > maxCells)
        {
            rowCount = maxCells / colCount;
            if (rowCount < 1) rowCount = 1;
        }

        currentGridRowCount = rowCount;
        currentGridColCount = colCount;

        float usableW = 880f;
        float leftEdgeX = -450f;
        float rowLabelW = 300f;
        if (colCount <= 3) rowLabelW = 340f;
        else if (colCount == 4) rowLabelW = 320f;
        else if (colCount == 5) rowLabelW = 305f;
        else rowLabelW = 240f;

        float colsAreaW = usableW - rowLabelW - 12f;
        float colSlotW = colsAreaW / colCount;

        if (gridCornerHeaderText != null)
        {
            RectTransform cornerRt = gridCornerHeaderText.GetComponent<RectTransform>();
            if (cornerRt != null)
            {
                cornerRt.anchoredPosition = new Vector2(leftEdgeX + 10f + (rowLabelW - 14f) * 0.5f, 0f);
                cornerRt.sizeDelta = new Vector2(rowLabelW - 14f, 42f);
            }
        }

        int headerFontSize = 19;
        if (colCount >= 6) headerFontSize = 16;
        else if (colCount == 5) headerFontSize = 18;

        if (gridColHeaderTexts != null)
        {
            for (int c = 0; c < gridColHeaderTexts.Length; c++)
            {
                if (gridColHeaderTexts[c] != null)
                {
                    if (c < colCount)
                    {
                        gridColHeaderTexts[c].gameObject.SetActive(true);
                        gridColHeaderTexts[c].text = GetGridColLabel(actualQuestionIndex, c);
                        gridColHeaderTexts[c].fontSize = headerFontSize;
                        RectTransform hRt = gridColHeaderTexts[c].GetComponent<RectTransform>();
                        if (hRt != null)
                        {
                            float cx = leftEdgeX + rowLabelW + 12f + colSlotW * (c + 0.5f);
                            hRt.anchoredPosition = new Vector2(cx, 0f);
                            hRt.sizeDelta = new Vector2(colSlotW - 6f, 44f);
                        }
                    }
                    else
                    {
                        gridColHeaderTexts[c].gameObject.SetActive(false);
                    }
                }
            }
        }

        float rowH = 56f;
        float rowGap = 5f;
        if (rowCount == 1) { rowH = 76f; rowGap = 8f; }
        else if (rowCount == 2) { rowH = 72f; rowGap = 8f; }
        else if (rowCount == 3) { rowH = 68f; rowGap = 8f; }
        else if (rowCount == 4) { rowH = 62f; rowGap = 6f; }

        int rowFontSize = (rowCount <= 4) ? 20 : 18;
        float topPad = 4f;
        float totalContentH = topPad * 2f + rowCount * rowH + (rowCount - 1) * rowGap;
        if (totalContentH < 284f) totalContentH = 284f;

        if (gridScrollContent != null)
        {
            gridScrollContent.sizeDelta = new Vector2(900f, totalContentH);
            gridScrollContent.anchoredPosition = Vector2.zero;
        }
        if (gridScrollRect != null)
        {
            gridScrollRect.verticalNormalizedPosition = 1f;
        }

        if (gridRowObjects != null)
        {
            for (int r = 0; r < gridRowObjects.Length; r++)
            {
                if (gridRowObjects[r] != null)
                {
                    if (r < rowCount)
                    {
                        gridRowObjects[r].SetActive(true);
                        float ry = -topPad - r * (rowH + rowGap) - rowH * 0.5f;
                        RectTransform rRt = gridRowObjects[r].GetComponent<RectTransform>();
                        if (rRt != null)
                        {
                            rRt.anchorMin = new Vector2(0.5f, 1f);
                            rRt.anchorMax = new Vector2(0.5f, 1f);
                            rRt.pivot = new Vector2(0.5f, 0.5f);
                            rRt.anchoredPosition = new Vector2(-10f, ry);
                            rRt.sizeDelta = new Vector2(usableW, rowH);
                        }

                        if (gridRowLabelTexts != null && r < gridRowLabelTexts.Length && gridRowLabelTexts[r] != null)
                        {
                            gridRowLabelTexts[r].text = GetGridRowLabel(actualQuestionIndex, r);
                            gridRowLabelTexts[r].fontSize = rowFontSize;
                            RectTransform lRt = gridRowLabelTexts[r].GetComponent<RectTransform>();
                            if (lRt != null)
                            {
                                lRt.anchorMin = new Vector2(0f, 0.5f);
                                lRt.anchorMax = new Vector2(0f, 0.5f);
                                lRt.pivot = new Vector2(0f, 0.5f);
                                lRt.anchoredPosition = new Vector2(12f, 0f);
                                lRt.sizeDelta = new Vector2(rowLabelW - 16f, rowH - 6f);
                            }
                        }
                    }
                    else
                    {
                        gridRowObjects[r].SetActive(false);
                    }
                }
            }
        }

        float btnW = colSlotW - 12f;
        if (btnW > 96f) btnW = 96f;
        if (btnW < 36f) btnW = 36f;
        float btnH = rowH - 12f;
        if (btnH > 50f) btnH = 50f;
        if (btnH < 34f) btnH = 34f;

        int activeCells = rowCount * colCount;
        if (gridCellButtons != null)
        {
            for (int k = 0; k < gridCellButtons.Length; k++)
            {
                if (gridCellButtons[k] != null)
                {
                    if (k < activeCells)
                    {
                        gridCellButtons[k].SetActive(true);
                        int r = k / colCount;
                        int c = k % colCount;
                        float cx = leftEdgeX + rowLabelW + 12f + colSlotW * (c + 0.5f);
                        float ry = -topPad - r * (rowH + rowGap) - rowH * 0.5f;

                        RectTransform bRt = gridCellButtons[k].GetComponent<RectTransform>();
                        if (bRt != null)
                        {
                            bRt.anchorMin = new Vector2(0.5f, 1f);
                            bRt.anchorMax = new Vector2(0.5f, 1f);
                            bRt.pivot = new Vector2(0.5f, 0.5f);
                            bRt.anchoredPosition = new Vector2(cx, ry);
                            bRt.sizeDelta = new Vector2(btnW, btnH);
                        }
                    }
                    else
                    {
                        gridCellButtons[k].SetActive(false);
                    }
                }
            }
        }
    }

    private int GetRequiredGridSelectionCount()
    {
        if (currentGridRowCount <= 0 || currentGridColCount <= 0) return 0;
        return (currentGridRowCount <= currentGridColCount) ? currentGridRowCount : currentGridColCount;
    }

    private bool IsGridColumnSelectedInOtherRow(int col, int excludeRow)
    {
        if (currentGridRowSelections == null) return false;
        for (int r = 0; r < currentGridRowCount && r < currentGridRowSelections.Length; r++)
        {
            if (r != excludeRow && currentGridRowSelections[r] == col)
            {
                return true;
            }
        }
        return false;
    }

    private bool HasDuplicateGridColumns()
    {
        if (currentGridRowSelections == null) return false;
        for (int r1 = 0; r1 < currentGridRowCount && r1 < currentGridRowSelections.Length; r1++)
        {
            int c1 = currentGridRowSelections[r1];
            if (c1 < 0) continue;
            for (int r2 = r1 + 1; r2 < currentGridRowCount && r2 < currentGridRowSelections.Length; r2++)
            {
                if (currentGridRowSelections[r2] == c1)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void RefreshGridVisualState()
    {
        int answeredRows = 0;

        if (gridColHeaderTexts != null)
        {
            for (int c = 0; c < currentGridColCount && c < gridColHeaderTexts.Length; c++)
            {
                if (gridColHeaderTexts[c] != null)
                {
                    bool colUsed = IsGridColumnSelectedInOtherRow(c, -1);
                    gridColHeaderTexts[c].color = colUsed
                        ? new Color(0.48f, 0.95f, 0.68f, 1f)
                        : new Color(0.92f, 0.96f, 1f, 1f);
                }
            }
        }

        for (int r = 0; r < currentGridRowCount; r++)
        {
            int selCol = (currentGridRowSelections != null && r < currentGridRowSelections.Length)
                ? currentGridRowSelections[r]
                : -1;
            if (selCol >= 0) answeredRows++;

            if (gridRowBgImages != null && r < gridRowBgImages.Length && gridRowBgImages[r] != null)
            {
                if (selCol >= 0)
                {
                    gridRowBgImages[r].color = new Color(0.14f, 0.34f, 0.25f, 0.55f);
                }
                else
                {
                    gridRowBgImages[r].color = (r % 2 == 0)
                        ? new Color(1f, 1f, 1f, 0.05f)
                        : new Color(1f, 1f, 1f, 0.10f);
                }
            }

            for (int c = 0; c < currentGridColCount; c++)
            {
                int cellIdx = r * currentGridColCount + c;
                bool isSelected = (selCol == c);
                bool usedInOtherRow = !isSelected && IsGridColumnSelectedInOtherRow(c, r);

                if (gridCellButtonImages != null && cellIdx < gridCellButtonImages.Length && gridCellButtonImages[cellIdx] != null)
                {
                    if (isSelected)
                    {
                        gridCellButtonImages[cellIdx].color = new Color(0.18f, 0.75f, 0.42f, 1f);
                    }
                    else if (usedInOtherRow)
                    {
                        gridCellButtonImages[cellIdx].color = new Color(0.15f, 0.17f, 0.23f, 0.78f);
                    }
                    else
                    {
                        gridCellButtonImages[cellIdx].color = new Color(0.22f, 0.25f, 0.34f, 1f);
                    }
                }
                if (gridCellButtonTexts != null && cellIdx < gridCellButtonTexts.Length && gridCellButtonTexts[cellIdx] != null)
                {
                    gridCellButtonTexts[cellIdx].text = isSelected ? "●" : "○";
                    if (isSelected)
                    {
                        gridCellButtonTexts[cellIdx].color = Color.white;
                    }
                    else if (usedInOtherRow)
                    {
                        gridCellButtonTexts[cellIdx].color = new Color(0.45f, 0.48f, 0.55f, 0.78f);
                    }
                    else
                    {
                        gridCellButtonTexts[cellIdx].color = new Color(0.78f, 0.82f, 0.88f, 1f);
                    }
                }
            }
        }

        int requiredRows = GetRequiredGridSelectionCount();
        bool allAnswered = (requiredRows > 0 && answeredRows >= requiredRows && !HasDuplicateGridColumns());
        if (gridSubmitButton != null)
        {
            gridSubmitButton.interactable = allAnswered;
        }
        if (gridSubmitButtonImage != null)
        {
            gridSubmitButtonImage.color = allAnswered
                ? new Color(0.2f, 0.75f, 0.35f, 1f)
                : new Color(0.32f, 0.34f, 0.40f, 1f);
        }
        if (gridSubmitButtonText != null)
        {
            gridSubmitButtonText.text = allAnswered
                ? "決定"
                : $"各行・各列1つずつ選択してください ({answeredRows} / {requiredRows})";
            gridSubmitButtonText.color = allAnswered
                ? Color.white
                : new Color(0.78f, 0.78f, 0.78f, 1f);
        }
    }

    public string GetCategoryDescriptionForType(int type)
    {
        if (type == 0) return choiceCategoryDescription != null ? choiceCategoryDescription : "";
        if (type == 3) return gridCategoryDescription != null ? gridCategoryDescription : "";
        if (type == 1) return sliderCategoryDescription != null ? sliderCategoryDescription : "";
        if (type == 2) return textCategoryDescription != null ? textCategoryDescription : "";
        return "";
    }

    public void ShowQuestion(int index)
    {
        if (index < 0 || index >= questionTexts.Length) return;

        int actualIndex = GetActualQuestionIndex(index);
        if (actualIndex < 0 || actualIndex >= questionTexts.Length) actualIndex = index;

        if (containerConsent != null) containerConsent.SetActive(false);

        int type = (answerTypes != null && actualIndex < answerTypes.Length) ? answerTypes[actualIndex] : 0;
        string catDesc = GetCategoryDescriptionForType(type);
        bool hasCatDesc = !string.IsNullOrEmpty(catDesc);

        if (categoryDescriptionText == null && surveyPanel != null)
        {
            Transform cdTr = surveyPanel.transform.Find("CategoryDescriptionText");
            if (cdTr != null) categoryDescriptionText = cdTr.GetComponent<Text>();
        }

        if (titleText != null)
        {
            RectTransform tRt = titleText.GetComponent<RectTransform>();
            if (tRt != null)
            {
                if (hasCatDesc)
                {
                    tRt.anchoredPosition = new Vector2(0f, 250f);
                    tRt.sizeDelta = new Vector2(900f, 44f);
                }
                else
                {
                    tRt.anchoredPosition = new Vector2(0f, 224f);
                    tRt.sizeDelta = new Vector2(900f, 56f);
                }
            }
            titleText.fontSize = hasCatDesc ? 31 : 36;
            titleText.text = $"質問 ({index + 1} / {questionTexts.Length})";
        }

        if (categoryDescriptionText != null)
        {
            if (hasCatDesc)
            {
                categoryDescriptionText.gameObject.SetActive(true);
                categoryDescriptionText.text = catDesc;
                RectTransform dRt = categoryDescriptionText.GetComponent<RectTransform>();
                if (dRt != null)
                {
                    if (type == 3)
                    {
                        dRt.anchoredPosition = new Vector2(0f, 204f);
                        dRt.sizeDelta = new Vector2(900f, 44f);
                        categoryDescriptionText.fontSize = 19;
                    }
                    else
                    {
                        dRt.anchoredPosition = new Vector2(0f, 196f);
                        dRt.sizeDelta = new Vector2(900f, 56f);
                        categoryDescriptionText.fontSize = 21;
                    }
                }
            }
            else
            {
                categoryDescriptionText.gameObject.SetActive(false);
            }
        }

        if (questionText != null)
        {
            RectTransform qRt = questionText.GetComponent<RectTransform>();
            if (hasCatDesc && categoryDescriptionText == null)
            {
                // UI再生成前でも説明文がそのまま表示されるリッチテキストフォールバック
                questionText.supportRichText = true;
                if (qRt != null)
                {
                    if (type == 3)
                    {
                        qRt.anchoredPosition = new Vector2(0f, 174f);
                        qRt.sizeDelta = new Vector2(900f, 96f);
                    }
                    else
                    {
                        qRt.anchoredPosition = new Vector2(0f, 155f);
                        qRt.sizeDelta = new Vector2(900f, 132f);
                    }
                }
                int descSize = (type == 3) ? 18 : 21;
                questionText.fontSize = (type == 3) ? 25 : 30;
                questionText.text = $"<size={descSize}><color=#A8D4FF>{catDesc}</color></size>\n{questionTexts[actualIndex]}";
            }
            else if (hasCatDesc)
            {
                if (qRt != null)
                {
                    if (type == 3)
                    {
                        qRt.anchoredPosition = new Vector2(0f, 150f);
                        qRt.sizeDelta = new Vector2(900f, 56f);
                    }
                    else
                    {
                        qRt.anchoredPosition = new Vector2(0f, 126f);
                        qRt.sizeDelta = new Vector2(900f, 80f);
                    }
                }
                questionText.fontSize = (type == 3) ? 25 : 30;
                questionText.text = questionTexts[actualIndex];
            }
            else
            {
                if (qRt != null)
                {
                    if (type == 3)
                    {
                        qRt.anchoredPosition = new Vector2(0f, 162f);
                        qRt.sizeDelta = new Vector2(900f, 68f);
                    }
                    else
                    {
                        qRt.anchoredPosition = new Vector2(0f, 130f);
                        qRt.sizeDelta = new Vector2(900f, 100f);
                    }
                }
                questionText.fontSize = (type == 3) ? 28 : 32;
                questionText.text = questionTexts[actualIndex];
            }
        }

        if (containerChoice != null) containerChoice.SetActive(false);
        if (containerRating != null) containerRating.SetActive(false);
        if (containerTextInput != null) containerTextInput.SetActive(false);
        if (containerGrid != null) containerGrid.SetActive(false);

        switch (type)
        {
            case 0: // 選択式
                if (containerChoice != null) containerChoice.SetActive(true);

                int count = (choiceCounts != null && actualIndex < choiceCounts.Length) ? choiceCounts[actualIndex] : 2;
                if (count < 1) count = 2;
                if (choiceButtons != null)
                {
                    for (int i = 0; i < choiceButtons.Length; i++)
                    {
                        if (choiceButtons[i] != null)
                        {
                            if (i < count)
                            {
                                choiceButtons[i].SetActive(true);
                                if (choiceButtonTexts != null && i < choiceButtonTexts.Length && choiceButtonTexts[i] != null)
                                {
                                    choiceButtonTexts[i].text = GetOptionLabel(actualIndex, i);
                                }
                            }
                            else
                            {
                                choiceButtons[i].SetActive(false);
                            }
                        }
                    }
                    LayoutChoiceButtons(count);
                }
                break;

            case 1: // スライダー評価式
                if (containerRating != null) containerRating.SetActive(true);
                if (ratingSlider != null)
                {
                    int maxVal = (sliderMaxValues != null && actualIndex < sliderMaxValues.Length) ? sliderMaxValues[actualIndex] : 5;
                    if (maxVal < 2) maxVal = 5;

                    ratingSlider.minValue = 1;
                    ratingSlider.maxValue = maxVal;
                    ratingSlider.value = Mathf.CeilToInt(maxVal / 2.0f);
                    UpdateRatingText();
                }
                break;

            case 2: // 記述回答式
                if (containerTextInput != null) containerTextInput.SetActive(true);
                if (inputFieldArea != null)
                {
                    inputFieldArea.text = "";
                }
                break;

            case 3: // 選択式（グリッド）
                if (containerGrid == null)
                {
                    EnsureUIReferences();
                }
                if (containerGrid != null)
                {
                    containerGrid.SetActive(true);
                    int rCount = (gridRowCounts != null && actualIndex < gridRowCounts.Length) ? gridRowCounts[actualIndex] : 2;
                    int cCount = (gridColCounts != null && actualIndex < gridColCounts.Length) ? gridColCounts[actualIndex] : 3;
                    if (rCount < 1) rCount = 1;
                    if (cCount < 2) cCount = 2;

                    if (currentGridRowSelections == null || currentGridRowSelections.Length < 20)
                    {
                        currentGridRowSelections = new int[20];
                    }
                    for (int r = 0; r < currentGridRowSelections.Length; r++)
                    {
                        currentGridRowSelections[r] = -1;
                    }

                    LayoutGridMatrix(actualIndex, rCount, cCount);
                    if (currentGridRowCount > currentGridColCount)
                    {
                        Debug.LogWarning($"[SurveyManager] 選択式（グリッド）Q{actualIndex + 1}: 行数({currentGridRowCount})が列数({currentGridColCount})を超えています。1列につき1回答のため、最大{currentGridColCount}行の選択で決定可能になります。");
                    }
                    RefreshGridVisualState();
                    DisablePhysicalCollisions();
                }
                else
                {
                    Debug.LogError("[SurveyManager] Container_Grid が見つかりません。UnityエディタのSurveyManagerインスペクターで「★ UIを自動生成・一括セットアップ」を実行してください。");
                }
                break;
        }
    }

    public void SelectAnswer(int answerValue)
    {
        int actualIndex = GetActualQuestionIndex(currentIndex);
        if (userAnswers != null && actualIndex >= 0 && actualIndex < userAnswers.Length)
        {
            userAnswers[actualIndex] = answerValue;
            userTextAnswers[actualIndex] = answerValue.ToString();
        }

        NextQuestion();
    }

    public void OnChoiceOption0() { SelectAnswer(0); }
    public void OnChoiceOption1() { SelectAnswer(1); }
    public void OnChoiceOption2() { SelectAnswer(2); }
    public void OnChoiceOption3() { SelectAnswer(3); }
    public void OnChoiceOption4() { SelectAnswer(4); }
    public void OnChoiceOption5() { SelectAnswer(5); }
    public void OnChoiceOption6() { SelectAnswer(6); }
    public void OnChoiceOption7() { SelectAnswer(7); }
    public void OnChoiceOption8() { SelectAnswer(8); }
    public void OnChoiceOption9() { SelectAnswer(9); }
    public void OnChoiceOption10() { SelectAnswer(10); }
    public void OnChoiceOption11() { SelectAnswer(11); }
    public void OnChoiceOption12() { SelectAnswer(12); }
    public void OnChoiceOption13() { SelectAnswer(13); }
    public void OnChoiceOption14() { SelectAnswer(14); }
    public void OnChoiceOption15() { SelectAnswer(15); }
    public void OnChoiceOption16() { SelectAnswer(16); }
    public void OnChoiceOption17() { SelectAnswer(17); }
    public void OnChoiceOption18() { SelectAnswer(18); }
    public void OnChoiceOption19() { SelectAnswer(19); }
    public void OnChoiceOption20() { SelectAnswer(20); }
    public void OnChoiceOption21() { SelectAnswer(21); }
    public void OnChoiceOption22() { SelectAnswer(22); }
    public void OnChoiceOption23() { SelectAnswer(23); }

    public void SelectGridCell(int cellIndex)
    {
        if (currentGridColCount <= 0 || currentGridRowCount <= 0) return;

        int row = cellIndex / currentGridColCount;
        int col = cellIndex % currentGridColCount;
        if (row < 0 || row >= currentGridRowCount || col < 0 || col >= currentGridColCount) return;

        if (currentGridRowSelections == null || currentGridRowSelections.Length <= row)
        {
            int[] expanded = new int[20];
            for (int i = 0; i < expanded.Length; i++) expanded[i] = -1;
            if (currentGridRowSelections != null)
            {
                for (int i = 0; i < currentGridRowSelections.Length && i < expanded.Length; i++)
                {
                    expanded[i] = currentGridRowSelections[i];
                }
            }
            currentGridRowSelections = expanded;
        }

        // Googleフォーム「1列につき1つの回答に制限する」方式:
        // 他の行ですでに同じ列が選ばれていた場合、前の行の選択を自動解除して今押した行へ移す
        for (int r = 0; r < currentGridRowCount && r < currentGridRowSelections.Length; r++)
        {
            if (r != row && currentGridRowSelections[r] == col)
            {
                currentGridRowSelections[r] = -1;
            }
        }

        currentGridRowSelections[row] = col;
        RefreshGridVisualState();
    }

    private string SanitizeLogSegment(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("|", "｜").Replace(":", "：").Replace("\r", " ").Replace("\n", " ").Trim();
    }

    public void OnSubmitGrid()
    {
        if (currentGridRowCount <= 0 || currentGridColCount <= 0) return;
        if (currentGridRowSelections == null) return;
        if (HasDuplicateGridColumns()) return;

        int answeredRows = 0;
        for (int r = 0; r < currentGridRowCount && r < currentGridRowSelections.Length; r++)
        {
            if (currentGridRowSelections[r] >= 0)
            {
                answeredRows++;
            }
        }

        int requiredRows = GetRequiredGridSelectionCount();
        if (answeredRows < requiredRows) return;

        int actualIndex = GetActualQuestionIndex(currentIndex);
        string combined = "";
        for (int r = 0; r < currentGridRowCount; r++)
        {
            string rLabel = SanitizeLogSegment(GetGridRowLabel(actualIndex, r));
            int selCol = (r < currentGridRowSelections.Length) ? currentGridRowSelections[r] : -1;
            string cLabel = (selCol >= 0) ? SanitizeLogSegment(GetGridColLabel(actualIndex, selCol)) : "未選択";
            if (r > 0) combined += " / ";
            combined += $"{rLabel}={cLabel}";
        }

        if (userTextAnswers != null && actualIndex >= 0 && actualIndex < userTextAnswers.Length)
        {
            userTextAnswers[actualIndex] = combined;
        }
        if (userAnswers != null && actualIndex >= 0 && actualIndex < userAnswers.Length)
        {
            userAnswers[actualIndex] = answeredRows;
        }

        NextQuestion();
    }

    public void OnGridCell0() { SelectGridCell(0); }
    public void OnGridCell1() { SelectGridCell(1); }
    public void OnGridCell2() { SelectGridCell(2); }
    public void OnGridCell3() { SelectGridCell(3); }
    public void OnGridCell4() { SelectGridCell(4); }
    public void OnGridCell5() { SelectGridCell(5); }
    public void OnGridCell6() { SelectGridCell(6); }
    public void OnGridCell7() { SelectGridCell(7); }
    public void OnGridCell8() { SelectGridCell(8); }
    public void OnGridCell9() { SelectGridCell(9); }
    public void OnGridCell10() { SelectGridCell(10); }
    public void OnGridCell11() { SelectGridCell(11); }
    public void OnGridCell12() { SelectGridCell(12); }
    public void OnGridCell13() { SelectGridCell(13); }
    public void OnGridCell14() { SelectGridCell(14); }
    public void OnGridCell15() { SelectGridCell(15); }
    public void OnGridCell16() { SelectGridCell(16); }
    public void OnGridCell17() { SelectGridCell(17); }
    public void OnGridCell18() { SelectGridCell(18); }
    public void OnGridCell19() { SelectGridCell(19); }
    public void OnGridCell20() { SelectGridCell(20); }
    public void OnGridCell21() { SelectGridCell(21); }
    public void OnGridCell22() { SelectGridCell(22); }
    public void OnGridCell23() { SelectGridCell(23); }
    public void OnGridCell24() { SelectGridCell(24); }
    public void OnGridCell25() { SelectGridCell(25); }
    public void OnGridCell26() { SelectGridCell(26); }
    public void OnGridCell27() { SelectGridCell(27); }
    public void OnGridCell28() { SelectGridCell(28); }
    public void OnGridCell29() { SelectGridCell(29); }
    public void OnGridCell30() { SelectGridCell(30); }
    public void OnGridCell31() { SelectGridCell(31); }
    public void OnGridCell32() { SelectGridCell(32); }
    public void OnGridCell33() { SelectGridCell(33); }
    public void OnGridCell34() { SelectGridCell(34); }
    public void OnGridCell35() { SelectGridCell(35); }
    public void OnGridCell36() { SelectGridCell(36); }
    public void OnGridCell37() { SelectGridCell(37); }
    public void OnGridCell38() { SelectGridCell(38); }
    public void OnGridCell39() { SelectGridCell(39); }
    public void OnGridCell40() { SelectGridCell(40); }
    public void OnGridCell41() { SelectGridCell(41); }
    public void OnGridCell42() { SelectGridCell(42); }
    public void OnGridCell43() { SelectGridCell(43); }
    public void OnGridCell44() { SelectGridCell(44); }
    public void OnGridCell45() { SelectGridCell(45); }
    public void OnGridCell46() { SelectGridCell(46); }
    public void OnGridCell47() { SelectGridCell(47); }
    public void OnGridCell48() { SelectGridCell(48); }
    public void OnGridCell49() { SelectGridCell(49); }
    public void OnGridCell50() { SelectGridCell(50); }
    public void OnGridCell51() { SelectGridCell(51); }
    public void OnGridCell52() { SelectGridCell(52); }
    public void OnGridCell53() { SelectGridCell(53); }
    public void OnGridCell54() { SelectGridCell(54); }
    public void OnGridCell55() { SelectGridCell(55); }
    public void OnGridCell56() { SelectGridCell(56); }
    public void OnGridCell57() { SelectGridCell(57); }
    public void OnGridCell58() { SelectGridCell(58); }
    public void OnGridCell59() { SelectGridCell(59); }
    public void OnGridCell60() { SelectGridCell(60); }
    public void OnGridCell61() { SelectGridCell(61); }
    public void OnGridCell62() { SelectGridCell(62); }
    public void OnGridCell63() { SelectGridCell(63); }
    public void OnGridCell64() { SelectGridCell(64); }
    public void OnGridCell65() { SelectGridCell(65); }
    public void OnGridCell66() { SelectGridCell(66); }
    public void OnGridCell67() { SelectGridCell(67); }
    public void OnGridCell68() { SelectGridCell(68); }
    public void OnGridCell69() { SelectGridCell(69); }
    public void OnGridCell70() { SelectGridCell(70); }
    public void OnGridCell71() { SelectGridCell(71); }
    public void OnGridCell72() { SelectGridCell(72); }
    public void OnGridCell73() { SelectGridCell(73); }
    public void OnGridCell74() { SelectGridCell(74); }
    public void OnGridCell75() { SelectGridCell(75); }
    public void OnGridCell76() { SelectGridCell(76); }
    public void OnGridCell77() { SelectGridCell(77); }
    public void OnGridCell78() { SelectGridCell(78); }
    public void OnGridCell79() { SelectGridCell(79); }
    public void OnGridCell80() { SelectGridCell(80); }
    public void OnGridCell81() { SelectGridCell(81); }
    public void OnGridCell82() { SelectGridCell(82); }
    public void OnGridCell83() { SelectGridCell(83); }
    public void OnGridCell84() { SelectGridCell(84); }
    public void OnGridCell85() { SelectGridCell(85); }
    public void OnGridCell86() { SelectGridCell(86); }
    public void OnGridCell87() { SelectGridCell(87); }
    public void OnGridCell88() { SelectGridCell(88); }
    public void OnGridCell89() { SelectGridCell(89); }
    public void OnGridCell90() { SelectGridCell(90); }
    public void OnGridCell91() { SelectGridCell(91); }
    public void OnGridCell92() { SelectGridCell(92); }
    public void OnGridCell93() { SelectGridCell(93); }
    public void OnGridCell94() { SelectGridCell(94); }
    public void OnGridCell95() { SelectGridCell(95); }
    public void OnGridCell96() { SelectGridCell(96); }
    public void OnGridCell97() { SelectGridCell(97); }
    public void OnGridCell98() { SelectGridCell(98); }
    public void OnGridCell99() { SelectGridCell(99); }
    public void OnGridCell100() { SelectGridCell(100); }
    public void OnGridCell101() { SelectGridCell(101); }
    public void OnGridCell102() { SelectGridCell(102); }
    public void OnGridCell103() { SelectGridCell(103); }
    public void OnGridCell104() { SelectGridCell(104); }
    public void OnGridCell105() { SelectGridCell(105); }
    public void OnGridCell106() { SelectGridCell(106); }
    public void OnGridCell107() { SelectGridCell(107); }
    public void OnGridCell108() { SelectGridCell(108); }
    public void OnGridCell109() { SelectGridCell(109); }
    public void OnGridCell110() { SelectGridCell(110); }
    public void OnGridCell111() { SelectGridCell(111); }
    public void OnGridCell112() { SelectGridCell(112); }
    public void OnGridCell113() { SelectGridCell(113); }
    public void OnGridCell114() { SelectGridCell(114); }
    public void OnGridCell115() { SelectGridCell(115); }
    public void OnGridCell116() { SelectGridCell(116); }
    public void OnGridCell117() { SelectGridCell(117); }
    public void OnGridCell118() { SelectGridCell(118); }
    public void OnGridCell119() { SelectGridCell(119); }

    public void OnRatingSliderChanged()
    {
        UpdateRatingText();
    }

    private void UpdateRatingText()
    {
        if (ratingSlider != null && ratingValueText != null)
        {
            int val = Mathf.RoundToInt(ratingSlider.value);
            int max = Mathf.RoundToInt(ratingSlider.maxValue);
            ratingValueText.text = $"評価: {val} / {max}";
        }
    }

    public void OnSubmitRating()
    {
        int val = (ratingSlider != null) ? Mathf.RoundToInt(ratingSlider.value) : 3;
        SelectAnswer(val);
    }

    public void OnSubmitTextAnswer()
    {
        string text = (inputFieldArea != null) ? inputFieldArea.text : "";
        int actualIndex = GetActualQuestionIndex(currentIndex);
        if (userTextAnswers != null && actualIndex >= 0 && actualIndex < userTextAnswers.Length)
        {
            userTextAnswers[actualIndex] = text;
            userAnswers[actualIndex] = -1;
        }

        NextQuestion();
    }

    public void NextQuestion()
    {
        currentIndex++;
        if (currentIndex < questionTexts.Length)
        {
            ShowQuestion(currentIndex);
        }
        else
        {
            FinishSurvey();
        }
    }

    public void FinishSurvey()
    {
        hasAnswered = true;

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string playerName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        if (surveyPanel != null) surveyPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(true);

        if (resultMessageText != null)
        {
            resultMessageText.text = "ご回答ありがとうございました！";
        }

        string fullLog = $"[MVP_Q] {playerName}: ";
        for (int i = 0; i < questionTexts.Length; i++)
        {
            int qNum = i + 1;
            string qText = questionTexts[i];
            int type = (answerTypes != null && i < answerTypes.Length) ? answerTypes[i] : 0;
            string typeStr = GetAnswerTypeName(type);
            string answerContent = GetAnswerContentString(i, type);

            if (i > 0) fullLog += " | ";
            fullLog += $"Q{qNum}: {qText}: {typeStr}: {answerContent}";
        }

        syncedLatestResultLog = fullLog;

        // 安全な記録と段階的リトライによるシリアライズ送信
        SubmitAnswerAndSerialize(playerName);

        // 回答完了パネルを5秒後に自動的に閉じる (視界や移動の妨げを防止)
        SendCustomEventDelayedSeconds(nameof(HideResultPanel), 5.0f);
    }

    /// <summary>
    /// 回答完了・辞退後に結果パネルを自動非表示にする (5秒タイマー用)
    /// </summary>
    public void HideResultPanel()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    public void TriggerDelayedLogBroadcast()
    {
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnReceiveSurveyResultLogNet));
    }

    public void OnReceiveSurveyResultLogNet()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string localName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        if (CheckIfRecordMaster(localName))
        {
            // 届いた最新ログから回答者名を抽出し、マスターローカルの確定リストに登録
            string extractedName = ExtractPlayerNameFromLog(syncedLatestResultLog);
            if (!string.IsNullOrEmpty(extractedName))
            {
                RegisterPlayerAnsweredByMaster(extractedName);
            }

            // syncedAnsweredPlayers に含まれるプレイヤーもマスター確定リストに取り込む
            SyncAnsweredPlayersToMasterList();

            if (!string.IsNullOrEmpty(syncedLatestResultLog) && syncedLatestResultLog != lastProcessedLog)
            {
                lastProcessedLog = syncedLatestResultLog;
                Debug.Log(syncedLatestResultLog);
            }

            UpdateMasterStatusText();

            // マスターがオーナーであれば最新確定リストで同期文字列を自動修復
            if (Networking.IsOwner(gameObject))
            {
                RepairAndBroadcastAnsweredList();
            }
        }
    }

    /// <summary>
    /// RecordMasterNames にプレイヤーが明確に含まれているか判定する
    /// </summary>
    private bool CheckIfRecordMaster(string playerName)
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        // Unity エディタ内のテスト(Playモード)のみ True
        if (localPlayer == null || !localPlayer.IsValid()) return true;

        if (string.IsNullOrEmpty(playerName)) return false;
        string cleanNameNorm = NormalizePlayerNameForMatch(playerName);

        // 1. 外部 UdonBehaviour の RecordMasterNames チェック
        if (targetUdonBehaviour != null && !string.IsNullOrEmpty(recordMasterVariableName))
        {
            object val = targetUdonBehaviour.GetProgramVariable(recordMasterVariableName);
            if (val != null)
            {
                string[] targetNames = (string[])val;
                if (targetNames != null && targetNames.Length > 0)
                {
                    for (int i = 0; i < targetNames.Length; i++)
                    {
                        if (targetNames[i] != null && NormalizePlayerNameForMatch(targetNames[i]) == cleanNameNorm)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        // 2. インスペクター直書きの allowedUserNames チェック (RecordMasterNames に見つからなかった場合のフォールバック兼併用チェック)
        if (allowedUserNames != null && allowedUserNames.Length > 0)
        {
            for (int i = 0; i < allowedUserNames.Length; i++)
            {
                if (allowedUserNames[i] != null && NormalizePlayerNameForMatch(allowedUserNames[i]) == cleanNameNorm)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool CheckIfUserInVariable(string playerName, string variableName)
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null || !localPlayer.IsValid())
        {
            return true;
        }

        if (string.IsNullOrEmpty(playerName)) return false;
        string cleanNameNorm = NormalizePlayerNameForMatch(playerName);

        bool hasTargetData = false;

        if (targetUdonBehaviour != null && !string.IsNullOrEmpty(variableName))
        {
            string[] targetNames = null;
            if (variableName == jsonNamesVariableName)
            {
                targetNames = GetListModeJsonNames();
            }
            else
            {
                object val = targetUdonBehaviour.GetProgramVariable(variableName);
                if (val != null)
                {
                    targetNames = (string[])val;
                }
            }

            if (targetNames != null && targetNames.Length > 0)
            {
                hasTargetData = true;
                for (int i = 0; i < targetNames.Length; i++)
                {
                    if (targetNames[i] != null && NormalizePlayerNameForMatch(targetNames[i]) == cleanNameNorm)
                    {
                        return true;
                    }
                }
            }
        }

        if (allowedUserNames != null && allowedUserNames.Length > 0)
        {
            for (int i = 0; i < allowedUserNames.Length; i++)
            {
                if (allowedUserNames[i] != null && NormalizePlayerNameForMatch(allowedUserNames[i]) == cleanNameNorm)
                {
                    return true;
                }
            }
        }

        // どちらのリストにもデータが全く設定されていない場合はデフォルトで true
        if (!hasTargetData && (allowedUserNames == null || allowedUserNames.Length == 0))
        {
            return true;
        }

        return false;
    }

    private string GetAnswerTypeName(int type)
    {
        switch (type)
        {
            case 0: return "選択式";
            case 1: return "スライダー評価式";
            case 2: return "記述回答式";
            case 3: return "選択式（グリッド）";
            default: return "選択式";
        }
    }

    private string GetAnswerContentString(int index, int type)
    {
        if (type == 0)
        {
            int selectedOptIdx = (userAnswers != null && index < userAnswers.Length) ? userAnswers[index] : 0;
            return GetOptionLabel(index, selectedOptIdx);
        }
        else if (type == 1)
        {
            int val = (userAnswers != null && index < userAnswers.Length) ? userAnswers[index] : 0;
            return val.ToString();
        }
        else if (type == 2 || type == 3)
        {
            return (userTextAnswers != null && index < userTextAnswers.Length && userTextAnswers[index] != null) ? userTextAnswers[index] : "";
        }
        return "";
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    private void OnValidate()
    {
        if (containerConsent == null)
        {
            Transform t = transform.Find("SurveyPanel/Container_Consent");
            if (t != null) containerConsent = t.gameObject;
        }
        if (consentNoticeDisplay == null && containerConsent != null)
        {
            Transform t = containerConsent.transform.Find("ConsentNoticeDisplay");
            if (t != null) consentNoticeDisplay = t.GetComponent<Text>();
        }
        if (consentNoticeDisplay != null && consentNoticeDisplay.text != consentNoticeText)
        {
            consentNoticeDisplay.text = consentNoticeText;
            UnityEditor.EditorUtility.SetDirty(consentNoticeDisplay);
        }

        if (containerDtcConsent == null)
        {
            Transform t = transform.Find("Container_DtcConsent");
            if (t == null && transform.parent != null) t = transform.parent.Find("Container_DtcConsent");
            if (t != null) containerDtcConsent = t.gameObject;
        }
        if (dtcConsentTitleDisplay == null && containerDtcConsent != null)
        {
            Transform t = containerDtcConsent.transform.Find("DtcTitleText");
            if (t == null) t = containerDtcConsent.transform.Find("TitleText");
            if (t != null) dtcConsentTitleDisplay = t.GetComponent<Text>();
        }
        if (dtcConsentTitleDisplay != null && !string.IsNullOrEmpty(dtcConsentTitleText) && dtcConsentTitleDisplay.text != dtcConsentTitleText)
        {
            dtcConsentTitleDisplay.text = dtcConsentTitleText;
            UnityEditor.EditorUtility.SetDirty(dtcConsentTitleDisplay);
        }
        if (dtcConsentNoticeDisplay == null && containerDtcConsent != null)
        {
            Transform t = containerDtcConsent.transform.Find("DtcConsentNoticeDisplay");
            if (t == null) t = containerDtcConsent.transform.Find("ConsentNoticeDisplay");
            if (t != null) dtcConsentNoticeDisplay = t.GetComponent<Text>();
        }
        if (dtcConsentNoticeDisplay != null && dtcConsentNoticeDisplay.text != dtcConsentNoticeText)
        {
            dtcConsentNoticeDisplay.text = dtcConsentNoticeText;
            UnityEditor.EditorUtility.SetDirty(dtcConsentNoticeDisplay);
        }
    }
#endif
}
