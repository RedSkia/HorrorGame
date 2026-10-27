using UnityEngine;
using UnityEngine.AI;

// One monster: enum state machine (Patrol, Chase, Check, Attack, Eat, Flee) walking on the NavMesh.
// Spawned by MazeSpawner; asks MonsterSenses what it notices; raises GameEvents.MonsterAttacked when it attacks.
// Check = walk to a spot, wait/look around, give up -> Patrol (lost sight, noise, locker, meat). A hidden player is never caught.
// Drives the model's Animator (Models/Monster/Monster.controller): Speed for idle/walk/run, Rage and Attack triggers.
[RequireComponent(typeof(NavMeshAgent), typeof(AudioSource), typeof(MonsterSenses))]
public class MonsterAI : MonoBehaviour
{
    enum State { Patrol, Chase, Check, Attack, Eat, Flee }

    const float SampleRadius = 2f;
    const float BaitReach = 1.5f;   // metres from the meat at which it is eaten

    // Timings (seconds) and tuning shared by every monster type.
    const float GraceTime = 8f;            // senses are ignored this long after spawn/reset
    const float PatrolWait = 2f;           // pause at each patrol point
    const float ScreamDuration = 1.5f;     // standing still while the chase roar plays
    const float ChaseMemory = 1f;          // keeps chasing the player's real position this long after losing sight
    const float RoarCooldown = 10f;        // no new roar (and pause) within this time of the last one
    const float AttackCooldown = 2f;
    const float GrowlInterval = 5f;
    const float LookAroundSpeed = 90f;     // degrees per second while looking around
    const float BiteInterval = 1f;         // between bites while eating meat
    const float LockerCheckRadius = 5f;    // a checking monster this close to a hidden player may walk to the locker
    const float LockerCheckChance = 0.4f;  // chance it checks when it did not see the player hide
    const float LockerWaitTime = 3f;       // standing at the locker before giving up
    const float FleeSeconds = 4f;          // light-shy monsters flee this long
    const float FleeDistance = 10f;        // ...this far from the player
    const float StepDistance = 1.4f;       // metres walked between two footsteps
    const float StepVolume = 0.8f;
    const float StepMinDistance = 2f, StepMaxDistance = 16f;   // 3D falloff of the monster's sounds
    const float ScreamShakeRange = 12f;    // the chase roar shakes the camera within this range
    const float ScreamShakeDegrees = 1.5f, ScreamShakeSeconds = 0.6f;

    [SerializeField] Animator animator;
    [SerializeField] AudioClip roarClip;     // spotting the player, starts a chase
    [SerializeField] AudioClip[] attackClips; // the catch (jump scare), one picked at random
    [SerializeField] AudioClip growlClip;
    [SerializeField] AudioClip[] stepClips;   // footsteps, played on the same 3D source

    MonsterConfig config;   // the monster type, assigned in Initialize
    NavMeshAgent agent;
    AudioSource audioSource;
    MonsterSenses senses;
    MazeBuilder maze;

    State state;
    bool stopped;
    float timer;
    float graceTimer;
    float growlTimer;
    float memoryTimer;   // seconds left of chasing without sight
    float lastRoarTime = -RoarCooldown;
    float biteTimer;
    float walked;        // metres since the last footstep
    Vector3 lastKnownPosition;

