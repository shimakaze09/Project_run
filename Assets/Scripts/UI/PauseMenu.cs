using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Run.Common;
using Run.Core;

namespace Run.UI
{
    /// <summary>
    /// Builds and drives the pause overlay: Resume / Settings / Restart / Quit, toggled by
    /// Escape (or the Android back button, which Unity also maps to Escape).
    /// </summary>
    /// <remarks>
    /// Like <see cref="HudController"/>, the canvas is constructed in code so
    /// dropping this component into the scene is all that's required. Pausing only
    /// makes sense mid-run, so the toggle is ignored outside
    /// <see cref="GameState.Playing"/>, and an active pause is force-cleared if the
    /// run ends (or the object is destroyed) while paused, so it never leaves
    /// <see cref="Time.timeScale"/> stuck at zero. Settings is a second full-screen view
    /// (see <see cref="SettingsMenu"/>) that swaps in over this one rather than a button
    /// on the same card, so Escape backs out of it one step at a time instead of resuming
    /// the run straight from the middle of adjusting a slider.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PauseMenu : MonoBehaviour
    {
        // Shared with the rest of the game's look (see VisualArtSetup's Ink/Mint/Gold
        // and the HudController plate/bar colors) so the pause screen reads as part
        // of the same world instead of a generic system dialog.
        private static readonly Color Ink = new Color(0.045f, 0.1f, 0.17f);
        private static readonly Color Panel = new Color(0.06f, 0.11f, 0.17f, 0.97f);
        private static readonly Color ButtonBg = new Color(0.1f, 0.15f, 0.23f, 1f);
        private static readonly Color Mint = new Color(0.43f, 0.94f, 0.78f);
        private static readonly Color Danger = new Color(0.95f, 0.45f, 0.35f);

        private GameManager _game;
        private SettingsMenu _settingsMenu;
        private CanvasGroup _panelGroup;
        private bool _isPaused;
        private bool _showingSettings;
        private GameObject _resumeButtonObject;
        private GameObject _settingsButtonObject;

        private void Awake()
        {
            BuildCanvas();

            _settingsMenu = gameObject.AddComponent<SettingsMenu>();
            _settingsMenu.BackRequested += CloseSettings;
        }

        private void Start()
        {
            _game = GameManager.Instance;
            if (_game != null)
            {
                _game.StateChanged += OnGameStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.StateChanged -= OnGameStateChanged;
            }

            if (_settingsMenu != null)
            {
                _settingsMenu.BackRequested -= CloseSettings;
            }

            if (_isPaused)
            {
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            // Handled directly rather than trusting the EventSystem's own Submit action
            // end-to-end - one less link in the chain between pressing Enter and something
            // actually happening.
            if (_isPaused && RunInput.SubmitPressed())
            {
                ActivateSelected();
            }

            if (!RunInput.PausePressed())
            {
                return;
            }

            if (_showingSettings)
            {
                CloseSettings();
            }
            else if (_isPaused)
            {
                Resume();
            }
            else if (_game != null && _game.State == GameState.Playing)
            {
                Pause();
            }
        }

        private static void ActivateSelected()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.TryGetComponent<Button>(out var button) && button.interactable)
            {
                button.onClick.Invoke();
            }
        }

        private void OnGameStateChanged(GameState state)
        {
            if (_isPaused && state != GameState.Playing)
            {
                Resume();
            }
        }

        public void Pause()
        {
            _isPaused = true;
            Time.timeScale = 0f;
            SetPanelVisible(true);

            // Arrow keys/WASD navigate relative to whatever is currently selected, so without
            // this, a fresh pause would have nothing selected and keyboard navigation would
            // have no starting point to move from.
            EventSystem.current?.SetSelectedGameObject(_resumeButtonObject);
        }

        public void Resume()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            SetPanelVisible(false);

            // Defensive: a run ending while Settings is open (see OnGameStateChanged) should
            // never leave the settings canvas showing over gameplay.
            if (_showingSettings)
            {
                _showingSettings = false;
                _settingsMenu.Hide();
            }

            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void OpenSettings()
        {
            _showingSettings = true;
            SetPanelVisible(false);
            _settingsMenu.Show();
            EventSystem.current?.SetSelectedGameObject(_settingsMenu.FirstFocusObject);
        }

        private void CloseSettings()
        {
            _showingSettings = false;
            _settingsMenu.Hide();
            SetPanelVisible(true);
            EventSystem.current?.SetSelectedGameObject(_settingsButtonObject);
        }

