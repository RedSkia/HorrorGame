using System;
using UnityEngine;

// Static events between scripts (GameManager, monsters, interactables raise; the HUD listens), so gameplay never calls the UI.
public static class GameEvents
{
    public static event Action<Transform> MonsterAttacked; // a monster bit (GameManager decides if it counts)
    public static event Action<Transform> PlayerCaught;   // the attacking monster; the catch counts (camera, HUD)
    public static event Action PlayerRespawned;
    public static event Action<float, float> CameraShake; // degrees, seconds
    public static event Action Escaped;                 // walked through the open exit
    public static event Action<float> InteractProgress; // 0-1 while E is held on something that needs holding
    public static event Action<bool, float> Fade;       // to black (true) or back to clear (false), seconds
    public static event Action PlayRequested;
    public static event Action ReloadRequested;
    public static event Action ResumeRequested;
    public static event Action<bool> PauseChanged;      // paused or resumed
    public static event Action<bool> MapToggled;        // the full map opened or closed (HUD raises)
    public static event Action<int> MenuShown;         // best level
    public static event Action<int, int> LevelStarted;  // level, best level
    public static event Action<int> LivesChanged;
    public static event Action CheatsChanged;
    public static event Action<string> Message;
    public static event Action<Vector3, float> NoiseMade; // position, radius (a trapped chest)
    public static event Action ChestOpened;
    public static event Action MeatThrown;
    public static event Action<Meat, float> MeatLanded;   // meat, noise radius
    public static event Action MapFragmentFound;
    public static event Action<bool[]> RewardOffered;   // which Rewards (index = enum value) can be taken
    public static event Action<Reward> RewardChosen;
    public static event Action<int, int, RunStats> GameOver;      // level, best level, stats of the run

    // "?.Invoke()" calls every listener, and does nothing when nobody listens.
    public static void RaiseMonsterAttacked(Transform monster) => MonsterAttacked?.Invoke(monster);
    public static void RaisePlayerCaught(Transform monster) => PlayerCaught?.Invoke(monster);
    public static void RaisePlayerRespawned() => PlayerRespawned?.Invoke();
    public static void RaiseCameraShake(float degrees, float seconds) => CameraShake?.Invoke(degrees, seconds);
    public static void RaiseEscaped() => Escaped?.Invoke();
    public static void RaiseInteractProgress(float progress) => InteractProgress?.Invoke(progress);
    public static void RaiseFade(bool toBlack, float seconds) => Fade?.Invoke(toBlack, seconds);
    public static void RaisePlayRequested() => PlayRequested?.Invoke();
    public static void RaiseReloadRequested() => ReloadRequested?.Invoke();
    public static void RaiseResumeRequested() => ResumeRequested?.Invoke();
    public static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);
    public static void RaiseMapToggled(bool open) => MapToggled?.Invoke(open);
    public static void RaiseMenuShown(int best) => MenuShown?.Invoke(best);
    public static void RaiseLevelStarted(int level, int best) => LevelStarted?.Invoke(level, best);
    public static void RaiseLivesChanged(int lives) => LivesChanged?.Invoke(lives);
    public static void RaiseCheatsChanged() => CheatsChanged?.Invoke();
    public static void RaiseMessage(string text) => Message?.Invoke(text);
    public static void RaiseNoiseMade(Vector3 position, float radius) => NoiseMade?.Invoke(position, radius);
    public static void RaiseChestOpened() => ChestOpened?.Invoke();
    public static void RaiseMeatThrown() => MeatThrown?.Invoke();
    public static void RaiseMeatLanded(Meat meat, float radius) => MeatLanded?.Invoke(meat, radius);
    public static void RaiseMapFragmentFound() => MapFragmentFound?.Invoke();
    public static void RaiseRewardOffered(bool[] available) => RewardOffered?.Invoke(available);
    public static void RaiseRewardChosen(Reward reward) => RewardChosen?.Invoke(reward);
    public static void RaiseGameOver(int level, int best, RunStats stats) => GameOver?.Invoke(level, best, stats);
}
