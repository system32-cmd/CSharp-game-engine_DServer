using System;
using System.IO;

namespace SGE
{
    public static class BuildManager
    {
        public static void BuildRuntime(EngineCore engine, string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("Output folder must be specified.", nameof(outputFolder));

            Directory.CreateDirectory(outputFolder);
            var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
            var runtimeExe = Path.Combine(outputFolder, "GameRuntime.exe");
            File.Copy(exePath, runtimeExe, true);

            SceneSerializer.SavePackage(outputFolder, engine);

            var screenshotPath = Path.Combine(outputFolder, "scene.bmp");
            engine.SaveScreenshot(screenshotPath);

            var manifest = Path.Combine(outputFolder, "runtime_manifest.txt");
            File.WriteAllText(manifest, $"Runtime folder generated: {DateTime.Now}\r\nGame runtime: {Path.GetFileName(runtimeExe)}\r\nScene: {engine.Scene.Name}\r\nScreenshot: {Path.GetFileName(screenshotPath)}\r\n");
        }
    }
}