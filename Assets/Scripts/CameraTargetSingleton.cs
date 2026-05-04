using UnityEngine;

namespace TMG.Survivors
{
    public class CameraTargetSingleton : MonoBehaviour
    {
        public static CameraTargetSingleton Instance;

        public void Awake()
        {
            if (Instance != null)
            {
                Debug.LogWarning("Warning multiple instances of CameraTargetSingleton detected. Destroying new instance.", Instance);
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }
    }
}