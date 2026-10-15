using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Run.Common;
using Run.Core;

namespace Run.UI
{
    /// <summary>
    /// The title screen: PLAY GAME / SETTINGS / QUIT GAME. Lives in its own scene
    /// (<see cref="GameplaySceneName"/> is loaded separately), so unlike an in-scene overlay it
    /// never runs alongside - or shows through onto - the gameplay scene underneath.
    /// </summary>
    /// <remarks>
    /// Pressing Play loads the gameplay scene outright (<see cref="SceneManager.LoadScene(string)"/>),
    /// which destroys this scene and everything in it - there's no state to reset for next time,
    /// unlike the old in-scene-overlay version this replaced.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        private const string GameplaySceneName = "SampleScene";

        // Matches the palette used by PauseMenu/SettingsMenu/DifficultyMenu/HudController/VisualArtSetup.
        private static readonly Color Ink = new Color(0.045f, 0.1f, 0.17f);
        private static readonly Color Panel = new Color(0.06f, 0.11f, 0.17f, 0.97f);
        private static readonly Color ButtonBg = new Color(0.1f, 0.15f, 0.23f, 1f);
        private static readonly Color Mint = new Color(0.43f, 0.94f, 0.78f);
        private static readonly Color Danger = new Color(0.95f, 0.45f, 0.35f);
        // Matches VisualArtSetup's Gold - reused here only for the Settings button, so the
        // title-screen row reads as Play/Settings/Quit at a glance the way the reference art's
        // Play/Options/Quit row does, without introducing a new accent color to the game.
        private static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        private static readonly GameMode[] ModeOptions = { GameMode.Runner, GameMode.Environment };
        private static readonly string[] ModeLabels = { "RUNNER", "ENVIRONMENT" };

        private SettingsMenu _settingsMenu;
        // Only the card (title/buttons) hides while Settings/mode choice is open, matching how
        // PauseMenu swaps its card for SettingsMenu's - the background scenery stays up throughout.
        private CanvasGroup _cardGroup;
        private CanvasGroup _modeCardGroup;
        private bool _showingSettings;
        private bool _showingModeChoice;
        private GameObject _playButtonObject;
        private GameObject _settingsButtonObject;
        private readonly Image[] _modeButtons = new Image[ModeOptions.Length];
        private readonly GameObject[] _modeButtonObjects = new GameObject[ModeOptions.Length];
        private int _modeHoveredIndex = -1;
        private int _modeFocusedIndex = -1;

        private void Awake()
        {
            BuildCanvas();

            _settingsMenu = gameObject.AddComponent<SettingsMenu>();
            _settingsMenu.BackRequested += CloseSettings;
        }

        private void Start()
        {
            EventSystem.current?.SetSelectedGameObject(_playButtonObject);
        }

        private void OnDestroy()
        {
            if (_settingsMenu != null)
            {
                _settingsMenu.BackRequested -= CloseSettings;
            }
        }

