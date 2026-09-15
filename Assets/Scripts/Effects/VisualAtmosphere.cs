using UnityEngine;
using Run.Core;

namespace Run.Effects
{
    /// <summary>Camera-relative scenery. Visual-only parallax stays continuous across world shifts.</summary>
    public sealed class VisualAtmosphere : MonoBehaviour
    {
        public static VisualAtmosphere Instance { get; private set; }
        private Camera _camera;
        private Transform _scenery;
        private Transform _far;
        private Transform _near;
        private float _worldOffset;
        private float _moteTimer;

        private void Awake() { Instance = this; }
        private void Start()
        {
            _camera = Camera.main;
            var prefab = Resources.Load<GameObject>("Scenery/Twilight");
            if (_camera == null || prefab == null) return;
            _scenery = Instantiate(prefab, transform).transform;
            _far = _scenery.Find("Far Mountains");
            _near = _scenery.Find("Near Mountains");
        }
        private void LateUpdate()
        {
            if (_scenery == null || _camera == null) return;
            _scenery.position = new Vector3(_camera.transform.position.x, _camera.transform.position.y, 0f);
            float distance = _camera.transform.position.x + _worldOffset;
            if (_far != null) _far.localPosition = new Vector3(-Mathf.Repeat(distance * 0.12f, 40f), 0f, 0f);
            if (_near != null) _near.localPosition = new Vector3(-Mathf.Repeat(distance * 0.3f, 40f), 0f, 0f);
            // Emission stops at Game Over, but existing particles finish naturally.
            if (GameManager.Instance == null || GameManager.Instance.State == GameState.GameOver) return;
            _moteTimer -= Time.deltaTime;
            if (_moteTimer <= 0f)
            {
                _moteTimer = 0.3f;
                float phase = Time.time * 0.618f;
                float halfWidth = _camera.orthographicSize * _camera.aspect;
                Vector3 position = _camera.transform.position + new Vector3(
                    Mathf.Sin(phase * 7f) * halfWidth, Mathf.Sin(phase * 3f) * 4f, 10f);
                GameFeel.Instance?.Emit(BurstKind.Mote, position);
            }
        }
        public void Shift(float dx) { _worldOffset -= dx; }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
