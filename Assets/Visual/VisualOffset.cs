using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Visual
{
    public class VisualOffset : NetworkBehaviour
    {
        [SerializeField] private Transform visualOffsetObj;

        public class VisualOffsetComponent
        {
            public Vector3 Offset;
        }
        
        private readonly List<VisualOffsetComponent> _components = new();
        
        /// <summary>
        /// Calcule l'offset visuel total en additionnant tous les composants actifs
        /// </summary>
        private Vector3 GetFinalOffset()
        {
            Vector3 total = Vector3.zero;
            for (int i = 0; i < _components.Count; i++)
            {
                total += _components[i].Offset;
            }
            return total;
        }
        
        /// <summary>
        /// Ajoute un composant d'offset visuel à la liste
        /// </summary>
        public void AddComponent(VisualOffsetComponent component)
        {
            if (!_components.Contains(component)) _components.Add(component);
        }

        /// <summary>
        /// Retire un composant d'offset visuel de la liste
        /// </summary>
        public void RemoveComponent(VisualOffsetComponent component)
        {
            _components.Remove(component);
        }

        /// <summary>
        /// Applique l'offset visuel final à l'objet visuel avant le rendu
        /// </summary>
        private void LateUpdate()
        {
            visualOffsetObj.localPosition = GetFinalOffset();
        }
    }
}