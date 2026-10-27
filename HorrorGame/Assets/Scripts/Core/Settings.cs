using UnityEngine;
using UnityEngine.Audio;

// Player settings kept in PlayerPrefs; read where they are used, written by the HUD settings panel.
public static class Settings
{
    public static bool HeadBob
    {
        get => PlayerPrefs.GetInt("HeadBob", 1) == 1;
        set => PlayerPrefs.SetInt("HeadBob", value ? 1 : 0);
    }

    // Mouse sensitivity in Source-engine units (0.022 degrees per mouse count at 1; 0.2-10, default 2.3 = about 0.05 degrees per count).
    public const float DefaultSensitivity = 2.3f;
    public static float Sensitivity
    {
        get => PlayerPrefs.GetFloat("MouseSensitivity", DefaultSensitivity);
        set => PlayerPrefs.SetFloat("MouseSensitivity", value);
    }

    // Vertical field of view in degrees (60-100, default = the player camera's original 60); PlayerCamera applies it every frame.
    public const float DefaultFov = 60f;
    public static float Fov
    {
        get => PlayerPrefs.GetFloat("Fov", DefaultFov);
        set => PlayerPrefs.SetFloat("Fov", value);
    }

    // Master volume 0-1 (default 1); ApplyVolume sets it on the mixer.
    public static float Volume
    {
        get => PlayerPrefs.GetFloat("Volume", 1f);
        set => PlayerPrefs.SetFloat("Volume", value);
    }

    public static void ApplyVolume(AudioMixer mixer) => mixer.SetFloat("MasterVolume", Mathf.Max(-80f, 20f * Mathf.Log10(Volume)));
}
