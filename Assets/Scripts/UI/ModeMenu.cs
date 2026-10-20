using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Run.Common;
using Run.Core;

namespace Run.UI
{
    /// <summary>
    /// "PLAY AS: RUNNER / ENVIRONMENT" buttons on the Ready screen, below the difficulty picker.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="DifficultyMenu"/>: built in code, shown only during
    /// <see cref="GameState.Ready"/>, and reachable with arrow keys (Unity's automatic navigation
    /// moves between the two rows). Choosing only records the mode in
    /// <see cref="GameModeSettings"/>; the run starts as usual.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ModeMenu : MonoBehaviour
    {
        // Palette shared with DifficultyMenu.
        private static readonly Color Panel = new Color(0.06f, 0.11f, 0.17f, 0.85f);
        private static readonly Color ButtonBg = new Color(0.1f, 0.15f, 0.23f, 1f);
        private static readonly Color ButtonIdle = new Color(ButtonBg.r, ButtonBg.g, ButtonBg.b, 0f);
        private static readonly Color ButtonHighlight = Color.Lerp(ButtonBg, Color.white, 0.4f);
        private static readonly Color Mint = new Color(0.43f, 0.94f, 0.78f);

        private static readonly GameMode[] Options = { GameMode.Runner, GameMode.Environment };
        private static readonly string[] Labels = { "RUNNER", "ENVIRONMENT" };

        private GameManager _game;
        private CanvasGroup _group;
        private readonly Image[] _buttons = new Image[Options.Length];
        private readonly GameObject[] _buttonObjects = new GameObject[Options.Length];
        private int _hoveredIndex = -1;
        private int _focusedIndex = -1;

        private void Awake() => BuildCanvas();

        private void OnEnable() => GameModeSettings.Changed += OnModeChanged;

        private void OnDisable() => GameModeSettings.Changed -= OnModeChanged;

        private void Start()
        {
            _game = GameManager.Instance;
            if (_game != null)
            {
                _game.StateChanged += OnGameStateChanged;
                OnGameStateChanged(_game.State);
            }
            RefreshSelection();
        }

        private void OnDestroy()
        {
            if (_game != null) _game.StateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameState state)
        {
            bool visible = state == GameState.Ready;
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }

        private void OnModeChanged(GameMode mode) => RefreshSelection();

        private void Update()
        {
            // Enter on a focused mode button. (DifficultyMenu also submits whatever is focused;
            // choosing a mode twice is harmless.)
            if (!_group.interactable || !RunInput.SubmitPressed()) return;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int index = System.Array.IndexOf(_buttonObjects, selected);
            if (index >= 0) GameModeSettings.Set(Options[index]);
        }

        private void SetHovered(int index, bool hovered)
        {
            if (hovered) _hoveredIndex = index;
            else if (_hoveredIndex == index) _hoveredIndex = -1;
            RefreshSelection();
        }

        private void SetFocused(int index, bool focused)
        {
            if (focused) _focusedIndex = index;
            else if (_focusedIndex == index) _focusedIndex = -1;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool highlighted = i == _hoveredIndex || i == _focusedIndex;
                _buttons[i].color = Options[i] == GameModeSettings.Current ? Mint
                    : highlighted ? ButtonHighlight : ButtonIdle;
            }
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Mode Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // same layer as the difficulty picker
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Matching width only (the default) leaves very little vertical room on a short/wide
            // window; balancing both axes keeps edge-hugging UI from being pushed off-screen.
            scaler.matchWidthOrHeight = 0.5f;

            var groupObject = new GameObject("Group", typeof(RectTransform), typeof(CanvasGroup));
            groupObject.transform.SetParent(canvas.transform, false);
            _group = groupObject.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var panel = new GameObject("Panel", typeof(Image)).GetComponent<Image>();
            panel.transform.SetParent(groupObject.transform, false);
            panel.sprite = Shapes.RoundedSquare;
            panel.type = Image.Type.Sliced;
            panel.color = Panel;
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            panel.rectTransform.anchoredPosition = new Vector2(0f, -385f); // just below the difficulty panel
            panel.rectTransform.sizeDelta = new Vector2(580f, 130f);

            CreateLabel(panel.transform, "PLAY AS", new Vector2(0f, 38f), new Vector2(540f, 34f), 22, Mint);

            float[] xPositions = { -140f, 140f };
            for (int i = 0; i < Options.Length; i++)
            {
                var button = CreateButton(panel.transform, Labels[i], new Vector2(xPositions[i], -18f), i);
                _buttons[i] = button.GetComponent<Image>();
                _buttonObjects[i] = button.gameObject;
            }
        }

        private Button CreateButton(Transform parent, string text, Vector2 position, int index)
        {
            var buttonObject = new GameObject(text + " Button", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.sprite = Shapes.RoundedSquare;
            image.type = Image.Type.Sliced;
            image.color = ButtonIdle;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(250f, 56f);
            rect.anchoredPosition = position;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None; // colours come from RefreshSelection
            GameMode mode = Options[index];
            button.onClick.AddListener(() => GameModeSettings.Set(mode));

            var relay = buttonObject.AddComponent<InteractionRelay>();
            relay.Menu = this;
            relay.Index = index;

            CreateLabel(buttonObject.transform, text, Vector2.zero, new Vector2(240f, 50f), 22, Color.white);
            return button;
        }

        private static Text CreateLabel(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var label = new GameObject("Label", typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = FontStyle.Bold;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return label;
        }

        /// <summary>Forwards pointer enter/exit and keyboard focus from a button back to the menu.</summary>
        private sealed class InteractionRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
        {
            public ModeMenu Menu;
            public int Index;

            public void OnPointerEnter(PointerEventData eventData) => Menu.SetHovered(Index, true);
            public void OnPointerExit(PointerEventData eventData) => Menu.SetHovered(Index, false);
            public void OnSelect(BaseEventData eventData) => Menu.SetFocused(Index, true);
            public void OnDeselect(BaseEventData eventData) => Menu.SetFocused(Index, false);
        }
    }
}
