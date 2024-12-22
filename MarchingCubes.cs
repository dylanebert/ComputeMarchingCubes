using UnityEngine;

namespace MarchingCubes {
    public class MarchingCubes : MonoBehaviour {
        [SerializeField] private int _chunkSize = 16;
        [SerializeField] private float _gridScale = 1f;

        public static MarchingCubes Instance { get; private set; }

        public static int ChunkSize => Instance._chunkSize;
        public static float GridScale => Instance._gridScale;

        private void Awake() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("Multiple MarchingCubes instances found in scene. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
