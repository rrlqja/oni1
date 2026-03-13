using System.Collections.Generic;
using UnityEngine;

namespace Core.Simulation.Definitions
{
    [CreateAssetMenu(
        fileName = "ElementDatabase",
        menuName = "Simulation/Element Database")]
    public sealed class ElementDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<ElementDefinitionSO> elements = new();

        public IReadOnlyList<ElementDefinitionSO> Elements => elements;

        public bool TryGetById(byte id, out ElementDefinitionSO definition)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                ElementDefinitionSO current = elements[i];
                if (current == null)
                    continue;

                if (current.Id == id)
                {
                    definition = current;
                    return true;
                }
            }

            definition = null;
            return false;
        }

#if UNITY_EDITOR
        public void SetDefinitionsForTests(IEnumerable<ElementDefinitionSO> definitions)
        {
            elements.Clear();

            if (definitions == null)
                return;

            foreach (ElementDefinitionSO definition in definitions)
            {
                if (definition == null)
                    continue;

                elements.Add(definition);
            }
        }

        private void OnValidate()
        {
            var seenIds = new HashSet<byte>();

            for (int i = 0; i < elements.Count; i++)
            {
                ElementDefinitionSO element = elements[i];

                if (element == null)
                    continue;

                if (!seenIds.Add(element.Id))
                {
                    Debug.LogError(
                        $"Duplicate Element Id detected in database '{name}'. " +
                        $"Element '{element.name}' uses duplicated Id {element.Id}.",
                        this);
                }
            }
        }
#endif
    }
}