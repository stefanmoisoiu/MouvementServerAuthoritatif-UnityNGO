using Unity.Netcode;
using UnityEngine;

namespace Input
{
    [System.Serializable]
    public struct InputPayload : INetworkSerializable
    {
        public int Tick;
        
        public Vector2 Move;
        public bool Jump;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            
            serializer.SerializeValue(ref Move);
            serializer.SerializeValue(ref Jump);
        }
        
        public bool IsDifferentEnough(InputPayload other) =>
            Vector2.Distance(Move, other.Move) > 0.01f ||
            Jump != other.Jump;
    }
}