        public void RestartRun()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            _game?.Restart();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetPanelVisible(bool visible)
        {
            _panelGroup.alpha = visible ? 1f : 0f;
            _panelGroup.blocksRaycasts = visible;
            _panelGroup.interactable = visible;
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Pause Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above the HUD

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panelObject = new GameObject("Panel", typeof(CanvasGroup));
            panelObject.transform.SetParent(canvas.transform, false);
            _panelGroup = panelObject.GetComponent<CanvasGroup>();

            var dim = new GameObject("Dim", typeof(Image)).GetComponent<Image>();
            dim.transform.SetParent(panelObject.transform, false);
            dim.color = new Color(Ink.r, Ink.g, Ink.b, 0.8f);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;

            // A soft mint glow behind the card, matching the accent used for the HUD's
            // progress bar and the game's Mint palette color, gives the panel a subtle
            // border. Both use the sliced 9-patch so the rounded corners stay crisp
            // instead of stretching into pixelated ellipses at this size.
            var glow = new GameObject("Card Glow", typeof(Image)).GetComponent<Image>();
            glow.transform.SetParent(panelObject.transform, false);
            glow.sprite = Shapes.RoundedSquare;
            glow.type = Image.Type.Sliced;
            glow.color = new Color(Mint.r, Mint.g, Mint.b, 0.22f);
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glow.rectTransform.sizeDelta = new Vector2(424f, 604f);

            var card = new GameObject("Card", typeof(Image)).GetComponent<Image>();
            card.transform.SetParent(panelObject.transform, false);
            card.sprite = Shapes.RoundedSquare;
            card.type = Image.Type.Sliced;
            card.color = Panel;
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            card.rectTransform.sizeDelta = new Vector2(400f, 580f);

            CreateLabel(card.transform, "Title", "PAUSED", new Vector2(0f, 228f), new Vector2(360f, 74f),
                46, Mint, bold: true, shadow: true);

            var divider = new GameObject("Divider", typeof(Image)).GetComponent<Image>();
            divider.transform.SetParent(card.transform, false);
            divider.sprite = Shapes.Rectangle;
            divider.color = new Color(Mint.r, Mint.g, Mint.b, 0.35f);
            divider.rectTransform.anchorMin = divider.rectTransform.anchorMax = divider.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            divider.rectTransform.sizeDelta = new Vector2(300f, 3f);
            divider.rectTransform.anchoredPosition = new Vector2(0f, 166f);

            _resumeButtonObject = CreateButton(card.transform, "Resume Button", "RESUME", new Vector2(0f, 92f), Mint, Ink, Resume).gameObject;
            _settingsButtonObject = CreateButton(card.transform, "Settings Button", "SETTINGS", new Vector2(0f, -4f), ButtonBg, Color.white, OpenSettings).gameObject;
            CreateButton(card.transform, "Restart Button", "RESTART", new Vector2(0f, -100f), ButtonBg, Color.white, RestartRun);
            CreateButton(card.transform, "Quit Button", "QUIT", new Vector2(0f, -196f), ButtonBg, Danger, QuitGame);

            SetPanelVisible(false);
        }

        private Text CreateLabel(Transform parent, string name, string text, Vector2 position, Vector2 size,
            int fontSize, Color color, bool bold = false, bool shadow = false)
        {
            var labelObject = new GameObject(name, typeof(Text));
            labelObject.transform.SetParent(parent, false);

            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;

            if (shadow)
            {
                var labelShadow = labelObject.AddComponent<Shadow>();
                labelShadow.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.7f);
                labelShadow.effectDistance = new Vector2(1.5f, -2f);
            }

            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            return label;
        }

        private Button CreateButton(Transform parent, string name, string text, Vector2 position,
            Color fillColor, Color textColor, UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.sprite = Shapes.RoundedSquare;
            image.type = Image.Type.Sliced;
            image.color = fillColor;

            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 78f);
            rect.anchoredPosition = position;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            // Unity's built-in ColorTint transition multiplies its state color against the
            // graphic's own color, which only ever darkens an already-bright fill like Mint
            // and reads as barely-there on the dark buttons too. Driving the highlight
            // directly (hover OR keyboard focus, whichever) gives a clear, predictable cue
            // instead, and doubles as the only visible indicator of keyboard navigation.
            button.transition = Selectable.Transition.None;
            var highlight = buttonObject.AddComponent<ButtonHighlight>();
            highlight.Image = image;
            highlight.NormalColor = fillColor;
            highlight.HighlightColor = Color.Lerp(fillColor, Color.white, 0.3f);

            CreateLabel(buttonObject.transform, "Label", text, Vector2.zero, new Vector2(300f, 60f),
                30, textColor, bold: true);

            return button;
        }

        /// <summary>Lightens a button's fill on mouse hover or keyboard focus, whichever is active.</summary>
        private sealed class ButtonHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
        {
            public Image Image;
            public Color NormalColor;
            public Color HighlightColor;
            private bool _hovered;
            private bool _focused;

            public void OnPointerEnter(PointerEventData eventData) { _hovered = true; Refresh(); }
            public void OnPointerExit(PointerEventData eventData) { _hovered = false; Refresh(); }
            public void OnSelect(BaseEventData eventData) { _focused = true; Refresh(); }
            public void OnDeselect(BaseEventData eventData) { _focused = false; Refresh(); }

            private void Refresh() => Image.color = (_hovered || _focused) ? HighlightColor : NormalColor;
        }
    }
}
