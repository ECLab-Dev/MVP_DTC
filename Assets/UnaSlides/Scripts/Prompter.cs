using System;
using UdonSharp;
using UnityEngine.UI;
using VRC.SDKBase;

namespace UnaPresentation
{
    public class Prompter : UdonSharpBehaviour
    {
        public PresentationEngine engine;
        public InputField NoteField;
        public Toggle SyncToggle;
        public Toggle PickupToggle;
        public Text TimerText;
        public Text NoteNumberText;
        public Text ClockText;

        private string[] notes = new string[999];
        private int noteNumber = 1;
        private VRC_Pickup pickup;

        private void Start()
        {
            pickup = (VRC_Pickup)gameObject.GetComponent(typeof(VRC_Pickup));
            if (Networking.LocalPlayer == null) return;
            notes[0] = $"Local prompter for {Networking.LocalPlayer.displayName}. Enter note here for slide 1.";
        }

        private void LateUpdate()
        {
            ClockText.text = DateTime.Now.ToString("HH:mm");

            if (SyncToggle.isOn && engine.SlideNumber > 0)
            {
                noteNumber = engine.SlideNumber;

                if (engine.SessionStatus == 2)  // 2:IN_SESSION
                {
                    TimerText.text = $"{engine.GetPresentationElapsed().ToString(@"mm\:ss")}";
                }
            }

            NoteField.text = notes[noteNumber - 1];
            NoteNumberText.text = $"{noteNumber}/999";

            pickup.pickupable = PickupToggle.isOn;
        }

        public void UpdateNote()
        {
            var text = NoteField.text;
            if (text.Contains("\n====\n"))
            {
                var noteTexts = text.Split(new string[] { "\n====\n" }, StringSplitOptions.None);
                for (var i = 0; i < noteTexts.Length && i < notes.Length; i++)
                {
                    notes[i] = noteTexts[i];
                }
                NoteField.text = notes[noteNumber - 1];
            }
            else
            {
                notes[noteNumber - 1] = text;
            }
        }

        public void Next()
        {
            if (SyncToggle.isOn)
            {
                engine.Next();
            }
            else if (noteNumber < 999)
            {
                noteNumber += 1;
            }
        }

        public void Back()
        {
            if (SyncToggle.isOn)
            {
                engine.Back();
            }
            else if (noteNumber > 1)
            {
                noteNumber -= 1;
            }
        }

        public void Skip()
        {
            if (SyncToggle.isOn)
            {
                engine.PlayVideo();
            }
        }
    }
}
