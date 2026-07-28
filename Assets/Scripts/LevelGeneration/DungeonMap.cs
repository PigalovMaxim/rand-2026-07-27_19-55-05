namespace Roguelike.LevelGeneration
{
    /// <summary>
    /// Сетка подземелья: стены и пол.
    /// </summary>
    public sealed class DungeonMap
    {
        public int Width { get; }
        public int Height { get; }
        public bool[,] FloorTiles { get; }

        public DungeonMap(int width, int height)
        {
            Width = width;
            Height = height;
            FloorTiles = new bool[width, height];
        }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public void SetFloor(int x, int y)
        {
            if (IsInside(x, y))
                FloorTiles[x, y] = true;
        }

        public void CarveRoom(Room room)
        {
            for (var x = room.X; x < room.X + room.Width; x++)
            {
                for (var y = room.Y; y < room.Y + room.Height; y++)
                    SetFloor(x, y);
            }
        }

        public void CarveHorizontalTunnel(int fromX, int toX, int y, int thickness = 1)
        {
            var start = fromX < toX ? fromX : toX;
            var end = fromX < toX ? toX : fromX;
            var yStart = y - (thickness - 1) / 2;

            for (var x = start; x <= end; x++)
            {
                for (var offset = 0; offset < thickness; offset++)
                    SetFloor(x, yStart + offset);
            }
        }

        public void CarveVerticalTunnel(int x, int fromY, int toY, int thickness = 1)
        {
            var start = fromY < toY ? fromY : toY;
            var end = fromY < toY ? toY : fromY;
            var xStart = x - (thickness - 1) / 2;

            for (var y = start; y <= end; y++)
            {
                for (var offset = 0; offset < thickness; offset++)
                    SetFloor(xStart + offset, y);
            }
        }
    }
}
