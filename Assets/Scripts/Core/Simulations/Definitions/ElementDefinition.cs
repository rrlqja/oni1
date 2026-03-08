using Core.Simulation.Data;

namespace Core.Simulation.Definitions
{
    public sealed class ElementDefinition
    {
        public byte Id { get; }
        public string Name { get; }
        public ElementBehaviorType BehaviorType { get; }
        public DisplacementPriority DisplacementPriority { get; }
        public int DefaultMass { get; }
        public int MaxMass { get; }
        public bool IsSolid { get; }

        public ElementDefinition(
            byte id,
            string name,
            ElementBehaviorType behaviorType,
            DisplacementPriority displacementPriority,
            int defaultMass,
            int maxMass,
            bool isSolid)
        {
            Id = id;
            Name = name;
            BehaviorType = behaviorType;
            DisplacementPriority = displacementPriority;
            DefaultMass = defaultMass;
            MaxMass = maxMass;
            IsSolid = isSolid;
        }
    }
}