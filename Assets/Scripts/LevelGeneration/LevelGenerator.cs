using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Roguelike.LevelGeneration
{
    /// <summary>
    /// Процедурная генерация уровня из комнат и коридоров.
    /// </summary>
    public sealed class LevelGenerator : MonoBehaviour
    {
        private const string FloorTilemapName = "Floor";
        private const string WallTilemapName = "Wall";
        private const string GridObjectName = "Grid";
        private const string FloorTileResourcePath = "Tiles/FloorTile";
        private const string WallTileResourcePath = "Tiles/WallTile";

        [Header("Комнаты")]
        [SerializeField]
        [Min(2)]
        private int roomCount = 8;

        [SerializeField]
        [Min(3)]
        private int minRoomSize = 4;

        [SerializeField]
        [Min(3)]
        private int maxRoomSize = 10;

        [Header("Карта")]
        [SerializeField]
        [Min(20)]
        private int mapWidth = 80;

        [SerializeField]
        [Min(20)]
        private int mapHeight = 60;

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

        [Header("Визуализация")]
        [SerializeField]
        private Tilemap floorTilemap;

        [SerializeField]
        private Tilemap wallTilemap;

        [SerializeField]
        private TileBase floorTile;

        [SerializeField]
        private TileBase wallTile;

        private TileBase runtimeFloorTile;
        private TileBase runtimeWallTile;

        public int RoomCount => roomCount;

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
            ClearTilemaps();

            var map = new DungeonMap(mapWidth, mapHeight);
            var rooms = PlaceRooms(map);

            if (rooms.Count < 2)
            {
                Debug.LogWarning("LevelGenerator: удалось разместить меньше двух комнат. Увеличьте карту или уменьшите roomCount.");
                return;
            }

            ConnectRooms(map, rooms);
            RenderMap(map);

            Debug.Log($"LevelGenerator: сгенерировано комнат {rooms.Count}, карта {mapWidth}x{mapHeight}.");
        }

        private bool EnsureReferences()
        {
            ResolveTilemaps();
            ResolveTiles();

            if (floorTilemap == null || wallTilemap == null)
            {
                Debug.LogError("LevelGenerator: не найдены Tilemap Floor/Wall. Меню Roguelike → Setup Level Generation Scene.");
                return false;
            }

            if (floorTile == null || wallTile == null)
            {
                Debug.LogError("LevelGenerator: не найдены тайлы пола/стены.");
                return false;
            }

            return true;
        }

        private void ResolveTilemaps()
        {
            if (floorTilemap != null && wallTilemap != null)
            {
                return;
            }

            var grid = GameObject.Find(GridObjectName);
            if (grid == null)
            {
                return;
            }

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

#if UNITY_EDITOR
            if (floorTile == null)
            {
                floorTile = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/FloorTile.asset");
            }

            if (wallTile == null)
            {
                wallTile = UnityEditor.AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/WallTile.asset");
            }
#endif

            if (floorTile == null)
            {
                floorTile = GetOrCreateRuntimeTile(ref runtimeFloorTile, new Color(0.55f, 0.47f, 0.35f));
            }

            if (wallTile == null)
            {
                wallTile = GetOrCreateRuntimeTile(ref runtimeWallTile, new Color(0.18f, 0.18f, 0.22f));
            }
        }

        private static TileBase GetOrCreateRuntimeTile(ref TileBase cachedTile, Color color)
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

            if (!TryPlaceRandomRoom(map, rooms, minSize, maxSize))
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

        private void ConnectRooms(DungeonMap map, List<Room> rooms)
        {
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

                connectedRoomIndexes.Add(bestPendingIndex);
                pendingRoomIndexes.Remove(bestPendingIndex);
            }
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

        private void RenderMap(DungeonMap map)
        {
            for (var x = 0; x < map.Width; x++)
            {
                for (var y = 0; y < map.Height; y++)
                {
                    var cell = new Vector3Int(x, y, 0);

                    if (map.FloorTiles[x, y])
                    {
                        floorTilemap.SetTile(cell, floorTile);
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
    }
}
