using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Run.Common;
using Run.Core;

namespace Run.UI
{
    /// <summary>
    /// A row of Easy / Normal / Hard buttons shown while the game is waiting for the player
    /// to start a run, letting them pick a challenge level beforehand.
    /// </summary>
    /// <remarks>
    /// The selection is only recorded here — <see cref="DifficultySettings"/> persists it and
    /// <see cref="Run.Player.PlayerController"/> / <see cref="Run.Generation.LevelGenerator"/>
    /// read it at the start of each run, so this menu stays decoupled from gameplay tuning.
    /// Like <see cref="HudController"/> and <see cref="PauseMenu"/>, the canvas is built in
    /// code, and it only shows during <see cref="GameState.Ready"/> so it never competes with
    /// the HUD once a run is live.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DifficultyMenu : MonoBehaviour
    {
        // Matches the palette used by PauseMenu/HudController/VisualArtSetup.
        private static readonly Color Ink = new Color(0.045f, 0.1f, 0.17f);
        private static readonly Color Panel = new Color(0.06f, 0.11f, 0.17f, 0.85f);
        private static readonly Color ButtonBg = new Color(0.1f, 0.15f, 0.23f, 1f);
        private static readonly Color ButtonIdle = new Color(ButtonBg.r, ButtonBg.g, ButtonBg.b, 0f);
        private static readonly Color Mint = new Color(0.43f, 0.94f, 0.78f);

        private static readonly Difficulty[] Options = { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard };
        private static readonly string[] Labels = { "EASY", "NORMAL", "HARD" };

        private GameManager _game;
        private CanvasGroup _group;
        private readonly Image[] _buttons = new Image[Options.Length];
        private readonly GameObject[] _buttonObjects = new GameObject[Options.Length];
        private int _hoveredIndex = -1;

        private void Awake() => BuildCanvas();

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
            if (_game != null)
            {
                _game.StateChanged -= OnGameStateChanged;
            }
        }

        private void OnGameStateChanged(GameState state)
        {
            bool visible = state == GameState.Ready;
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;

            if (visible)
            {
                // Arrow keys/WASD navigate relative to whatever is currently selected, so give
                // keyboard focus to whichever difficulty is already chosen as soon as the menu
                // becomes reachable, rather than leaving nothing selected to navigate from.
                int index = System.Array.IndexOf(Options, DifficultySettings.Current);
                if (index >= 0)
                {
                    EventSystem.current?.SetSelectedGameObject(_buttonObjects[index]);
                }
            }
            else
            {
                EventSystem.current?.SetSelectedGameObject(null);
            }
        }

        private void Choose(Difficulty difficulty)
        {
            DifficultySettings.Set(difficulty);
            RefreshSelection();
        }

        private void SetHovered(int index, bool hovered)
        {
            if (hovered)
            {
                _hoveredIndex = index;
            }
            else if (_hoveredIndex == index)
            {
                _hoveredIndex = -1;
            }

            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                _buttons[i].color = Options[i] == DifficultySettings.Current
                    ? Mint
                    : i == _hoveredIndex ? ButtonBg : ButtonIdle;
            }
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Difficulty Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // above the HUD's centred prompt, below the pause menu (100)

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var groupObject = new GameObject("Group", typeof(CanvasGroup));
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
            panel.rectTransform.anchoredPosition = new Vector2(0f, -230f);
            panel.rectTransform.sizeDelta = new Vector2(580f, 160f);

            CreateLabel(panel.transform, "CHOOSE DIFFICULTY", new Vector2(0f, 48f), new Vector2(540f, 40f), 24, Mint);

            float[] xPositions = { -190f, 0f, 190f };
            for (int i = 0; i < Options.Length; i++)
            {
                var button = CreateButton(panel.transform, Labels[i] + " Button", Labels[i],
                    new Vector2(xPositions[i], -34f), Options[i], i);
                _buttons[i] = button.GetComponent<Image>();
                _buttonObjects[i] = button.gameObject;
            }
        }

        private Text CreateLabel(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var labelObject = new GameObject("Label", typeof(Text));
            labelObject.transform.SetParent(parent, false);

            var label = labelObject.GetComponent<Text>();
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

        private Button CreateButton(Transform parent, string name, string text, Vector2 position, Difficulty difficulty, int index)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.sprite = Shapes.RoundedSquare;
            image.type = Image.Type.Sliced;
            image.color = ButtonIdle;

            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(160f, 68f);
            rect.anchoredPosition = position;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Choose(difficulty));

            // The fill itself is only shown on hover (see RefreshSelection); the built-in
            // transition just adds a touch of press feedback on top of that.
            var colors = button.colors;
            colors.pressedColor = Color.Lerp(ButtonBg, Ink, 0.35f);
            button.colors = colors;

            var relay = buttonObject.AddComponent<HoverRelay>();
            relay.Menu = this;
            relay.Index = index;

            CreateLabel(buttonObject.transform, text, Vector2.zero, new Vector2(150f, 50f), 22, Color.white);

            return button;
        }

        /// <summary>Forwards pointer enter/exit from a button back to the owning menu.</summary>
        private sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public DifficultyMenu Menu;
            public int Index;

            public void OnPointerEnter(PointerEventData eventData) => Menu.SetHovered(Index, true);
            public void OnPointerExit(PointerEventData eventData) => Menu.SetHovered(Index, false);
        }
    }
}
