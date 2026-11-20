using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;
//using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using UdonSharp;
using Debug = UnityEngine.Debug;

namespace UnaPresentation
{
    public class PresentationEngine : UdonSharpBehaviour
    {
        //public VRCUnityVideoPlayer VideoPlayer;
        public VRCAVProVideoPlayer VideoPlayer;
        [SerializeField]
        private GameObject splashScreen;
        [SerializeField]
        private GameObject whiteScreen;
        [SerializeField]
        private GameObject blackScreen;
        public GameObject MessageWindow;
        private Text messageText;
        [SerializeField]
        private Text debugText;
        [SerializeField]
        private Toggle debugToggle;
        [SerializeField]
        private bool DebugMode = false;
        private float startAt = 0.0f;

        [UdonSynced]
        public float SlideDuration = 1.0f;
        [UdonSynced]
        public float StartOffset = 0.35f;

        [UdonSynced]
        public VRCUrl PresentationUrl;
        private VRCUrl previousPresentationUrl;

        [UdonSynced]
        public int SlideNumber = 0;
        private int localSlideNumber = 0;

        [UdonSynced]
        public int MaxSlideNumber = 0;
        private Stopwatch presentationTimer = new Stopwatch();

        // Local status
        private const int STATUS_STOP = 0;
        private const int STATUS_LOADING = 1;
        private const int STATUS_IN_SESSION = 2;
        public int SessionStatus = 0;

        [UdonSynced]                            
        public bool IsPlayingVideo = false;

        [UdonSynced]
        public string SpeakerName = string.Empty;

        [UdonSynced]
        public int PagenationScope = 1;    // 0:None, 1:Speaker, 2:Everyone
        public bool IsUsingPen = false;

        [UdonSynced]
        public bool IsBlackout = false;
        [UdonSynced]
        public bool IsWhiteout = false;

        private Stopwatch retryTimer = new Stopwatch();
        private float retryInterval = 5.5f;

        // Player status
        public int PlayersTotal { get; private set; }
        public int PlayersInSession { get; private set; }
        public int PlayersLoading { get; private set; }
        public int PlayersStop { get; private set; }

        private int debugInterval = 10;
        private int debugIntervalCounter = 0;

        private void Start()
        {
            debugToggle.isOn = DebugMode;
            VideoPlayer.Loop = false;
            VideoPlayer.EnableAutomaticResync = false;

            messageText = MessageWindow.GetComponentInChildren<Text>();
            if (Networking.IsOwner(gameObject))
            {
                ChangeOwner();
            }
        }

        private void Update()
        {
            DebugMode = debugToggle.isOn;

            if (DebugMode)
            {
                if (debugIntervalCounter == debugInterval)
                {
                    debugIntervalCounter = 0;
                }
                else
                {
                    debugIntervalCounter += 1;
                }
            }

            // NOTE: Do not apply synced variables at OnDeserialize for late-commoer

            if (retryTimer.IsRunning)
            {
                var msecToReload = retryInterval * 1000 - retryTimer.ElapsedMilliseconds;
                if (msecToReload > 0)
                {
                    ShowMessage($"Wait {(int)(msecToReload / 1000)} sec to reload...");
                }
                else
                {
                    // Retry when interval elapsed
                    retryTimer.Reset();
                    retryTimer.Stop();
                    ResetPresentation();
                }
            }

            if (PresentationUrl == null || string.IsNullOrEmpty(PresentationUrl.Get()))
            {
                // Presentation Url is empty
                ResetPresentation();
            }
            else if (previousPresentationUrl != null && PresentationUrl.Get() != previousPresentationUrl.Get())
            {
                // Presentation Url changed
                LoadPresentation();
            }

            splashScreen.SetActive(SessionStatus == STATUS_STOP);
            whiteScreen.SetActive(IsWhiteout);
            blackScreen.SetActive(IsBlackout);

            if (SessionStatus == STATUS_IN_SESSION)
            {
                HideMessage();

                if (DebugMode)
                {
                    var targetTime = (SlideNumber - 1) * SlideDuration + StartOffset;
                    if (targetTime > VideoPlayer.GetDuration() - StartOffset)
                    {
                        targetTime = VideoPlayer.GetDuration() - StartOffset;
                    }
                    debugText.text = $"#{SlideNumber} / Actual {VideoPlayer.GetTime()}sec / Calc {targetTime}sec\nDuration {SlideDuration}sec / Offset {StartOffset}sec / Start at {startAt}sec";

                    if (debugIntervalCounter == 0)
                    {
                        Debug.Log(debugText.text);
                    }
                }
                debugText.gameObject.SetActive(DebugMode);

                // Play/Pause video
                if (IsPlayingVideo)
                {
                    if (!VideoPlayer.IsPlaying)
                    {
                        VideoPlayer.Play();
                    }
                }
                else
                {
                    if (VideoPlayer.IsPlaying)
                    {
                        Debug.Log($"Still playing even pause() called right after play(): {VideoPlayer.IsPlaying}");

                        VideoPlayer.Pause();

                        // Comment out temporarily to prevent back to the point to pause right after play()

                        //// Calculate slide number and update to continuing static slide show
                        //SlideNumber = (int)(Math.Floor(VideoPlayer.GetTime()) / SlideDuration + 1);
                        //RequestSerialization();
                    }

                    // Show current slide when SlideNumber is updated (synced)
                    if (localSlideNumber != SlideNumber)
                    {
                        if (DebugMode)
                        {
                            Debug.Log($"SlideNumber updated: {localSlideNumber} -> {SlideNumber}");
                        }

                        // Update slide number and set target time
                        localSlideNumber = SlideNumber;
                        var targetTime = (SlideNumber - 1) * SlideDuration + StartOffset;
                        if (targetTime > VideoPlayer.GetDuration() - StartOffset)
                        {
                            targetTime = VideoPlayer.GetDuration() - StartOffset;
                        }

                        // Set time to show
                        VideoPlayer.SetTime(targetTime);

                        // Play and pause to update screen
                        VideoPlayer.Play();
                        VideoPlayer.Pause();

                        if (DebugMode)
                        {
                            Debug.Log($"#{SlideNumber} / VideoPlayer Actual time: {VideoPlayer.GetTime()} / Target time: {targetTime}");
                        }
                    }
                }
            }
        }

