using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace UnaSlides
{
    public class UserGuide : UdonSharpBehaviour
    {
        [SerializeField]
        private Text JapanaseText;
        [SerializeField]
        private Text EnglishText;
        [SerializeField]
        private Text ButtonText;
        [SerializeField]
        private InputField YouTubeInput;
        [SerializeField]
        private InputField QuestInput;

        public void ToggleLanguages()
        {
            if (JapanaseText.gameObject.activeSelf)
            {
                JapanaseText.gameObject.SetActive(false);
                EnglishText.gameObject.SetActive(true);
                ButtonText.text = "日本語";
            }
            else
            {
                EnglishText.gameObject.SetActive(false);
                JapanaseText.gameObject.SetActive(true);
                ButtonText.text = "English";
            }
        }

        public void ConvertUrl()
        {
            QuestInput.text = "https://unagiken.com/yt?url=" + YouTubeInput.text;
        }

        public void ClearUrls()
        {
            YouTubeInput.text = string.Empty;
            QuestInput.text = string.Empty;
        }
    }
}
