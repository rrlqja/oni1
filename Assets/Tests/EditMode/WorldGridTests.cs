using NUnit.Framework;
using Core.Simulation.Data;
using Core.Simulation.Runtime;

public class WorldGridTests
{
    [Test]
    public void WorldGrid_CanBeCreated_WithValidSize()
    {
        var grid = new WorldGrid(10, 10);

        Assert.AreEqual(10, grid.Width);
        Assert.AreEqual(10, grid.Height);
        Assert.AreEqual(100, grid.Length);
    }

    [Test]
    public void WorldGrid_DefaultCells_AreVacuum()
    {
        var grid = new WorldGrid(10, 10);

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                SimCell cell = grid.GetCell(x, y);

                Assert.AreEqual(0, cell.ElementId, $"Cell at ({x}, {y}) is not Vacuum.");
                Assert.AreEqual(0, cell.Mass, $"Cell at ({x}, {y}) mass is not zero.");
            }
        }
    }

    [Test]
    public void WorldGrid_CanReadAndWriteCell_ByXY()
    {
        var grid = new WorldGrid(10, 10);

        var sand = new SimCell(elementId: 2, mass: 1000);

        grid.SetCell(3, 4, sand);
        SimCell read = grid.GetCell(3, 4);

        Assert.AreEqual(2, read.ElementId);
        Assert.AreEqual(1000, read.Mass);
    }

    [Test]
    public void WorldGrid_CanModifyCell_ByRef()
    {
        var grid = new WorldGrid(10, 10);

        ref SimCell cell = ref grid.GetCellRef(2, 5);
        cell = new SimCell(elementId: 1, mass: 500);

        SimCell read = grid.GetCell(2, 5);

        Assert.AreEqual(1, read.ElementId);
        Assert.AreEqual(500, read.Mass);
    }

    [Test]
    public void TickMeta_IsIndependent_FromCellState()
    {
        var grid = new WorldGrid(10, 10);

        // 셀 상태 변경
        grid.SetCell(1, 1, new SimCell(elementId: 2, mass: 1000));

        // TickMeta 확인 및 변경
        ref TickMeta meta = ref grid.GetTickMetaRef(1, 1);
        meta.MarkActed(7);
        meta.AddReservation(TickReservationMask.SourceReserved);

        // 셀 상태는 그대로여야 함
        SimCell cell = grid.GetCell(1, 1);
        Assert.AreEqual(2, cell.ElementId);
        Assert.AreEqual(1000, cell.Mass);

        // TickMeta도 따로 유지되어야 함
        Assert.IsTrue(meta.HasActedThisTick(7));
        Assert.IsTrue(meta.HasReservation(TickReservationMask.SourceReserved));
    }

    [Test]
    public void ChangingCell_DoesNotModifyTickMeta()
    {
        var grid = new WorldGrid(10, 10);

        ref TickMeta meta = ref grid.GetTickMetaRef(4, 4);
        meta.MarkActed(3);
        meta.AddReservation(TickReservationMask.TargetReserved);

        grid.SetCell(4, 4, new SimCell(elementId: 2, mass: 777));

        ref TickMeta checkMeta = ref grid.GetTickMetaRef(4, 4);
        Assert.IsTrue(checkMeta.HasActedThisTick(3));
        Assert.IsTrue(checkMeta.HasReservation(TickReservationMask.TargetReserved));

        SimCell cell = grid.GetCell(4, 4);
        Assert.AreEqual(2, cell.ElementId);
        Assert.AreEqual(777, cell.Mass);
    }
}