        private void Update()
        {
            // Handled directly rather than trusting the EventSystem's own Submit action
            // end-to-end - one less link in the chain between pressing Enter and something
            // actually happening. Mirrors PauseMenu/DifficultyMenu.
            if (RunInput.SubmitPressed())
            {
                ActivateSelected();
            }

            if (RunInput.PausePressed())
            {
                if (_showingSettings)
                {
                    CloseSettings();
                }
                else if (_showingModeChoice)
                {
                    CloseModeChoice();
                }
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

        private void OpenModeChoice()
        {
            _showingModeChoice = true;
            SetCardVisible(false);
            RefreshModeSelection();
            SetModeCardVisible(true);

            int index = System.Array.IndexOf(ModeOptions, GameModeSettings.Current);
            EventSystem.current?.SetSelectedGameObject(index >= 0 ? _modeButtonObjects[index] : _modeButtonObjects[0]);
        }

        private void CloseModeChoice()
        {
            _showingModeChoice = false;
            SetModeCardVisible(false);
            SetCardVisible(true);
            EventSystem.current?.SetSelectedGameObject(_playButtonObject);
        }

        private void ChooseMode(GameMode mode)
        {
            GameModeSettings.Set(mode);
            SceneManager.LoadScene(GameplaySceneName);
        }

        private void SetModeHovered(int index, bool hovered)
        {
            if (hovered) _modeHoveredIndex = index;
            else if (_modeHoveredIndex == index) _modeHoveredIndex = -1;
            RefreshModeSelection();
        }

        private void SetModeFocused(int index, bool focused)
        {
            if (focused) _modeFocusedIndex = index;
            else if (_modeFocusedIndex == index) _modeFocusedIndex = -1;
            RefreshModeSelection();
        }

        private void RefreshModeSelection()
        {
            for (int i = 0; i < _modeButtons.Length; i++)
            {
                bool highlighted = i == _modeHoveredIndex || i == _modeFocusedIndex;
                _modeButtons[i].color = ModeOptions[i] == GameModeSettings.Current
                    ? Mint
                    : highlighted ? Color.Lerp(ButtonBg, Color.white, 0.4f) : ButtonBg;
            }
        }

        private void OpenSettings()
        {
            _showingSettings = true;
            SetCardVisible(false);
            _settingsMenu.Show();
            EventSystem.current?.SetSelectedGameObject(_settingsMenu.FirstFocusObject);
        }

        private void CloseSettings()
        {
            _showingSettings = false;
            _settingsMenu.Hide();
            SetCardVisible(true);
            EventSystem.current?.SetSelectedGameObject(_settingsButtonObject);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetCardVisible(bool visible)
        {
            _cardGroup.alpha = visible ? 1f : 0f;
            _cardGroup.blocksRaycasts = visible;
            _cardGroup.interactable = visible;
        }

        private void SetModeCardVisible(bool visible)
        {
            _modeCardGroup.alpha = visible ? 1f : 0f;
            _modeCardGroup.blocksRaycasts = visible;
            _modeCardGroup.interactable = visible;
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Main Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Strictly below SettingsMenu's own canvas (sortingOrder 100), so Settings always
            // draws on top unambiguously. Relying on same-sortingOrder tie-breaking (creation
            // order) worked for PauseMenu, which has nothing else in its scene, but this canvas
            // now also carries the full-screen background image, and ties between two Overlay
            // canvases aren't guaranteed to resolve the way creation order would suggest.
            canvas.sortingOrder = 50;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            BuildBackground(canvas.transform);

            // No card panel or dimming behind the title/buttons - the background art itself,
            // plus their own shadow/fill, carries legibility, matching the reference splash look.
            var cardObject = new GameObject("Card Group", typeof(CanvasGroup));
            cardObject.transform.SetParent(canvas.transform, false);
            _cardGroup = cardObject.GetComponent<CanvasGroup>();

            BuildTitle(cardObject.transform);

            // A row of pill buttons along the bottom, matching the reference layout, instead of
            // the old stacked card - one per option the game actually has (no Collection-style
            // extras). Color-coded the same way the reference's row is: a bright primary action,
            // then a secondary, then danger-red for Quit.
            _playButtonObject = CreateButton(cardObject.transform, "Play Button", "PLAY", new Vector2(-340f, -420f), Mint, Ink, OpenModeChoice).gameObject;
            _settingsButtonObject = CreateButton(cardObject.transform, "Settings Button", "SETTINGS", new Vector2(0f, -420f), Gold, Ink, OpenSettings).gameObject;
            CreateButton(cardObject.transform, "Quit Button", "QUIT", new Vector2(340f, -420f), Danger, Ink, QuitGame);

            SetCardVisible(true);

            BuildModeCard(canvas.transform);
        }

        /// <summary>
        /// Full-screen title-screen artwork. Loaded as a plain Texture2D (not pre-imported as a
        /// Sprite) and wrapped at runtime, so the asset works regardless of whatever Texture Type
        /// its import settings default to - this is the one raster image in a project where
        /// every other piece of art is procedurally generated.
        /// </summary>
        private void BuildBackground(Transform parent)
        {
            var texture = Resources.Load<Texture2D>("Art/MainMenuBackground");
            var background = new GameObject("Background", typeof(Image)).GetComponent<Image>();
            background.transform.SetParent(parent, false);
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            background.raycastTarget = false;

            if (texture != null)
            {
                background.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                // Falls back to the dusk sky color alone if the art is ever missing, rather than
                // showing nothing behind the title/buttons.
                background.color = Ink;
            }
        }

        /// <summary>
        /// The "ENDLESS RUNNER" logo. TextMeshPro rather than the legacy Text the rest of this
        /// menu uses, since a title needs tighter letter-spacing and a cleaner, bolder look than
        /// legacy Text can give it. Stacked on two lines and shifted left/up rather than centered,
        /// so it sits in the clear sky of the background art instead of behind the moon.
        /// </summary>
        private void BuildTitle(Transform parent)
        {
            var titleObject = new GameObject("Title", typeof(RectTransform));
            titleObject.transform.SetParent(parent, false);

            var rect = titleObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 260f);
            rect.anchoredPosition = new Vector2(-470f, 300f);

            var title = titleObject.AddComponent<TextMeshProUGUI>();
            title.text = "ENDLESS\nRUNNER";
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.fontSize = 100;
            title.lineSpacing = -12f;
            title.characterSpacing = 4f;
            // A warm cream rather than Mint, so the title pops as its own focal point instead of
            // blending into the Mint PLAY button beneath it - closer to the moon's own glow color
            // without being the exact same gold used for the Settings button.
            title.color = new Color(0.98f, 0.93f, 0.78f);
            title.raycastTarget = false;

            var shadow = titleObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.85f);
            shadow.effectDistance = new Vector2(3f, -4f);
        }

        /// <summary>
        /// The "CHOOSE MODE" panel Play Game swaps in: Runner/Environment, matching the current
        /// selection in <see cref="GameModeSettings"/> the same way <see cref="DifficultyMenu"/>
        /// highlights the active difficulty on the Ready screen. Picking either sets the mode and
        /// goes straight into the gameplay scene - no separate confirm step needed.
        /// </summary>
        private void BuildModeCard(Transform parent)
        {
            var modeCardObject = new GameObject("Mode Card Group", typeof(CanvasGroup));
            modeCardObject.transform.SetParent(parent, false);
            _modeCardGroup = modeCardObject.GetComponent<CanvasGroup>();

            var glow = new GameObject("Card Glow", typeof(Image)).GetComponent<Image>();
            glow.transform.SetParent(modeCardObject.transform, false);
            glow.sprite = Shapes.RoundedSquare;
            glow.type = Image.Type.Sliced;
            glow.color = new Color(Mint.r, Mint.g, Mint.b, 0.22f);
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glow.rectTransform.sizeDelta = new Vector2(464f, 364f);

            var card = new GameObject("Card", typeof(Image)).GetComponent<Image>();
            card.transform.SetParent(modeCardObject.transform, false);
            card.sprite = Shapes.RoundedSquare;
            card.type = Image.Type.Sliced;
            card.color = Panel;
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            card.rectTransform.sizeDelta = new Vector2(440f, 340f);

            CreateLabel(card.transform, "Title", "CHOOSE MODE", new Vector2(0f, 128f), new Vector2(400f, 60f),
                36, Mint, bold: true, shadow: true);

            var divider = new GameObject("Divider", typeof(Image)).GetComponent<Image>();
            divider.transform.SetParent(card.transform, false);
            divider.sprite = Shapes.Rectangle;
            divider.color = new Color(Mint.r, Mint.g, Mint.b, 0.35f);
            divider.rectTransform.anchorMin = divider.rectTransform.anchorMax = divider.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            divider.rectTransform.sizeDelta = new Vector2(360f, 3f);
            divider.rectTransform.anchoredPosition = new Vector2(0f, 76f);

            for (int i = 0; i < ModeOptions.Length; i++)
            {
                GameMode mode = ModeOptions[i];
                var button = CreateModeButton(card.transform, ModeLabels[i], new Vector2(0f, 20f - i * 88f), i, () => ChooseMode(mode));
                _modeButtons[i] = button.GetComponent<Image>();
                _modeButtonObjects[i] = button.gameObject;
            }

            CreateButton(card.transform, "Mode Back Button", "BACK", new Vector2(0f, -128f), ButtonBg, Color.white, CloseModeChoice);

            SetModeCardVisible(false);
        }

        private Button CreateModeButton(Transform parent, string text, Vector2 position, int index, UnityAction onClick)
        {
            var buttonObject = new GameObject(text + " Button", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.sprite = Shapes.RoundedSquare;
            image.type = Image.Type.Sliced;
            image.color = ButtonBg;

            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 64f);
            rect.anchoredPosition = position;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            // Color is driven entirely by RefreshModeSelection (current mode OR hover/focus,
            // whichever), not Unity's built-in transition - see ButtonHighlight's comment on
            // PauseMenu/this file's own Play/Settings/Quit buttons for why.
            button.transition = Selectable.Transition.None;

            var relay = buttonObject.AddComponent<ModeButtonRelay>();
            relay.Menu = this;
            relay.Index = index;

            CreateLabel(buttonObject.transform, "Label", text, Vector2.zero, new Vector2(300f, 50f),
                26, Color.white, bold: true);

            return button;
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
            // graphic's own color, which only ever darkens an already-bright fill like Mint and
            // reads as barely-there on the dark buttons too. Driving the highlight directly
            // (hover OR keyboard focus, whichever) gives a clear, predictable cue instead, and
            // doubles as the only visible indicator of keyboard navigation. Mirrors PauseMenu.
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

        /// <summary>Forwards pointer enter/exit and keyboard focus from a mode button back to the menu. Mirrors DifficultyMenu's InteractionRelay.</summary>
        private sealed class ModeButtonRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
        {
            public MainMenu Menu;
            public int Index;

            public void OnPointerEnter(PointerEventData eventData) => Menu.SetModeHovered(Index, true);
            public void OnPointerExit(PointerEventData eventData) => Menu.SetModeHovered(Index, false);
            public void OnSelect(BaseEventData eventData) => Menu.SetModeFocused(Index, true);
            public void OnDeselect(BaseEventData eventData) => Menu.SetModeFocused(Index, false);
        }
    }
}
