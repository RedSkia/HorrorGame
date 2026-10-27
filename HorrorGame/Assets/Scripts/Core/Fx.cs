using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

// Small static helpers: colour a spawned object, run a timed animation, play a one-shot sound.
public static class Fx
{
    // AudioSource.PlayClipAtPoint routed to the SFX mixer group (the built-in one always plays on the master).
    // GameManager sets Group once; sounds for other groups (Monsters, UI) use their own AudioSource.
    public static AudioMixerGroup Group { get; set; }

    public static void PlayAt(AudioClip clip, Vector3 position, float delay = 0f)
    {
        var go = new GameObject("OneShot " + clip.name) { transform = { position = position } };
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.outputAudioMixerGroup = Group;
        source.PlayDelayed(delay);
        UnityEngine.Object.Destroy(go, clip.length + delay);
    }

    // Coroutine helper for timed animations: calls step(0..1) every frame for the given seconds and always ends on step(1).
    public static IEnumerator Tween(float seconds, Action<float> step)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            step(t);
            yield return null;
        }
        step(1f);
    }

    // Multiplies the base colour of every renderer (per-object material copy); optionally also sets a glow.
    public static void Tint(GameObject target, Color color, Color? emission = null)
    {
        foreach (Renderer part in target.GetComponentsInChildren<Renderer>())
        {
            Material material = part.material;
            material.SetColor("_BaseColor", material.GetColor("_BaseColor") * color);
            if (emission == null) continue;
            material.SetColor("_EmissionColor", emission.Value);
            material.EnableKeyword("_EMISSION");
        }
    }
}
