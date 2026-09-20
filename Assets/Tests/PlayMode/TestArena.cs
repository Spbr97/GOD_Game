using System;
using System.Collections;
using System.Collections.Generic;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Memory;
using Game.Quests;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Game.Tests.Play
{
    /// <summary>
    /// Builds a throwaway arena for a PlayMode test and tears it down again.
    ///
    /// PlayMode tests exist to cover what EditMode cannot: coroutines, physics
    /// triggers, navigation and anything that only goes wrong after several frames
    /// of a real game loop. Every one of the three defects found at the end of
    /// TASK 004 was of that kind, and none of the 78 EditMode tests could have
    /// caught them.
    ///
    /// The arena is built in code rather than loaded from <c>Avarsha.unity</c> on
    /// purpose. A test that depends on the hub scene fails whenever someone moves a
    /// building, which teaches the team to ignore it.
    /// </summary>
    public class TestArena : IDisposable
    {
        private readonly List<GameObject> objects = new();
        private readonly List<Object> assets = new();
        private NavMeshSurface surface;

        public GameObject Floor { get; private set; }

        // ---------------------------------------------------------------- lifetime

        /// <summary>Registers an object for destruction when the arena goes away.</summary>
        public GameObject Track(GameObject go)
        {
            objects.Add(go);
            return go;
        }

        /// <summary>Registers a ScriptableObject created for one test.</summary>
        public T TrackAsset<T>(T asset) where T : Object
        {
            assets.Add(asset);
            return asset;
        }

        public void Dispose()
        {
            if (surface != null)
            {
                surface.RemoveData();
            }

            // Immediate rather than deferred: the next test's setup runs before the
            // end of frame would arrive, and a surviving manager singleton would be
            // found by the next test's Instance lookup.
            for (var i = objects.Count - 1; i >= 0; i--)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }

            for (var i = assets.Count - 1; i >= 0; i--)
            {
                if (assets[i] != null)
                {
                    Object.DestroyImmediate(assets[i]);
                }
            }

            objects.Clear();
            assets.Clear();

            // WorldState survives scene loads by design, so it is emptied rather than
            // destroyed; see the comment on EnsureWorldState.
            if (WorldState.Instance != null)
            {
                WorldState.Instance.ResetAll();
            }

            Difficulty.Reset();
        }

        // ------------------------------------------------------------------ ground

        /// <summary>A flat slab centred on the origin, with its walking surface at y = 0.</summary>
        public GameObject BuildFloor(float size = 80f)
        {
            Floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Floor.name = "Floor";
            Floor.transform.position = new Vector3(0f, -0.5f, 0f);
            Floor.transform.localScale = new Vector3(size, 1f, size);
            return Track(Floor);
        }

        /// <summary>A solid wall, for line-of-sight tests.</summary>
        public GameObject BuildWall(Vector3 centre, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.position = centre;
            wall.transform.localScale = size;
            return Track(wall);
        }

        /// <summary>
        /// Bakes a NavMesh over whatever colliders exist right now. Call it after the
        /// floor and before the actors: an enemy capsule standing on the floor at bake
        /// time would be carved out of the mesh it is supposed to stand on.
        /// </summary>
        public void BuildNavMesh()
        {
            var go = Track(new GameObject("Navigation"));
            surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            Assert.IsTrue(surface.navMeshData != null, "The test arena failed to bake a NavMesh.");
        }

        // --------------------------------------------------------------- singletons

        /// <summary>
        /// World state is a DontDestroyOnLoad singleton whose Awake destroys any
        /// duplicate, so it is created once per play session and reused. Dispose empties
        /// it instead of destroying it, which keeps tests independent of each other.
        /// </summary>
        public WorldState EnsureWorldState()
        {
            if (WorldState.Instance != null)
            {
                WorldState.Instance.ResetAll();
                return WorldState.Instance;
            }

            var go = new GameObject("WorldState");
            var state = go.AddComponent<WorldState>();
            return state;
        }

        public QuestManager SpawnQuestManager(params QuestDefinition[] catalogue)
        {
            var go = Track(new GameObject("QuestManager"));
            go.SetActive(false);
            var manager = go.AddComponent<QuestManager>();
            manager.Configure(catalogue);
            go.SetActive(true);
            return manager;
        }

        public MemoryManager SpawnMemoryManager(params MemoryFragment[] catalogue)
        {
            var go = Track(new GameObject("MemoryManager"));
            go.SetActive(false);
            var manager = go.AddComponent<MemoryManager>();
            manager.Configure(catalogue);
            go.SetActive(true);
            return manager;
        }

        public CheckpointManager SpawnCheckpointManager()
        {
            return Track(new GameObject("CheckpointManager")).AddComponent<CheckpointManager>();
        }

        // ------------------------------------------------------------------- player

        /// <summary>
        /// The player as combat and AI see it: something that can die, can be hit, and
        /// is found by <see cref="PlayerDeath"/> rather than by tag.
        ///
        /// The body carries no solid collider. Nothing needs the player to be solid
        /// here, and a capsule around the enemy's target would sit between the eye and
        /// the chest on some line-of-sight tests.
        /// </summary>
        public PlayerRig SpawnPlayer(Vector3 position, bool withWeapon = false, bool withCombatInput = false)
        {
            var root = Track(new GameObject("Player"));
            root.SetActive(false);
            root.transform.position = position;

            // Kinematic, gravity off: trigger volumes need a Rigidbody on one side, and
            // the tests place the player by hand rather than letting it fall.
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            // A sleeping Rigidbody stops receiving OnTriggerStay, and a kinematic body
            // that never moves falls asleep almost immediately — which would silently
            // stop hazard volumes from ticking.
            body.sleepThreshold = 0f;

            var health = root.AddComponent<HealthComponent>();
            health.Configure(100f);
            var stamina = root.AddComponent<StaminaComponent>();
            var divine = root.AddComponent<DivineEnergyComponent>();

            var hurtbox = AddHurtbox(root, "Hurtbox", new Vector3(0f, 1f, 0f), 0.5f, 2f);
            hurtbox.Configure(health, 1f, Faction.Player);

            var rig = new PlayerRig
            {
                Root = root,
                Health = health,
                Stamina = stamina,
                DivineEnergy = divine,
                Hurtbox = hurtbox
            };

            if (withWeapon)
            {
                var weaponGo = new GameObject("Weapon");
                weaponGo.transform.SetParent(root.transform, false);
                rig.Weapon = weaponGo.AddComponent<WeaponController>();

                var hitGo = new GameObject("HitVolume");
                hitGo.transform.SetParent(weaponGo.transform, false);
                hitGo.transform.localPosition = new Vector3(0f, 1f, 1.1f);

                var box = hitGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.6f, 1.6f, 1.8f);

                var rb = hitGo.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                rig.WeaponHitbox = hitGo.AddComponent<Hitbox>();
                rig.WeaponHitbox.SetOwner(root);
                rig.WeaponHitbox.SetFaction(Faction.Player);
            }

            if (withCombatInput)
            {
                var actions = BuildInputActions();
                rig.Guard = root.AddComponent<GuardController>();
                rig.LockOn = root.AddComponent<LockOnController>();
                rig.LockOn.Configure(actions);
                rig.Combat = root.AddComponent<CombatController>();
                rig.Combat.Configure(actions);
            }

            // Added last so its default "disable while dead" list can find the combat
            // controller, exactly as it does on the real player prefab.
            rig.Death = root.AddComponent<PlayerDeath>();

            root.SetActive(true);
            return rig;
        }

        /// <summary>
        /// A minimal input asset with the actions <see cref="CombatController"/> looks
        /// for. Built in code so the tests do not depend on the contents of
        /// PlayerControls.inputactions, which designers are free to rebind.
        /// </summary>
        public InputActionAsset BuildInputActions()
        {
            var asset = TrackAsset(ScriptableObject.CreateInstance<InputActionAsset>());
            var map = asset.AddActionMap("Gameplay");
            map.AddAction("LightAttack", InputActionType.Button);
            map.AddAction("HeavyAttack", InputActionType.Button);
            map.AddAction("Dodge", InputActionType.Button);
            map.AddAction("Guard", InputActionType.Button);
            map.AddAction("LockOn", InputActionType.Button);
            map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            return asset;
        }

        // ------------------------------------------------------------------ targets

        /// <summary>A hittable object with health and nothing else — the training dummy.</summary>
        public DummyRig SpawnDummy(string name, Vector3 position, float health = 60f,
            Faction faction = Faction.Neutral, int hurtboxes = 1)
        {
            var root = Track(new GameObject(name));
            root.SetActive(false);
            root.transform.position = position;

            var pool = root.AddComponent<HealthComponent>();
            pool.Configure(health);

            var rig = new DummyRig { Root = root, Health = pool, Hurtboxes = new Hurtbox[hurtboxes] };

            for (var i = 0; i < hurtboxes; i++)
            {
                var hurtbox = AddHurtbox(root, $"Hurtbox{i}", new Vector3(0f, 0.6f + i * 0.5f, 0f), 0.5f, 1f);
                hurtbox.Configure(pool, 1f, faction);
                rig.Hurtboxes[i] = hurtbox;
            }

            root.SetActive(true);
            return rig;
        }

        // ------------------------------------------------------------------- enemies

        public EnemyGroup SpawnGroup(Vector3 position, int simultaneousAttackers, float radius = 25f)
        {
            var go = Track(new GameObject("EnemyGroup"));
            go.transform.position = position;
            var group = go.AddComponent<EnemyGroup>();
            group.Configure(simultaneousAttackers, radius);
            return group;
        }

        public PatrolRoute SpawnPatrolRoute(params Vector3[] points)
        {
            var go = Track(new GameObject("PatrolRoute"));
            go.SetActive(false);

            var transforms = new Transform[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var waypoint = new GameObject($"Waypoint{i}");
                waypoint.transform.SetParent(go.transform, false);
                waypoint.transform.position = points[i];
                transforms[i] = waypoint.transform;
            }

            var route = go.AddComponent<PatrolRoute>();
            route.Configure(transforms, true, 0.2f);
            go.SetActive(true);
            return route;
        }

        /// <summary>
        /// An archetype with test-friendly numbers. Everything the state machine
        /// branches on is supplied, so a test never depends on the tuning of a shipped
        /// archetype asset.
        /// </summary>
        public EnemyArchetype NewArchetype(string id = "TEST_ENEMY", float health = 60f, float damage = 10f,
            float attackRange = 2.4f, float telegraph = 0.4f, float poise = 20f,
            float sight = 25f, float sightCone = 200f)
        {
            var archetype = TrackAsset(ScriptableObject.CreateInstance<EnemyArchetype>());
            archetype.name = id;
            archetype.Configure(id, id, EnemyClass.ForgottenSoldier, health, damage, attackRange, telegraph,
                poise, sight, sightCone);
            archetype.ConfigureBehaviour(30f, 0f, 2f, 1f, 2f, 3f);
            return archetype;
        }

        /// <summary>
        /// One enemy, built the way the Avarsha encounter builds them: an empty root at
        /// ground level (a NavMeshAgent snaps its own transform to the mesh, so a
        /// centre-pivoted capsule would sink to the waist) with the body as a child.
        ///
        /// Deliberately no <see cref="EnemyHealth"/>: it despawns corpses two seconds
        /// after death, and a test that wants to assert on a dead enemy needs it to
        /// still be there.
        /// </summary>
        public EnemyRig SpawnEnemy(string name, Vector3 position, EnemyArchetype archetype,
            PatrolRoute route = null, EnemyGroup group = null, Quaternion? facing = null,
            float bodyHeight = 2f, float bodyRadius = 0.5f)
        {
            var root = Track(new GameObject(name));
            root.SetActive(false);
            root.transform.SetPositionAndRotation(position, facing ?? Quaternion.identity);

            if (group != null)
            {
                root.transform.SetParent(group.transform, true);
            }

            // Visual only. Its collider is removed so it cannot block the enemy's own
            // line-of-sight ray, which starts inside it.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, bodyHeight * 0.5f, 0f);
            body.transform.localScale = new Vector3(bodyRadius * 2f, bodyHeight * 0.5f, bodyRadius * 2f);

            var health = root.AddComponent<HealthComponent>();
            health.Configure(archetype.MaxHealth);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = bodyRadius;
            agent.height = bodyHeight;
            agent.baseOffset = 0f;
            agent.speed = archetype.PatrolSpeed;
            agent.angularSpeed = 720f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.3f;

            var hurtbox = AddHurtbox(root, "Hurtbox", new Vector3(0f, bodyHeight * 0.5f, 0f), bodyRadius, bodyHeight);
            hurtbox.Configure(health, 1f, Faction.Hostile);

            var hitGo = new GameObject("HitVolume");
            hitGo.transform.SetParent(root.transform, false);
            hitGo.transform.localPosition = new Vector3(0f, bodyHeight * 0.5f, archetype.AttackRange * 0.5f);

            var box = hitGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.4f, 1.6f, archetype.AttackRange);

            // OnTriggerEnter between two triggers needs a Rigidbody on one side.
            var rb = hitGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var hitbox = hitGo.AddComponent<Hitbox>();
            hitbox.SetOwner(root);
            hitbox.SetFaction(Faction.Hostile);

            var perception = root.AddComponent<EnemyPerception>();
            perception.Configure(archetype);

            var navigator = root.AddComponent<EnemyNavigator>();
            navigator.Configure(agent);

            var combatant = root.AddComponent<EnemyCombatant>();
            combatant.Configure(archetype, hitbox, body.GetComponentsInChildren<Renderer>());

            var stagger = root.AddComponent<EnemyStagger>();
            stagger.Configure(archetype);

            var controller = root.AddComponent<EnemyController>();
            controller.Configure(archetype, route, group);

            root.SetActive(true);

            return new EnemyRig
            {
                Root = root,
                Controller = controller,
                Combatant = combatant,
                Stagger = stagger,
                Navigator = navigator,
                Perception = perception,
                Health = health,
                Hurtbox = hurtbox,
                Hitbox = hitbox,
                Agent = agent,
                Archetype = archetype
            };
        }

        private Hurtbox AddHurtbox(GameObject root, string name, Vector3 localPosition, float radius, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPosition;

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.radius = radius;
            capsule.height = height;

            return go.AddComponent<Hurtbox>();
        }

        // ------------------------------------------------------------------ waiting

        /// <summary>
        /// Runs the game loop until <paramref name="condition"/> holds, and fails the
        /// test with a readable message if it never does. Every wait in these tests has
        /// a deadline: a PlayMode test that hangs takes the whole suite with it.
        /// </summary>
        public static IEnumerator Until(Func<bool> condition, string description, float seconds = 6f)
        {
            var deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), $"Timed out after {seconds:0.#}s waiting for {description}.");
        }

        /// <summary>Runs the game loop for a fixed span, calling <paramref name="perFrame"/> each frame.</summary>
        public static IEnumerator Observe(float seconds, Action perFrame)
        {
            var deadline = Time.time + seconds;
            while (Time.time < deadline)
            {
                perFrame();
                yield return null;
            }
        }
    }

    public class PlayerRig
    {
        public GameObject Root;
        public HealthComponent Health;
        public StaminaComponent Stamina;
        public DivineEnergyComponent DivineEnergy;
        public PlayerDeath Death;
        public Hurtbox Hurtbox;
        public WeaponController Weapon;
        public Hitbox WeaponHitbox;
        public CombatController Combat;
        public GuardController Guard;
        public LockOnController LockOn;

        public Vector3 Position
        {
            get => Root.transform.position;
            set => Root.transform.position = value;
        }
    }

    public class DummyRig
    {
        public GameObject Root;
        public HealthComponent Health;
        public Hurtbox[] Hurtboxes;
    }

    public class EnemyRig
    {
        public GameObject Root;
        public EnemyController Controller;
        public EnemyCombatant Combatant;
        public EnemyStagger Stagger;
        public EnemyNavigator Navigator;
        public EnemyPerception Perception;
        public HealthComponent Health;
        public Hurtbox Hurtbox;
        public Hitbox Hitbox;
        public NavMeshAgent Agent;
        public EnemyArchetype Archetype;

        public Vector3 Position => Root.transform.position;
        public EnemyState State => Controller.State;
        public float DistanceFromHome => Vector3.Distance(Position, Controller.Home);
    }
}
