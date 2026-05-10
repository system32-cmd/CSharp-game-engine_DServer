using Raylib_cs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace SGE
{
    public sealed class EngineCore
    {
        private Thread? _renderThread;
        private volatile bool _running;
        private Camera3D _camera;
        public Scene Scene { get; } = new();
        public ScriptSystem Scripts { get; } = new();
        public List<AssetItem> Assets { get; } = new();
        public Action<string>? LogMessage { get; set; }

        public EngineCore()
        {
            _camera = new Camera3D
            {
                Position = new Vector3(16f, 14f, 16f),
                Target = new Vector3(0f, 1f, 0f),
                Up = new Vector3(0f, 1f, 0f),
                FovY = 45f,
                Projection = CameraProjection.Perspective
            };
            Scene.Entities.Add(new GameObject { Name = "Cube", Position = new Vector3(0f, 1f, 0f), Scale = new Vector3(2f, 2f, 2f) });
            Scene.Entities.Add(new GameObject { Name = "Player", Position = new Vector3(-4f, 0.5f, -2f), Scale = new Vector3(1f, 1f, 1f) });
        }

        public void StartPreview()
        {
            if (_running) return;
            _running = true;
            _renderThread = new Thread(RunLoop) { IsBackground = true };
            _renderThread.Start();
        }

        public void StopPreview()
        {
            _running = false;
            if (_renderThread?.IsAlive == true)
            {
                _renderThread.Join(500);
            }
        }

        private void RunLoop()
        {
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
            Raylib.InitWindow(1024, 720, "SGE Preview");
            Raylib.SetTargetFPS(60);
            while (_running && !Raylib.WindowShouldClose())
            {
                var dt = Raylib.GetFrameTime();
                Update(dt);
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Raylib_cs.Color.RayWhite);
                Draw();
                Raylib.EndDrawing();
            }
            Raylib.CloseWindow();
            _running = false;
        }

        public void Update(float dt)
        {
            foreach (var entity in Scene.Entities)
            {
                entity.Script?.Update(entity, dt);
            }
        }

        public void Draw()
        {
            Raylib.BeginMode3D(_camera);
            Raylib.DrawGrid(20, 1.0f);
            foreach (var entity in Scene.Entities)
            {
                entity.Draw3D();
            }
            Raylib.EndMode3D();
            Raylib.DrawText("SGE Editor Preview", 16, 16, 20, Raylib_cs.Color.DarkGray);
        }

        public void CreateAsset(AssetItem item)
        {
            Assets.Add(item);
        }

        public void ApplyScriptToSelected(GameObject entity, AssetItem asset)
        {
            var script = Scripts.LoadScript(asset, msg => LogMessage?.Invoke(msg));
            if (script != null)
            {
                entity.Script = script;
                entity.ScriptAssetName = asset.Name;
                entity.Script.Start(entity);
                LogMessage?.Invoke($"Script {asset.Name} attached to {entity.Name}.");
            }
        }

        public void ApplyTextureToSelected(GameObject entity, AssetItem asset)
        {
            if (asset.Type != ItemType.Texture)
                return;

            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), GetSafeTempFileName(asset.Name) + asset.Metadata);
                File.WriteAllBytes(tempPath, asset.Payload);
                var texture = Raylib.LoadTexture(tempPath);
                entity.Texture = texture;
                entity.TextureAssetName = asset.Name;
                entity.VisualizationType = ItemType.Texture;
                LogMessage?.Invoke($"Texture {asset.Name} applied to {entity.Name}.");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Failed to apply texture: {ex.Message}");
            }
        }

        public void ApplyModelToSelected(GameObject entity, AssetItem asset)
        {
            if (asset.Type != ItemType.Model)
                return;

            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), GetSafeTempFileName(asset.Name) + ".glb");
                File.WriteAllBytes(tempPath, asset.Payload);
                var model = Raylib.LoadModel(tempPath);
                entity.Model = model;
                entity.ModelAssetName = asset.Name;
                entity.VisualizationType = ItemType.Model;
                LogMessage?.Invoke($"Model {asset.Name} applied to {entity.Name}.");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Failed to apply model: {ex.Message}");
            }
        }

        private static string GetSafeTempFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(safe) ? "texture_asset" : safe;
        }

        public void SaveScreenshot(string path)
        {
            if (Raylib.IsWindowReady())
            {
                Raylib.TakeScreenshot(path);
                return;
            }
            BmpHelper.SavePlaceholderBmp(path, Scene.Name);
        }
    }
}