        // Handler for SlideController operations
        public bool OpenPresentation(VRCUrl url)
        {
            if (!Networking.IsOwner(gameObject)) return false;

            PresentationUrl = url;
            RequestSerialization();
            return true;
        }

        public bool StopPresentation()
        {
            if (!Networking.IsOwner(gameObject)) return false;

            PresentationUrl = VRCUrl.Empty;
            SlideDuration = 1.0f;
            RequestSerialization();
            return true;
        }

        public void LoadPresentation()
        {
            ResetPresentation();
            ShowMessage("Loading slide...");
            SessionStatus = STATUS_LOADING;
            VideoPlayer.LoadURL(PresentationUrl);
            previousPresentationUrl = PresentationUrl;
        }

        public void ResetPresentation()
        {
            VideoPlayer.Stop();
            previousPresentationUrl = VRCUrl.Empty;
            SessionStatus = STATUS_STOP;
            if (Networking.IsOwner(gameObject))
            {
                // Just the owner (not the audience) can reset the slideshow status.
                SlideNumber = 0;
                MaxSlideNumber = 0;
                IsPlayingVideo = false;
                RequestSerialization();
                ResetPlayersStatus();
            }
        }

        public void ChangeOwner()
        {
            if (Networking.LocalPlayer == null) return;

            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            SpeakerName = Networking.LocalPlayer.displayName;
            RequestSerialization();
        }

        public void SetDuration(float duration)
        {
            SlideDuration = duration;
            MaxSlideNumber = (int)(Math.Floor(VideoPlayer.GetDuration()) / SlideDuration + 1);
            RequestSerialization();
        }

        public void SetOffset(float offset)
        {
            StartOffset = offset;
            RequestSerialization();
        }

        public void ToggleMode()
        {
            if (!Networking.IsOwner(Networking.LocalPlayer, gameObject)) return;

            if (PagenationScope == 2)
            {
                PagenationScope = 0;
            }
            else
            {
                PagenationScope += 1;
            }
            RequestSerialization();
        }

        // Event handlers for video player
        public override void OnVideoReady()
        {
            if (Networking.IsOwner(Networking.LocalPlayer, gameObject))
            {
                //if (DebugMode)
                //{
                //    VideoPlayer.Pause();
                //    VideoPlayer.SetTime(0.0f);
                //    startAt = VideoPlayer.GetTime();
                //}

                // Update and sync presentation statuses (presentation control will be processed at Update())
                SlideNumber = 1;
                MaxSlideNumber = (int)(Math.Floor(VideoPlayer.GetDuration()) / SlideDuration + 1);
                RequestSerialization();
            }

            presentationTimer.Restart();
            SessionStatus = STATUS_IN_SESSION;
        }

