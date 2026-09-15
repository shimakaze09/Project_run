using UnityEngine;
using Run.Core;

namespace Run.Effects
{
    /// <summary>Small, camera-culled ambient sparks for authored coins and hazards.</summary>
    public sealed class ItemSparkle : MonoBehaviour
    {
        public BurstKind Kind = BurstKind.Spark;
        [Min(0.1f)] public float Interval = 0.7f;
        private float _timer;
        private Camera _camera;
        private void OnEnable() { _timer = Mathf.Abs(transform.position.x * 0.137f) % Mathf.Max(0.1f, Interval); }
        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Mathf.Max(0.1f, Interval);
            if (_camera == null) _camera = Camera.main;
            if (_camera == null || Mathf.Abs(transform.position.x - _camera.transform.position.x) >
                _camera.orthographicSize * _camera.aspect + 1f) return;
            GameFeel.Instance?.Emit(Kind, transform.position + Vector3.up * 0.25f);
        }
    }
}
