using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// All on-screen UI (UI Toolkit, HUD.uxml): status, battery/stamina, prompt, messages, hotbar, cheat tags, maps,
// caught/level fade, start menu, pause, reward choice and game over. Only listens to GameEvents and reads player state; gameplay never calls it.
[RequireComponent(typeof(UIDocument))]
public class HUD : MonoBehaviour
{
    const float MessageSeconds = 2.5f;       // a HUD message stays visible this long
    const float CaughtFadeSeconds = 1.5f;    // fade in from black after being caught

    [SerializeField] GameConfig config;
    [SerializeField] PlayerInventory inventory;
    [SerializeField] PlayerController playerController;
    [SerializeField] Flashlight flashlight;
    [SerializeField] Interactor interactor;
    [SerializeField] InputActionReference mapToggle;
    [SerializeField] MazeMap map;
    [Tooltip("2D source (UI mixer group) with the button click clip.")]
    [SerializeField] AudioSource clickSource;

    Label prompt;
    Label level;
    Label best;
    Label message;
    Label batteryPercent;
    Label menuTitle;
    Label menuBest;
    Label gameOverText;
    VisualElement gameOverStats;
    VisualElement controls;
    VisualElement settings;
    Button headBobButton;
    Slider sensitivitySlider, fovSlider, volumeSlider;
    Label sensitivityValue, fovValue, volumeValue;
    VisualElement batteryFill;
    VisualElement staminaFill;
    VisualElement godModeTag;
    VisualElement fullbrightTag;
    VisualElement menu;
    VisualElement gameOver;
    VisualElement pausePanel;
    VisualElement pauseSide;   // the pause screen's slot for the settings and controls panels
    VisualElement fade;
    VisualElement holdBar;
    VisualElement holdFill;
    VisualElement bigMap;
    VisualElement rewardScreen;
    MapView fullMap;
    Button[] rewardButtons;   // index = Reward value
    VisualElement[] hearts;
    VisualElement[] slots;
    float messageHideTime;
    float fadeAlpha;
    float fadeTarget;   // 0 clear, 1 black
    float fadeSeconds = 1f;   // seconds for a full fade
    float nextFlickerTime;
    int flickersLeft;

    void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        prompt = root.Q<Label>("prompt");
        level = root.Q<Label>("level");
        best = root.Q<Label>("best");
        message = root.Q<Label>("message");
        batteryPercent = root.Q<Label>("battery-percent");
        menuTitle = root.Q<Label>("menu-title");
        menuBest = root.Q<Label>("menu-best");
        gameOverText = root.Q<Label>("gameover-text");
        gameOverStats = root.Q("gameover-stats");
        controls = root.Q("controls");
        settings = root.Q("settings");
        headBobButton = root.Q<Button>("headbob-toggle");
        sensitivitySlider = root.Q<Slider>("sensitivity");
        sensitivityValue = root.Q<Label>("sensitivity-value");
        fovSlider = root.Q<Slider>("fov");
        fovValue = root.Q<Label>("fov-value");
        volumeSlider = root.Q<Slider>("volume");
        volumeValue = root.Q<Label>("volume-value");
        RefreshSettings();
        batteryFill = root.Q("battery-fill");
        staminaFill = root.Q("stamina-fill");
        godModeTag = root.Q("cheat-god");
        fullbrightTag = root.Q("cheat-fullbright");
        menu = root.Q("menu");
        gameOver = root.Q("gameover");
        pausePanel = root.Q("pause");
        pauseSide = root.Q("pause-side");
        fade = root.Q("fade");
        holdBar = root.Q("hold");
        holdFill = root.Q("hold-fill");
        bigMap = root.Q("bigmap");
        rewardScreen = root.Q("reward");
        fullMap = AddMap(root.Q("bigmap-panel"));
        hearts = BuildHearts(root.Q("lives"), config.maxLives);
        slots = BuildSlots(root.Q("hotbar"), inventory.Items.Length);

