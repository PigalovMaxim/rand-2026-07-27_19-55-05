using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Roguelike.LevelGeneration
{
    /// <summary>
    /// Процедурная генерация уровня из комнат и коридоров.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed partial class LevelGenerator : MonoBehaviour
    {
        private const string FloorTilemapName = "Floor";
        private const string WallTilemapName = "Wall";
        private const string GridObjectName = "Grid";
        private const string FloorTileResourcePath = "Tiles/FloorTile";
        private const string WallTileResourcePath = "Tiles/WallTile";
        private const string StartFloorTileResourcePath = "Tiles/StartFloorTile";
        private const string BossFloorTileResourcePath = "Tiles/BossFloorTile";
        private const string ItemFloorTileResourcePath = "Tiles/ItemFloorTile";
        private const string ItemsRootName = "LevelItems";
        private const string WallCollidersRootName = "WallColliders";
        private const string WallCompositeObjectName = "WallComposite";

        [Header("Комнаты")]
        [SerializeField]
        [Min(2)]
        private int roomCount = 8;

        [SerializeField]
        [Min(0)]
        private int itemRoomCount = 1;

        [SerializeField]
        [Min(3)]
        private int minRoomSize = 6;

        [SerializeField]
        [Min(3)]
        private int maxRoomSize = 12;

        [Header("Карта")]
        [SerializeField]
        [Min(20)]
        private int mapWidth = 48;

        [SerializeField]
        [Min(20)]
        private int mapHeight = 32;

        [SerializeField]
        [Min(1)]
        private int roomPlacementAttempts = 200;

        [SerializeField]
        [Min(0)]
        private int roomPadding = 1;

        [SerializeField]
        [Min(1)]
        private int corridorGap = 1;

        [Header("Случайность")]
        [SerializeField]
        private int seed;

        [SerializeField]
        private bool useRandomSeed = true;

        [SerializeField]
        private bool generateOnStart = true;

        [SerializeField]
        private bool anchorGenerationToPlayer = true;

        [Header("Визуализация")]
        [SerializeField]
        private Tilemap floorTilemap;

        [SerializeField]
        private Tilemap wallTilemap;

        [SerializeField]
        private TileBase floorTile;

        [SerializeField]
        private TileBase wallTile;

        [SerializeField]
        private TileBase startFloorTile;

        [SerializeField]
        private TileBase bossFloorTile;

        [SerializeField]
        private TileBase itemFloorTile;

        [SerializeField]
        private GameObject itemPrefab;

        [SerializeField]
        private Transform itemsRoot;

        private Transform wallCollidersRoot;

        private TileBase runtimeFloorTile;
        private TileBase runtimeWallTile;
        private TileBase runtimeStartFloorTile;
        private TileBase runtimeBossFloorTile;
        private TileBase runtimeItemFloorTile;

        private readonly List<Room> generatedItemRooms = new();

        public int RoomCount => roomCount;
        public int ItemRoomCount => itemRoomCount;
        public IReadOnlyList<Room> ItemRooms => generatedItemRooms;
        public Room StartRoom { get; private set; }
        public Room BossRoom { get; private set; }
        public IReadOnlyList<Room> GeneratedRooms { get; private set; }

        private void Start()
        {
            if (generateOnStart)
            {
                Generate();
            }
        }

        [ContextMenu("Generate Level")]
        public void Generate()
        {
            if (!EnsureReferences())
            {
                return;
            }

            ApplySeed();
            ResetGridPosition();
            DisableLegacyArenaEarly();
            ClearTilemaps();
            ClearWallColliders();
            ClearSpawnedItems();
            ClearSpawnedEnemies();

            var map = new DungeonMap(mapWidth, mapHeight);
            var rooms = PlaceRooms(map);

            if (rooms.Count < 2)
            {
                Debug.LogWarning("LevelGenerator: удалось разместить меньше двух комнат. Увеличьте карту или уменьшите roomCount.");
                return;
            }

            var roomConnections = ConnectRooms(map, rooms);
            var startRoomIndex = 0;
            var bossRoomIndex = FindBossRoomIndex(rooms, roomConnections, startRoomIndex);
            var itemRoomIndexes = FindItemRoomIndexes(rooms, startRoomIndex, bossRoomIndex, itemRoomCount);
            StartRoom = rooms[startRoomIndex];
            BossRoom = rooms[bossRoomIndex];
            generatedItemRooms.Clear();

            foreach (var itemRoomIndex in itemRoomIndexes)
            {
                generatedItemRooms.Add(rooms[itemRoomIndex]);
            }

            RenderMap(map, rooms, startRoomIndex, bossRoomIndex, itemRoomIndexes);

            if (anchorGenerationToPlayer)
            {
                AlignGridToPlayer();
            }

            BuildPerimeterWallColliders(map);

            SpawnItems(generatedItemRooms);
            GeneratedRooms = new List<Room>(rooms);
            FinalizeGameplay(rooms, startRoomIndex);

            Debug.Log(
                $"LevelGenerator: сгенерировано комнат {rooms.Count}, карта {mapWidth}x{mapHeight}. " +
                $"Старт {StartRoom.Center}, босс {BossRoom.Center}, комнат с предметом {generatedItemRooms.Count}, " +
                $"врагов ~{CountSpawnedEnemies(rooms, startRoomIndex)}.");
        }

        private bool EnsureReferences()
        {
            EnsureGridInfrastructure();
            ResolveTilemaps();
            ResolveTiles();

            if (floorTilemap == null || wallTilemap == null)
            {
                Debug.LogError("LevelGenerator: не найдены Tilemap Floor/Wall. Меню Roguelike → Setup Level Generation Scene.");
                return false;
            }

            if (floorTile == null || wallTile == null || startFloorTile == null || bossFloorTile == null || itemFloorTile == null)
            {
                Debug.LogError("LevelGenerator: не найдены тайлы пола, стены, старта, босса или предмета.");
                return false;
            }

            return true;
        }

        private void ResolveTilemaps()
        {
            if (!IsValidTilemapReference(floorTilemap) || !IsValidTilemapReference(wallTilemap))
            {
                floorTilemap = null;
                wallTilemap = null;
            }

            if (floorTilemap != null && wallTilemap != null)
            {
                return;
            }

            var grid = GameObject.Find(GridObjectName);
            if (grid != null)
            {
                if (floorTilemap == null)
                {
                    var floor = grid.transform.Find(FloorTilemapName);
                    if (floor != null)
                    {
                        floorTilemap = floor.GetComponent<Tilemap>();
                    }
                }

                if (wallTilemap == null)
                {
                    var wall = grid.transform.Find(WallTilemapName);
                    if (wall != null)
                    {
                        wallTilemap = wall.GetComponent<Tilemap>();
                    }
                }
            }

            if (floorTilemap != null && wallTilemap != null)
            {
                return;
            }

            var tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            foreach (var tilemap in tilemaps)
            {
                if (floorTilemap == null && tilemap.name == FloorTilemapName)
                {
                    floorTilemap = tilemap;
                }

                if (wallTilemap == null && tilemap.name == WallTilemapName)
                {
                    wallTilemap = tilemap;
                }
            }
        }

        private void ResolveTiles()
        {
            if (floorTile == null)
            {
                floorTile = Resources.Load<TileBase>(FloorTileResourcePath);
            }

            if (wallTile == null)
            {
                wallTile = Resources.Load<TileBase>(WallTileResourcePath);
            }

            if (startFloorTile == null)
            {
                startFloorTile = Resources.Load<TileBase>(StartFloorTileResourcePath);
            }

            if (bossFloorTile == null)
            {
                bossFloorTile = Resources.Load<TileBase>(BossFloorTileResourcePath);
            }

            if (itemFloorTile == null)
            {
                itemFloorTile = Resources.Load<TileBase>(ItemFloorTileResourcePath);
            }

#if UNITY_EDITOR
            if (floorTile == null)
            {
                floorTile = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/FloorTile.asset");
            }

            if (wallTile == null)
            {
                wallTile = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/WallTile.asset");
            }

            if (startFloorTile == null)
            {
                startFloorTile = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/StartFloorTile.asset");
            }

            if (bossFloorTile == null)
            {
                bossFloorTile = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/BossFloorTile.asset");
            }

            if (itemFloorTile == null)
            {
                itemFloorTile = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/ItemFloorTile.asset");
            }
#endif

            if (floorTile == null)
            {
                floorTile = GetOrCreateRuntimeTile(ref runtimeFloorTile, new Color(0.55f, 0.47f, 0.35f));
            }

            if (wallTile == null)
            {
                wallTile = GetOrCreateRuntimeTile(ref runtimeWallTile, new Color(0.18f, 0.18f, 0.22f), withCollider: true);
            }

            if (wallTile is Tile wallTileAsset)
            {
                wallTileAsset.colliderType = Tile.ColliderType.Grid;
            }

            if (startFloorTile == null)
            {
                startFloorTile = GetOrCreateRuntimeTile(ref runtimeStartFloorTile, new Color(0.35f, 0.85f, 0.4f));
            }

            if (bossFloorTile == null)
            {
                bossFloorTile = GetOrCreateRuntimeTile(ref runtimeBossFloorTile, new Color(0.9f, 0.25f, 0.25f));
            }

            if (itemFloorTile == null)
            {
                itemFloorTile = GetOrCreateRuntimeTile(ref runtimeItemFloorTile, new Color(0.94f, 0.78f, 0.25f));
            }
        }

        private static TileBase GetOrCreateRuntimeTile(ref TileBase cachedTile, Color color, bool withCollider = false)
        {
            if (cachedTile != null)
            {
                return cachedTile;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f);

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = withCollider ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            cachedTile = tile;

            return cachedTile;
        }

        private void ApplySeed()
        {
            var appliedSeed = useRandomSeed
                ? Random.Range(int.MinValue, int.MaxValue)
                : seed;

            Random.InitState(appliedSeed);
            Debug.Log($"LevelGenerator: seed = {appliedSeed}");
        }

        private void ClearSpawnedItems()
        {
            if (itemsRoot == null)
            {
                return;
            }

            for (var i = itemsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(itemsRoot.GetChild(i).gameObject);
            }
        }

        private static List<int> FindItemRoomIndexes(
            IReadOnlyList<Room> rooms,
            int startRoomIndex,
            int bossRoomIndex,
            int requestedItemRoomCount)
        {
            var selectedRoomIndexes = new List<int>();

            if (requestedItemRoomCount <= 0)
            {
                return selectedRoomIndexes;
            }

            var eligibleRoomIndexes = new List<int>();

            for (var i = 0; i < rooms.Count; i++)
            {
                if (i == startRoomIndex || i == bossRoomIndex)
                {
                    continue;
                }

                eligibleRoomIndexes.Add(i);
            }

            for (var i = eligibleRoomIndexes.Count - 1; i > 0; i--)
            {
                var swapIndex = Random.Range(0, i + 1);
                (eligibleRoomIndexes[i], eligibleRoomIndexes[swapIndex]) =
                    (eligibleRoomIndexes[swapIndex], eligibleRoomIndexes[i]);
            }

            var itemRoomsToPlace = Mathf.Min(requestedItemRoomCount, eligibleRoomIndexes.Count);

            for (var i = 0; i < itemRoomsToPlace; i++)
            {
                selectedRoomIndexes.Add(eligibleRoomIndexes[i]);
            }

            return selectedRoomIndexes;
        }

        private void SpawnItems(IReadOnlyList<Room> itemRooms)
        {
            if (itemRooms.Count == 0)
            {
                return;
            }

            var parent = GetItemsRoot();

            foreach (var itemRoom in itemRooms)
            {
                var itemPosition = GetRoomWorldCenter(itemRoom);

                if (itemPrefab != null)
                {
                    Instantiate(itemPrefab, itemPosition, Quaternion.identity, parent);
                    continue;
                }

                CreateDefaultItem(itemPosition, parent);
            }
        }

        private Transform GetItemsRoot()
        {
            if (itemsRoot != null)
            {
                return itemsRoot;
            }

            var existingRoot = transform.Find(ItemsRootName);
            if (existingRoot != null)
            {
                itemsRoot = existingRoot;
                return itemsRoot;
            }

            var rootObject = new GameObject(ItemsRootName);
            rootObject.transform.SetParent(transform, false);
            itemsRoot = rootObject.transform;
            return itemsRoot;
        }

        private Vector3 GetRoomWorldCenter(Room room)
        {
            var cell = new Vector3Int(room.Center.x, room.Center.y, 0);
            return floorTilemap.GetCellCenterWorld(cell);
        }

        private GameObject CreateDefaultItem(Vector3 position, Transform parent)
        {
            var itemObject = new GameObject("LevelItem");
            itemObject.transform.SetParent(parent, false);
            itemObject.transform.position = position;
            itemObject.AddComponent<ItemPickup>();

            var spriteRenderer = itemObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateDefaultItemSprite();
            spriteRenderer.sortingOrder = 2;

            return itemObject;
        }

        private static Sprite CreateDefaultItemSprite()
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var fillColor = new Color(0.95f, 0.85f, 0.2f, 1f);

            for (var x = 0; x < 16; x++)
            {
                for (var y = 0; y < 16; y++)
                {
                    var isBorder = x == 0 || y == 0 || x == 15 || y == 15;
                    texture.SetPixel(x, y, isBorder ? Color.black : fillColor);
                }
            }

            texture.Apply();
            texture.filterMode = FilterMode.Point;

            return Sprite.Create(
                texture,
                new Rect(0, 0, 16, 16),
                new Vector2(0.5f, 0.5f),
                16f);
        }

        private void ClearTilemaps()
        {
            floorTilemap.ClearAllTiles();
            wallTilemap.ClearAllTiles();
        }

        private List<Room> PlaceRooms(DungeonMap map)
        {
            var rooms = new List<Room>(roomCount);
            var minSize = minRoomSize <= maxRoomSize ? minRoomSize : maxRoomSize;
            var maxSize = minRoomSize <= maxRoomSize ? maxRoomSize : minRoomSize;

            if (!TryPlaceStartRoomAtMapCenter(map, rooms, minSize, maxSize))
            {
                return rooms;
            }

            for (var i = 1; i < roomCount; i++)
            {
                if (TryPlaceClusteredRoom(map, rooms, minSize, maxSize))
                {
                    continue;
                }

                if (!TryPlaceRandomRoom(map, rooms, minSize, maxSize))
                {
                    Debug.LogWarning($"LevelGenerator: не удалось разместить комнату {i + 1} из {roomCount}.");
                }
            }

            return rooms;
        }

        private bool TryPlaceStartRoomAtMapCenter(DungeonMap map, List<Room> rooms, int minSize, int maxSize)
        {
            var width = Random.Range(minSize, maxSize + 1);
            var height = Random.Range(minSize, maxSize + 1);
            var anchorX = map.Width / 2;
            var anchorY = map.Height / 2;
            var x = anchorX - width / 2;
            var y = anchorY - height / 2;
            x = Mathf.Clamp(x, 1, map.Width - width - 1);
            y = Mathf.Clamp(y, 1, map.Height - height - 1);

            AddRoom(map, rooms, new Room(x, y, width, height));
            return true;
        }

        private bool TryPlaceRandomRoom(DungeonMap map, List<Room> rooms, int minSize, int maxSize)
        {
            for (var attempt = 0; attempt < roomPlacementAttempts; attempt++)
            {
                var width = Random.Range(minSize, maxSize + 1);
                var height = Random.Range(minSize, maxSize + 1);
                var x = Random.Range(1, map.Width - width - 1);
                var y = Random.Range(1, map.Height - height - 1);
                var candidate = new Room(x, y, width, height);

                if (HasOverlap(candidate, rooms))
                {
                    continue;
                }

                AddRoom(map, rooms, candidate);
                return true;
            }

            return false;
        }

        private bool TryPlaceClusteredRoom(DungeonMap map, List<Room> rooms, int minSize, int maxSize)
        {
            var sides = new[] { 0, 1, 2, 3 };

            for (var attempt = 0; attempt < roomPlacementAttempts; attempt++)
            {
                var anchor = rooms[Random.Range(0, rooms.Count)];
                var width = Random.Range(minSize, maxSize + 1);
                var height = Random.Range(minSize, maxSize + 1);
                var side = sides[Random.Range(0, sides.Length)];

                if (!TryBuildAdjacentRoom(anchor, width, height, side, out var candidate))
                {
                    continue;
                }

                if (!IsInsideMap(map, candidate) || HasOverlap(candidate, rooms))
                {
                    continue;
                }

                AddRoom(map, rooms, candidate);
                return true;
            }

            return false;
        }

        private bool TryBuildAdjacentRoom(Room anchor, int width, int height, int side, out Room candidate)
        {
            var gap = Mathf.Max(roomPadding, corridorGap);
            var offset = Random.Range(-height + 2, anchor.Height - 1);

            switch (side)
            {
                case 0:
                    candidate = new Room(anchor.X + anchor.Width + gap, anchor.Y + offset, width, height);
                    return true;
                case 1:
                    candidate = new Room(anchor.X - width - gap, anchor.Y + offset, width, height);
                    return true;
                case 2:
                    offset = Random.Range(-width + 2, anchor.Width - 1);
                    candidate = new Room(anchor.X + offset, anchor.Y + anchor.Height + gap, width, height);
                    return true;
                default:
                    offset = Random.Range(-width + 2, anchor.Width - 1);
                    candidate = new Room(anchor.X + offset, anchor.Y - height - gap, width, height);
                    return true;
            }
        }

        private static bool IsInsideMap(DungeonMap map, Room room)
        {
            return room.X >= 1
                && room.Y >= 1
                && room.X + room.Width < map.Width - 1
                && room.Y + room.Height < map.Height - 1;
        }

        private static void AddRoom(DungeonMap map, List<Room> rooms, Room room)
        {
            rooms.Add(room);
            map.CarveRoom(room);
        }

        private bool HasOverlap(Room candidate, List<Room> rooms)
        {
            foreach (var room in rooms)
            {
                if (candidate.Intersects(room, roomPadding))
                {
                    return true;
                }
            }

            return false;
        }

        private List<(int From, int To)> ConnectRooms(DungeonMap map, List<Room> rooms)
        {
            var roomConnections = new List<(int From, int To)>();
            var connectedRoomIndexes = new List<int> { 0 };
            var pendingRoomIndexes = new List<int>();

            for (var i = 1; i < rooms.Count; i++)
            {
                pendingRoomIndexes.Add(i);
            }

            while (pendingRoomIndexes.Count > 0)
            {
                var bestConnectedIndex = -1;
                var bestPendingIndex = -1;
                var bestDistance = int.MaxValue;

                foreach (var connectedIndex in connectedRoomIndexes)
                {
                    foreach (var pendingIndex in pendingRoomIndexes)
                    {
                        var distance = rooms[connectedIndex].GetGapDistanceTo(rooms[pendingIndex]);
                        if (distance >= bestDistance)
                        {
                            continue;
                        }

                        bestDistance = distance;
                        bestConnectedIndex = connectedIndex;
                        bestPendingIndex = pendingIndex;
                    }
                }

                if (bestConnectedIndex < 0 || bestPendingIndex < 0)
                {
                    break;
                }

                CarveCorridorBetweenRooms(
                    map,
                    rooms[bestConnectedIndex],
                    rooms[bestPendingIndex]);

                roomConnections.Add((bestConnectedIndex, bestPendingIndex));
                connectedRoomIndexes.Add(bestPendingIndex);
                pendingRoomIndexes.Remove(bestPendingIndex);
            }

            return roomConnections;
        }

        private static int FindBossRoomIndex(
            IReadOnlyList<Room> rooms,
            IReadOnlyList<(int From, int To)> roomConnections,
            int startRoomIndex)
        {
            var distances = CalculateRoomPathDistances(rooms, roomConnections, startRoomIndex);
            var bossRoomIndex = startRoomIndex;
            var maxDistance = -1f;

            for (var i = 0; i < rooms.Count; i++)
            {
                if (i == startRoomIndex)
                {
                    continue;
                }

                if (distances[i] <= maxDistance)
                {
                    continue;
                }

                maxDistance = distances[i];
                bossRoomIndex = i;
            }

            return bossRoomIndex;
        }

        private static float[] CalculateRoomPathDistances(
            IReadOnlyList<Room> rooms,
            IReadOnlyList<(int From, int To)> roomConnections,
            int startRoomIndex)
        {
            var adjacency = BuildRoomAdjacency(rooms, roomConnections);
            var distances = new float[rooms.Count];

            for (var i = 0; i < distances.Length; i++)
            {
                distances[i] = float.PositiveInfinity;
            }

            distances[startRoomIndex] = 0f;
            var visited = new bool[rooms.Count];

            for (var step = 0; step < rooms.Count; step++)
            {
                var currentIndex = GetClosestUnvisitedRoomIndex(distances, visited);
                if (currentIndex < 0 || float.IsPositiveInfinity(distances[currentIndex]))
                {
                    break;
                }

                visited[currentIndex] = true;

                foreach (var neighbour in adjacency[currentIndex])
                {
                    var pathLength = distances[currentIndex] + neighbour.Distance;
                    if (pathLength < distances[neighbour.Index])
                    {
                        distances[neighbour.Index] = pathLength;
                    }
                }
            }

            return distances;
        }

        private static List<(int Index, float Distance)>[] BuildRoomAdjacency(
            IReadOnlyList<Room> rooms,
            IReadOnlyList<(int From, int To)> roomConnections)
        {
            var adjacency = new List<(int Index, float Distance)>[rooms.Count];

            for (var i = 0; i < rooms.Count; i++)
            {
                adjacency[i] = new List<(int Index, float Distance)>();
            }

            foreach (var connection in roomConnections)
            {
                var pathDistance = Vector2Int.Distance(
                    rooms[connection.From].Center,
                    rooms[connection.To].Center);

                adjacency[connection.From].Add((connection.To, pathDistance));
                adjacency[connection.To].Add((connection.From, pathDistance));
            }

            return adjacency;
        }

        private static int GetClosestUnvisitedRoomIndex(float[] distances, bool[] visited)
        {
            var closestIndex = -1;
            var closestDistance = float.PositiveInfinity;

            for (var i = 0; i < distances.Length; i++)
            {
                if (visited[i] || distances[i] >= closestDistance)
                {
                    continue;
                }

                closestDistance = distances[i];
                closestIndex = i;
            }

            return closestIndex;
        }

        private void CarveCorridorBetweenRooms(DungeonMap map, Room fromRoom, Room toRoom)
        {
            var from = fromRoom.GetConnectionPointToward(toRoom);
            var to = toRoom.GetConnectionPointToward(fromRoom);
            CarveCorridor(map, from, to);
        }

        private void CarveCorridor(DungeonMap map, Vector2Int from, Vector2Int to)
        {
            if (Random.value < 0.5f)
            {
                map.CarveHorizontalTunnel(from.x, to.x, from.y);
                map.CarveVerticalTunnel(to.x, from.y, to.y);
            }
            else
            {
                map.CarveVerticalTunnel(from.x, from.y, to.y);
                map.CarveHorizontalTunnel(from.x, to.x, to.y);
            }
        }

        private void RenderMap(
            DungeonMap map,
            IReadOnlyList<Room> rooms,
            int startRoomIndex,
            int bossRoomIndex,
            IReadOnlyList<int> itemRoomIndexes)
        {
            var startRoom = rooms[startRoomIndex];
            var bossRoom = rooms[bossRoomIndex];
            var itemRoomIndexSet = new HashSet<int>(itemRoomIndexes);

            for (var x = 0; x < map.Width; x++)
            {
                for (var y = 0; y < map.Height; y++)
                {
                    var cell = new Vector3Int(x, y, 0);

                    if (map.FloorTiles[x, y])
                    {
                        floorTilemap.SetTile(cell, ResolveFloorTile(x, y, rooms, startRoom, bossRoom, itemRoomIndexSet));
                        floorTilemap.SetColor(cell, Color.white);
                    }
                    else
                    {
                        wallTilemap.SetTile(cell, wallTile);
                    }
                }
            }

            floorTilemap.RefreshAllTiles();
            wallTilemap.RefreshAllTiles();
            floorTilemap.CompressBounds();
            wallTilemap.CompressBounds();
        }

        private TileBase ResolveFloorTile(
            int x,
            int y,
            IReadOnlyList<Room> rooms,
            Room startRoom,
            Room bossRoom,
            HashSet<int> itemRoomIndexes)
        {
            if (startRoom.Contains(x, y))
            {
                return startFloorTile;
            }

            if (bossRoom.Contains(x, y))
            {
                return bossFloorTile;
            }

            foreach (var itemRoomIndex in itemRoomIndexes)
            {
                if (rooms[itemRoomIndex].Contains(x, y))
                {
                    return itemFloorTile;
                }
            }

            return floorTile;
        }

        private static bool IsValidTilemapReference(Tilemap tilemap)
        {
            return tilemap != null && tilemap.layoutGrid != null;
        }

        private void EnsureGridInfrastructure()
        {
            var gridObject = GameObject.Find(GridObjectName);
            if (gridObject == null)
            {
                return;
            }

            if (gridObject.GetComponent<Grid>() == null)
            {
                gridObject.AddComponent<Grid>();
            }

            EnsureTilemapChild(gridObject.transform, FloorTilemapName, sortingOrder: 0, isWalkableFloor: true);
            EnsureTilemapChild(gridObject.transform, WallTilemapName, sortingOrder: 1, isWalkableFloor: false);
        }

        private void EnsureTilemapChild(Transform gridTransform, string tilemapName, int sortingOrder, bool isWalkableFloor)
        {
            var tilemapTransform = gridTransform.Find(tilemapName);
            if (tilemapTransform == null)
            {
                return;
            }

            var tilemapObject = tilemapTransform.gameObject;

            if (tilemapObject.GetComponent<Tilemap>() == null)
            {
                tilemapObject.AddComponent<Tilemap>();
            }

            var renderer = tilemapObject.GetComponent<TilemapRenderer>();
            if (renderer == null)
            {
                renderer = tilemapObject.AddComponent<TilemapRenderer>();
            }

            renderer.sortingOrder = sortingOrder;

            if (isWalkableFloor)
            {
                var walkableLayer = LayerMask.NameToLayer("Walkable");
                if (walkableLayer >= 0)
                {
                    tilemapObject.layer = walkableLayer;
                }
            }
            else
            {
                tilemapObject.tag = "Wall";
                DisableTilemapCollider(tilemapObject);
            }
        }

        private static void DisableTilemapCollider(GameObject wallObject)
        {
            var tilemapCollider = wallObject.GetComponent<TilemapCollider2D>();
            if (tilemapCollider != null)
            {
                tilemapCollider.enabled = false;
            }
        }

        private void ClearWallColliders()
        {
            if (wallCollidersRoot != null)
            {
                Destroy(wallCollidersRoot.gameObject);
                wallCollidersRoot = null;
                return;
            }

            var gridTransform = GetGridTransform();
            if (gridTransform == null)
            {
                return;
            }

            var existingRoot = gridTransform.Find(WallCollidersRootName);
            if (existingRoot != null)
            {
                Destroy(existingRoot.gameObject);
            }
        }

        private void BuildPerimeterWallColliders(DungeonMap map)
        {
            ClearWallColliders();

            if (wallTilemap == null)
            {
                return;
            }

            DisableTilemapCollider(wallTilemap.gameObject);

            var gridTransform = GetGridTransform();
            if (gridTransform == null)
            {
                return;
            }

            var rootObject = new GameObject(WallCollidersRootName);
            wallCollidersRoot = rootObject.transform;
            wallCollidersRoot.SetParent(gridTransform, false);

            var compositeObject = new GameObject(WallCompositeObjectName);
            compositeObject.transform.SetParent(wallCollidersRoot, false);
            compositeObject.layer = wallTilemap.gameObject.layer;
            compositeObject.tag = "Wall";

            var rigidbody = compositeObject.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Static;

            var compositeCollider = compositeObject.AddComponent<CompositeCollider2D>();
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;

            var cellSize = wallTilemap.layoutGrid.cellSize;
            var colliderSize = new Vector2(cellSize.x, cellSize.y);
            var wallCount = 0;

            for (var x = 0; x < map.Width; x++)
            {
                for (var y = 0; y < map.Height; y++)
                {
                    if (!IsBlockingWall(map, x, y))
                    {
                        continue;
                    }

                    var cell = new Vector3Int(x, y, 0);
                    var worldCenter = wallTilemap.GetCellCenterWorld(cell);

                    var colliderObject = new GameObject($"Wall_{x}_{y}");
                    colliderObject.transform.SetParent(compositeObject.transform, false);
                    colliderObject.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
                    colliderObject.layer = compositeObject.layer;
                    colliderObject.tag = "Wall";

                    var boxCollider = colliderObject.AddComponent<BoxCollider2D>();
                    boxCollider.size = colliderSize;
                    boxCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
                    wallCount++;
                }
            }

            Physics2D.SyncTransforms();
            Debug.Log($"LevelGenerator: создано коллайдеров стен {wallCount}.");
        }

        private static bool IsBlockingWall(DungeonMap map, int x, int y)
        {
            if (!map.IsInside(x, y) || map.FloorTiles[x, y])
            {
                return false;
            }

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    var neighborX = x + dx;
                    var neighborY = y + dy;

                    if (map.IsInside(neighborX, neighborY) && map.FloorTiles[neighborX, neighborY])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Transform GetGridTransform()
        {
            if (floorTilemap == null)
            {
                return null;
            }

            var parent = floorTilemap.transform.parent;
            if (parent != null && parent.name == GridObjectName)
            {
                return parent;
            }

            return floorTilemap.transform;
        }

        private void ResetGridPosition()
        {
            var gridTransform = GetGridTransform();
            if (gridTransform == null)
            {
                return;
            }

            var position = gridTransform.position;
            gridTransform.position = new Vector3(0f, 0f, position.z);
        }

        private void AlignGridToPlayer()
        {
            ResolvePlayerTransform();

            if (playerTransform == null)
            {
                return;
            }

            var gridTransform = GetGridTransform();
            if (gridTransform == null)
            {
                return;
            }

            var startRoomWorldCenter = GetRoomWorldCenter(StartRoom);
            var offset = playerTransform.position - startRoomWorldCenter;
            offset.z = 0f;
            gridTransform.position += offset;
        }
    }
}
