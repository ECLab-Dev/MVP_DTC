using System;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Components;
using UdonSharp;

namespace UnaPresentation
{
    public class SlideController : UdonSharpBehaviour
    {
        public PresentationEngine engine;
        public InputField NextSlideInputField;
        public Text Title;
        public Text Clock;

        // Control
        public GameObject ControlUI;
        public Text Timer;
        public Text SlideCounter;
        [SerializeField]
        private GameObject[] ModeButtonIcons;

        // Load
        public GameObject LoadUI;
        public VRCUrlInputField UrlInputField;
        public InputField SlideDurationField;
        public InputField SlideOffsetField;
        public Text PlayerStatusText;

        private void LateUpdate()
        {
            Clock.text = DateTime.Now.ToString("HH:mm");
            PlayerStatusText.text = engine.PlayersTotal > 0 ? $"{engine.PlayersInSession}/{engine.PlayersTotal}" : "Status";
            for (var i = 0; i < ModeButtonIcons.Length; i++)
            {
                ModeButtonIcons[i].SetActive(i == engine.PagenationScope);
            }
            if (engine.SessionStatus == 2)  // 2:IN_SESSION
            {
                Timer.text = $"{engine.GetPresentationElapsed().ToString(@"mm\:ss")}";
                SlideCounter.text = $"{engine.SlideNumber}/{engine.MaxSlideNumber}";
            }
            if (!string.IsNullOrEmpty(engine.SpeakerName))
            {
                Title.text = $"{engine.SpeakerName}";
            }
        }

        public void Next()
        {
            engine.Next();
        }

        public void Back()
        {
            engine.Back();
        }

        public void GoTo()
        {
            int targetSlide = -1;
            if (int.TryParse(NextSlideInputField.text, out targetSlide))
            {
                engine.GoTo(targetSlide);
                NextSlideInputField.text = string.Empty;
            }
            else
            {
                NextSlideInputField.text = "invalid";
            }
        }

        public void Skip()
        {
            engine.PlayVideo();
        }

        public void BackToTop()
        {
            engine.GoToTop();
        }

        public void ToggleMode()
        {
            engine.ToggleMode();
        }

        public void UpdatePlayerStatus()
        {
            engine.RequireSessionStatus();
        }

        public override void OnPickup()
        {
            engine.ChangeOwner();
        }

        public void OnUrlInput()
        {
            if (engine.OpenPresentation(UrlInputField.GetUrl()))
            {
                ActivateControlUI();
            }
            else
            {
                UrlInputField.SetUrl(VRCUrl.Empty);
            }
        }

        public void OnReloadButton()
        {
            engine.ResetPresentation();
        }
        public void OnStopButton()
        {
            if (engine.StopPresentation())
            {
                UrlInputField.SetUrl(VRCUrl.Empty);
                SlideDurationField.text = string.Empty;
            }
        }

        public void OnDurationChanged()
        {
            float slideDuration;
            if (!float.TryParse(SlideDurationField.text, out slideDuration))
            {
                slideDuration = 1.0f;
            }

            engine.SetDuration(slideDuration);
            SlideDurationField.text = slideDuration.ToString();
        }

        public void OnOffsetChanged()
        {
            float slideOffset;
            if (!float.TryParse(SlideOffsetField.text, out slideOffset))
            {
                slideOffset = 0.35f;
            }

            engine.SetOffset(slideOffset);
            SlideOffsetField.text = slideOffset.ToString();
        }

        public void ActivateControlUI()
        {
            LoadUI.SetActive(false);
            ControlUI.SetActive(true);
        }

        public void ActivateLoadUI()
        {
            ControlUI.SetActive(false);
            LoadUI.SetActive(true);
        }
    }
}
