using UnityEngine;

namespace Roguelike.LevelGeneration
{
    /// <summary>
    /// Прямоугольная комната на сетке подземелья.
    /// </summary>
    public readonly struct Room
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        public Room(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Vector2Int Center => new(X + Width / 2, Y + Height / 2);

        public bool Contains(int x, int y)
        {
            return x >= X
                && x < X + Width
                && y >= Y
                && y < Y + Height;
        }

        public int GetGapDistanceTo(Room other)
        {
            var deltaX = Mathf.Max(0, Mathf.Max(X - (other.X + other.Width), other.X - (X + Width)));
            var deltaY = Mathf.Max(0, Mathf.Max(Y - (other.Y + other.Height), other.Y - (Y + Height)));

            return deltaX + deltaY;
        }

        public Vector2Int GetConnectionPointToward(Room other)
        {
            var pointX = Mathf.Clamp(other.Center.x, X, X + Width - 1);
            var pointY = Mathf.Clamp(other.Center.y, Y, Y + Height - 1);

            if (Mathf.Abs(other.Center.x - Center.x) >= Mathf.Abs(other.Center.y - Center.y))
                pointX = other.Center.x < Center.x ? X : X + Width - 1;
            else
                pointY = other.Center.y < Center.y ? Y : Y + Height - 1;

            return new Vector2Int(pointX, pointY);
        }

        public bool Intersects(Room other, int padding)
        {
            return X < other.X + other.Width + padding
                && X + Width + padding > other.X
                && Y < other.Y + other.Height + padding
                && Y + Height + padding > other.Y;
        }
    }
}