    // Check data, set on enter
    float checkWait;     // seconds to wait at the spot
    bool spinAround;     // look around (turn on the spot) while waiting
    bool curious;        // still notices glow/noise (searching, not investigating or at a locker)
    bool atLocker;       // walking to / waiting at the hidden player's locker
    bool lockerRolled;   // the locker check is decided once per Check
    Meat bait;           // meat being run to (and then eaten), else null

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        senses = GetComponent<MonsterSenses>();
        audioSource.minDistance = StepMinDistance;
        audioSource.maxDistance = StepMaxDistance;
    }

    void OnEnable()
    {
        GameEvents.NoiseMade += OnNoiseMade;
        GameEvents.MeatLanded += OnMeatLanded;
    }

    void OnDisable()
    {
        GameEvents.NoiseMade -= OnNoiseMade;
        GameEvents.MeatLanded -= OnMeatLanded;
    }

    public void Initialize(MonsterConfig config, MazeBuilder maze, PlayerController playerController, Flashlight flashlight)
    {
        this.config = config;
        this.maze = maze;
        senses.Initialize(config, playerController, flashlight);
        Fx.Tint(gameObject, config.tint, config.emission);   // the type's look: blind and light-shy monsters differ
        Respawn(transform.position);
    }

    // Moves the monster to a new home (MazeSpawner picks it) and starts over with a grace period.
    public void Respawn(Vector3 position)
    {
        agent.Warp(position);
        graceTimer = GraceTime;
        EnterPatrol();
    }

    void Update()
    {
        if (Time.deltaTime == 0f) return;   // paused: no sensing, no state changes
        timer -= Time.deltaTime;
        graceTimer -= Time.deltaTime;
        animator.SetFloat("Speed", agent.velocity.magnitude);
        UpdateFootsteps();

        // A light-shy monster hit by the beam runs off, whatever it was doing (except biting or eating).
        if (config.fearsLight && state != State.Flee && state != State.Attack && state != State.Eat && graceTimer <= 0f && senses.LitByFlashlight())
            EnterFlee();

        switch (state)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase: UpdateChase(); break;
            case State.Check: UpdateCheck(); break;
            case State.Attack: UpdateAttack(); break;
            case State.Eat: UpdateEat(); break;
            case State.Flee: UpdateFlee(); break;
        }
    }

    // One step per StepDistance walked (silent standing).
    void UpdateFootsteps()
    {
        float speed = agent.velocity.magnitude;
        walked = speed < 0.1f ? 0f : walked + speed * Time.deltaTime;
        if (walked < StepDistance) return;
        walked = 0f;
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(stepClips[Random.Range(0, stepClips.Length)], StepVolume);
    }

    void EnterPatrol()
    {
        state = State.Patrol;
        GoToRandomPoint();
    }

    void UpdatePatrol()
    {
        if (Sense()) return;
        if (Finished(PatrolWait)) GoToRandomPoint();
    }

    void EnterChase(bool scream)
    {
        state = State.Chase;
        growlTimer = 0f;
        memoryTimer = ChaseMemory;
        if (!scream || Time.time - lastRoarTime < RoarCooldown)
        {
            StartRunning();
            return;
        }
        lastRoarTime = Time.time;
        Stop(ScreamDuration);
        audioSource.PlayOneShot(roarClip);
        animator.SetTrigger("Rage");
        if (senses.DistanceToPlayer <= ScreamShakeRange)
            GameEvents.RaiseCameraShake(ScreamShakeDegrees, ScreamShakeSeconds);
    }

    void UpdateChase()
    {
        if (stopped)
        {
            FacePlayer();
            if (timer <= 0f) StartRunning();
            return;
        }

        if (senses.CanSeePlayer())
        {
            memoryTimer = ChaseMemory;
            lastKnownPosition = senses.PlayerPosition;
        }
        else
        {
            // Lost sight: keeps heading for the real position for ChaseMemory, never for a hidden player.
            memoryTimer -= Time.deltaTime;
            if (memoryTimer <= 0f || senses.PlayerHidden)
            {
                EnterSearch(lastKnownPosition);
                return;
            }
        }

        agent.SetDestination(senses.PlayerPosition);
        Growl();
        if (senses.DistanceToPlayer <= config.attackRange) EnterAttack();
    }

    // The one "go somewhere, then wait" state. Set on enter: where, how fast, how long to wait, whether to look around, optional meat.
    void EnterCheck(Vector3 position, float speed, float wait, bool spin, Meat meat = null)
    {
        state = State.Check;
        checkWait = wait;
        spinAround = spin;
        curious = !spin;
        bait = meat;
        lockerRolled = atLocker = false;
        MoveTo(position, speed);
    }

    void EnterSearch(Vector3 position) => EnterCheck(position, config.walkSpeed, config.searchDuration, true);

    void EnterInvestigate(Vector3 position, float speed, Meat meat = null) => EnterCheck(position, speed, config.investigateDuration, true, meat);

    void UpdateCheck()
    {
        if (bait != null) { if (RunToBait()) return; }   // while running to meat the player is ignored
        else if (Sense()) return;
        if (stopped && spinAround) transform.Rotate(0f, LookAroundSpeed * Time.deltaTime, 0f);
        if (CheckLockerNow()) return;
        if (Finished(checkWait)) EnterPatrol();
    }

    // Follows the (moving) meat; true when it claimed it and started eating.
    bool RunToBait()
    {
        Vector3 meatPosition = bait.transform.position;
        if (!stopped && (meatPosition - agent.destination).sqrMagnitude > 0.25f) agent.SetDestination(meatPosition);
        if (Vector3.Distance(transform.position, meatPosition) > BaitReach) return false;
        if (bait.TryClaim())
        {
            EnterEat();
            return true;
        }
        bait = null;   // someone else got it: just look around
        return false;
    }

    // Arrived at the spot where it lost the player. If the player is hiding close by: always checks the locker
    // when it saw them hide, otherwise only by chance. True when it started the check.
    bool CheckLockerNow()
    {
        if (lockerRolled || !stopped || !senses.PlayerHidden) return false;
        lockerRolled = true;
        if (senses.DistanceToPlayer > LockerCheckRadius) return false;
        if (!senses.SawPlayerHide && Random.value > LockerCheckChance) return false;
        // The hidden player stands next to their locker: walk there, wait in front of it (tension), give up. It never opens or attacks.
        atLocker = true;
        curious = spinAround = false;
        checkWait = LockerWaitTime;
        MoveTo(senses.PlayerPosition, config.walkSpeed);
        return true;
    }

    void OnNoiseMade(Vector3 position, float radius)
    {
        if (!IgnoresNoise) Hear(position, radius, null);
    }

    // Meat pulls a monster away from anything but the catch, a locker check, fleeing and eating (so also out of a chase).
    // The meat repeats the call while unclaimed; a monster already running to it just keeps going.
    void OnMeatLanded(Meat meat, float radius)
    {
        if (graceTimer > 0f || Busy || bait == meat) return;
        Hear(meat.transform.position, radius, meat);
    }

    // A noise (a trapped chest, thrown meat) within hearing range: run there and look around; the first to arrive eats meat.
    void Hear(Vector3 position, float radius, Meat meat)
    {
        if (Vector3.Distance(transform.position, position) <= radius * config.noiseRadiusScale)
            EnterInvestigate(position, config.runSpeed, meat);
    }

    // In its grace period, or busy with the player (or fleeing).
    bool IgnoresNoise => graceTimer > 0f || state == State.Chase || Busy;

    bool Busy => state is State.Attack or State.Flee or State.Eat || atLocker && state == State.Check;

    // Eats the claimed meat: stands still and bites; no senses, no attack, no flee until it is done.
    void EnterEat()
    {
        state = State.Eat;
        Stop(config.eatSeconds);
        biteTimer = 0f;
        FaceTowards(bait.transform.position, 1f);
    }

    void UpdateEat()
    {
        biteTimer -= Time.deltaTime;
        if (biteTimer <= 0f)
        {
            biteTimer = BiteInterval;
            animator.SetTrigger("Attack");
        }
        if (timer > 0f) return;
        if (bait != null) Destroy(bait.gameObject);
        bait = null;
        EnterPatrol();
    }

    // Runs away from the player for a few seconds, then goes back to patrolling.
    void EnterFlee()
    {
        state = State.Flee;
        bait = null;
        timer = FleeSeconds;
        Vector3 away = transform.position - senses.PlayerPosition;
        away.y = 0f;
        Vector3 target = transform.position + away.normalized * FleeDistance;
        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, FleeDistance / 2f, NavMesh.AllAreas)) hit.position = transform.position;
        MoveTo(hit.position, config.runSpeed);
    }

    void UpdateFlee()
    {
        if (timer <= 0f) EnterPatrol();
    }

    void EnterAttack()
    {
        state = State.Attack;
        Stop(AttackCooldown);
        animator.SetTrigger("Attack");
        audioSource.PlayOneShot(attackClips[Random.Range(0, attackClips.Length)]);
        GameEvents.RaiseMonsterAttacked(transform);
    }

    void UpdateAttack()
    {
        FacePlayer();
        if (timer <= 0f) EnterChase(false);
    }

    // True when a sense changed the state. Ignored during the grace period.
    bool Sense()
    {
        if (graceTimer > 0f) return false;

        if (senses.CanSeePlayer())
        {
            lastKnownPosition = senses.PlayerPosition;
            EnterChase(true);
            return true;
        }
        if ((state != State.Check || curious) && (senses.SensesGlow() || senses.HearsPlayer()))
        {
            EnterInvestigate(senses.PlayerPosition, config.walkSpeed);
            return true;
        }
        return false;
    }

    // Walks to the current destination, then waits there; true once the wait is over.
    bool Finished(float waitSeconds)
    {
        if (stopped) return timer <= 0f;
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            Stop(waitSeconds);
        return false;
    }

    void GoToRandomPoint()
    {
        Vector3 point = transform.position;
        if (NavMesh.SamplePosition(maze.RandomCellPosition(), out NavMeshHit hit, SampleRadius, NavMesh.AllAreas))
            point = hit.position;
        MoveTo(point, config.walkSpeed);
    }

    void StartRunning() => MoveTo(senses.PlayerPosition, config.runSpeed);

    void MoveTo(Vector3 position, float speed)
    {
        stopped = false;
        agent.isStopped = false;
        agent.speed = speed;
        agent.SetDestination(position);
    }

    void Stop(float seconds)
    {
        stopped = true;
        timer = seconds;
        agent.isStopped = true;
    }

    void Growl()
    {
        growlTimer -= Time.deltaTime;
        if (growlTimer > 0f) return;
        growlTimer = GrowlInterval;
        audioSource.PlayOneShot(growlClip);
    }

    void FacePlayer() => FaceTowards(senses.PlayerPosition, 8f * Time.deltaTime);

    // Turns on the spot toward a point; blend 1 = snap.
    void FaceTowards(Vector3 target, float blend)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction == Vector3.zero) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), blend);
    }
}
