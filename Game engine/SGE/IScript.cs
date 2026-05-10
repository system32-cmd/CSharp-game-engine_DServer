namespace SGE
{
    public interface IScript
    {
        void Start(GameObject self);
        void Update(GameObject self, float deltaTime);
    }
}
