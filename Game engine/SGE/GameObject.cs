using Raylib_cs;
using Rectangle = Raylib_cs.Rectangle;
using Color = Raylib_cs.Color;
using System.Numerics;

namespace SGE
{
    public sealed class GameObject
    {
        public string Name { get; set; } = "NewEntity";
        public Vector3 Position { get; set; } = Vector3.Zero;
        public Vector3 Rotation { get; set; } = Vector3.Zero;
        public Vector3 Scale { get; set; } = Vector3.One;
        public ItemType VisualizationType { get; set; } = ItemType.Texture;
        public Texture2D? Texture { get; set; }
        public string TextureAssetName { get; set; } = string.Empty;
        public Model? Model { get; set; }
        public string ModelAssetName { get; set; } = string.Empty;
        public IScript? Script { get; set; }
        public string ScriptAssetName { get; set; } = string.Empty;

        public void Draw2D()
        {
            var rect = new Rectangle(Position.X, Position.Y, Scale.X, Scale.Y);
            Raylib.DrawRectangleRec(rect, Color.Green);
            Raylib.DrawText(Name, (int)Position.X + 4, (int)Position.Y + 4, 12, Color.White);
        }

        public void Draw3D()
        {
            if (Model.HasValue)
            {
                Raylib.DrawModel(Model.Value, Position, Scale.X, Color.White);
            }
            else
            {
                Raylib.DrawCube(Position, Scale.X, Scale.Y, Scale.Z, Color.SkyBlue);
            }
            Raylib.DrawCubeWires(Position, Scale.X, Scale.Y, Scale.Z, Color.Black);
        }
    }
}
