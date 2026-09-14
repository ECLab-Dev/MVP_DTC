# MVP Lab
At Metaverse Public (MVP) Lab, we view the metaverse as a society and conduct research on interesting phenomena occurring there. We also aim to establish research methods suitable for the metaverse.

![MVP Logo](Images/MVP_Logo.png "Logo")

# MVP_DTC - Data Collection System by MVP Lab
A comprehensive data collection and management system designed for VR environments such as VRChat, integrating **movement data collection & secure cloud synchronization** alongside an **interactive in-world questionnaire survey system**.
---
## 🚀 Modules Overview
This repository consists of three core modules tailored for different data collection requirements:
| Module | Description |
| :--- | :--- |
| **`MVP_DTC`** | Monitors local VRChat logs and extracts spatial movement & tracking data. |
| **`MVP_DTC_Online`** | Automatically uploads and synchronizes movement data directly to designated secure cloud storage. |
| **`MVP_Questionnaire`** | Interactive in-world survey system built with UdonSharp for dynamic questionnaire deployment and real-time response monitoring in VRChat. |
---

# MVP_DTC
Data collection system by MVP Lab

# Requirement
VRChat SDK Ver. 3.6.1 or later
Unity 2022.3.22f1

![Construction of Data collection prefab](Images/DatacollectionPrefab.png "Datacollection")

# How to use (MVP_DTC)
## Prefab set
1. Clone or download this repository.
2. Import the contents of this repository into your Unity project.
3. Put the "MVP_DTC.prefab" (./MVP_DTC/Prefabs) to hierarchy.

## UDON Behaviour (Datacollection)
### Main custom
1. Set some VRChat IDs to "Staffnames".
2. Enter the Name (Text) under the Index corresponding to the number of players you want to record in "RecordNames" (max 30 number).
3. Enter the Value (Text) under the Index corresponding to the number of players you want to record in "RecordValues" (max 30 number).
4. Set the button UI on the Agreebutton.
5. Set the RecordAgree event of Data collection UDON Behaviour to the On Click event of the button UI.
![Staffnames](Images/StaffNames.png "StaffNames")
![RecordNames](Images/RecordNames.png "RecordNames")
![RecordValues](Images/RecordValues.png "RecordValues")

### Other variables
|  Name  |  Type  |  Usage  |  Default   |
| ---- | ---- | ---- | ---- |
|  OwnerObject  |  Gameobject  |  Set objects with the UDON Behaviour for “Datacollection” inserted  |  Datacollection  |
|  AgreeButton  |  UIButton  |  Set the UIButton to confirm consent  |  AgreeButton  |
|  IndexText  |  UIText  |  Display the current number of record players  |  CurrentNumber  |
|  MainCanvas  |  Canvas  |  Debug display canvas for Datacollection  |  Datacollection  |
|  ButtonASource  |  AudioSource  |  Audio output source when the consent button is pressed  |  Agreement  |

## Place of log
With the default settings, it will be output to the user folder (Exp. C:\Users\[USER NAME]\AppData\LocalLow\VRChat\VRChat).

The default structure of log data is as follows:

[VRChat ID]: [Index]: [Head angular (yaw, pitch roll)] : [Avatar angular (yaw, pitch roll)] : [AVatar position]: [mm/dd/yyyy HH:MM:SS: milisec]

# 📡 MVP_DTC_Online (Cloud Data Collection & Live Web Dashboard)
`MVP_DTC_Online` extends the core `MVP_DTC` system by introducing a local web-based live dashboard (`VRCLogLiveWebStream`) combined with automated cloud data backup capabilities. It allows experimenters and researchers to track player movements on a real-time 2D radar, review live questionnaire responses, and automatically upload collected log data to a designated secure cloud storage endpoint (e.g., Google Sheets via Google Apps Script) upon session completion.

# How to use (MVP_DTC_Online)
## Server setting
MVP_DTC_Online loads a user name file—equivalent to “Staffnames”—from a user-created external server. The system then loads the file containing the names of players to be tracked from the external server and records data only for specific players.
The file that records “Staffnames” and player names follows the JSON format below:

```json
{
  "PlayerNames": [
    "NAME1",
    "NAME2",
    "NAME3",
    "NAME4"
  ],
  "StaffNames": [
    "Name1",
    "Name2"
  ]
}
```

## Prefab set
1. Clone or download this repository.
2. Import the contents of this repository into your Unity project.
3. Put the "MVP_DTC_Online.prefab" (./MVP_DTC/Prefabs) to hierarchy.

## UDON Behaviour (Datacollection_Json)
### Main custom
1. Set “JsonURL” to an external server link that provides access to a JSON file containing “Staffnames” and the names of the players to be recorded.

![MVP_DTC_Online](Images/MVP_DTC_Online.png "MVP_DTC_Online")

### 🌟 Key Features
- 🎯 **Real-Time 2D Radar Visualizer**: Visualizes nearby player coordinates on an interactive 2D radar map in real time.
- 📋 **Live Questionnaire Cards**: Displays incoming survey responses instantly as participants complete questionnaires in VRChat.
- ☁️ **Automated Cloud Data Upload**: Securely uploads recorded movement logs and survey results to your specified cloud storage endpoint (e.g., Google Apps Script / Webhook) when VRChat is closed.
- 🎥 **Integrated HLS Video Streaming**: Automatically manages MediaMTX and FFmpeg dependencies to provide low-latency video streaming capabilities.
---
### 🚀 Setup & Usage Instructions
#### Step 1: Download & Extraction
1. Download `VRCLogLiveWebStream.zip` from the latest release or repository folder.
2. Extract the ZIP archive to any directory on your local machine.
#### Step 2: Launch Application
1. Double-click `VRCLogLiveWebStream.exe` to start the application.
2. **First-Time Dependency Download**:
   If FFmpeg or MediaMTX are not detected on your system, the application will automatically download them on first launch. Real-time download progress (`%` and `MB / total MB`) will be displayed directly in the command prompt window.
