using UnityEngine;

// Monster balance numbers: speeds, senses, timings, type look (assets: Settings/MonsterConfig, MonsterConfigBlind, MonsterConfigLightshy). Feel numbers are consts in MonsterAI/MonsterSenses.
[CreateAssetMenu(menuName = "HorrorGame/Monster Config", fileName = "MonsterConfig")]
public class MonsterConfig : ScriptableObject
{
    [Header("Movement")]
    public float walkSpeed = 1.8f;
    [Tooltip("Slightly below the player's sprint speed (5.5).")]
    public float runSpeed = 4.3f;

    [Header("Senses")]
    public float sightRangeDark = 5f;
    public float sightRangeLit = 11f;
    [Tooltip("Flashlight glow: investigates without line of sight within this range.")]
    public float glowRange = 6f;
    [Tooltip("Sprinting noise range.")]
    public float noiseRange = 8f;

    [Header("Type")]
    [Tooltip("Meat noise radius is multiplied by this (blind monsters hear further).")]
    public float noiseRadiusScale = 1f;
    [Tooltip("Notices the player this close with line of sight even when sight is 0 (blind monsters).")]
    public float touchRange = 0f;
    [Tooltip("Flees for a while when the flashlight beam hits it.")]
    public bool fearsLight;
    [Tooltip("Model colour multiplier and glow, so each type looks different.")]
    public Color tint = Color.white;
    [ColorUsage(false, true)] public Color emission = Color.black;

    [Header("Meat distraction")]
    [Tooltip("Seconds it eats thrown meat, ignoring the player completely.")]
    public float eatSeconds = 5f;

    [Header("Attack")]
    public float attackRange = 1.2f;

    [Header("Timings (seconds)")]
    public float searchDuration = 5f;
    public float investigateDuration = 4f;
}
