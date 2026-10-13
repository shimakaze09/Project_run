using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Run.Common;
using Run.EnvironmentMode;

namespace Run.UI
{
    /// <summary>
    /// The environment player's HUD: an energy bar, the trap hotbar, a hint explaining why the
    /// spot under the cursor is blocked, and hit/trap counters. Only visible while the human is
    /// placing traps (see <see cref="EnvironmentPlayer.IsActive"/>).
    /// </summary>
    /// <remarks>
    /// Built in code like the other HUD canvases. Cards can also be clicked to select them; they
    /// don't take keyboard focus, so a click on a card never blocks the next click in the world.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnvironmentHud : MonoBehaviour
    {
        // Palette shared with PauseMenu/DifficultyMenu.
        private static readonly Color Panel = new Color(0.06f, 0.11f, 0.17f, 0.85f);
        private static readonly Color CardIdle = new Color(0.1f, 0.15f, 0.23f, 1f);
        private static readonly Color Mint = new Color(0.43f, 0.94f, 0.78f);
        private static readonly Color Gold = new Color(0.98f, 0.82f, 0.28f);
        private static readonly Color Coral = new Color(0.95f, 0.45f, 0.35f);
        private static readonly Color Ink = new Color(0.045f, 0.1f, 0.17f);

        private const float CardWidth = 210f;
        private const float CardGap = 16f;

        private CanvasGroup _group;
        private RectTransform _energyFill;
        private Text _energyLabel;
        private Text _hintLabel;
        private Text _statsLabel;
        private Image[] _cardImages = new Image[0];
        private Text[] _cardLabels = new Text[0];
        private CanvasGroup[] _cardGroups = new CanvasGroup[0];
        private RectTransform _cardsRoot;
        private EnvironmentPlayer _player;

        private void Awake() => BuildCanvas();

        private void Update()
        {
            if (_player == null)
            {
                _player = EnvironmentPlayer.Instance;
                if (_player != null) BuildCards();
            }

            bool visible = _player != null && _player.IsActive;
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            if (!visible) return;

            float fraction = _player.MaxEnergy > 0f ? _player.Energy / _player.MaxEnergy : 0f;
            _energyFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            _energyLabel.text = $"ENERGY  {Mathf.FloorToInt(_player.Energy)}";

            for (int i = 0; i < _cardImages.Length; i++)
            {
                bool selected = i == _player.SelectedIndex;
                _cardImages[i].color = selected ? Mint : CardIdle;
                _cardLabels[i].color = selected ? Ink : Color.white;
                _cardGroups[i].alpha = _player.CanAfford(i) ? 1f : 0.45f;
            }

            _hintLabel.text = _player.Hint ?? string.Empty;
            _statsLabel.text = $"HITS  {_player.HitsLanded}     TRAPS  {_player.TrapsPlaced}";
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Environment HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40; // above the HUD, below the difficulty (50) and pause (100) menus
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var groupObject = new GameObject("Group", typeof(RectTransform), typeof(CanvasGroup));
            groupObject.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)groupObject.transform);
            _group = groupObject.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false; // cards use pointer clicks only, never keyboard focus

            var panel = MakeImage(groupObject.transform, "Panel", Panel, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(720f, 170f));
            panel.raycastTarget = true; // clicks on the panel never fall through into the world

            var barBack = MakeImage(panel.transform, "Energy Bar", new Color(0.08f, 0.12f, 0.18f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(660f, 22f));
            var fill = MakeImage(barBack.transform, "Fill", Gold, Vector2.zero, Vector2.zero, Vector2.zero);
            _energyFill = fill.rectTransform;
            _energyFill.anchorMin = Vector2.zero;
            _energyFill.anchorMax = new Vector2(0f, 1f);
            _energyFill.offsetMin = _energyFill.offsetMax = Vector2.zero;
            _energyLabel = MakeLabel(barBack.transform, "Energy", 18, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(300f, 22f));
            _energyLabel.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);

            _hintLabel = MakeLabel(groupObject.transform, "Hint", 26, Coral, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 212f), new Vector2(900f, 36f));
            MakeLabel(groupObject.transform, "Controls", 18, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(900f, 24f)).text =
                "1-3 / WHEEL  PICK TRAP      CLICK  PLACE AHEAD OF THE RUNNER";
            _statsLabel = MakeLabel(groupObject.transform, "Stats", 26, Color.white, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(-30f, -28f), new Vector2(500f, 36f));

            _cardsRoot = panel.rectTransform;
        }

        private void BuildCards()
        {
            var cards = _player.Cards;
            _cardImages = new Image[cards.Count];
            _cardLabels = new Text[cards.Count];
            _cardGroups = new CanvasGroup[cards.Count];
            float total = cards.Count * CardWidth + (cards.Count - 1) * CardGap;

            for (int i = 0; i < cards.Count; i++)
            {
                float x = -total * 0.5f + CardWidth * 0.5f + i * (CardWidth + CardGap);
                var card = MakeImage(_cardsRoot, cards[i].Label + " Card", CardIdle, new Vector2(0.5f, 0f), new Vector2(x, 22f), new Vector2(CardWidth, 92f));
                card.raycastTarget = true;
                _cardGroups[i] = card.gameObject.AddComponent<CanvasGroup>();
                var click = card.gameObject.AddComponent<CardClick>();
                click.Hud = this;
                click.Index = i;

                _cardImages[i] = card;
                _cardLabels[i] = MakeLabel(card.transform, "Label", 24, Color.white, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(CardWidth, 32f));
                _cardLabels[i].text = $"{i + 1}  {cards[i].Label}";
                var cost = MakeLabel(card.transform, "Cost", 20, Gold, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(CardWidth, 28f));
                cost.text = $"{cards[i].Cost} ENERGY";
                cost.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            }
        }

        private void OnCardClicked(int index) => _player?.Select(index);

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image MakeImage(Transform parent, string name, Color color, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var image = new GameObject(name, typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = Shapes.RoundedSquare;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, anchor.y);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return image;
        }

        private static Text MakeLabel(Transform parent, string name, int fontSize, Color color, TextAnchor align,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            var label = new GameObject(name, typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = FontStyle.Bold;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return label;
        }

        /// <summary>Selects a card on click; deliberately not a Selectable, so it never holds focus.</summary>
        private sealed class CardClick : MonoBehaviour, IPointerClickHandler
        {
            public EnvironmentHud Hud;
            public int Index;

            public void OnPointerClick(PointerEventData eventData) => Hud.OnCardClicked(Index);
        }
    }
}
