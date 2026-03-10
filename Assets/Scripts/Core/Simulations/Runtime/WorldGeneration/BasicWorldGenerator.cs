using System;
using Core.Simulation.Data;
using Core.Simulation.Definitions;

namespace Core.Simulation.Runtime
{
    public static class BasicWorldGenerator
    {
        public static void Generate(WorldGrid grid, ElementRegistry registry)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            FillWithVacuum(grid, registry);
            CreateBorderBedrock(grid, registry);
        }

        private static void FillWithVacuum(WorldGrid grid, ElementRegistry registry)
        {
            ref readonly ElementRuntimeDefinition vacuum = ref registry.Get(BuiltInElementIds.Vacuum);
            grid.Fill(vacuum.Id, vacuum.DefaultMass);
            grid.ClearAllTickReservations();
        }

        private static void CreateBorderBedrock(WorldGrid grid, ElementRegistry registry)
        {
            ref readonly ElementRuntimeDefinition bedrock = ref registry.Get(BuiltInElementIds.Bedrock);
            SimCell bedrockCell = new SimCell(
                elementId: bedrock.Id,
                mass: bedrock.DefaultMass,
                temperature: 0);

            int maxX = grid.Width - 1;
            int maxY = grid.Height - 1;

            for (int x = 0; x < grid.Width; x++)
            {
                grid.SetCell(x, 0, bedrockCell);
                grid.SetCell(x, maxY, bedrockCell);
            }

            for (int y = 1; y < maxY; y++)
            {
                grid.SetCell(0, y, bedrockCell);
                grid.SetCell(maxX, y, bedrockCell);
            }
        }
    }
}