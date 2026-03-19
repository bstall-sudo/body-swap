namespace AppV2.Runtime.Scripts.Dialogue
{
    public interface IState
    {
        DialogueMode Mode { get; }

        void Enter();
        void Tick(float dt);
        void Exit();
    }
}