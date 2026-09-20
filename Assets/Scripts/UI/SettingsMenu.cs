using System;
using UnityEngine;
using UnityEngine.UI;
using Run.Common;
using Run.Core;

namespace Run.UI
{
    /// <summary>
    /// Master / Music / SFX volume sliders, each independently adjustable and backed by
    /// <see cref="VolumePreferences"/>, which is what actually persists them and applies them to
    /// playing audio.
    /// </summary>
    /// <remarks>
    /// Built as its own canvas in code, the same way as <see cref="PauseMenu"/> and
    /// <see cref="DifficultyMenu"/>. This component only knows how to show and hide itself and
    /// report when Back is pressed — <see cref="PauseMenu"/> (the current entry point) owns
    /// when that happens relative to its own panel.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SettingsMenu : MonoBehaviour
    {
        // Matches the palette used by PauseMenu/DifficultyMenu/HudController/VisualArtSetup.
        private static readonly Color Ink = new Color(0.045f, 0.1f, 0.17f);
        private static readonly Color Panel = new Color(0.06f, 0.11f, 0.17f, 0.97f);
        private static readonly Color ButtonBg = new Color(0.1f, 0.15f, 0.23f, 1f);
        private static readonly Color Mint = new Color(0.43f, 0.94f, 0.78f);
        private static readonly Color Track = new Color(0.03f, 0.06f, 0.1f, 1f);

        private const float SliderWidth = 320f;
        private const float HandleSize = 26f;

        /// <summary>Raised when the Back button is pressed.</summary>
        public event Action BackRequested;

        /// <summary>The control to give keyboard focus to when this menu becomes visible.</summary>
        public GameObject FirstFocusObject => _backButtonObject;

        private CanvasGroup _group;
        private Text _masterValueLabel;
        private Text _musicValueLabel;
        private Text _sfxValueLabel;
        private GameObject _backButtonObject;

        private void Awake() => BuildCanvas();

        public void Show()
        {
            RefreshLabels();
            SetVisible(true);
        }

        public void Hide() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
        }

        private void RefreshLabels()
        {
            _masterValueLabel.text = FormatPercent(VolumePreferences.Master);
            _musicValueLabel.text = FormatPercent(VolumePreferences.Music);
            _sfxValueLabel.text = FormatPercent(VolumePreferences.Sfx);
        }

        private static string FormatPercent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        private void Back() => BackRequested?.Invoke();

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Settings Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // same layer as PauseMenu's card - only one of the two is ever visible

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var groupObject = new GameObject("Group", typeof(CanvasGroup));
            groupObject.transform.SetParent(canvas.transform, false);
            _group = groupObject.GetComponent<CanvasGroup>();

            var dim = new GameObject("Dim", typeof(Image)).GetComponent<Image>();
            dim.transform.SetParent(groupObject.transform, false);
            dim.color = new Color(Ink.r, Ink.g, Ink.b, 0.8f);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;

            var glow = new GameObject("Card Glow", typeof(Image)).GetComponent<Image>();
            glow.transform.SetParent(groupObject.transform, false);
            glow.sprite = Shapes.RoundedSquare;
            glow.type = Image.Type.Sliced;
            glow.color = new Color(Mint.r, Mint.g, Mint.b, 0.22f);
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glow.rectTransform.sizeDelta = new Vector2(444f, 564f);

            var card = new GameObject("Card", typeof(Image)).GetComponent<Image>();
            card.transform.SetParent(groupObject.transform, false);
            card.sprite = Shapes.RoundedSquare;
            card.type = Image.Type.Sliced;
            card.color = Panel;
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            card.rectTransform.sizeDelta = new Vector2(420f, 540f);

            CreateLabel(card.transform, "Title", "SETTINGS", new Vector2(0f, 198f), new Vector2(380f, 60f),
                40, Mint, TextAnchor.MiddleCenter, bold: true);

            var divider = new GameObject("Divider", typeof(Image)).GetComponent<Image>();
            divider.transform.SetParent(card.transform, false);
            divider.sprite = Shapes.Rectangle;
            divider.color = new Color(Mint.r, Mint.g, Mint.b, 0.35f);
            divider.rectTransform.anchorMin = divider.rectTransform.anchorMax = divider.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            divider.rectTransform.sizeDelta = new Vector2(340f, 3f);
            divider.rectTransform.anchoredPosition = new Vector2(0f, 148f);

