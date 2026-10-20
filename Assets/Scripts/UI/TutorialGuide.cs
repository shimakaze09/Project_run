using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Run.Common;
using Run.Core;
using Run.EnvironmentMode;
using Run.Player;

namespace Run.UI
{
    /// <summary>
    /// The tutorial: a banner near the top of the screen that walks the player through the
    /// controls and basic mechanics, one step at a time, while they play a real run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Steps move on when the player does what the step asks (start running, jump, collect a
    /// coin, place a trap...) or after a set time for steps that are only information. The
    /// guide watches the existing game events (state changes, jumps, coins, power-ups) rather
    /// than changing any gameplay code, and shows a different set of steps for Runner and
    /// Environment mode. Timers use scaled time, so pausing the game pauses the tutorial too.
    /// </para>
    /// <para>
    /// Added by <see cref="PauseMenu"/> only when <see cref="TutorialSettings.Active"/> is set,
    /// the same way it adds <see cref="SettingsMenu"/>. The banner is built in code like every
    /// other menu, and is purely visual: no raycast target and no GraphicRaycaster, so it can
    /// never swallow the click or tap that the player needs for jumping or placing traps.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TutorialGuide : MonoBehaviour
    {
        // Matches the palette used by PauseMenu/MainMenu/HudController/VisualArtSetup.
        private static readonly Color Panel = new Color(0.06f, 0.11f, 0.17f, 0.93f);
        private static readonly Color Mint = new Color(0.43f, 0.94f, 0.78f);
        private static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        private sealed class Step
        {
            public string Title;
            public string Body;

            /// <summary>The step stays up at least this long. For time-only steps this is its whole duration.</summary>
            public float MinSeconds;

            /// <summary>Moves on by itself after this long even if <see cref="IsDone"/> is still false (0 = wait forever).</summary>
            public float MaxSeconds;

            /// <summary>Whether the player has done what the step asked. Null means the step only waits out its time.</summary>
            public Func<bool> IsDone;

            /// <summary>Runs when the step appears, to reset anything it watches for.</summary>
            public Action OnEnter;
        }

        private readonly List<Step> _steps = new List<Step>();

        private GameManager _game;
        private PlayerController _runner;
        private int _index;
        private float _elapsed;
        private bool _finished;
        private bool _running;
        private bool _gameOver;

        private bool _jumped;
        private bool _coinCollected;
        private bool _powerUpTaken;
        private int _selectedAtStepStart;

        private CanvasGroup _group;
        private Text _stepLabel;
        private Text _titleLabel;
        private Text _bodyLabel;

        private void Awake() => BuildCanvas();

        private void Start()
        {
            _game = GameManager.Instance;
            if (_game == null)
            {
                // Nothing to guide without a game loop. Only this component is switched off:
                // it shares its GameObject with PauseMenu, so the object itself must stay.
                enabled = false;
                return;
            }

            _runner = FindFirstObjectByType<PlayerController>();
            int hearts = 3;
            if (_runner != null)
            {
                _runner.Jumped += OnJumped;
                var health = _runner.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    hearts = health.MaxHearts;
                }
            }

            _game.StateChanged += ApplyState;
            _game.CoinsChanged += OnCoinsChanged;
            PowerUpEvents.OnPowerUpActivated += OnPowerUpActivated;

            ApplyState(_game.State);

            if (_game.RunMode == GameMode.Environment)
            {
                BuildEnvironmentSteps();
            }
            else
            {
                BuildRunnerSteps(hearts);
            }

            EnterStep();
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.Jumped -= OnJumped;
            }

            if (_game != null)
            {
                _game.StateChanged -= ApplyState;
                _game.CoinsChanged -= OnCoinsChanged;
            }

