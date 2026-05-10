using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Python.Runtime;
using Raylib_cs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SGE
{
    public sealed class ScriptSystem
    {
        private bool _pythonInitialized;

        public ScriptSystem()
        {
            TryInitializePython();
        }

        public IScript? LoadScript(AssetItem item, Action<string> log)
        {
            try
            {
                return item.Type switch
            {
                    ItemType.ScriptCSharp => LoadCSharp(item, log),
                    ItemType.ScriptPython => LoadPython(item, log),
                    ItemType.ScriptCpp => LoadCpp(item, log),
                    _ => null,
                };
            }
            catch (Exception ex)
            {
                log($"Script load error: {ex.Message}");
                return null;
            }
        }

        private IScript? LoadCSharp(AssetItem item, Action<string> log)
        {
            var source = item.GetText();
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Raylib).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IScript).Assembly.Location),
            };

            var compilation = CSharpCompilation.Create(
                Path.GetRandomFileName(),
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var output = new MemoryStream();
            var result = compilation.Emit(output);
            if (!result.Success)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                    {
                        log(diagnostic.ToString());
                    }
                }
                return null;
            }

            output.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(output.ToArray());
            var type = assembly.GetTypes().FirstOrDefault(t => typeof(IScript).IsAssignableFrom(t) && !t.IsAbstract);
            if (type == null)
            {
                log("C# script must implement IScript.");
                return null;
            }
            return Activator.CreateInstance(type) as IScript;
        }

        private IScript? LoadPython(AssetItem item, Action<string> log)
        {
            if (!_pythonInitialized)
            {
                log("Python runtime not available.");
                return null;
            }

            using var gil = Py.GIL();
            dynamic pyModule = PyModule.FromString("script_module_" + Guid.NewGuid().ToString("N"), item.GetText());
            if (pyModule == null)
            {
                log("Could not compile Python script.");
                return null;
            }
            return new PythonScriptWrapper(pyModule);
        }

        private IScript? LoadCpp(AssetItem item, Action<string> log)
        {
            var path = Path.Combine(Path.GetTempPath(), item.Name + ".dll");
            File.WriteAllBytes(path, item.Payload);
            if (!NativeLibrary.TryLoad(path, out var handle))
            {
                log($"Could not load native module {item.Name}.");
                return null;
            }

            return new NativeCppScript(handle, log);
        }

        private void TryInitializePython()
        {
            try
            {
                PythonEngine.Initialize();
                _pythonInitialized = true;
            }
            catch
            {
                _pythonInitialized = false;
            }
        }
    }

    internal sealed class PythonScriptWrapper : IScript
    {
        private readonly dynamic _module;

        public PythonScriptWrapper(dynamic module)
        {
            _module = module;
        }

        public void Start(GameObject self)
        {
            try { _module.start(self); } catch { }
        }

        public void Update(GameObject self, float deltaTime)
        {
            try { _module.update(self, deltaTime); } catch { }
        }
    }

    internal sealed class NativeCppScript : IScript
    {
        private readonly IntPtr _handle;
        private delegate void NativeAction(IntPtr self, float deltaTime);
        private readonly NativeAction? _start;
        private readonly NativeAction? _update;

        public NativeCppScript(IntPtr handle, Action<string> log)
        {
            _handle = handle;
            _start = LoadDelegate<NativeAction>("Start", log);
            _update = LoadDelegate<NativeAction>("Update", log);
        }

        public void Start(GameObject self)
        {
            _start?.Invoke(IntPtr.Zero, 0f);
        }

        public void Update(GameObject self, float deltaTime)
        {
            _update?.Invoke(IntPtr.Zero, deltaTime);
        }

        private T? LoadDelegate<T>(string name, Action<string> log) where T : Delegate
        {
            if (!NativeLibrary.TryGetExport(_handle, name, out var symbol))
            {
                log($"Missing C++ export: {name}");
                return null;
            }
            return Marshal.GetDelegateForFunctionPointer<T>(symbol);
        }
    }
}
