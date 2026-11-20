using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using QvPen.UdonScript;

namespace UnaPresentation
{
    public class PenTablet : UdonSharpBehaviour
    {
        [SerializeField]
        private PresentationEngine engine;
        [SerializeField]
        private GameObject qvPen;
        private QvPen_PenManager[] penManagers;
        [SerializeField]
        private PenTabletManager manager;

        [SerializeField]
        private Toggle pickupToggle;
        [SerializeField]
        private VRC_Pickup pickup;
        public bool IsPickable { get { return pickupToggle.isOn; } }
        [SerializeField]
        private Toggle surftraceToggle;
        public bool UseSurftrace { get { return surftraceToggle.isOn; } }
        [SerializeField]
        private BoxCollider boxCollider;
        [SerializeField]
        private GameObject whiteScreen;
        [SerializeField]
        private GameObject blackScreen;

        private void Start()
        {
            pickup = (VRC_Pickup)gameObject.GetComponent(typeof(VRC_Pickup));
            boxCollider = gameObject.GetComponent<BoxCollider>();
        }

        private void LateUpdate()
        {
            if (engine == null || qvPen == null) return;

            whiteScreen.SetActive(engine.IsWhiteout);
            blackScreen.SetActive(engine.IsBlackout);
            pickup.pickupable = pickupToggle.isOn;
            boxCollider.isTrigger = !surftraceToggle.isOn;
        }

        public void Initialize(PresentationEngine engine, GameObject qvPen, PenTabletManager manager)
        {
            this.engine = engine;
            this.qvPen = qvPen;
            penManagers = qvPen.GetComponentsInChildren<QvPen_PenManager>();
            this.manager = manager;
        }

        public void Activate(Vector3 position, Quaternion rotation, bool isPickable, bool useSurftrace)
        {
            pickup.Drop();
            transform.position = position;
            transform.rotation = rotation;
            pickupToggle.isOn = isPickable;
            surftraceToggle.isOn = useSurftrace;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }

        // UI Event handlers
        public void OnBlackButton()
        {
            engine.ToggleBlackout();
        }
        public void OnWhiteButton()
        {
            engine.ToggleWhiteout();
        }
        public void OnClearButton()
        {
            // Replace this code to use other pen system.
            if (penManagers.Length == 0) qvPen.GetComponentsInChildren<QvPen_PenManager>();

            foreach (var penManager in penManagers)
            {
                penManager.SendCustomNetworkEvent(NetworkEventTarget.All, "Clear");
            }
        }

        public void OnSurftraceToggle()
        {
            if (penManagers.Length == 0) qvPen.GetComponentsInChildren<QvPen_PenManager>();

            foreach (var penManager in penManagers)
            {
                penManager._SetUsingSurftraceMode(surftraceToggle.isOn);
            }
        }

        public void OnResizeButton()
        {
            if (manager != null)
            {
                manager.ToggleSize();
            }
        }
    }
}