        // Button callbacks live and die with this document's elements, so they are not removed in OnDisable.
        OnClick(root, "play", OnPlay);
        OnClick(root, "retry", GameEvents.RaiseReloadRequested);
        OnClick(root, "menu-quit", Application.Quit);
        OnClick(root, "controls-toggle", ToggleControls);
        OnClick(root, "gameover-quit", Application.Quit);
        OnClick(root, "resume", GameEvents.RaiseResumeRequested);
        OnClick(root, "pause-controls", ToggleControls);
        OnClick(root, "pause-menu", GameEvents.RaiseReloadRequested);
        OnClick(root, "pause-quit", Application.Quit);
        OnClick(root, "settings-toggle", ToggleSettings);
        OnClick(root, "pause-settings", ToggleSettings);
        OnClick(root, "settings-back", ToggleSettings);
        OnClick(root, "headbob-toggle", ToggleHeadBob);
        sensitivitySlider.RegisterValueChangedCallback(e => { Settings.Sensitivity = e.newValue; RefreshSettings(); });
        fovSlider.RegisterValueChangedCallback(e => { Settings.Fov = e.newValue; RefreshSettings(); });
        volumeSlider.RegisterValueChangedCallback(e => { Settings.Volume = e.newValue; Settings.ApplyVolume(clickSource.outputAudioMixerGroup.audioMixer); RefreshSettings(); });
        OnClick(root, "settings-reset", ResetSettings);
        StepWithArrows(sensitivitySlider, 0.1f);
        StepWithArrows(fovSlider, 5f);
        StepWithArrows(volumeSlider, 0.05f);
        clickSource.ignoreListenerPause = true;   // also audible while the game is paused
        root.Query<Button>().ForEach(b => b.clicked += () => clickSource.Play());
        rewardButtons = new[] { root.Q<Button>("reward-life"), root.Q<Button>("reward-meat"), root.Q<Button>("reward-battery"), root.Q<Button>("reward-shortcut") };
        for (int i = 0; i < rewardButtons.Length; i++)
        {
            var reward = (Reward)i;
            rewardButtons[i].clicked += () => ChooseReward(reward);
        }

        Show(menu, true);
        Show(gameOver, false);
        Show(pausePanel, false);
        Show(rewardScreen, false);

