using Input;
using Networking;
using Unity.Netcode;
using UnityEngine;

namespace Physics
{
    public class Movement : NetworkBehaviour, IPhysicsComponent
    {
        [SerializeField] private float moveSpeed;
        
        public MovementPayload Step(InputPayload inputPayload, MovementPayload movementPayload, float deltaTime)
        {
            Vector3 velocity = new Vector3(
                    inputPayload.Move.x * moveSpeed,
                    movementPayload.Velocity.y ,
                    inputPayload.Move.y * moveSpeed);
            
            movementPayload.Velocity = velocity;
            return movementPayload;
        }
    }
}