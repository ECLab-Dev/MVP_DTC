using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class SurveyManager : UdonSharpBehaviour
{
    [Header("アンケートデータ設定 (Inspectorで設定)")]
    [Tooltip("質問文のリスト")]
    public string[] questionTexts = new string[] {
        "イベントは楽しめましたか？",
        "使用しているプラットフォームは？",
        "このワールドの満足度を教えてください",
        "ご意見・ご感想を自由にご記述ください"
    };

    [Tooltip("回答形式のタイプ (0: 選択式, 1: スライダー評価式, 2: 記述回答式)")]
    public int[] answerTypes = new int[] { 0, 0, 1, 2 };

    [Tooltip("選択式の場合の択数 (例: 2択なら2, 3択なら3, 4択なら4)")]
    public int[] choiceCounts = new int[] { 2, 4, 0, 0 };

    [Tooltip("選択式のラベル名リスト (質問数 x 6要素の固定長)")]
    public string[] optionLabels = new string[] {
        "いいえ", "はい", "", "", "", "",
        "PC (VR)", "PC (Desktop)", "Quest", "iOS", "", "",
        "", "", "", "", "", "",
        "", "", "", "", "", ""
    };

    [Tooltip("スライダー評価式の場合の最大評価値 (例: 5なら1~5段階, 10なら1~10段階)")]
    public int[] sliderMaxValues = new int[] { 5, 5, 5, 5 };

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

    [Header("アンケート同意確認画面 UIコンテナ参照")]
    public GameObject containerConsent;
    public Text consentNoticeDisplay;
    public Button consentAgreeButton;
    public Button consentDisagreeButton;
    [TextArea(3, 8)]
    public string consentNoticeText = "【アンケート参加・データ収集に関する同意確認】\n\n本アンケートの回答データは統計・報告目的のみに使用されます。\n内容に同意いただける方は「同意する」を押してアンケートへお進みください。\n「同意しない」を押した場合は、アンケートに回答せず終了します。";

    [Header("DTC (位置・視線データ収集) 同意確認画面 UI参照")]
    public GameObject containerDtcConsent;
    public Text dtcConsentNoticeDisplay;
    public Button dtcConsentAgreeButton;
    public Button dtcConsentDisagreeButton;
    [TextArea(3, 8)]
    public string dtcConsentNoticeText = "【位置・視線データ収集（MVP_DTC）に関する同意確認】\n\n本ワールドでは研究・統計分析を目的として、プレイヤーの位置および頭部姿勢データ（MVP_DTC）を記録する機能があります。\nデータ収集にご同意いただける方は「同意する」を押してください。\n「同意しない」を押した場合、あなたの位置・視線データは記録されません。";

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
    private int[] userAnswers;
    private string[] userTextAnswers;
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

    void Start()
    {
        EnsureUIReferences();

        bool isOwnerOrLocal = (Networking.LocalPlayer == null || !Networking.LocalPlayer.IsValid() || Networking.IsOwner(gameObject));
        if (isOwnerOrLocal)
        {
            bool isAll = (initialTargetMode == 1);
            syncedTargetAllMode = isAll;
            syncedDtcTargetAllMode = isAll;
            RequestSerialization();
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
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 10.0f);

        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreSurveyForLocalUser), 0.5f);
        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreSurveyForLocalUser), 1.5f);
        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreSurveyForLocalUser), 3.5f);

        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreDtcConsentForLocalUser), 0.6f);
        SendCustomEventDelayedSeconds(nameof(CheckAndRestoreDtcConsentForLocalUser), 1.6f);
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        EnsureUIReferences();
        RefreshMasterPanelVisibility();
        CheckAndRestoreSurveyForLocalUser();
        CheckAndRestoreDtcConsentForLocalUser();
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 0.5f);
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 2.0f);
        SendCustomEventDelayedSeconds(nameof(RefreshMasterPanelVisibility), 5.0f);
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

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer != null && localPlayer.IsValid() && player != null && player.IsValid())
        {
            if (player.isLocal)
            {
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

        if (!CheckIfUserInVariable(localName, jsonNamesVariableName)) return;

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
    /// syncedAnsweredPlayers にマージして全クライアントへ再配信（自動修復）する
    /// </summary>
    private void RepairAndBroadcastAnsweredList()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null || !localPlayer.IsValid()) return;
        if (!CheckIfRecordMaster(localPlayer.displayName)) return;
        if (!Networking.IsOwner(gameObject)) return;

        if (string.IsNullOrEmpty(masterRecordedAnsweredPlayers)) return;

        bool changed = false;
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

        if (changed)
        {
            RequestSerialization();
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
        string cleanPName = pName.Trim();

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null || !localPlayer.IsValid())
        {
            if (cleanPName.ToLower() == "localuser") return true;
            if (allowedUserNames != null)
            {
                for (int i = 0; i < allowedUserNames.Length; i++)
                {
                    if (allowedUserNames[i] != null && allowedUserNames[i].Trim().ToLower() == cleanPName.ToLower()) return true;
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
                if (players[i].displayName != null && players[i].displayName.Trim().ToLower() == cleanPName.ToLower()) return true;
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
        string[] targetNames = null;
        if (targetUdonBehaviour != null && !string.IsNullOrEmpty(jsonNamesVariableName))
        {
            object val = targetUdonBehaviour.GetProgramVariable(jsonNamesVariableName);
            if (val != null)
            {
                targetNames = (string[])val;
            }
        }
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

        if (localPlayer != null && localPlayer.IsValid())
        {
            if (string.IsNullOrEmpty(syncedDtcConsentedPlayers))
            {
                syncedDtcConsentedPlayers = ",";
            }
            string nameKey = playerName + ",";
            if (!syncedDtcConsentedPlayers.Contains("," + nameKey))
            {
                syncedDtcConsentedPlayers += nameKey;
            }

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
        }

        Debug.LogWarning($"★ [MVP_DTC] {playerName}: 同意（データ記録対象に登録されました）");

        if (containerDtcConsent != null) containerDtcConsent.SetActive(false);

        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnReceiveDtcStatusUpdateNet));
    }

    public void OnDtcConsentDisagree()
    {
        EnsureUIReferences();

        hasDtcResponded = true;

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        string playerName = (localPlayer != null && localPlayer.IsValid()) ? localPlayer.displayName : "LocalUser";

        if (localPlayer != null && localPlayer.IsValid())
        {
            if (string.IsNullOrEmpty(syncedDtcDisagreedPlayers))
            {
                syncedDtcDisagreedPlayers = ",";
            }
            string nameKey = playerName + ",";
            if (!syncedDtcDisagreedPlayers.Contains("," + nameKey))
            {
                syncedDtcDisagreedPlayers += nameKey;
            }

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
        }

        Debug.LogWarning($"★ [MVP_DTC] {playerName}: 同意辞退（データ記録対象外）");

        if (containerDtcConsent != null) containerDtcConsent.SetActive(false);

        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnReceiveDtcStatusUpdateNet));
    }

    public void OnReceiveDtcStatusUpdateNet()
    {
        RefreshMasterPanelVisibility();
    }

    public bool CheckIfDtcResponded(string playerName)
    {
        if (hasDtcResponded) return true;
        if (string.IsNullOrEmpty(playerName)) return false;
        string nameKey = "," + playerName.Trim().ToLower() + ",";

        bool isAgreed = !string.IsNullOrEmpty(syncedDtcConsentedPlayers) && syncedDtcConsentedPlayers.ToLower().Contains(nameKey);
        bool isDisagreed = !string.IsNullOrEmpty(syncedDtcDisagreedPlayers) && syncedDtcDisagreedPlayers.ToLower().Contains(nameKey);

        return isAgreed || isDisagreed;
    }

    public bool IsUserDtcTarget(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return false;
        // RecordMasterNamesのプレイヤーは同意確認パネルの選択に関わらず、デフォルトで[MVP_DTC]データ記録対象とする
        if (CheckIfRecordMaster(playerName)) return true;

        if (syncedDtcTargetAllMode)
        {
            string nameKey = "," + playerName.Trim().ToLower() + ",";
            return !string.IsNullOrEmpty(syncedDtcConsentedPlayers) && syncedDtcConsentedPlayers.ToLower().Contains(nameKey);
        }
        else
        {
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
            if (targetUdonBehaviour != null && !string.IsNullOrEmpty(jsonNamesVariableName))
            {
                object val = targetUdonBehaviour.GetProgramVariable(jsonNamesVariableName);
                if (val != null)
                {
                    targetNames = (string[])val;
                }
            }
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
            if (totalTargetCount == 0 && !syncedTargetAllMode)
            {
                masterStatusText.text = $"<b>【配信対象: {modeTitle}】</b>\n対象ユーザーリスト (JsonNamesString) が空です。";
            }
            else
            {
                int percent = (presentTargetCount > 0) ? Mathf.RoundToInt((float)answeredCount / presentTargetCount * 100f) : 0;
                string header = $"<b>【配信対象: {modeTitle}】</b>\n<b>【滞在中回答進捗: {answeredCount} / {presentTargetCount} 人 ({percent}%)】</b>\n\n";

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
        hasDtcResponded = false;
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
        currentIndex = 0;

        // 全回答者の画面からアンケートパネルおよび完了結果パネルを即座に消す（非表示）
        if (surveyPanel != null) surveyPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);

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
                titleText.text = "アンケート参加の同意確認";
            }
            if (questionText != null)
            {
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

    public void OnConsentAgree()
    {
        EnsureUIReferences();

        if (containerConsent != null) containerConsent.SetActive(false);

        if (questionTexts != null && questionTexts.Length > 0)
        {
            userAnswers = new int[questionTexts.Length];
            userTextAnswers = new string[questionTexts.Length];

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

    public void ShowQuestion(int index)
    {
        if (index < 0 || index >= questionTexts.Length) return;

        if (containerConsent != null) containerConsent.SetActive(false);

        if (titleText != null)
        {
            titleText.text = $"質問 ({index + 1} / {questionTexts.Length})";
        }
        if (questionText != null)
        {
            questionText.text = questionTexts[index];
        }

        if (containerChoice != null) containerChoice.SetActive(false);
        if (containerRating != null) containerRating.SetActive(false);
        if (containerTextInput != null) containerTextInput.SetActive(false);

        int type = (answerTypes != null && index < answerTypes.Length) ? answerTypes[index] : 0;

        switch (type)
        {
            case 0: // 選択式
                if (containerChoice != null) containerChoice.SetActive(true);

                int count = (choiceCounts != null && index < choiceCounts.Length) ? choiceCounts[index] : 2;
                if (count < 1) count = 2;
                if (choiceButtons != null)
                {
                    int labelOffset = index * 6;
                    for (int i = 0; i < choiceButtons.Length; i++)
                    {
                        if (choiceButtons[i] != null)
                        {
                            if (i < count)
                            {
                                choiceButtons[i].SetActive(true);
                                int labelIdx = labelOffset + i;
                                if (choiceButtonTexts != null && i < choiceButtonTexts.Length && choiceButtonTexts[i] != null)
                                {
                                    if (optionLabels != null && labelIdx < optionLabels.Length && !string.IsNullOrEmpty(optionLabels[labelIdx]))
                                    {
                                        choiceButtonTexts[i].text = optionLabels[labelIdx];
                                    }
                                    else
                                    {
                                        choiceButtonTexts[i].text = $"選択肢 {i + 1}";
                                    }
                                }
                            }
                            else
                            {
                                choiceButtons[i].SetActive(false);
                            }
                        }
                    }
                }
                break;

            case 1: // スライダー評価式
                if (containerRating != null) containerRating.SetActive(true);
                if (ratingSlider != null)
                {
                    int maxVal = (sliderMaxValues != null && index < sliderMaxValues.Length) ? sliderMaxValues[index] : 5;
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
        }
    }

    public void SelectAnswer(int answerValue)
    {
        if (userAnswers != null && currentIndex < userAnswers.Length)
        {
            userAnswers[currentIndex] = answerValue;
            userTextAnswers[currentIndex] = answerValue.ToString();
        }

        NextQuestion();
    }

    public void OnChoiceOption0() { SelectAnswer(0); }
    public void OnChoiceOption1() { SelectAnswer(1); }
    public void OnChoiceOption2() { SelectAnswer(2); }
    public void OnChoiceOption3() { SelectAnswer(3); }
    public void OnChoiceOption4() { SelectAnswer(4); }
    public void OnChoiceOption5() { SelectAnswer(5); }

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
        if (userTextAnswers != null && currentIndex < userTextAnswers.Length)
        {
            userTextAnswers[currentIndex] = text;
            userAnswers[currentIndex] = -1;
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
        string cleanName = playerName.Trim();

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
                        if (targetNames[i] != null && targetNames[i].Trim().ToLower() == cleanName.ToLower())
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
                if (allowedUserNames[i] != null && allowedUserNames[i].Trim().ToLower() == cleanName.ToLower())
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
        string cleanName = playerName.Trim();

        bool hasTargetData = false;

        if (targetUdonBehaviour != null && !string.IsNullOrEmpty(variableName))
        {
            object val = targetUdonBehaviour.GetProgramVariable(variableName);
            if (val != null)
            {
                string[] targetNames = (string[])val;
                if (targetNames != null && targetNames.Length > 0)
                {
                    hasTargetData = true;
                    for (int i = 0; i < targetNames.Length; i++)
                    {
                        if (targetNames[i] != null && targetNames[i].Trim().ToLower() == cleanName.ToLower())
                        {
                            return true;
                        }
                    }
                }
            }
        }

        if (allowedUserNames != null && allowedUserNames.Length > 0)
        {
            for (int i = 0; i < allowedUserNames.Length; i++)
            {
                if (allowedUserNames[i] != null && allowedUserNames[i].Trim().ToLower() == cleanName.ToLower())
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
            default: return "選択式";
        }
    }

    private string GetAnswerContentString(int index, int type)
    {
        if (type == 0)
        {
            int selectedOptIdx = (userAnswers != null && index < userAnswers.Length) ? userAnswers[index] : 0;
            int labelOffset = index * 6 + selectedOptIdx;
            if (optionLabels != null && labelOffset < optionLabels.Length && !string.IsNullOrEmpty(optionLabels[labelOffset]))
            {
                return optionLabels[labelOffset];
            }
            return $"選択肢{selectedOptIdx + 1}";
        }
        else if (type == 1)
        {
            int val = (userAnswers != null && index < userAnswers.Length) ? userAnswers[index] : 0;
            return val.ToString();
        }
        else if (type == 2)
        {
            return (userTextAnswers != null && index < userTextAnswers.Length) ? userTextAnswers[index] : "";
        }
        return "";
    }
}
