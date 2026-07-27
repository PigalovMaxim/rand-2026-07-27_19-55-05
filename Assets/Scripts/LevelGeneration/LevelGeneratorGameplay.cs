using System.Collections.Generic;
using NavMeshPlus.Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike.LevelGeneration
{
    public sealed partial class LevelGenerator
    {
        private const string EnemiesRootName = "LevelEnemies";
        private const string LegacyRoomObjectName = "Room";
        private const string SceneEnemyObjectName = "Enemy";
        private const string PlayerTag = "Player";
        private const string WhiteSquareSpriteResourcePath = "Sprites/WhiteSquare";

        [Header("Геймплей")]
        [SerializeField]
        [Min(0)]
        private int enemiesPerRoom = 1;

        [SerializeField]
        private bool spawnEnemiesInStartRoom;

        [SerializeField]
        private GameObject enemyPrefab;

        [SerializeField]
        private Transform enemiesRoot;

        [SerializeField]
        private Transform playerTransform;

        [SerializeField]
        private MapNavigation mapNavigation;

        [SerializeField]
        private bool disableLegacyRoom = true;

        [SerializeField]
        private bool disableSceneEnemy = true;

        private Sprite runtimeEnemySprite;

        private void FinalizeGameplay(IReadOnlyList<Room> rooms, int startRoomIndex)
        {
            ConfigureNavigationGeometry();
            DisableLegacyArena();
            RebakeNavigation();

            if (!anchorGenerationToPlayer)
            {
                PlacePlayerAtStartRoom();
            }

            SpawnEnemiesInRooms(rooms, startRoomIndex);
        }

        private int CountSpawnedEnemies(IReadOnlyList<Room> rooms, int startRoomIndex)
        {
            if (enemiesPerRoom <= 0)
            {
                return 0;
            }

            var roomCountWithEnemies = 0;

            for (var roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                if (!spawnEnemiesInStartRoom && roomIndex == startRoomIndex)
                {
                    continue;
                }

                roomCountWithEnemies++;
            }

            return roomCountWithEnemies * enemiesPerRoom;
        }

        private void ClearSpawnedEnemies()
        {
            if (enemiesRoot == null)
            {
                return;
            }

            for (var i = enemiesRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(enemiesRoot.GetChild(i).gameObject);
            }
        }

        private void ConfigureNavigationGeometry()
        {
            var walkableLayer = LayerMask.NameToLayer("Walkable");

            if (floorTilemap != null && walkableLayer >= 0)
            {
                floorTilemap.gameObject.layer = walkableLayer;
            }

            if (wallTilemap != null)
            {
                wallTilemap.gameObject.tag = "Wall";
            }
        }

        private void DisableLegacyArena()
        {
            DisableLegacyArenaEarly();
        }

        private void DisableLegacyArenaEarly()
        {
            if (disableLegacyRoom)
            {
                var legacyRoom = GameObject.Find(LegacyRoomObjectName);
                if (legacyRoom != null)
                {
                    legacyRoom.SetActive(false);
                }
            }

            if (!disableSceneEnemy)
            {
                return;
            }

            var sceneEnemy = GameObject.Find(SceneEnemyObjectName);
            if (sceneEnemy != null)
            {
                sceneEnemy.SetActive(false);
            }
        }

        private void RebakeNavigation()
        {
            if (mapNavigation == null)
            {
                mapNavigation = FindFirstObjectByType<MapNavigation>();
            }

            mapNavigation?.Bake();
        }

        private void PlacePlayerAtStartRoom()
        {
            ResolvePlayerTransform();

            if (playerTransform == null)
            {
                return;
            }

            var spawnPosition = GetRoomWorldCenter(StartRoom);
            playerTransform.position = spawnPosition;

            var playerRigidbody = playerTransform.GetComponent<Rigidbody2D>();
            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector2.zero;
                playerRigidbody.angularVelocity = 0f;
            }
        }

        private void ResolvePlayerTransform()
        {
            if (playerTransform != null)
            {
                return;
            }

            var playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
            }
        }

        private void SpawnEnemiesInRooms(IReadOnlyList<Room> rooms, int startRoomIndex)
        {
            if (enemiesPerRoom <= 0)
            {
                return;
            }

            var parent = GetEnemiesRoot();

            for (var roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                if (!spawnEnemiesInStartRoom && roomIndex == startRoomIndex)
                {
                    continue;
                }

                for (var enemyIndex = 0; enemyIndex < enemiesPerRoom; enemyIndex++)
                {
                    SpawnEnemy(GetRandomRoomWorldPosition(rooms[roomIndex]), parent);
                }
            }
        }

        private Transform GetEnemiesRoot()
        {
            if (enemiesRoot != null)
            {
                return enemiesRoot;
            }

            var existingRoot = transform.Find(EnemiesRootName);
            if (existingRoot != null)
            {
                enemiesRoot = existingRoot;
                return enemiesRoot;
            }

            var rootObject = new GameObject(EnemiesRootName);
            rootObject.transform.SetParent(transform, false);
            enemiesRoot = rootObject.transform;
            return enemiesRoot;
        }

        private Vector3 GetRandomRoomWorldPosition(Room room)
        {
            var x = Random.Range(room.X, room.X + room.Width);
            var y = Random.Range(room.Y, room.Y + room.Height);
            return floorTilemap.GetCellCenterWorld(new Vector3Int(x, y, 0));
        }

        private void SpawnEnemy(Vector3 position, Transform parent)
        {
            if (enemyPrefab != null)
            {
                var enemyObject = Instantiate(enemyPrefab, position, Quaternion.identity, parent);
                var enemy = enemyObject.GetComponent<Enemy>();
                if (enemy != null && playerTransform != null)
                {
                    enemy.SetTarget(playerTransform);
                }

                return;
            }

            CreateDefaultEnemy(position, parent);
        }

        private void CreateDefaultEnemy(Vector3 position, Transform parent)
        {
            var enemyObject = new GameObject("Enemy");
            enemyObject.transform.SetParent(parent, false);
            enemyObject.transform.position = position;
            enemyObject.transform.localScale = new Vector3(0.8f, 1.1f, 1f);

            var spriteRenderer = enemyObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = ResolveEnemySprite();
            spriteRenderer.color = new Color(0.75f, 0.15f, 0.2f, 1f);
            spriteRenderer.sortingOrder = 10;

            var rigidbody = enemyObject.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
            rigidbody.gravityScale = 0f;
            rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;

            enemyObject.AddComponent<BoxCollider2D>();
            enemyObject.AddComponent<NavMeshAgent>();
            enemyObject.AddComponent<AgentOverride2d>();

            var enemy = enemyObject.AddComponent<Enemy>();
            if (playerTransform != null)
            {
                enemy.SetTarget(playerTransform);
            }
        }

        private Sprite ResolveEnemySprite()
        {
            if (runtimeEnemySprite != null)
            {
                return runtimeEnemySprite;
            }

            runtimeEnemySprite = Resources.Load<Sprite>(WhiteSquareSpriteResourcePath);

#if UNITY_EDITOR
            if (runtimeEnemySprite == null)
            {
                runtimeEnemySprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/WhiteSquare.png");
            }
#endif

            if (runtimeEnemySprite != null)
            {
                return runtimeEnemySprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            runtimeEnemySprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return runtimeEnemySprite;
        }
    }
}
