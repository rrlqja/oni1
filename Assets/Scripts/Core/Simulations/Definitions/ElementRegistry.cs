using System;
using UnityEngine;

namespace Core.Simulation.Definitions
{
    public sealed class ElementRegistry
    {
        private const int MaxElementCount = 256;

        private readonly ElementRuntimeDefinition[] _definitions = new ElementRuntimeDefinition[MaxElementCount];
        private readonly bool[] _isRegistered = new bool[MaxElementCount];

        public ElementRegistry(ElementDatabaseSO database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            Build(database);
        }

        private void Build(ElementDatabaseSO database)
        {
            var elements = database.Elements;

            for (int i = 0; i < elements.Count; i++)
            {
                ElementDefinitionSO source = elements[i];

                if (source == null)
                {
                    Debug.LogWarning($"ElementDatabase '{database.name}' contains a null element entry.", database);
                    continue;
                }

                byte id = source.Id;

                if (_isRegistered[id])
                {
                    throw new InvalidOperationException(
                        $"Duplicate element id detected while building registry. id={id}, element='{source.ElementName}'");
                }

                _definitions[id] = source.ToRuntimeDefinition();
                _isRegistered[id] = true;
            }

            if (!_isRegistered[0])
            {
                throw new InvalidOperationException(
                    "Element id 0 must be reserved for Vacuum and must exist in the ElementDatabase.");
            }
        }

        public bool IsRegistered(byte id)
        {
            return _isRegistered[id];
        }

        public ref readonly ElementRuntimeDefinition Get(byte id)
        {
            if (!_isRegistered[id])
                throw new InvalidOperationException($"ElementRuntimeDefinition not found. id={id}");

            return ref _definitions[id];
        }

        public byte RequireId(string elementName)
        {
            for (int i = 0; i < _definitions.Length; i++)
            {
                if (!_isRegistered[i])
                    continue;

                if (string.Equals(_definitions[i].Name, elementName, StringComparison.Ordinal))
                    return (byte)i;
            }

            throw new InvalidOperationException($"Element '{elementName}' was not found in registry.");
        }
    }
}