            _masterValueLabel = CreateRow(card.transform, "MASTER", 92f, VolumePreferences.Master, VolumePreferences.SetMaster);
            _musicValueLabel = CreateRow(card.transform, "MUSIC", -8f, VolumePreferences.Music, VolumePreferences.SetMusic);
            _sfxValueLabel = CreateRow(card.transform, "SFX", -108f, VolumePreferences.Sfx, VolumePreferences.SetSfx);

            _backButtonObject = CreateButton(card.transform, "Back Button", "BACK", new Vector2(0f, -216f), Back).gameObject;

            SetVisible(false);
        }

        /// <summary>One labelled, live-updating slider row: name + percentage on top, slider beneath.</summary>
        private Text CreateRow(Transform parent, string name, float centerY, float initialValue, Action<float> onChanged)
        {
            CreateLabel(parent, name + " Name", name, new Vector2(-150f, centerY + 32f), new Vector2(200f, 32f),
                22, Color.white, TextAnchor.MiddleLeft, bold: true);
            var valueLabel = CreateLabel(parent, name + " Value", FormatPercent(initialValue), new Vector2(150f, centerY + 32f), new Vector2(100f, 32f),
                22, Mint, TextAnchor.MiddleRight, bold: true);

            var slider = CreateSlider(parent, name + " Slider", new Vector2(0f, centerY), initialValue);
            slider.onValueChanged.AddListener(value =>
            {
                onChanged(value);
                valueLabel.text = FormatPercent(value);
            });

            return valueLabel;
        }

        private Text CreateLabel(Transform parent, string name, string text, Vector2 position, Vector2 size,
            int fontSize, Color color, TextAnchor alignment, bool bold = false)
        {
            var labelObject = new GameObject(name, typeof(Text));
            labelObject.transform.SetParent(parent, false);

            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;

            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            return label;
        }

        private Button CreateButton(Transform parent, string name, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.sprite = Shapes.RoundedSquare;
            image.type = Image.Type.Sliced;
            image.color = ButtonBg;

            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 78f);
            rect.anchoredPosition = position;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            CreateLabel(buttonObject.transform, "Label", text, Vector2.zero, new Vector2(300f, 60f),
                30, Color.white, TextAnchor.MiddleCenter, bold: true);

            return button;
        }

        /// <summary>
        /// Builds a standard three-piece Unity <see cref="Slider"/> (background, fill area,
        /// handle) entirely at runtime, matching how every other visual in this project is
        /// built rather than requiring a prefab.
        /// </summary>
        private Slider CreateSlider(Transform parent, string name, Vector2 position, float initialValue)
        {
            const float height = 14f;

            var rootObject = new GameObject(name, typeof(Image), typeof(Slider));
            rootObject.transform.SetParent(parent, false);

            var background = rootObject.GetComponent<Image>();
            background.sprite = Shapes.RoundedSquare;
            background.type = Image.Type.Sliced;
            background.color = Track;

            var rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(SliderWidth, height);
            rootRect.anchoredPosition = position;

            var fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObject.transform.SetParent(rootObject.transform, false);
            var fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
            fillAreaRect.sizeDelta = new Vector2(-HandleSize, height);
            fillAreaRect.anchoredPosition = Vector2.zero;

            var fillObject = new GameObject("Fill", typeof(Image));
            fillObject.transform.SetParent(fillAreaObject.transform, false);
            var fillImage = fillObject.GetComponent<Image>();
            fillImage.sprite = Shapes.RoundedSquare;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = Mint;
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.sizeDelta = Vector2.zero;

            var handleAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaObject.transform.SetParent(rootObject.transform, false);
            var handleAreaRect = handleAreaObject.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = new Vector2(-HandleSize, 0f);
            handleAreaRect.anchoredPosition = Vector2.zero;

            var handleObject = new GameObject("Handle", typeof(Image));
            handleObject.transform.SetParent(handleAreaObject.transform, false);
            var handleImage = handleObject.GetComponent<Image>();
            handleImage.sprite = Shapes.Circle;
            handleImage.color = Color.white;
            var handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.sizeDelta = new Vector2(HandleSize, HandleSize);

            var slider = rootObject.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.transition = Selectable.Transition.None;
            slider.SetValueWithoutNotify(initialValue);

            return slider;
        }
    }
}