#### Step 3: Web Dashboard Configuration
1. Open any modern web browser and navigate to:
   ```text
   http://localhost:5000
2. Configure the parameters in the left sidebar as detailed below:

![Application](Images/VRChatLogLiveWebStreaming.png "Application")

| Field Name | Location in App | What to Enter | Description & Purpose |
| :--- | :--- | :--- | :--- |
| **VRChat Player Name**<br>*(VRChat プレイヤー名)* | Application Settings | Your VRChat in-game Display Name<br>*(e.g., `Sakurada`)* | Identifies the host/local player's log entries in recorded data files and cloud backups. |
| **Cloud Storage URL**<br>*(クラウド保存 URL)* | Application Settings | Webhook Endpoint URL<br>*(e.g., Google Apps Script Web App URL: `https://script.google.com/macros/s/.../exec`)* | The destination URL where movement logs and survey responses are automatically POSTed when VRChat is closed. |
| **API Key / Access Token**<br>*(API Key / アクセストークン)* | Application Settings | Secret Authentication Token / Password *(Optional)* | Passed along with the payload to authenticate and authorize upload requests on your server/GAS script. |
| **Radar Display Range**<br>*(レーダー表示範囲)* | Radar Display Settings | Range Slider<br>*(10m – 200m, Default: `50m`)* | Adjusts the zoom scale/radius of the 2D radar map view. |
| **Center Player**<br>*(中心にするプレイヤー)* | Radar Display Settings | Dropdown Menu<br>*(Select `World Origin (0,0)` or any detected player)* | Sets the relative coordinate origin (center point) for player tracking on the radar map. |

3. **Saving / Resetting Configuration**:
   - Click **💾 Save Application Settings** (`アプリ設定を保存`) after inputting your details. This saves your credentials to `config-live.json` so they are automatically loaded on subsequent launches.
   - Click **🔄 Reset Settings to Default** (`設定を初期値にリセット`) if you need to clear all input fields and wipe local cached credentials before passing the application to another user.
#### Step 4: Data Collection Workflow
1. Keep `VRCLogLiveWebStream.exe` running in the background during your VRChat session.
2. Monitor player positions on the 2D radar and view real-time survey response cards via the dashboard.
3. Upon closing VRChat, the application automatically aggregates the session logs and transmits them to your configured cloud storage endpoint.

# MVP_Questionnaire
`MVP_Questionnaire` is an automated in-world survey system built on UdonSharp (SDK3). It provides a full suite for administrators to broadcast surveys, monitor response rates in real-time, and collect structured responses directly inside VRChat worlds.
## 🌟 Key Features
### ① 3 Question Formats
- **Multiple Choice**: Choice buttons (e.g., Yes/No, Platform Selection).
- **Rating Slider**: 1-to-N scale evaluation (e.g., 5-point satisfaction rating).
- **Text Input**: Free-form text responses using VRChat in-game keyboard (`InputField`).
### ② Dual Target Modes (Radio Button Selection)
Administrators can toggle between two target modes directly on the control panel:
- **🎯 List-Restricted Mode (`JsonNamesString`)**:
  - Displays the survey only to pre-registered, pre-consented participants.
  - Skips the consent screen and starts immediately at Question 1.
- **🌐 All Instance Players Mode (with Consent Screen)**:
  - Broadcasts to **all players present in the instance** (excluding `RecordMaster` staff).
  - Prompts participants with an initial **Consent Confirmation Screen** ("Agree" / "Disagree").
  - If the player selects **Agree**, they proceed to the survey questions.
  - If the player selects **Disagree**, the survey terminates cleanly and counts the response as completed/declined.
### ③ Master Control Panel for Administrators
Dedicated control panel interface for event hosts and administrators (`RecordMaster`):
- **Real-Time Progress Monitoring**: Tracks response percentages, lists present unanswered players, completed players, and departed participants.
- **📋 Re-display to Unanswered Players**: One-click re-display of active surveys for participants who closed the window or re-joined.
- **🔄 Survey Reset**: Instantly resets the survey state for a new session.
- **Safety Locks & Error Guards**:
  - Automatically locks mode selection during active survey distribution.
  - Disables the start button when 0 valid target respondents are present in the instance.
---

![MVP_Questionnaire](Images/MVP_Questionnnaire.png "MVP_Questionnaire")

## 🛠️ Setup & Usage
### 1. Unity UI Auto-Setup (`MVP_Questionnaire`)
1. Place `SurveyManager.cs` and `Editor/SurveyManagerEditor.cs` in your Unity project (`Assets/Editor`).
2. Attach `SurveyManager` to your target Canvas object.
3. Click **"★ Auto-Generate & Setup UI"** in the Inspector to automatically construct all panels, consent screens, radio buttons, and component bindings.
### 2. Variable References
- **`RecordMasterNames`**: Array of DisplayNames for administrators/staff (granted control panel access; excluded from survey prompts).
- **`JsonNamesString`**: Array of target DisplayNames for List-Restricted Mode.
---
## 📝 Output Log Format
Survey responses are recorded as structured log entries:
```text
[MVP_Q] PlayerName: Q1: Did you enjoy the event?: Choice: Yes | Q2: Satisfaction: Rating: 5 | Q3: Comments: Text: Great experience!

# Reference
[VRChat内位置情報・アンケート収集解析ツールYAIBAの紹介](https://note.com/cocu_tan/n/n70972d7646bd)
<br>
[YAIBA-VRC](https://note.com/cocu_tan/n/n70972d7646bd)

# Acknowledgement
This project was supported by MVP Lab.
