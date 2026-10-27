using UnityEngine;
using UnityEngine.InputSystem;

// Testing cheats on the "Cheats" scene object. F1 = god mode, F2 = fullbright.
// Other scripts only read the static flags (Flashlight, PlayerController, GameManager); the HUD listens to GameEvents.CheatsChanged.
public class DebugCheats : MonoBehaviour
{
    // God mode: monsters can't catch you, battery and stamina don't drain.
    public static bool GodMode { get; private set; }
    // Fullbright: bright ambient light and no fog, to inspect the maze.
    public static bool Fullbright { get; private set; }

    [SerializeField] InputActionReference godModeAction;
    [SerializeField] InputActionReference fullbrightAction;
    // Pre-tinted warm so the blue post colour filter (0.88, 0.94, 1) leaves a neutral grey. PlayerController.ApplyAmbient applies it.
    public static readonly Color FullbrightAmbient = new Color(0.91f, 0.85f, 0.8f);

    bool savedFog;

    void OnEnable()
    {
        godModeAction.action.Enable();
        fullbrightAction.action.Enable();
    }

    void OnDisable()
    {
        godModeAction.action.Disable();
        fullbrightAction.action.Disable();
        if (Fullbright) SetFullbright(false);
        GodMode = false;
    }

    void Update()
    {
        if (godModeAction.action.WasPressedThisFrame())
        {
            GodMode = !GodMode;
            Announce("God mode", GodMode);
        }
        if (fullbrightAction.action.WasPressedThisFrame())
        {
            SetFullbright(!Fullbright);
            Announce("Fullbright", Fullbright);
        }
    }

    // Fog only; the ambient colour is owned by PlayerController.ApplyAmbient (a saved copy could be stale or hiding-boosted).
    void SetFullbright(bool on)
    {
        if (on == Fullbright) return;
        if (on) savedFog = RenderSettings.fog;
        RenderSettings.fog = on ? false : savedFog;
        Fullbright = on;
    }

    static void Announce(string cheat, bool on)
    {
        GameEvents.RaiseMessage($"{cheat} {(on ? "ON" : "OFF")}");
        GameEvents.RaiseCheatsChanged();
    }
}