        public override void OnVideoError(VideoError err)
        {
            if (err == VideoError.RateLimited)
            {
                // Retry after interval
                retryTimer.Restart();
            }
            else
            {
                SessionStatus = STATUS_STOP;
                ShowMessage($"Error: {err}");
            }
        }

        // Presentation control
        public void Next()
        {
            if (PagenationScope == 0) return;

            if (IsUsingPen) return;

            if (Networking.IsOwner(gameObject))
            {
                GoTo(SlideNumber + 1);
                BreakBW();
            }
            else
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NextFromAudience");
            }
        }

        public void NextFromAudience()
        {
            if (PagenationScope == 2)
            {
                Next();
            }
        }

        public void Back()
        {
            if (PagenationScope == 0) return;

            if (IsUsingPen) return;

            if (Networking.IsOwner(gameObject))
            {
                GoTo(SlideNumber - 1);
                BreakBW();
            }
            else
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "BackFromAudience");
            }
        }

        public void BackFromAudience()
        {
            if (PagenationScope == 2)
            {
                Back();
            }
        }

        public void GoTo(int slideNumber)
        {
            if (!Networking.IsOwner(Networking.LocalPlayer, gameObject)) return;
            if (SessionStatus != STATUS_IN_SESSION) return;

            if (slideNumber <= 0)
            {
                SlideNumber = 1;
            }
            else if (slideNumber > MaxSlideNumber)
            {
                SlideNumber = MaxSlideNumber;
            }
            else
            {
                SlideNumber = slideNumber;
            }
            BreakBW();

            RequestSerialization();
        }

        public void GoToTop()
        {
            if (!Networking.IsOwner(Networking.LocalPlayer, gameObject)) return;

            presentationTimer.Restart();
            GoTo(1);
            BreakBW();
        }

        public bool PlayVideo()
        {
            if (!Networking.IsOwner(Networking.LocalPlayer, gameObject)) return IsPlayingVideo;
            if (SessionStatus != STATUS_IN_SESSION) return IsPlayingVideo;

            IsPlayingVideo = !IsPlayingVideo;
            BreakBW();
            RequestSerialization();
            return IsPlayingVideo;
        }

        public void  ToggleWhiteout()
        {
            if (!Networking.IsOwner(Networking.LocalPlayer, gameObject)) return;

            IsWhiteout = !IsWhiteout;
            IsBlackout = false;
            RequestSerialization();
        }

        public void ToggleBlackout()
        {
            if (!Networking.IsOwner(Networking.LocalPlayer, gameObject)) return;

            IsBlackout = !IsBlackout;
            IsWhiteout = false;
            RequestSerialization();
        }

        public void BreakBW()
        {
            if (!Networking.IsOwner(Networking.LocalPlayer, gameObject)) return;

            IsWhiteout = false;
            IsBlackout = false;
            RequestSerialization();
        }

        // UdonSharp doesn't allow to access getter prop from other scripts :(
        public TimeSpan GetPresentationElapsed()
        {
            return presentationTimer.Elapsed;
        }

        // Status Checker. This is NOT EXACTLY CORRECT because this doesn't lock the thread to handle the status of each player
        public void RequireSessionStatus()
        {
            if (!Networking.IsOwner(Networking.LocalPlayer, gameObject)) return;

            ResetPlayersStatus();
            PlayersTotal = VRCPlayerApi.GetPlayerCount();
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "RespondSessionStatus");
        }

        public void HandleStatusInSession()
        {
            PlayersInSession += 1;
        }

        public void HandleStatusLoading()
        {
            PlayersLoading += 1;
        }

        public void HandleStatusStop()
        {
            PlayersStop += 1;
        }

        public void RespondSessionStatus()
        {
            if (SessionStatus == STATUS_IN_SESSION)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "HandleStatusInSession");
            }
            else if (SessionStatus == STATUS_LOADING)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "HandleStatusLoading");
            }
            else
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "HandleStatusStop");
            }
        }

        private void ResetPlayersStatus()
        {
            PlayersTotal = 0;
            PlayersInSession = 0;
            PlayersLoading = 0;
            PlayersStop = 0;
        }

        // Message
        public void ShowMessage(string message)
        {
            messageText.text = message;
            MessageWindow.SetActive(true);
        }

        public void HideMessage()
        {
            MessageWindow.SetActive(false);
            messageText.text = string.Empty;
        }
    }
}
