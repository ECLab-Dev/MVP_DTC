using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace UnaPresentation
{
    public class PenTabletManager : UdonSharpBehaviour
    {
        [SerializeField]
        private PresentationEngine engine;
        [SerializeField]
        private GameObject qvPen;
        [SerializeField]
        private PenTablet normalTablet;
        [SerializeField]
        private PenTablet largeTablet;
        [UdonSynced]
        private bool enlarged;

        private void Start()
        {
            normalTablet.Initialize(engine, qvPen, this);
            largeTablet.Initialize(engine, qvPen, this);
        }

        private void LateUpdate()
        {
            if (enlarged != largeTablet.gameObject.activeSelf)
            {
                var currentUI = normalTablet.gameObject.activeSelf ? normalTablet : largeTablet;
                var targetUI = normalTablet.gameObject.activeSelf ? largeTablet : normalTablet;

                currentUI.Deactivate();
                targetUI.Activate(
                    currentUI.transform.position,
                    currentUI.transform.rotation,
                    currentUI.IsPickable,
                    currentUI.UseSurftrace
                );
            }
        }

        public void ToggleSize()
        {
            if (Networking.IsOwner(Networking.LocalPlayer, gameObject))
            {
                enlarged = !enlarged;
                RequestSerialization();
            }
            else
            {
                SendCustomNetworkEvent(NetworkEventTarget.Owner, "ToggleSize");
            }
        }
    }
}
