 using System;
using System.Collections.Generic;
using System.Windows;
using Point = System.Windows.Point;

namespace OpenAnt
{
    public class SpatialGrid
    {
        private List<ProceduralAnt>[] _cells;
        private int _cols;
        private int _rows;
        private double _cellSize;
        
        // Reusable list to avoid allocation
        private List<ProceduralAnt> _queryResult = new List<ProceduralAnt>(32);

        public SpatialGrid(double width, double height, double cellSize)
        {
            if (cellSize <= 0) cellSize = 1;
            _cellSize = cellSize;
            
            _cols = Math.Max(1, (int)Math.Ceiling(width / cellSize));
            _rows = Math.Max(1, (int)Math.Ceiling(height / cellSize));
            
            _cells = new List<ProceduralAnt>[_cols * _rows];
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = new List<ProceduralAnt>(4);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i].Clear();
            }
        }

        public void Add(ProceduralAnt ant)
        {
            int col = (int)(ant.Position.X / _cellSize);
            int row = (int)(ant.Position.Y / _cellSize);
            
            // Clamp to grid
            if (col < 0) col = 0;
            if (col >= _cols) col = _cols - 1;
            if (row < 0) row = 0;
            if (row >= _rows) row = _rows - 1;
            
            int index = row * _cols + col;
            _cells[index].Add(ant);
        }

        public List<ProceduralAnt> Query(Point position, double radius)
        {
            _queryResult.Clear();
            
            int minCol = (int)((position.X - radius) / _cellSize);
            int maxCol = (int)((position.X + radius) / _cellSize);
            int minRow = (int)((position.Y - radius) / _cellSize);
            int maxRow = (int)((position.Y + radius) / _cellSize);

            // Clamp range
            if (minCol < 0) minCol = 0;
            if (maxCol >= _cols) maxCol = _cols - 1;
            if (minRow < 0) minRow = 0;
            if (maxRow >= _rows) maxRow = _rows - 1;

            for (int r = minRow; r <= maxRow; r++)
            {
                for (int c = minCol; c <= maxCol; c++)
                {
                    int index = r * _cols + c;
                    var cellAnts = _cells[index];
                    
                    // Add all ants in cell (filtering by actual distance happens later)
                    // Or we can just add them and let the caller filter
                    // To be safe, let's just add them all, caller usually does distance check anyway
                    _queryResult.AddRange(cellAnts);
                }
            }
            
            return _queryResult;
        }
    }
}
