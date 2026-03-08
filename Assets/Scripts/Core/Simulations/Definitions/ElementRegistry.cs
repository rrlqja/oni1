using System;
using System.Collections.Generic;
using Core.Simulation.Data;

namespace Core.Simulation.Definitions
{
    public sealed class ElementRegistry
    {
        private readonly Dictionary<byte, ElementDefinition> _byId = new();

        public ElementRegistry()
        {
            Register(new ElementDefinition(
                id: 0,
                name: "Vacuum",
                behaviorType: ElementBehaviorType.Vacuum,
                displacementPriority: DisplacementPriority.Vacuum,
                defaultMass: 0,
                maxMass: 0,
                isSolid: false));

            Register(new ElementDefinition(
                id: 1,
                name: "Wall",
                behaviorType: ElementBehaviorType.StaticSolid,
                displacementPriority: DisplacementPriority.StaticSolid,
                defaultMass: 1000,
                maxMass: 1000,
                isSolid: true));

            Register(new ElementDefinition(
                id: 2,
                name: "Sand",
                behaviorType: ElementBehaviorType.FallingSolid,
                displacementPriority: DisplacementPriority.FallingSolid,
                defaultMass: 1000,
                maxMass: 1000,
                isSolid: true));
        }

        public void Register(ElementDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            _byId[definition.Id] = definition;
        }

        public ElementDefinition Get(byte id)
        {
            if (!_byId.TryGetValue(id, out var definition))
                throw new InvalidOperationException($"ElementDefinition not found. id={id}");

            return definition;
        }
    }
}