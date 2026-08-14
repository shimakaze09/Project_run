using UnityEngine;
using UnityEngine.UI;
using Run.Core;

namespace Run.UI
{
    /// <summary>
    /// Builds and drives the on-screen HUD: live score, best score, and a centred
    /// prompt for the Ready / Game Over states.
    /// </summary>
    /// <remarks>
    /// The canvas and its labels are constructed in code so the HUD is entirely
    /// self-contained — dropping this one component into the scene is all that is
    /// required. It listens to <see cref="GameManager"/> events rather than polling,
    /// and unsubscribes on destroy to avoid dangling handlers across scene reloads.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _fontSize = 40;
        [SerializeField] private Color _textColor = Color.white;

        private Text _scoreLabel;
        private Text _bestLabel;
        private Text _centerLabel;
        private GameManager _game;

        private void Awake()
        {
            BuildCanvas();
        }

        private void Start()
        {
            _game = GameManager.Instance;
            if (_game == null)
            {
                return;
            }

            _game.ScoreChanged += OnScoreChanged;
            _game.StateChanged += OnStateChanged;

            OnScoreChanged(_game.Score);
            OnStateChanged(_game.State);
        }

        private void OnDestroy()
        {
            if (_game == null)
            {
                return;
            }

            _game.ScoreChanged -= OnScoreChanged;
            _game.StateChanged -= OnStateChanged;
        }

        private void OnScoreChanged(int score)
        {
            if (_scoreLabel != null)
            {
                _scoreLabel.text = $"SCORE  {score}";
            }

            if (_bestLabel != null && _game != null)
            {
                _bestLabel.text = $"BEST  {_game.BestScore}";
            }
        }

        private void OnStateChanged(GameState state)
        {
            if (_centerLabel == null)
            {
                return;
            }

            switch (state)
            {
                case GameState.Ready:
                    _centerLabel.text = "TAP / SPACE TO START";
                    _centerLabel.enabled = true;
                    break;

                case GameState.Playing:
                    _centerLabel.enabled = false;
                    break;

                case GameState.GameOver:
                    _centerLabel.text = "GAME OVER\nTAP / SPACE TO RESTART";
                    _centerLabel.enabled = true;
                    break;
            }
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _scoreLabel = CreateLabel(canvas.transform, "Score",
                anchor: new Vector2(0f, 1f), position: new Vector2(30f, -24f),
                align: TextAnchor.UpperLeft, size: _fontSize);

            _bestLabel = CreateLabel(canvas.transform, "Best",
                anchor: new Vector2(0f, 1f), position: new Vector2(30f, -24f - _fontSize - 8f),
                align: TextAnchor.UpperLeft, size: Mathf.RoundToInt(_fontSize * 0.7f));

            _centerLabel = CreateLabel(canvas.transform, "Center",
                anchor: new Vector2(0.5f, 0.5f), position: Vector2.zero,
                align: TextAnchor.MiddleCenter, size: Mathf.RoundToInt(_fontSize * 1.1f));
        }

        private Text CreateLabel(Transform parent, string name, Vector2 anchor, Vector2 position, TextAnchor align, int size)
        {
            var labelObject = new GameObject(name, typeof(Text));
            labelObject.transform.SetParent(parent, false);

            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.color = _textColor;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = label.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(1000f, 120f);
            rect.anchoredPosition = position;

            return label;
        }
    }
}
