using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.LevelGeneration
{
    public sealed partial class LevelGenerator
    {
        private const string EnemiesRootName = "LevelEnemies";
        private const string PlayerTag = "Player";

        [Header("Геймплей")]
        [SerializeField, Min(0)] private int enemiesPerRoom = 1;
        [SerializeField] private bool spawnEnemiesInStartRoom;
        [SerializeField] private Transform enemiesRoot;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private MapNavigation mapNavigation;

        private void FinalizeGameplay(IReadOnlyList<Room> rooms, int startRoomIndex)
        {
            ConfigureNavigationGeometry();
            RebakeNavigation();
            ResolvePlayerTransform();

            if (!anchorGenerationToPlayer)
                PlacePlayerAtStartRoom();

            SpawnEnemiesInRooms(rooms, startRoomIndex);
        }

        private int CountSpawnedEnemies(IReadOnlyList<Room> rooms, int startRoomIndex)
        {
            if (enemiesPerRoom <= 0)
                return 0;

            var roomCountWithEnemies = 0;

            for (var roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                if (!spawnEnemiesInStartRoom && roomIndex == startRoomIndex)
                    continue;

                roomCountWithEnemies++;
            }

            return roomCountWithEnemies * enemiesPerRoom;
        }

        private void ClearSpawnedEnemies()
        {
            if (enemiesRoot == null)
                return;

            for (var i = enemiesRoot.childCount - 1; i >= 0; i--)
                Destroy(enemiesRoot.GetChild(i).gameObject);
        }

        private void ConfigureNavigationGeometry()
        {
            var walkableLayer = LayerMask.NameToLayer("Walkable");

            if (floorTilemap != null && walkableLayer >= 0)
                floorTilemap.gameObject.layer = walkableLayer;

            if (wallTilemap != null)
                wallTilemap.gameObject.tag = "Wall";
        }

        private void RebakeNavigation()
        {
            if (mapNavigation == null)
                mapNavigation = FindAnyObjectByType<MapNavigation>();

            mapNavigation?.Bake();
        }

        private void PlacePlayerAtStartRoom()
        {
            ResolvePlayerTransform();

            if (playerTransform == null)
                return;

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
                return;

            var playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
            if (playerObject != null)
                playerTransform = playerObject.transform;
        }

        private void SpawnEnemiesInRooms(IReadOnlyList<Room> rooms, int startRoomIndex)
        {
            if (enemiesPerRoom <= 0)
                return;

            var parent = GetEnemiesRoot();

            for (var roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                if (!spawnEnemiesInStartRoom && roomIndex == startRoomIndex)
                    continue;

                for (var enemyIndex = 0; enemyIndex < enemiesPerRoom; enemyIndex++)
                    SpawnEnemy(GetRandomRoomWorldPosition(rooms[roomIndex]), parent);
            }
        }

        private Transform GetEnemiesRoot()
        {
            if (enemiesRoot != null)
                return enemiesRoot;

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
            var enemyObject = Instantiate(_enemyPrefab, position, Quaternion.identity, parent);
            var enemy = enemyObject.GetComponent<Enemy>();
            if (enemy != null && playerTransform != null)
                enemy.SetTarget(playerTransform);
        }
    }
}
