using System;
using UnityEngine;

namespace Run.Core
{
    /// <summary>
    /// Persists the player's independently-adjustable Master, Music, and SFX volume
    /// levels (each in [0, 1]) across sessions, mirroring <see cref="DifficultySettings"/>'s
    /// pattern so audio-consuming systems (see <see cref="Run.Audio.AudioManager"/>) can react
    /// to changes without knowing where they came from.
    /// </summary>
    public static class VolumePreferences
    {
        private const string MasterKey = "run.audio.master";
        private const string MusicKey = "run.audio.music";
        private const string SfxKey = "run.audio.sfx";

        private static float _master = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
        private static float _music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
        private static float _sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 1f));

        /// <summary>Raised whenever <see cref="SetMaster"/>, <see cref="SetMusic"/>, or <see cref="SetSfx"/> changes a value.</summary>
        public static event Action Changed;

        public static float Master => _master;
        public static float Music => _music;
        public static float Sfx => _sfx;

        /// <summary>Effective music volume — Master and Music scale together.</summary>
        public static float MusicVolume => _master * _music;

        /// <summary>Effective SFX volume — Master and SFX scale together.</summary>
        public static float SfxVolume => _master * _sfx;

        public static void SetMaster(float value) => Apply(MasterKey, value, ref _master);
        public static void SetMusic(float value) => Apply(MusicKey, value, ref _music);
        public static void SetSfx(float value) => Apply(SfxKey, value, ref _sfx);

        private static void Apply(string key, float value, ref float field)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(field, value))
            {
                return;
            }

            field = value;
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
