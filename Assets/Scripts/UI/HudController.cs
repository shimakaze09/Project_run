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
        private Text _coinsLabel;
        private Text _coinPopup;
        private CanvasGroup _barGroup;
        private int _displayedCoins;
        private float _popupTime;
        private float _fadeTime;
        private bool _barComplete;
        [SerializeField, Min(0.01f)] private float _barFadeDuration = 0.6f;
        [SerializeField, Min(0.01f)] private float _coinPopupDuration = 0.7f;
        private RectTransform _progressFill;
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

            _displayedCoins = _game.Coins;
            _game.ScoreChanged += OnScoreChanged;
            _game.StateChanged += OnStateChanged;
            _game.DistanceChanged += OnDistanceChanged;
            _game.CoinsChanged += OnCoinsChanged;
            OnDistanceChanged(_game.Distance);
            OnCoinsChanged(_game.Coins);

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
            _game.DistanceChanged -= OnDistanceChanged;
            _game.CoinsChanged -= OnCoinsChanged;
        }

        private void OnScoreChanged(int score)
        {
            if (_scoreLabel != null)
            {
                _scoreLabel.text = $"DISTANCE  {score}";
            }

            if (_bestLabel != null && _game != null)
            {
                _bestLabel.text = $"BEST  {_game.BestScore}";
            }
        }

        private void OnCoinsChanged(int coins)
        {
            int added = coins - _displayedCoins;
            _displayedCoins = coins;
            _coinsLabel.text = coins.ToString();
            if (added <= 0) return;
            _coinPopup.text = $"+{added}";
            _popupTime = _coinPopupDuration;
            _coinPopup.enabled = true;
            _coinPopup.color = new Color(0.98f, 0.82f, 0.28f, 1f);
            _coinPopup.rectTransform.anchoredPosition = new Vector2(
                _coinsLabel.rectTransform.anchoredPosition.x + _coinsLabel.preferredWidth + 12f, -112f);
        }

        private void OnDistanceChanged(float distance)
        {
            float progress = _game.LastRunProgress;
            _progressFill.anchorMax = new Vector2(progress, 1f);
            _barComplete = _game.LastRunDistance > 0f && progress >= 1f;
            if (!_barComplete)
            {
                _fadeTime = 0f;
                _barGroup.alpha = _game.LastRunDistance > 0f ? 1f : 0f;
            }
        }

        private void Update() => TickEffects(Time.unscaledDeltaTime);

        private void TickEffects(float deltaTime)
        {
            if (_barComplete)
            {
                _fadeTime += deltaTime;
                _barGroup.alpha = 1f - Mathf.Clamp01(_fadeTime / Mathf.Max(0.01f, _barFadeDuration));
            }
            if (_popupTime > 0f)
            {
                _popupTime = Mathf.Max(0f, _popupTime - deltaTime);
                float remaining = _popupTime / Mathf.Max(0.01f, _coinPopupDuration);
                var color = _coinPopup.color;
                color.a = remaining;
                _coinPopup.color = color;
                var position = _coinPopup.rectTransform.anchoredPosition;
                position.y = -112f + (1f - remaining) * 26f;
                _coinPopup.rectTransform.anchoredPosition = position;
                _coinPopup.enabled = _popupTime > 0f;
            }
        }
        private void OnStateChanged(GameState state)
        {
            if (_centerLabel == null)
            {
                return;
            }

            OnScoreChanged(_game.Score);
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

            var plate = new GameObject("Score Plate", typeof(Image)).GetComponent<Image>();
            plate.transform.SetParent(canvas.transform, false);
            plate.sprite = Run.Common.Shapes.RoundedSquare;
            plate.color = new Color(0.025f, 0.07f, 0.12f, 0.7f);
            plate.raycastTarget = false;
            plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0f, 1f);
            plate.rectTransform.pivot = new Vector2(0f, 1f);
            plate.rectTransform.anchoredPosition = new Vector2(12f, -12f);
            plate.rectTransform.sizeDelta = new Vector2(330f, 152f);

            _scoreLabel = CreateLabel(canvas.transform, "Score",
                anchor: new Vector2(0f, 1f), position: new Vector2(30f, -24f),
                align: TextAnchor.UpperLeft, size: _fontSize);

            _bestLabel = CreateLabel(canvas.transform, "Best",
                anchor: new Vector2(0f, 1f), position: new Vector2(30f, -24f - _fontSize - 8f),
                align: TextAnchor.UpperLeft, size: Mathf.RoundToInt(_fontSize * 0.7f));

            _coinsLabel = CreateLabel(canvas.transform, "Coins",
                anchor: new Vector2(0f, 1f), position: new Vector2(60f, -112f),
                align: TextAnchor.UpperLeft, size: 28);

            var coinIcon = new GameObject("Coin Icon", typeof(Image)).GetComponent<Image>();
            coinIcon.transform.SetParent(canvas.transform, false);
            coinIcon.color = new Color(0.98f, 0.82f, 0.28f);
            coinIcon.sprite = Run.Common.Shapes.Circle;
            coinIcon.raycastTarget = false;
            coinIcon.rectTransform.anchorMin = coinIcon.rectTransform.anchorMax = new Vector2(0f, 1f);
            coinIcon.rectTransform.pivot = new Vector2(0f, 1f);
            coinIcon.rectTransform.anchoredPosition = new Vector2(30f, -121f);
            coinIcon.rectTransform.sizeDelta = new Vector2(20f, 20f);
            _coinPopup = CreateLabel(canvas.transform, "Coin Pickup",
                anchor: new Vector2(0f, 1f), position: new Vector2(110f, -112f),
                align: TextAnchor.UpperLeft, size: 26);
            _coinPopup.enabled = false;

            var background = new GameObject("Last Run Bar", typeof(Image), typeof(CanvasGroup)).GetComponent<Image>();
            _barGroup = background.GetComponent<CanvasGroup>();
            _barGroup.blocksRaycasts = false;
            _barGroup.interactable = false;
            background.transform.SetParent(canvas.transform, false);
            background.color = new Color(0.08f, 0.12f, 0.18f, 0.9f);
            background.raycastTarget = false;
            var barRect = background.rectTransform;
            barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, -78f);
            barRect.sizeDelta = new Vector2(640f, 24f);
            var fill = new GameObject("Fill", typeof(Image)).GetComponent<Image>();
            fill.transform.SetParent(barRect, false);
            fill.color = new Color(0.43f, 0.94f, 0.78f);
            fill.raycastTarget = false;
            _progressFill = fill.rectTransform;
            _progressFill.anchorMin = Vector2.zero;
            _progressFill.anchorMax = new Vector2(0f, 1f);
            _progressFill.offsetMin = _progressFill.offsetMax = Vector2.zero;
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
            label.raycastTarget = false;
            var shadow = labelObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.025f, 0.05f, 0.65f);
            shadow.effectDistance = new Vector2(1.5f, -2f);

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
