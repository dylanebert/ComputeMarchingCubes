using UnityEngine;

namespace MarchingCubes {
    [ExecuteInEditMode]
    public class MarchingCubes : MonoBehaviour {
        [SerializeField] private int _chunkSize = 16;
        [SerializeField] private float _scale = 1f;

        public static MarchingCubes Instance { get; private set; }

        public static int ChunkSize => Instance._chunkSize;
        public static float Scale => Instance._scale;

        private void OnEnable() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("Multiple MarchingCubes instances found in scene. Destroying duplicate.");
#if UNITY_EDITOR
                DestroyImmediate(gameObject);
#else
                Destroy(gameObject);
#endif
                return;
            }

            Instance = this;
        }
    }
}
