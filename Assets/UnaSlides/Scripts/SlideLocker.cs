using UdonSharp;
using UnityEngine;

namespace UnaPresentation
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class SlideLocker : UdonSharpBehaviour
    {
        [SerializeField]
        private PresentationEngine engine;

        private void Start()
        {
            if (engine == null)
            {
                engine = GameObject.Find("UnaSlides").GetComponent<PresentationEngine>();
            }
        }

        public override void OnPickup()
        {
            engine.IsUsingPen = true;
        }

        public override void OnDrop()
        {
            engine.IsUsingPen = false;
        }
    }
}
