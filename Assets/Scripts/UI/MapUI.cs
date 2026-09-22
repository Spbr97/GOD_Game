using System.Collections.Generic;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Dialogue;
using Game.Memory;
using Game.Quests;
using Game.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The Map screen (SPEC.md section 42's required screens).
    ///
    /// Drawn from the scene rather than from an authored image (SPEC.md section 78,
    /// placeholder-first): every marker is found by component type at the moment the
    /// map opens, projected from world XZ onto the panel. A hand-drawn map of Avarsha
    /// would be wrong the first time a building moves, and there are six more temples
    /// to come — this one is never out of date, and it costs nothing to keep.
    ///
    /// The extent comes from <see cref="WorldBounds"/> when the scene has one, so the
    /// map is stable as the player walks around instead of rescaling under them, and
    /// falls back to the bounding box of the markers themselves when it does not.
    ///
    /// Every marker carries a letter as well as a colour (SPEC.md section 43:
    /// important information is never communicated by colour alone).
    /// </summary>
    public class MapUI : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        [Header("Panels")]
        [SerializeField] private GameObject mapRoot;

        [Tooltip("Markers are created under this. Its rect is the map's drawing area.")]
        [SerializeField] private RectTransform mapContent;

        [SerializeField] private Text titleLabel;
        [SerializeField] private Text legendLabel;

        [Tooltip("Selected when the map opens, so a gamepad has something to move from (SPEC.md section 43).")]
        [SerializeField] private Button closeButton;

        [Header("Drawing")]
        [Tooltip("Pixels of padding between the outermost marker and the panel edge.")]
        [SerializeField] private float edgePadding = 24f;

        [SerializeField] private float markerSize = 12f;
        [SerializeField] private float playerMarkerSize = 18f;

        private InputAction mapAction;
        private readonly List<GameObject> markers = new();

        // Same lazy-font trick as JournalUI: Resources.GetBuiltinResource refuses to
        // run during a MonoBehaviour's construction, which a static field initializer
        // counts as.
        private static Font cachedFont;
        private static Font BuiltinFont => cachedFont != null ? cachedFont : (cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public bool IsOpen { get; private set; }

        /// <summary>How many markers the last draw produced. Read by tests.</summary>
        public int MarkerCount => markers.Count;

        private void Awake()
        {
            if (mapRoot != null)
            {
                mapRoot.SetActive(false);
            }

            closeButton?.onClick.AddListener(Toggle);

            if (inputActions == null)
            {
                GameLogger.LogError(LogCategory.UI, "No InputActionAsset assigned to MapUI.", this);
                return;
            }

            mapAction = inputActions.FindActionMap("Gameplay")?.FindAction("Map");

            if (mapAction == null)
            {
                GameLogger.LogFallback(
                    LogCategory.UI,
                    "the Map screen has no key",
                    "MapUI.Awake",
                    "the Gameplay action map has no action called 'Map'",
                    "the map can still be opened from a button, but not from the keyboard or a pad",
                    this);
            }
        }

        private void OnEnable()
        {
            mapAction?.Enable();
            if (mapAction != null)
            {
                mapAction.performed += OnMapPressed;
            }
        }

        private void OnDisable()
        {
            if (mapAction != null)
            {
                mapAction.performed -= OnMapPressed;
            }

            mapAction?.Disable();
        }

        private void OnMapPressed(InputAction.CallbackContext context) => Toggle();

        /// <summary>
        /// Opens or closes the map. Public so a test drives it directly rather than
        /// simulating a keypress, the same seam <see cref="JournalUI.Toggle"/> has.
        /// </summary>
        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                Open();
            }
        }

        private void Open()
        {
            IsOpen = true;
            GameManager.Instance.Pause();

            if (mapRoot != null)
            {
                mapRoot.SetActive(true);
            }

            Redraw();

            if (EventSystem.current != null && closeButton != null)
            {
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            }
        }

        private void Close()
        {
            IsOpen = false;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            {
                GameManager.Instance.Resume();
            }

            if (mapRoot != null)
            {
                mapRoot.SetActive(false);
            }

            ClearMarkers();
        }

        // --------------------------------------------------------------------- drawing

        /// <summary>Rebuilds every marker. Public so a test can draw without opening a panel.</summary>
        public void Redraw()
        {
            ClearMarkers();

            if (mapContent == null)
            {
                return;
            }

            SetText(titleLabel, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            SetText(legendLabel,
                "P player   C checkpoint   N person   O objective   M memory   B boss");

            var entries = CollectEntries(out var player);

            if (entries.Count == 0)
            {
                AddLabel(new Vector2(0f, 0f), "Nothing on this map yet.", Color.grey);
                return;
            }

            var extent = ExtentFor(entries);

            foreach (var entry in entries)
            {
                AddMarker(Project(entry.WorldPosition, extent), entry);
            }

            if (player != null)
            {
                AddFacingTick(Project(player.position, extent), player.forward, extent);
            }
        }

        private readonly struct MapEntry
        {
            public readonly Vector3 WorldPosition;
            public readonly string Letter;
            public readonly string Label;
            public readonly Color Colour;
            public readonly bool IsPlayer;

            public MapEntry(Vector3 worldPosition, string letter, string label, Color colour, bool isPlayer = false)
            {
                WorldPosition = worldPosition;
                Letter = letter;
                Label = label;
                Colour = colour;
                IsPlayer = isPlayer;
            }
        }

        private List<MapEntry> CollectEntries(out Transform playerTransform)
        {
            var entries = new List<MapEntry>();
            playerTransform = null;

            // PlayerDeath marks the player, the same lookup HudUI and SaveManager use.
            var player = FindAnyObjectByType<PlayerDeath>();
            if (player != null)
            {
                playerTransform = player.transform;
                entries.Add(new MapEntry(player.transform.position, "P", "You", new Color(0.4f, 0.9f, 1f), isPlayer: true));
            }

            foreach (var checkpoint in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
            {
                var reached = checkpoint.HasBeenActivated;
                entries.Add(new MapEntry(
                    checkpoint.RespawnPosition,
                    "C",
                    reached ? $"{checkpoint.CheckpointId} (reached)" : checkpoint.CheckpointId,
                    reached ? new Color(0.5f, 0.95f, 0.6f) : new Color(0.55f, 0.55f, 0.55f)));
            }

            foreach (var npc in FindObjectsByType<NpcInteractable>(FindObjectsSortMode.None))
            {
                entries.Add(new MapEntry(npc.transform.position, "N", npc.NpcName, new Color(0.95f, 0.85f, 0.5f)));
            }

            foreach (var target in FindObjectsByType<QuestTarget>(FindObjectsSortMode.None))
            {
                entries.Add(new MapEntry(target.transform.position, "O", target.name, new Color(1f, 0.6f, 0.25f)));
            }

            // Only memories the player has actually found. Drawing the undiscovered
            // ones would turn the map into a collectible checklist and delete the
            // exploration SPEC.md section 27 asks the world to reward.
            var memories = MemoryManager.Instance;
            foreach (var pickup in FindObjectsByType<MemoryPickup>(FindObjectsSortMode.None))
            {
                var fragment = pickup.Memory;
                if (fragment == null || memories == null || !memories.IsDiscovered(fragment.MemoryId))
                {
                    continue;
                }

                entries.Add(new MapEntry(pickup.transform.position, "M", fragment.Title, new Color(0.75f, 0.6f, 0.95f)));
            }

            foreach (var boss in FindObjectsByType<BossController>(FindObjectsSortMode.None))
            {
                entries.Add(new MapEntry(
                    boss.transform.position,
                    "B",
                    boss.Defeated ? $"{boss.DisplayName} (defeated)" : boss.DisplayName,
                    boss.Defeated ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.95f, 0.35f, 0.35f)));
            }

            return entries;
        }

        /// <summary>The world rectangle the map covers, as centre plus half-size on X and Z.</summary>
        private (Vector3 Centre, Vector2 HalfSize) ExtentFor(List<MapEntry> entries)
        {
            var bounds = WorldBounds.Instance;
            if (bounds != null)
            {
                var half = new Vector2(bounds.BoundsSize.x * 0.5f, bounds.BoundsSize.z * 0.5f);
                if (half.x > 0.01f && half.y > 0.01f)
                {
                    return (bounds.BoundsCentre, half);
                }
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var entry in entries)
            {
                min.x = Mathf.Min(min.x, entry.WorldPosition.x);
                min.y = Mathf.Min(min.y, entry.WorldPosition.z);
                max.x = Mathf.Max(max.x, entry.WorldPosition.x);
                max.y = Mathf.Max(max.y, entry.WorldPosition.z);
            }

            var centre = new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);

            // A floor on the half-size: a scene with one marker, or several stacked in
            // a line, would otherwise divide by nearly zero and throw every marker to
            // an edge.
            var halfSize = new Vector2(
                Mathf.Max((max.x - min.x) * 0.5f, 5f),
                Mathf.Max((max.y - min.y) * 0.5f, 5f));

            return (centre, halfSize);
        }

        private Vector2 Project(Vector3 worldPosition, (Vector3 Centre, Vector2 HalfSize) extent)
        {
            var rect = mapContent.rect;
            var drawable = new Vector2(
                Mathf.Max(rect.width * 0.5f - edgePadding, 1f),
                Mathf.Max(rect.height * 0.5f - edgePadding, 1f));

            // One scale for both axes, so the map is not stretched: a square arena
            // drawn into a wide panel must stay square or distances lie.
            var scale = Mathf.Min(drawable.x / extent.HalfSize.x, drawable.y / extent.HalfSize.y);

            return new Vector2(
                (worldPosition.x - extent.Centre.x) * scale,
                (worldPosition.z - extent.Centre.z) * scale);
        }

        private void AddMarker(Vector2 anchoredPosition, MapEntry entry)
        {
            var size = entry.IsPlayer ? playerMarkerSize : markerSize;

            var go = new GameObject($"Marker_{entry.Letter}", typeof(RectTransform));
            go.transform.SetParent(mapContent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(size, size);

            var image = go.AddComponent<Image>();
            image.color = entry.Colour;
            image.raycastTarget = false;

            // The letter sits on the marker, and the name beside it. Colour alone is
            // never the carrier (SPEC.md section 43).
            var letterGo = new GameObject("Letter", typeof(RectTransform));
            letterGo.transform.SetParent(rect, false);
            var letterRect = (RectTransform)letterGo.transform;
            letterRect.anchorMin = Vector2.zero;
            letterRect.anchorMax = Vector2.one;
            letterRect.offsetMin = Vector2.zero;
            letterRect.offsetMax = Vector2.zero;

            var letter = letterGo.AddComponent<Text>();
            letter.font = BuiltinFont;
            letter.fontSize = Mathf.RoundToInt(size * 0.8f);
            letter.fontStyle = FontStyle.Bold;
            letter.alignment = TextAnchor.MiddleCenter;
            letter.color = Color.black;
            letter.text = entry.Letter;
            letter.raycastTarget = false;

            markers.Add(go);

            AddLabel(anchoredPosition + new Vector2(size * 0.75f, 0f), entry.Label, entry.Colour);
        }

        private void AddLabel(Vector2 anchoredPosition, string text, Color colour)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(mapContent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(180f, 18f);

            var label = go.AddComponent<Text>();
            label.font = BuiltinFont;
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = colour;
            label.text = text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            markers.Add(go);
        }

        /// <summary>A short line out of the player marker showing which way they are facing.</summary>
        private void AddFacingTick(Vector2 anchoredPosition, Vector3 forward, (Vector3 Centre, Vector2 HalfSize) extent)
        {
            var flat = new Vector2(forward.x, forward.z);
            if (flat.sqrMagnitude < 0.0001f)
            {
                return;
            }

            flat.Normalize();

            var go = new GameObject("Facing", typeof(RectTransform));
            go.transform.SetParent(mapContent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(playerMarkerSize, 3f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(flat.y, flat.x) * Mathf.Rad2Deg);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.4f, 0.9f, 1f);
            image.raycastTarget = false;

            markers.Add(go);
        }

        private void ClearMarkers()
        {
            for (var i = 0; i < markers.Count; i++)
            {
                if (markers[i] != null)
                {
                    Destroy(markers[i]);
                }
            }

            markers.Clear();
        }

        private static void SetText(Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        /// <summary>Test and tooling seam.</summary>
        public void Configure(InputActionAsset actions, GameObject root, RectTransform content)
        {
            inputActions = actions;
            mapRoot = root;
            mapContent = content;
        }
    }
}
