using UnityEngine;
using UnityEngine.UI;
using UdonSharp;

namespace UnaPresentation
{
    public class TabletMonitor : UdonSharpBehaviour
    {
        [SerializeField]
        private PresentationEngine engine;
        [SerializeField]
        private Text SpeakerText;
        [SerializeField]
        private Text SlideNumberText;

        private void LateUpdate()
        {
            if (engine == null) return;

            SpeakerText.text = $"Speaker: {engine.SpeakerName}";
            // 2:IN_SESSION
            SlideNumberText.text = engine.SessionStatus == 2 ? $"{engine.SlideNumber}/{engine.MaxSlideNumber}" : "0/0";
        }

        public void SetEngine(PresentationEngine engine)
        {
            this.engine = engine;
        }
    }
}
