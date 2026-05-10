using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace SGE
{
    public static class SceneSerializer
    {
        public static void SavePackage(string folder, EngineCore engine)
        {
            if (string.IsNullOrWhiteSpace(folder))
                throw new ArgumentException("Output folder must be specified.", nameof(folder));

            Directory.CreateDirectory(folder);
            var package = new ScenePackageData
            {
                Name = engine.Scene.Name,
                Entities = engine.Scene.Entities.Select(ToState).ToList()
            };

            var json = JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(folder, "scene.json"), json, Encoding.UTF8);

            foreach (var asset in engine.Assets.Where(a => a.Type != ItemType.Scene))
            {
                var fileName = MakeSafeFileName(asset.Name) + ".item";
                ItemFile.Save(Path.Combine(folder, fileName), asset);
            }
        }

        public static ScenePackageData LoadPackage(string folder)
        {
            var packageFile = Path.Combine(folder, "scene.json");
            if (!File.Exists(packageFile))
                throw new FileNotFoundException("Scene package file not found.", packageFile);

            var json = File.ReadAllText(packageFile, Encoding.UTF8);
            var package = JsonSerializer.Deserialize<ScenePackageData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (package == null)
                throw new InvalidDataException("Failed to parse scene package JSON.");

            return package;
        }

        private static EntityState ToState(GameObject entity)
        {
            return new EntityState
            {
                Name = entity.Name,
                Position = new[] { entity.Position.X, entity.Position.Y, entity.Position.Z },
                Rotation = new[] { entity.Rotation.X, entity.Rotation.Y, entity.Rotation.Z },
                Scale = new[] { entity.Scale.X, entity.Scale.Y, entity.Scale.Z },
                ScriptAssetName = entity.ScriptAssetName,
                TextureAssetName = entity.TextureAssetName,
                ModelAssetName = entity.ModelAssetName,
                VisualizationType = entity.VisualizationType
            };
        }

        private static string MakeSafeFileName(string text)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.IsNullOrWhiteSpace(text)
                ? "asset"
                : new string(text.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        }
    }
}