        mapToggle.action.Enable();
        inventory.Changed += RefreshInventory;
        GameEvents.MenuShown += OnMenuShown;
        GameEvents.PauseChanged += OnPauseChanged;
        GameEvents.PlayerCaught += OnPlayerCaught;
        GameEvents.LevelStarted += OnLevelStarted;
        GameEvents.LivesChanged += OnLivesChanged;
        GameEvents.PlayerRespawned += OnPlayerRespawned;
        GameEvents.Message += OnMessage;
        GameEvents.CheatsChanged += RefreshCheats;
        GameEvents.GameOver += OnGameOver;
        GameEvents.RewardOffered += OnRewardOffered;
        GameEvents.InteractProgress += OnInteractProgress;
        GameEvents.Fade += OnFade;
        RefreshInventory();
        RefreshCheats();
    }

    void OnDisable()
    {
        mapToggle.action.Disable();
        inventory.Changed -= RefreshInventory;
        GameEvents.MenuShown -= OnMenuShown;
        GameEvents.PauseChanged -= OnPauseChanged;
        GameEvents.PlayerCaught -= OnPlayerCaught;
        GameEvents.LevelStarted -= OnLevelStarted;
        GameEvents.LivesChanged -= OnLivesChanged;
        GameEvents.PlayerRespawned -= OnPlayerRespawned;
        GameEvents.Message -= OnMessage;
        GameEvents.CheatsChanged -= RefreshCheats;
        GameEvents.GameOver -= OnGameOver;
        GameEvents.RewardOffered -= OnRewardOffered;
        GameEvents.InteractProgress -= OnInteractProgress;
        GameEvents.Fade -= OnFade;
    }

    void Update()
    {
        batteryFill.style.width = Length.Percent(flashlight.Charge * 100f);
        batteryFill.EnableInClassList("low", flashlight.Charge < Flashlight.FlickerThreshold);
        batteryPercent.text = $"{Mathf.RoundToInt(flashlight.Charge * 100f)}%";
        staminaFill.style.width = Length.Percent(playerController.Stamina01 * 100f);

        var target = interactor.Current;
        Show(prompt, target != null);
        if (target != null) prompt.text = $"[E] {target.GetPrompt(playerController)}";

        bool canOpenMap = !IsShown(menu) && Time.timeScale > 0f && !playerController.Frozen && !playerController.IsHidden;
        if (mapToggle.action.WasPressedThisFrame() && (canOpenMap || IsShown(bigMap))) SetMapOpen(!IsShown(bigMap));
        if (IsShown(bigMap)) fullMap.MarkDirtyRepaint();

        if (Time.unscaledTime >= messageHideTime) message.RemoveFromClassList("visible");


        fadeAlpha = Mathf.MoveTowards(fadeAlpha, fadeTarget, Time.unscaledDeltaTime / fadeSeconds);
        fade.style.opacity = fadeAlpha;

        if (IsShown(menu)) FlickerTitle();
    }

    static void OnClick(VisualElement root, string button, System.Action action) => root.Q<Button>(button).clicked += action;

    static void Show(VisualElement element, bool visible) => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

    static bool IsShown(VisualElement element) => element.style.display == DisplayStyle.Flex;

    // Menu title: mostly steady, then a short burst of dropouts like a failing neon tube.
    void FlickerTitle()
    {
        if (Time.unscaledTime < nextFlickerTime) return;
        if (flickersLeft > 0)
        {
            flickersLeft--;
            menuTitle.style.opacity = flickersLeft % 2 == 1 ? Random.Range(0.1f, 0.5f) : 1f;
            nextFlickerTime = Time.unscaledTime + Random.Range(0.03f, 0.09f);
        }
        else
        {
            flickersLeft = Random.Range(1, 4) * 2;
            nextFlickerTime = Time.unscaledTime + Random.Range(1.5f, 5f);
        }
    }

    MapView AddMap(VisualElement container)
    {
        var view = new MapView(map);
        view.AddToClassList("map");
        container.Add(view);
        return view;
    }

    // The open map freezes the player (GameManager listens; time keeps running).
    void SetMapOpen(bool open)
    {
        Show(bigMap, open);
        GameEvents.RaiseMapToggled(open);
    }

    void OnPlayerCaught(Transform monster) => SetMapOpen(false);

    // Shows the reward choice; options that cannot be taken (full lives, no inventory room) are disabled.
    void OnRewardOffered(bool[] available)
    {
        for (int i = 0; i < rewardButtons.Length; i++) rewardButtons[i].SetEnabled(available[i]);
        SetMapOpen(false);
        Show(rewardScreen, true);
    }

    void ChooseReward(Reward reward)
    {
        Show(rewardScreen, false);
        GameEvents.RaiseRewardChosen(reward);
    }

    void RefreshCheats()
    {
        Show(godModeTag, DebugCheats.GodMode);
        Show(fullbrightTag, DebugCheats.Fullbright);
    }

    void OnPlay()
    {
        Show(menu, false);
        GameEvents.RaisePlayRequested();
    }

    void OnMenuShown(int bestLevel)
    {
        menuBest.text = bestLevel > 0 ? $"BEST LEVEL  {bestLevel}" : "";
    }

    void OnLevelStarted(int levelNumber, int bestLevel)
    {
        level.text = $"LEVEL {levelNumber}";
        best.text = $"Best: {bestLevel}";
    }

    void OnLivesChanged(int lives)
    {
        for (int i = 0; i < hearts.Length; i++) hearts[i].EnableInClassList("full", i < lives);
    }

    // Black screen that fades back in once the player has respawned at the start.
    void OnPlayerRespawned()
    {
        fadeAlpha = 1f;
        OnFade(false, CaughtFadeSeconds);
    }

    void OnFade(bool toBlack, float seconds)
    {
        fadeTarget = toBlack ? 1f : 0f;
        fadeSeconds = Mathf.Max(seconds, 0.01f);
    }

    // Fill bar under the prompt while E is held on a door.
    void OnInteractProgress(float progress)
    {
        Show(holdBar, progress > 0f);
        holdFill.style.width = Length.Percent(progress * 100f);
    }

    void OnMessage(string text)
    {
        message.text = text;
        message.AddToClassList("visible");
        messageHideTime = Time.unscaledTime + MessageSeconds;
    }

    // The pause screen borrows the menu's settings and controls panels (the menu is never shown again during a run).
    void OnPauseChanged(bool paused)
    {
        if (paused) SetMapOpen(false);
        Show(pausePanel, paused);
        controls.RemoveFromClassList("open");
        settings.RemoveFromClassList("open");
        if (paused) { pauseSide.Add(settings); pauseSide.Add(controls); }
    }

    // Settings and controls share the space beside the buttons: opening one closes the other.
    void ToggleControls()
    {
        settings.RemoveFromClassList("open");
        controls.ToggleInClassList("open");
    }

    void ToggleSettings()
    {
        controls.RemoveFromClassList("open");
        settings.ToggleInClassList("open");
    }

    void ToggleHeadBob()
    {
        Settings.HeadBob = !Settings.HeadBob;
        RefreshSettings();
    }

    // Arrow keys / d-pad move the focused slider a fixed step (the default step is tiny).
    static void StepWithArrows(Slider slider, float step)
    {
        slider.RegisterCallback<NavigationMoveEvent>(e =>
        {
            float dir = e.direction == NavigationMoveEvent.Direction.Right || e.direction == NavigationMoveEvent.Direction.Up ? 1f
                      : e.direction == NavigationMoveEvent.Direction.Left || e.direction == NavigationMoveEvent.Direction.Down ? -1f : 0f;
            if (dir == 0f) return;
            slider.value = Mathf.Round((slider.value + dir * step) / step) * step;
            e.StopPropagation();
        }, TrickleDown.TrickleDown);
    }

    void ResetSettings()
    {
        Settings.HeadBob = true;
        Settings.Sensitivity = Settings.DefaultSensitivity;
        Settings.Fov = Settings.DefaultFov;
        Settings.Volume = 1f;
        Settings.ApplyVolume(clickSource.outputAudioMixerGroup.audioMixer);
        RefreshSettings();
    }

    void RefreshSettings()
    {
        headBobButton.text = Settings.HeadBob ? "HEAD BOB: ON" : "HEAD BOB: OFF";
        sensitivitySlider.SetValueWithoutNotify(Settings.Sensitivity);
        sensitivityValue.text = $"{Settings.Sensitivity:0.00}";
        fovSlider.SetValueWithoutNotify(Settings.Fov);
        fovValue.text = $"{Mathf.RoundToInt(Settings.Fov)}°";
        volumeSlider.SetValueWithoutNotify(Settings.Volume);
        volumeValue.text = $"{Mathf.RoundToInt(Settings.Volume * 100f)}%";
    }

    void OnGameOver(int levelNumber, int bestLevel, RunStats stats)
    {
        SetMapOpen(false);
        gameOverText.text = $"REACHED LEVEL {levelNumber}   ·   BEST {bestLevel}";
        int minutes = (int)(stats.Seconds / 60f);
        gameOverStats.Clear();
        AddStat("TIME SURVIVED", $"{minutes}:{(int)(stats.Seconds % 60f):00}");
        AddStat("LEVELS ESCAPED", stats.LevelsEscaped.ToString());
        AddStat("CHESTS OPENED", stats.ChestsOpened.ToString());
        AddStat("TIMES CAUGHT", stats.TimesCaught.ToString());
        AddStat("MEAT THROWN", stats.MeatThrown.ToString());
        Show(gameOver, true);
    }

    void AddStat(string name, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("stat-row");
        var nameLabel = new Label(name);
        nameLabel.AddToClassList("stat-name");
        var valueLabel = new Label(value);
        valueLabel.AddToClassList("stat-value");
        row.Add(nameLabel);
        row.Add(valueLabel);
        gameOverStats.Add(row);
    }

    static VisualElement[] BuildHearts(VisualElement container, int count)
    {
        var result = new VisualElement[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = new VisualElement();
            result[i].AddToClassList("heart");
            container.Add(result[i]);
        }
        return result;
    }

    static VisualElement[] BuildSlots(VisualElement container, int count)
    {
        var result = new VisualElement[count];
        for (int i = 0; i < count; i++)
        {
            var slot = new VisualElement();
            slot.AddToClassList("slot");
            slot.Add(new VisualElement { name = "icon" });
            slot.Add(new Label { name = "label" });
            slot.Add(new Label { name = "number", text = (i + 1).ToString() });
            slot.Add(new Label { name = "count" });
            container.Add(slot);
            result[i] = slot;
        }
        return result;
    }

    // Each slot always shows its item (dimmed at 0), its name and the count.
    void RefreshInventory()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            ItemData item = inventory.Items[i];
            int count = inventory.Count(i);

            slots[i].EnableInClassList("selected", i == inventory.SelectedSlot);
            slots[i].EnableInClassList("empty", count == 0);
            var icon = slots[i].Q("icon");
            icon.style.backgroundImage = new StyleBackground(item.icon);
            icon.style.unityBackgroundImageTintColor = item.color;
            slots[i].Q<Label>("label").text = item.displayName;
            slots[i].Q<Label>("count").text = count.ToString();
        }
    }
}
