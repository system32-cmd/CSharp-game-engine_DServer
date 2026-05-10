using System.Collections.Generic;

namespace SGE
{
    public sealed class Scene
    {
        public string Name { get; set; } = "Untitled";
        public List<GameObject> Entities { get; } = new();
    }
}
