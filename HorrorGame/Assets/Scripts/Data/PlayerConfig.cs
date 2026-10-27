using UnityEngine;

// Player, flashlight and meat balance numbers (asset: Settings/PlayerConfig).
[CreateAssetMenu(menuName = "HorrorGame/Player Config", fileName = "PlayerConfig")]
public class PlayerConfig : ScriptableObject
{
    public float walkSpeed = 3f;
    public float sprintSpeed = 5.5f;
    [Tooltip("Stamina (0-1) lost per second while sprinting. 0.1538 = 6.5 s of sprint.")]
    public float staminaDrainPerSecond = 0.1538f;
    [Tooltip("Stamina (0-1) regained per second while not sprinting.")]
    public float staminaRegenPerSecond = 0.13f;
    [Tooltip("Charge (0-1) lost per second while on. 1/120 = a full battery lasts 120 s.")]
    public float flashlightDrainPerSecond = 1f / 120f;
    [Tooltip("Seconds E must be held to unlock the exit door and shortcut doors.")]
    public float unlockHoldSeconds = 0.5f;

    [Header("Meat")]
    [Tooltip("Monsters within this distance (m) smell the meat (repeated while it lies there), also while chasing.")]
    public float meatNoiseRadius = 24f;
}
