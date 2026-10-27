using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Runs the game flow: start menu, build a level (maze, NavMesh, spawns), lives, escape, reward, pause, game over, best level.
// The only singleton-like script; talks to the HUD only through GameEvents.
public class GameManager : MonoBehaviour
{
    const string BestLevelKey = "BestLevel";
    public const float JumpScareSeconds = 1.2f;   // camera snaps to the monster and shakes before the fade and respawn (PlayerCamera reads it too)
    const float EscapeSeconds = 0.5f;             // frozen, uncatchable beat after walking through the exit before the fade
    const float LevelFadeSeconds = 0.8f;          // fade to black after the exit and back in on the next level

    [SerializeField] GameConfig config;
    [SerializeField] MazeBuilder mazeBuilder;
    [SerializeField] NavMeshSurface navMeshSurface;
    [SerializeField] MazeSpawner mazeSpawner;
    [SerializeField] Transform player;
    [SerializeField] PlayerInventory inventory;
    [SerializeField] MazeMap map;
    [SerializeField] ItemData meatItem;
    [SerializeField] ItemData batteryItem;
    [SerializeField] ItemData shortcutKeyItem;
    [Tooltip("Mixer group for all runtime one-shots (Fx.PlayAt).")]
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] InputActionReference pause;
    [Tooltip("0 = random seed each run (only used for level 1).")]
    [SerializeField] int seed;

    PlayerController playerController;
    CharacterController characterController;
    RunStats stats = new RunStats();
    float startTime;
    int level = 1;
    int lives;
    bool catching;         // jump scare running
    bool escaping;         // the beat and fade-out between walking through the exit and the reward screen
    bool playing;          // a run is on (not in the start menu or game over screen)
    bool paused;
    bool choosingReward;   // the reward screen is up (time frozen)
    bool mapOpen;          // the HUD's full map is open

    void Awake()
    {
        playerController = player.GetComponent<PlayerController>();
        characterController = player.GetComponent<CharacterController>();
        Fx.Group = sfxGroup;
    }

    void OnEnable()
    {
        pause.action.Enable();
        GameEvents.ResumeRequested += Resume;
        GameEvents.MapToggled += OnMapToggled;
        GameEvents.MonsterAttacked += OnMonsterAttacked;
        GameEvents.Escaped += OnEscaped;
        GameEvents.PlayRequested += StartGame;
        GameEvents.ReloadRequested += ReloadScene;
        GameEvents.ChestOpened += OnChestOpened;
        GameEvents.MeatThrown += OnMeatThrown;
        GameEvents.RewardChosen += OnRewardChosen;
    }

    void OnDisable()
    {
        pause.action.Disable();
        GameEvents.ResumeRequested -= Resume;
        GameEvents.MapToggled -= OnMapToggled;
        GameEvents.MonsterAttacked -= OnMonsterAttacked;
        GameEvents.Escaped -= OnEscaped;
        GameEvents.PlayRequested -= StartGame;
        GameEvents.ReloadRequested -= ReloadScene;
        GameEvents.ChestOpened -= OnChestOpened;
        GameEvents.MeatThrown -= OnMeatThrown;
        GameEvents.RewardChosen -= OnRewardChosen;
    }

    static int BestLevel
    {
        get => PlayerPrefs.GetInt(BestLevelKey, 0);
        set => PlayerPrefs.SetInt(BestLevelKey, value);
    }

    void OnChestOpened() => stats.ChestsOpened++;

    void OnMeatThrown() => stats.MeatThrown++;

    // Start menu: the player stays inactive (no input, no camera) until Play.
    void Start()
    {
        Settings.ApplyVolume(sfxGroup.audioMixer);
        player.gameObject.SetActive(false);
        SetCursorLocked(false);
        GameEvents.RaiseMenuShown(BestLevel);
    }

    void Update()
    {
        if (playing && !catching && !escaping && !choosingReward && pause.action.WasPressedThisFrame()) SetPaused(!paused);

        // Mouse look needs a locked cursor. The browser drops the lock on its own (alt-tab, a click outside, its Esc) and only
        // grants it during a click or key press, so a lock asked for from a UI button is deferred; keep asking while the player is in play.
        if (player.gameObject.activeSelf && Cursor.lockState != CursorLockMode.Locked) SetCursorLocked(true);
    }

    void Resume() => SetPaused(false);

    void OnMapToggled(bool open)
    {
        mapOpen = open;
        UpdateFrozen();
    }

    // The player cannot act while the map is open, during the jump scare and during the escape beat.
    void UpdateFrozen() => playerController.Frozen = mapOpen || catching || escaping;

    // Pause also mutes all audio.
    void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        Freeze(value);
        AudioListener.pause = value;
        GameEvents.RaisePauseChanged(value);
    }

    // Pause, reward choice and game over: time stops and the player is deactivated (its camera goes off, MenuCamera shows black behind the screen; no input works).
    void Freeze(bool frozen)
    {
        Time.timeScale = frozen ? 0f : 1f;
        player.gameObject.SetActive(!frozen);
        SetCursorLocked(!frozen);
    }

    void StartGame()
    {
        playing = true;
        stats = new RunStats();
        lives = config.maxLives;
        startTime = Time.time;
        Freeze(false);
        StartLevel();
    }

    void StartLevel()
    {
        int size = config.startMazeSize + level - 1;
        int levelSeed = seed != 0 && level == 1 ? seed : Random.Range(1, int.MaxValue);
        Debug.Log($"Level {level}, maze {size}x{size}, seed {levelSeed}");

        BestLevel = Mathf.Max(BestLevel, level);

        playerController.Leave();   // leave a locker (restores the ambient) before the old maze goes
        mazeSpawner.Clear();
        var grid = MazeGenerator.Generate(size, size, levelSeed, Mathf.Clamp(size / config.cellsPerRoom, 1, config.maxRooms), config.braidChance);
        List<Shortcut> shortcuts = MazePlacement.PickShortcuts(grid, config.ShortcutCount(size * size));
        foreach (Shortcut shortcut in shortcuts) MazeGenerator.SetShortcut(grid, shortcut, true);
        mazeBuilder.Build(grid);
        navMeshSurface.BuildNavMesh();
        mazeSpawner.Spawn(grid, level, shortcuts);
        map.Begin(grid, mazeSpawner.DoorCell);
        MovePlayerToStart();

        GameEvents.RaiseLevelStarted(level, BestLevel);
        GameEvents.RaiseLivesChanged(lives);
    }

    // A monster bit and it counts (PlayerCaught for camera/HUD): jump scare (PlayerCamera snaps the camera to the monster), then lose a life,
    // respawn at the start and send the monsters back to their spawn points. The player is frozen meanwhile.
    void OnMonsterAttacked(Transform monster)
    {
        if (DebugCheats.GodMode || lives <= 0 || catching || escaping) return;
        GameEvents.RaisePlayerCaught(monster);
        StartCoroutine(CaughtSequence());
    }

    IEnumerator CaughtSequence()
    {
        catching = true;
        UpdateFrozen();
        lives--;
        stats.TimesCaught++;
        GameEvents.RaiseLivesChanged(lives);

        yield return new WaitForSeconds(JumpScareSeconds);

        catching = false;
        UpdateFrozen();
        if (lives == 0)
        {
            EndGame();
            yield break;
        }

        GameEvents.RaiseMessage($"Caught! {lives} {(lives == 1 ? "life" : "lives")} left");
        MovePlayerToStart();
        mazeSpawner.ResetMonsters();
        GameEvents.RaisePlayerRespawned();
    }

    // The player walked through the open exit: a short beat (frozen, uncatchable), fade to black, then the level is done.
    void OnEscaped()
    {
        if (!escaping) StartCoroutine(EscapeSequence());
    }

    IEnumerator EscapeSequence()
    {
        escaping = true;
        UpdateFrozen();
        yield return new WaitForSeconds(EscapeSeconds);
        GameEvents.RaiseFade(true, LevelFadeSeconds);
        yield return new WaitForSeconds(LevelFadeSeconds);
        escaping = false;
        UpdateFrozen();

        level++;
        stats.LevelsEscaped++;
        inventory.Remove(ItemType.Key, 1);
        inventory.Remove(ItemType.ShortcutKey, int.MaxValue);   // its doors are gone with the level

        // Reward choice (frozen until one is picked); options that can't be taken are disabled.
        var available = new[]
        {
            lives < config.maxLives,
            inventory.HasRoom(meatItem),
            inventory.HasRoom(batteryItem),
            inventory.HasRoom(shortcutKeyItem),
        };
        if (System.Array.IndexOf(available, true) < 0)   // nothing could be taken: straight to the next level
        {
            StartNextLevel();
            yield break;
        }
        choosingReward = true;
        Freeze(true);
        GameEvents.RaiseRewardOffered(available);
    }

    void OnRewardChosen(Reward reward)
    {
        switch (reward)
        {
            case Reward.Life: lives++; break;
            case Reward.Meat: inventory.Add(meatItem, 1); break;
            case Reward.Battery: inventory.Add(batteryItem, 1); break;
            case Reward.ShortcutKey: inventory.Add(shortcutKeyItem, 1); break;
        }
        choosingReward = false;
        Freeze(false);
        StartNextLevel();
    }

    // The new level is built behind the black screen, then fades in.
    void StartNextLevel()
    {
        StartLevel();
        GameEvents.RaiseFade(false, LevelFadeSeconds);
        GameEvents.RaiseMessage($"You escaped! Level {level}");
    }

    void EndGame()
    {
        playing = false;
        stats.Seconds = Time.time - startTime;
        Freeze(true);
        GameEvents.RaiseGameOver(level, BestLevel, stats);
    }

    // Reloads the scene: level 1, full lives, fresh inventory, back at the start menu.
    void ReloadScene()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // A CharacterController overrides transform.position every frame, so it must be disabled while teleporting.
    void MovePlayerToStart()
    {
        characterController.enabled = false;
        player.position = mazeBuilder.CellToWorld(0, 0) + Vector3.up * 0.1f;
        characterController.enabled = true;
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
