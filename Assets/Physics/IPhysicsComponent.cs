using Input;
using Networking;

namespace Physics
{
    public interface IPhysicsComponent
    {
        public MovementPayload Step(InputPayload inputPayload, MovementPayload movementPayload, float deltaTime);
    }
}