using System;
using Core.Simulation.Data;

namespace Core.Simulation.Runtime
{
    public sealed class WorldGrid : IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public int Length => Width * Height;

        private readonly SimCell[] _cells;
        private readonly TickMeta[] _tickMetas;

        public WorldGrid(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;

            _cells = new SimCell[Length];
            _tickMetas = new TickMeta[Length];

            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = SimCell.Vacuum;
                _tickMetas[i] = TickMeta.CreateDefault();
            }
        }

        public void Dispose()
        {
            // ������ managed array�� ���� ���� ����
            // ���߿� NativeArray�� �ٲٸ� ���⼭ Dispose ó��
        }

        public bool InBounds(int x, int y)
        {
            return (uint)x < (uint)Width && (uint)y < (uint)Height;
        }

        public int ToIndex(int x, int y)
        {
            return y * Width + x;
        }

        public void ToXY(int index, out int x, out int y)
        {
            y = index / Width;
            x = index - (y * Width);
        }

        public ref SimCell GetCellRef(int x, int y)
        {
            int index = ToIndex(x, y);
            return ref _cells[index];
        }

        public ref SimCell GetCellRef(int index)
        {
            return ref _cells[index];
        }

        public SimCell GetCellByIndex(int index)
        {
            return _cells[index];
        }

        public ref TickMeta GetTickMetaRef(int x, int y)
        {
            int index = ToIndex(x, y);
            return ref _tickMetas[index];
        }

        public ref TickMeta GetTickMetaRef(int index)
        {
            return ref _tickMetas[index];
        }

        public SimCell GetCell(int x, int y)
        {
            return _cells[ToIndex(x, y)];
        }

        public void SetCell(int x, int y, SimCell cell)
        {
            _cells[ToIndex(x, y)] = cell;
        }

        public void ClearAllTickReservations()
        {
            for (int i = 0; i < _tickMetas.Length; i++)
            {
                _tickMetas[i].ClearReservations();
            }
        }

        public void Fill(byte elementId, int mass = 0, float temperature = 0f)
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = new SimCell(elementId, mass, temperature);
            }
        }
    }
}