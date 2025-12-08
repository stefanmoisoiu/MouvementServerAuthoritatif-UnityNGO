using Unity.Netcode;

namespace Input
{
    // Une structure pour envoyer plusieurs inputs d'un coup
    public struct InputRedundancyPayload : INetworkSerializable
    {
        public InputPayload[] Inputs;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Sérialisation d'un tableau pour Netcode
            int count = 0;
            if (!serializer.IsReader) count = Inputs.Length;
            
            serializer.SerializeValue(ref count);
            
            if (serializer.IsReader) Inputs = new InputPayload[count];
            
            for (int i = 0; i < count; i++)
            {
                Inputs[i].NetworkSerialize(serializer);
            }
        }
    }
}