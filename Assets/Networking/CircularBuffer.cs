using UnityEngine;

namespace Networking
{
    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private readonly int _bufferSize;

        public CircularBuffer(int size)
        {
            _bufferSize = size;
            _buffer = new T[size];
        }

        public void Add(T item, int index)
        {
            // Le modulo gère le retour au début du tableau automatiquement
            _buffer[index % _bufferSize] = item;
        }

        public T Get(int index)
        {
            return _buffer[index % _bufferSize];
        }

        public void Clear()
        {
            System.Array.Clear(_buffer, 0, _bufferSize);
        }
        
        /// <summary>
        /// Cherche dans tout le buffer un élément avec un tick spécifique
        /// </summary>
        /// <param name="targetTick">Le tick à rechercher</param>
        /// <param name="getTickFunc">Fonction pour extraire le tick de l'élément</param>
        /// <param name="item">L'élément trouvé</param>
        /// <returns>True si un élément avec le tick exact a été trouvé</returns>
        public bool FindByTick(int targetTick, System.Func<T, int> getTickFunc, out T item)
        {
            // Commence par vérifier l'emplacement attendu (plus rapide)
            int expectedIndex = targetTick % _bufferSize;
            T candidate = _buffer[expectedIndex];
            
            if (candidate != null && getTickFunc(candidate) == targetTick)
            {
                item = candidate;
                return true;
            }
            
            // Si pas trouvé à l'emplacement attendu, cherche dans tout le buffer
            for (int i = 0; i < _bufferSize; i++)
            {
                candidate = _buffer[i];
                if (candidate != null && getTickFunc(candidate) == targetTick)
                {
                    item = candidate;
                    return true;
                }
            }
            
            item = default(T);
            return false;
        }
        
        /// <summary>
        /// Trouve l'entrée la plus récente qui a un tick inférieur ou égal au tick cible
        /// </summary>
        public bool GetMostRecent(int targetTick, System.Func<T, int> getIndexFunc, out T res)
        {
            // Commence par le tick cible et remonte dans le temps
            T targetItem = Get(targetTick);
            if (getIndexFunc(targetItem) == targetTick)
            {
                res = Get(targetTick);
                return true;
            }
            
            for (int i = targetTick-1; i > Mathf.Max(0,targetTick - _bufferSize); i--)
            {
                T item = Get(i);
                if (item == null) continue;
                int itemTick = getIndexFunc(item);
                if (itemTick <= targetTick && itemTick > targetTick - _bufferSize / 2)
                {
                    res = item;
                    return true;
                }
            }
            res = default(T);
            return false;
        }

        public void Fill(T item)
        {
            for (int i = 0; i < _bufferSize; i++)
            {
                _buffer[i] = item;
            }
        }
    }
}