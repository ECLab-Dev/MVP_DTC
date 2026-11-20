using UnityEngine;
using UdonSharp;
using VRC.SDKBase;
using VRC.SDK3.Components;

namespace UnaPresentation
{
    public class TabletDispenser : UdonSharpBehaviour
    {
        [SerializeField]
        private PresentationEngine engine;
        private VRCObjectPool tabletPool;

        private void Start()
        {
            tabletPool = GetComponent<VRCObjectPool>();
            foreach (var tablet in tabletPool.Pool)
            {
                tablet.GetComponent<TabletMonitor>().SetEngine(engine);
            }
        }

        public void SpawnTablet()
        {
            if (!Networking.LocalPlayer.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            tabletPool.TryToSpawn();
        }
    }
}
