using System;
using Unity.Netcode;
using UnityEngine;

namespace Networking
{
    [Serializable]
    public struct MovementPayload : INetworkSerializable
    {
        public int Tick;
        public Vector3 Position;
        public Vector3 Velocity;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Velocity);
        }
    }
}