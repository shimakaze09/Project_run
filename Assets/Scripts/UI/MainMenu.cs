using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Run.Common;

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

        private SettingsMenu _settingsMenu;
        // Only the card (title/buttons) hides while Settings is open, matching how PauseMenu
        // swaps its card for SettingsMenu's - the background scenery stays up throughout.
        private CanvasGroup _cardGroup;
        private bool _showingSettings;
        private GameObject _playButtonObject;
        private GameObject _settingsButtonObject;

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

            if (_showingSettings && RunInput.PausePressed())
            {
                CloseSettings();
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

        private void PlayGame()
        {
            SceneManager.LoadScene(GameplaySceneName);
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

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Main Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Same order as PauseMenu/SettingsMenu: this component's own SettingsMenu canvas is
            // created right after this one in Awake, so at equal sortingOrder it wins the
            // sibling-order tiebreak and draws on top, exactly like PauseMenu's card does. An
            // overlay canvas always draws above the scenery behind it (a world-space camera
            // layer) regardless of this number.
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // A soft vignette, not a solid fill - unlike the old in-scene overlay this replaced,
            // the background here is the scenery behind the camera (see MainMenuSceneSetup), so
            // it should read through everywhere except right behind the card text.
            var scrim = new GameObject("Scrim", typeof(Image)).GetComponent<Image>();
            scrim.transform.SetParent(canvas.transform, false);
            scrim.color = new Color(Ink.r, Ink.g, Ink.b, 0.25f);
            scrim.rectTransform.anchorMin = Vector2.zero;
            scrim.rectTransform.anchorMax = Vector2.one;
            scrim.rectTransform.offsetMin = scrim.rectTransform.offsetMax = Vector2.zero;

            var cardObject = new GameObject("Card Group", typeof(CanvasGroup));
            cardObject.transform.SetParent(canvas.transform, false);
            _cardGroup = cardObject.GetComponent<CanvasGroup>();

            var glow = new GameObject("Card Glow", typeof(Image)).GetComponent<Image>();
            glow.transform.SetParent(cardObject.transform, false);
            glow.sprite = Shapes.RoundedSquare;
            glow.type = Image.Type.Sliced;
            glow.color = new Color(Mint.r, Mint.g, Mint.b, 0.22f);
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glow.rectTransform.sizeDelta = new Vector2(464f, 524f);

            var card = new GameObject("Card", typeof(Image)).GetComponent<Image>();
            card.transform.SetParent(cardObject.transform, false);
            card.sprite = Shapes.RoundedSquare;
            card.type = Image.Type.Sliced;
            card.color = Panel;
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            card.rectTransform.sizeDelta = new Vector2(440f, 500f);

            CreateLabel(card.transform, "Title", "ENDLESS RUNNER", new Vector2(0f, 198f), new Vector2(400f, 80f),
                48, Mint, bold: true, shadow: true);

            var divider = new GameObject("Divider", typeof(Image)).GetComponent<Image>();
            divider.transform.SetParent(card.transform, false);
            divider.sprite = Shapes.Rectangle;
            divider.color = new Color(Mint.r, Mint.g, Mint.b, 0.35f);
            divider.rectTransform.anchorMin = divider.rectTransform.anchorMax = divider.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            divider.rectTransform.sizeDelta = new Vector2(360f, 3f);
            divider.rectTransform.anchoredPosition = new Vector2(0f, 136f);

            _playButtonObject = CreateButton(card.transform, "Play Button", "PLAY GAME", new Vector2(0f, 62f), Mint, Ink, PlayGame).gameObject;
            _settingsButtonObject = CreateButton(card.transform, "Settings Button", "SETTINGS", new Vector2(0f, -38f), ButtonBg, Color.white, OpenSettings).gameObject;
            CreateButton(card.transform, "Quit Button", "QUIT GAME", new Vector2(0f, -138f), ButtonBg, Danger, QuitGame);

            SetCardVisible(true);
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
    }
}
