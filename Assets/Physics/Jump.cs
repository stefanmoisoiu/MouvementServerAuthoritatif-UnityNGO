using Input;
using Networking;
using Unity.Netcode;
using UnityEngine;

namespace Physics
{
    public class Jump : NetworkBehaviour, IPhysicsComponent
    {
        [SerializeField] private float jumpForce;
        
        public MovementPayload Step(InputPayload inputPayload, MovementPayload movementPayload, float deltaTime)
        {
            if (!inputPayload.Jump)
            {
                // Apply gravity when not jumping
                movementPayload.Velocity += UnityEngine.Physics.gravity * deltaTime;
                return movementPayload;
            }
            else
            {
                movementPayload.Velocity = new (movementPayload.Velocity.x, jumpForce, movementPayload.Velocity.z);
                return movementPayload;
            }
        }
    }
}