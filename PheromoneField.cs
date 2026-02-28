using System;
using System.Windows;
using Point = System.Windows.Point;

namespace OpenAnt
{
    public sealed class PheromoneField
    {
        private readonly float[] _strength;
        private readonly float[] _vx;
        private readonly float[] _vy;
        private readonly int _cols;
        private readonly int _rows;
        private readonly float _cellSize;

        public double WorldWidth { get; }
        public double WorldHeight { get; }

        public PheromoneField(double worldWidth, double worldHeight, double cellSize)
        {
            WorldWidth = Math.Max(1.0, worldWidth);
            WorldHeight = Math.Max(1.0, worldHeight);
            _cellSize = (float)Math.Max(4.0, cellSize);

            _cols = Math.Max(1, (int)Math.Ceiling(WorldWidth / _cellSize));
            _rows = Math.Max(1, (int)Math.Ceiling(WorldHeight / _cellSize));

            int count = _cols * _rows;
            _strength = new float[count];
            _vx = new float[count];
            _vy = new float[count];
        }

        public void Update(double dt)
        {
            float decay = (float)Math.Clamp(1.0 - AntConfig.PheromoneEvaporationPerSecond * dt, 0.0, 1.0);
            for (int i = 0; i < _strength.Length; i++)
            {
                _strength[i] *= decay;
                _vx[i] *= decay;
                _vy[i] *= decay;
            }
        }

        public void Deposit(Point position, Vector direction, double amount)
        {
            if (amount <= 0) return;

            int col = (int)(position.X / _cellSize);
            int row = (int)(position.Y / _cellSize);
            if ((uint)col >= (uint)_cols || (uint)row >= (uint)_rows) return;

            float dirX = (float)direction.X;
            float dirY = (float)direction.Y;
            float lenSq = dirX * dirX + dirY * dirY;
            if (lenSq < 1e-6f) return;

            float invLen = 1.0f / (float)Math.Sqrt(lenSq);
            dirX *= invLen;
            dirY *= invLen;

            int index = row * _cols + col;
            float a = (float)Math.Min(AntConfig.PheromoneMaxCellStrength, amount);

            float s = _strength[index] + a;
            if (s > AntConfig.PheromoneMaxCellStrength) s = (float)AntConfig.PheromoneMaxCellStrength;
            _strength[index] = s;

            _vx[index] += dirX * a;
            _vy[index] += dirY * a;

            float vLenSq = _vx[index] * _vx[index] + _vy[index] * _vy[index];
            if (vLenSq > AntConfig.PheromoneMaxCellStrength * AntConfig.PheromoneMaxCellStrength)
            {
                float vInv = (float)(AntConfig.PheromoneMaxCellStrength / Math.Sqrt(vLenSq));
                _vx[index] *= vInv;
                _vy[index] *= vInv;
            }
        }

        public (Vector direction, double strength) Sample(Point position)
        {
            int col = (int)(position.X / _cellSize);
            int row = (int)(position.Y / _cellSize);
            if ((uint)col >= (uint)_cols || (uint)row >= (uint)_rows) return (new Vector(0, 0), 0);

            int index = row * _cols + col;
            float s = _strength[index];
            if (s <= AntConfig.PheromoneMinDetectStrength) return (new Vector(0, 0), 0);

            float x = _vx[index];
            float y = _vy[index];
            float lenSq = x * x + y * y;
            if (lenSq < 1e-6f) return (new Vector(0, 0), s);

            float inv = 1.0f / (float)Math.Sqrt(lenSq);
            return (new Vector(x * inv, y * inv), s);
        }
    }
}
