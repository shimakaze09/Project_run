using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Run.Common;

namespace Run.Core
{
    /// <summary>High-level phases of a single play session.</summary>
    public enum GameState
    {
        /// <summary>Waiting for the player's first input before the world starts scrolling.</summary>
        Ready,

        /// <summary>The run is live: distance accumulates and hazards are lethal.</summary>
        Playing,

        /// <summary>The player has died; the world is frozen until a restart.</summary>
        GameOver
    }

    /// <summary>Why the current run ended (useful for UI, analytics, or audio cues).</summary>
    public enum DeathCause
    {
        Hazard,
        FellIntoPit,
        LeftBehind
    }

    /// <summary>
    /// Owns the session-level game loop: the <see cref="GameState"/> machine, the
    /// running score, and restart handling.
    /// </summary>
    /// <remarks>
    /// Other systems observe the manager through <see cref="StateChanged"/> and
    /// <see cref="ScoreChanged"/> rather than referencing one another directly, which
    /// keeps the scene loosely coupled and easy to extend. The manager is a light
    /// singleton so gameplay code can reach it via <see cref="Instance"/>.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        /// <summary>The active manager, or <c>null</c> before the scene has loaded one.</summary>
        public static GameManager Instance { get; private set; }

        [Header("Scoring")]
        [Tooltip("Points awarded per world unit travelled to the right.")]
        [SerializeField, Min(0f)] private float _pointsPerUnit = 1f;

        [Tooltip("PlayerPrefs key used to persist the best score across sessions.")]
        [SerializeField] private string _bestScoreKey = "run.bestScore";

        [Header("Restart")]
        [Tooltip("Minimum time on the Game Over screen before a restart press is accepted.")]
        [SerializeField, Min(0f)] private float _restartDelay = 0.5f;

        /// <summary>Raised whenever the game transitions between <see cref="GameState"/> values.</summary>
        public event Action<GameState> StateChanged;

        /// <summary>Raised whenever the integer <see cref="Score"/> changes.</summary>
        public event Action<int> ScoreChanged;

        public GameState State { get; private set; } = GameState.Ready;
        public int Score { get; private set; }
        public int BestScore { get; private set; }
        public DeathCause LastDeathCause { get; private set; }

        // Distance is measured from a tracked transform (the player) so the manager
        // never needs a hard reference to the player script.
        private Transform _distanceSource;
        private float _runStartX;
        private int _bonusPoints;
        private float _gameOverTime;
        private bool _restartArmed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BestScore = PlayerPrefs.GetInt(_bestScoreKey, 0);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _distanceSource = player.transform;
            }

            // Push the initial state/score so late-subscribing UI can initialise itself.
            StateChanged?.Invoke(State);
            ScoreChanged?.Invoke(Score);
        }

        private void Update()
        {
            switch (State)
            {
                case GameState.Playing:
                    RefreshScore();
                    break;

                case GameState.GameOver:
                    // Require the button released once, then a short settle time, before a
                    // fresh press restarts — so the input that killed the player can't
                    // instantly skip the Game Over screen.
                    if (!RunInput.JumpHeld())
                    {
                        _restartArmed = true;
                    }
                    if (_restartArmed &&
                        Time.unscaledTime - _gameOverTime >= _restartDelay &&
                        RunInput.ConfirmPressed())
                    {
                        Restart();
                    }
                    break;
            }
        }

        /// <summary>Transitions from <see cref="GameState.Ready"/> into an active run.</summary>
        public void BeginRun()
        {
            if (State != GameState.Ready)
            {
                return;
            }

            _runStartX = _distanceSource != null ? _distanceSource.position.x : 0f;
            _bonusPoints = 0;
            SetState(GameState.Playing);
        }

        /// <summary>Adds bonus points (e.g. from collected coins) during an active run.</summary>
        public void AddBonus(int points)
        {
            if (State != GameState.Playing || points <= 0)
            {
                return;
            }

            _bonusPoints += points;
            RefreshScore();
        }

        /// <summary>Ends the run, records the best score, and moves to <see cref="GameState.GameOver"/>.</summary>
        public void NotifyPlayerDied(DeathCause cause)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            LastDeathCause = cause;
            _gameOverTime = Time.unscaledTime;
            _restartArmed = false;

            if (Score > BestScore)
            {
                BestScore = Score;
                PlayerPrefs.SetInt(_bestScoreKey, BestScore);
                PlayerPrefs.Save();
            }

            SetState(GameState.GameOver);
        }

        /// <summary>Shifts the scoring origin so distance is preserved across a floating-origin re-base.</summary>
        public void ShiftDistanceOrigin(float dx)
        {
            _runStartX += dx;
        }

        /// <summary>Reloads the active scene to start a fresh run.</summary>
        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void RefreshScore()
        {
            float distance = _distanceSource != null ? _distanceSource.position.x - _runStartX : 0f;
            int distancePoints = Mathf.Max(0, Mathf.FloorToInt(distance * _pointsPerUnit));
            int total = _bonusPoints + distancePoints;

            if (total != Score)
            {
                Score = total;
                ScoreChanged?.Invoke(Score);
            }
        }

        private void SetState(GameState next)
        {
            if (State == next)
            {
                return;
            }

            State = next;
            StateChanged?.Invoke(State);
        }
    }
}