            PowerUpEvents.OnPowerUpActivated -= OnPowerUpActivated;
        }

        private void ApplyState(GameState state)
        {
            _running = state == GameState.Playing;
            _gameOver = state == GameState.GameOver;
        }

        private void OnJumped() => _jumped = true;

        private void OnCoinsChanged(int total) => _coinCollected = true;

        private void OnPowerUpActivated(PowerUpType type, float duration) => _powerUpTaken = true;

        private void Update()
        {
            if (_finished || _steps.Count == 0 || _gameOver)
            {
                Fade(0f);
                return;
            }

            Fade(1f);

            _elapsed += Time.deltaTime;
            if (StepComplete(_steps[_index]))
            {
                Advance();
            }
        }

        private void Fade(float target)
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime * 4f);
        }

        private bool StepComplete(Step step)
        {
            if (_elapsed < step.MinSeconds)
            {
                return false;
            }

            if (step.IsDone == null || step.IsDone())
            {
                return true;
            }

            return step.MaxSeconds > 0f && _elapsed >= step.MaxSeconds;
        }

        private void Advance()
        {
            _index++;
            _elapsed = 0f;

            if (_index >= _steps.Count)
            {
                _finished = true;
                return;
            }

            EnterStep();
        }

        private void EnterStep()
        {
            Step step = _steps[_index];
            step.OnEnter?.Invoke();

            _stepLabel.text = "STEP " + (_index + 1) + " / " + _steps.Count;
            _titleLabel.text = step.Title;
            _bodyLabel.text = step.Body;
        }


        private void BuildRunnerSteps(int hearts)
        {
            _steps.Add(new Step
            {
                Title = "WELCOME TO THE TUTORIAL",
                Body = "You run forward automatically. Your job is to jump,\n" +
                       "dodge and survive as long as you can.\n" +
                       "Press SPACE, W or UP ARROW (or click / tap) to start.",
                IsDone = () => _running
            });

            _steps.Add(new Step
            {
                Title = "JUMP",
                Body = "Press SPACE, W or UP ARROW (or click / tap) to jump.\n" +
                       "Tap for a short hop, HOLD the button for a higher jump.\n" +
                       "Try it now!",
                OnEnter = () => _jumped = false,
                IsDone = () => _jumped
            });

            _steps.Add(new Step
            {
                Title = "HEARTS",
                Body = "You start with " + hearts + " hearts. Spikes and enemies cost\n" +
                       "1 heart - lose them all and the run ends.\n" +
                       "Falling into a pit ends it instantly, so jump the gaps!",
                MinSeconds = 9f
            });

            _steps.Add(new Step
            {
                Title = "COINS",
                Body = "Collect coins as you run. They add to your saved\n" +
                       "coin total, shown under your distance score.",
                MinSeconds = 3f,
                MaxSeconds = 14f,
                OnEnter = () => _coinCollected = false,
                IsDone = () => _coinCollected
            });

            _steps.Add(new Step
            {
                Title = "POWER-UPS",
                Body = "Power-ups last a few seconds. SHIELD blocks one hit\n" +
                       "and SPEED BOOST makes you faster. Grab one as you run!",
                MinSeconds = 4f,
                MaxSeconds = 16f,
                OnEnter = () => _powerUpTaken = false,
                IsDone = () => _powerUpTaken
            });

            _steps.Add(new Step
            {
                Title = "DISTANCE AND SPEED",
                Body = "Your score is how far you run. The longer you survive,\n" +
                       "the faster the game gets.\n" +
                       "Press ESC at any time to pause.",
                MinSeconds = 8f
            });

            _steps.Add(CompleteStep());
        }

        private void BuildEnvironmentSteps()
        {
            _steps.Add(new Step
            {
                Title = "WELCOME TO THE TUTORIAL",
                Body = "In Environment mode YOU are the level. A computer\n" +
                       "runner runs and jumps by itself - your job is to stop it.\n" +
                       "Press SPACE (or click / tap) to start.",
                IsDone = () => _running
            });

            _steps.Add(new Step
            {
                Title = "PLACE A TRAP",
                Body = "Move the mouse over flat ground ahead of the runner.\n" +
                       "Green means you can build there, red means blocked\n" +
                       "(the reason shows on screen). LEFT-CLICK to place it.",
                MaxSeconds = 60f,
                IsDone = () => EnvironmentPlayer.Instance != null && EnvironmentPlayer.Instance.TrapsPlaced > 0
            });

            _steps.Add(new Step
            {
                Title = "CHOOSE YOUR TRAP",
                Body = "Press 1, 2 or 3 (or scroll the mouse wheel) to pick one.\n" +
                       "CRATE blocks the runner, SPIKES cost it a heart,\n" +
                       "and the DRONE patrols back and forth.",
                MinSeconds = 3f,
                MaxSeconds = 15f,
                OnEnter = () => _selectedAtStepStart = EnvironmentPlayer.Instance != null
                    ? EnvironmentPlayer.Instance.SelectedIndex
                    : 0,
                IsDone = () => EnvironmentPlayer.Instance != null &&
                               EnvironmentPlayer.Instance.SelectedIndex != _selectedAtStepStart
            });

            _steps.Add(new Step
            {
                Title = "ENERGY",
                Body = "Every trap costs ENERGY. It refills over time, and you\n" +
                       "get extra each time the runner loses a heart. You can't\n" +
                       "build inside the red zone right in front of the runner.",
                MinSeconds = 9f
            });

            _steps.Add(new Step
            {
                Title = "YOUR GOAL",
                Body = "Make the runner lose all its hearts as early as you can.\n" +
                       "The shorter the distance it survives, the better your score.",
                MinSeconds = 8f
            });

            _steps.Add(CompleteStep());
        }

        private static Step CompleteStep()
        {
            return new Step
            {
                Title = "TUTORIAL COMPLETE",
                Body = "You know the basics - keep playing to set a new best!\n" +
                       "Press ESC and choose MAIN MENU when you are done.",
                MinSeconds = 6f
            };
        }


        private void BuildCanvas()
        {
            // No GraphicRaycaster on purpose - see the class remarks.
            var canvasObject = new GameObject("Tutorial Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45; // above the HUDs (0-40), below the difficulty (50) and pause (100) menus

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var groupObject = new GameObject("Group", typeof(CanvasGroup));
            groupObject.transform.SetParent(canvas.transform, false);
            _group = groupObject.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Sits below the HUD's progress bar and power-up label (both near the top centre).
            var glow = new GameObject("Glow", typeof(Image)).GetComponent<Image>();
            glow.transform.SetParent(groupObject.transform, false);
            glow.sprite = Shapes.RoundedSquare;
            glow.type = Image.Type.Sliced;
            glow.color = new Color(Mint.r, Mint.g, Mint.b, 0.25f);
            glow.raycastTarget = false;
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            glow.rectTransform.pivot = new Vector2(0.5f, 1f);
            glow.rectTransform.anchoredPosition = new Vector2(0f, -160f);
            glow.rectTransform.sizeDelta = new Vector2(780f, 196f);

            var panel = new GameObject("Panel", typeof(Image)).GetComponent<Image>();
            panel.transform.SetParent(groupObject.transform, false);
            panel.sprite = Shapes.RoundedSquare;
            panel.type = Image.Type.Sliced;
            panel.color = Panel;
            panel.raycastTarget = false;
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            panel.rectTransform.pivot = new Vector2(0.5f, 1f);
            panel.rectTransform.anchoredPosition = new Vector2(0f, -170f);
            panel.rectTransform.sizeDelta = new Vector2(760f, 176f);

            _titleLabel = CreateLabel(panel.transform, "Title", new Vector2(0f, 1f), new Vector2(24f, -14f),
                new Vector2(560f, 40f), 30, Mint, TextAnchor.UpperLeft, bold: true, bestFit: true);
            _stepLabel = CreateLabel(panel.transform, "Step", new Vector2(1f, 1f), new Vector2(-24f, -18f),
                new Vector2(200f, 26f), 20, Gold, TextAnchor.UpperRight, bold: true, bestFit: false);
            _bodyLabel = CreateLabel(panel.transform, "Body", new Vector2(0f, 1f), new Vector2(24f, -62f),
                new Vector2(712f, 102f), 24, Color.white, TextAnchor.UpperLeft, bold: false, bestFit: true);
        }

        private static Text CreateLabel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            int fontSize, Color color, TextAnchor alignment, bool bold, bool bestFit)
        {
            var labelObject = new GameObject(name, typeof(Text));
            labelObject.transform.SetParent(parent, false);

            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;

            // Long lines shrink to fit the box rather than spilling out of the panel.
            label.resizeTextForBestFit = bestFit;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = fontSize;

            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            return label;
        }
    }
}
