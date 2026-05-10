using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace SGE
{
    public partial class EditorForm : Form
    {
        private readonly EngineCore _engine = new EngineCore();
        private readonly TreeView _assetTree = new TreeView { Dock = DockStyle.Left, Width = 220, BorderStyle = BorderStyle.FixedSingle };
        private readonly TextBox _logBox = new TextBox { Dock = DockStyle.Bottom, Height = 150, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.LightGreen, Font = new Font("Consolas", 9) };
        private readonly Panel _viewport = new Panel { Dock = DockStyle.Fill, BackColor = Color.DarkGray };
        private readonly MenuStrip _menu = new MenuStrip();
        private readonly ContextMenuStrip _assetContext = new ContextMenuStrip();
        private readonly ContextMenuStrip _entityContext = new ContextMenuStrip();
        private readonly ListView _entityList = new ListView { Dock = DockStyle.Right, Width = 240, View = View.Details, BorderStyle = BorderStyle.FixedSingle, FullRowSelect = true, GridLines = true, HeaderStyle = ColumnHeaderStyle.Nonclickable, Columns = { new ColumnHeader { Text = "Entity", Width = 180 } } };

        public EditorForm()
        {
            Text = "SGE Editor";
            Size = new Size(1300, 850);
            Controls.Add(_viewport);
            Controls.Add(_entityList);
            Controls.Add(_assetTree);
            Controls.Add(_logBox);
            Controls.Add(_menu);
            MainMenuStrip = _menu;
            SetupMenu();
            SetupAssetTree();
            SetupEntityList();
            SetupContexts();
            _engine.LogMessage = msg => Invoke(() => AppendLog(msg));
            AppendLog("SGE Editor initialized.");
        }

        private void SetupMenu()
        {
            var fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("New Scene", null, (s, e) => NewScene());
            fileMenu.DropDownItems.Add("Save Scene Package...", null, (s, e) => SaveScene());
            fileMenu.DropDownItems.Add("Load Scene Package...", null, (s, e) => LoadScene());
            fileMenu.DropDownItems.Add("Load Template Scene", null, (s, e) => LoadTemplateScene());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Build Runtime", null, (s, e) => BuildRuntime());
            _menu.Items.Add(fileMenu);

            var viewMenu = new ToolStripMenuItem("View");
            viewMenu.DropDownItems.Add("Start Preview", null, (s, e) => _engine.StartPreview());
            viewMenu.DropDownItems.Add("Stop Preview", null, (s, e) => _engine.StopPreview());
            _menu.Items.Add(viewMenu);

            var assetsMenu = new ToolStripMenuItem("Assets");
            assetsMenu.DropDownItems.Add("Import Texture...", null, (s, e) => ImportTexture());
            assetsMenu.DropDownItems.Add("Import Model...", null, (s, e) => ImportModel());
            assetsMenu.DropDownItems.Add("Import Script...", null, (s, e) => ImportScript());
            assetsMenu.DropDownItems.Add("New Script...", null, (s, e) => CreateScriptDialog());
            _menu.Items.Add(assetsMenu);
        }

        private void SetupAssetTree()
        {
            _assetTree.Nodes.Add("Assets", "Assets");
            _assetTree.ContextMenuStrip = _assetContext;
            _assetTree.AfterSelect += (s, e) => { if (e.Node?.Tag is AssetItem asset) ShowAssetDetails(asset); };
            RefreshAssetTree();
        }

        private void SetupEntityList()
        {
            _entityList.ContextMenuStrip = _entityContext;
            _entityList.SelectedIndexChanged += (s, e) => { if (_entityList.SelectedItems.Count > 0 && _entityList.SelectedItems[0].Tag is GameObject go) ShowEntityDetails(go); };
            RefreshEntityList();
        }

        private void SetupContexts()
        {
            _assetContext.Items.Add("Apply Asset to Selected Entity", null, (s, e) => ApplyAssetToSelectedEntity());
            _assetContext.Items.Add("Save Asset As...", null, (s, e) => SaveAssetAs());
            _assetContext.Items.Add("Remove Asset", null, (s, e) => RemoveAsset());

            _entityContext.Items.Add("Add Entity", null, (s, e) => AddEntity());
            _entityContext.Items.Add("Remove Entity", null, (s, e) => RemoveSelectedEntity());
            _entityContext.Items.Add("Detach Script", null, (s, e) => DetachScriptFromSelectedEntity());
        }

        private void NewScene()
        {
            _engine.Scene.Entities.Clear();
            RefreshEntityList();
            _logBox.AppendText("New scene created." + Environment.NewLine);
        }

        private void SaveScene()
        {
            using var dialog = new FolderBrowserDialog { Description = "Select folder to save scene package" };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    SceneSerializer.SavePackage(dialog.SelectedPath, _engine);
                    AppendLog($"Scene package saved to {dialog.SelectedPath}");
                }
                catch (Exception ex)
                {
                    AppendLog($"Scene save failed: {ex.Message}");
                }
            }
        }

        private void LoadScene()
        {
            using var dialog = new FolderBrowserDialog { Description = "Select folder containing saved scene package" };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var package = SceneSerializer.LoadPackage(dialog.SelectedPath);
                    _engine.Assets.Clear();
                    foreach (var file in Directory.GetFiles(dialog.SelectedPath, "*.item"))
                    {
                        try
                        {
                            var item = ItemFile.Load(file);
                            if (item.Type != ItemType.Scene)
                            {
                                _engine.Assets.Add(item);
                            }
                        }
                        catch
                        {
                            // ignore invalid asset files
                        }
                    }

                    _engine.Scene.Name = package.Name;
                    _engine.Scene.Entities.Clear();
                    foreach (var entityState in package.Entities)
                    {
                        var entity = new GameObject
                        {
                            Name = entityState.Name,
                            Position = new System.Numerics.Vector3(entityState.Position[0], entityState.Position[1], entityState.Position[2]),
                            Rotation = new System.Numerics.Vector3(entityState.Rotation[0], entityState.Rotation[1], entityState.Rotation[2]),
                            Scale = new System.Numerics.Vector3(entityState.Scale[0], entityState.Scale[1], entityState.Scale[2]),
                            VisualizationType = entityState.VisualizationType
                        };

                        if (!string.IsNullOrWhiteSpace(entityState.TextureAssetName))
                        {
                            var textureAsset = _engine.Assets.FirstOrDefault(a => string.Equals(a.Name, entityState.TextureAssetName, StringComparison.OrdinalIgnoreCase));
                            if (textureAsset != null)
                            {
                                _engine.ApplyTextureToSelected(entity, textureAsset);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(entityState.ModelAssetName))
                        {
                            var modelAsset = _engine.Assets.FirstOrDefault(a => string.Equals(a.Name, entityState.ModelAssetName, StringComparison.OrdinalIgnoreCase));
                            if (modelAsset != null)
                            {
                                _engine.ApplyModelToSelected(entity, modelAsset);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(entityState.ScriptAssetName))
                        {
                            var scriptAsset = _engine.Assets.FirstOrDefault(a => string.Equals(a.Name, entityState.ScriptAssetName, StringComparison.OrdinalIgnoreCase));
                            if (scriptAsset != null)
                            {
                                _engine.ApplyScriptToSelected(entity, scriptAsset);
                            }
                        }

                        _engine.Scene.Entities.Add(entity);
                    }

                    RefreshAssetTree();
                    RefreshEntityList();
                    AppendLog($"Loaded scene '{_engine.Scene.Name}' with {_engine.Scene.Entities.Count} entities and {_engine.Assets.Count} assets.");
                }
                catch (Exception ex)
                {
                    AppendLog($"Scene load failed: {ex.Message}");
                }
            }
        }

        private void LoadTemplateScene()
        {
            // Create a simple template scene with sample entities
            _engine.Scene.Name = "Template Scene";
            _engine.Scene.Entities.Clear();
            _engine.Assets.Clear();

            // Create a textured cube entity
            var cubeEntity = new GameObject
            {
                Name = "TexturedCube",
                Position = new System.Numerics.Vector3(0, 0, 0),
                Scale = new System.Numerics.Vector3(1, 1, 1),
                VisualizationType = ItemType.Texture
            };
            _engine.Scene.Entities.Add(cubeEntity);

            // Create a scripted entity
            var scriptedEntity = new GameObject
            {
                Name = "RotatingCube",
                Position = new System.Numerics.Vector3(2, 0, 0),
                Scale = new System.Numerics.Vector3(1, 1, 1),
                VisualizationType = ItemType.Texture
            };
            _engine.Scene.Entities.Add(scriptedEntity);

            // Create a model entity (placeholder, user can import GLB later)
            var modelEntity = new GameObject
            {
                Name = "ModelEntity",
                Position = new System.Numerics.Vector3(-2, 0, 0),
                Scale = new System.Numerics.Vector3(1, 1, 1),
                VisualizationType = ItemType.Model
            };
            _engine.Scene.Entities.Add(modelEntity);

            // Create sample assets
            var sampleScript = new AssetItem
            {
                Name = "RotateScript",
                Type = ItemType.ScriptCSharp,
                Payload = Encoding.UTF8.GetBytes(@"
using System;
using System.Numerics;

public class RotateScript : IScript
{
    public void Start(GameObject gameObject) { }

    public void Update(GameObject gameObject, float deltaTime)
    {
        gameObject.Rotation += new Vector3(0, deltaTime * 45, 0); // Rotate 45 degrees per second around Y axis
    }
}
"),
                Metadata = ".cs"
            };
            _engine.Assets.Add(sampleScript);
            _engine.ApplyScriptToSelected(scriptedEntity, sampleScript);

            RefreshAssetTree();
            RefreshEntityList();
            AppendLog("Loaded template scene with sample entities and script.");
        }

        private void BuildRuntime()
        {
            using var dialog = new FolderBrowserDialog { Description = "Select output folder for runtime build" };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    BuildManager.BuildRuntime(_engine, dialog.SelectedPath);
                    AppendLog($"Runtime built to {dialog.SelectedPath}");
                }
                catch (Exception ex)
                {
                    AppendLog($"Runtime build failed: {ex.Message}");
                }
            }
        }

        private void ImportTexture()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Import Texture",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tga|All Files|*.*"
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var payload = File.ReadAllBytes(dialog.FileName);
                var item = new AssetItem
                {
                    Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                    Type = ItemType.Texture,
                    Payload = payload,
                    Metadata = Path.GetExtension(dialog.FileName).ToLowerInvariant()
                };
                _engine.CreateAsset(item);
                RefreshAssetTree();
                AppendLog($"Imported texture '{item.Name}'.");
            }
        }

        private void ImportModel()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Import Model",
                Filter = "3D Model Files|*.glb;*.gltf|GLB Files|*.glb|glTF Files|*.gltf|All Files|*.*"
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var payload = File.ReadAllBytes(dialog.FileName);
                var item = new AssetItem
                {
                    Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                    Type = ItemType.Model,
                    Payload = payload,
                    Metadata = Path.GetExtension(dialog.FileName).ToLowerInvariant()
                };
                _engine.CreateAsset(item);
                RefreshAssetTree();
                AppendLog($"Imported model '{item.Name}'.");
            }
        }

        private void ImportScript()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Import Script",
                Filter = "Script Files|*.cs;*.py;*.cpp|C# Script|*.cs|Python Script|*.py|C++ Source|*.cpp|All Files|*.*"
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                var type = extension switch
                {
                    ".cs" => ItemType.ScriptCSharp,
                    ".py" => ItemType.ScriptPython,
                    ".cpp" => ItemType.ScriptCpp,
                    _ => ItemType.Unknown,
                };

                if (type == ItemType.Unknown)
                {
                    AppendLog("Unsupported script type.");
                    return;
                }

                var payload = Encoding.UTF8.GetBytes(File.ReadAllText(dialog.FileName));
                var item = new AssetItem
                {
                    Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                    Type = type,
                    Payload = payload,
                    Metadata = extension
                };
                _engine.CreateAsset(item);
                RefreshAssetTree();
                AppendLog($"Imported script '{item.Name}'.");
            }
        }

        private void CreateScriptDialog()
        {
            using var dialog = new Form
            {
                Text = "New Script",
                Size = new Size(700, 600),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var nameBox = new TextBox { Left = 12, Top = 12, Width = 660, PlaceholderText = "Script name" };
            var typeBox = new ComboBox { Left = 12, Top = 42, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            typeBox.Items.AddRange(new object[] { "C#", "Python", "C++" });
            typeBox.SelectedIndex = 0;

            var typeLabel = new Label { Left = 240, Top = 45, Text = "Script Type", AutoSize = true };
            var codeBox = new TextBox { Left = 12, Top = 72, Width = 660, Height = 420, Multiline = true, ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 10), AcceptsTab = true, WordWrap = false };
            codeBox.Text = GetScriptTemplate("C#");

            var saveButton = new Button { Text = "Create Script", Left = 12, Top = 500, Width = 140, Height = 30 };
            saveButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    MessageBox.Show(dialog, "Enter a script name.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var scriptType = typeBox.SelectedItem?.ToString() ?? "C#";
                var assetType = scriptType switch
                {
                    "C#" => ItemType.ScriptCSharp,
                    "Python" => ItemType.ScriptPython,
                    "C++" => ItemType.ScriptCpp,
                    _ => ItemType.ScriptCSharp,
                };

                var payload = Encoding.UTF8.GetBytes(codeBox.Text);
                var item = new AssetItem
                {
                    Name = nameBox.Text.Trim(),
                    Type = assetType,
                    Payload = payload,
                    Metadata = scriptType
                };

                _engine.CreateAsset(item);
                RefreshAssetTree();
                AppendLog($"Created new {scriptType} script '{item.Name}'.");
                dialog.Close();
            };

            var cancelButton = new Button { Text = "Cancel", Left = 162, Top = 500, Width = 120, Height = 30 };
            cancelButton.Click += (s, e) => dialog.Close();

            typeBox.SelectedIndexChanged += (s, e) => codeBox.Text = GetScriptTemplate(typeBox.SelectedItem?.ToString() ?? "C#");

            dialog.Controls.AddRange(new Control[] { nameBox, typeBox, typeLabel, codeBox, saveButton, cancelButton });
            dialog.ShowDialog(this);
        }

        private static string GetScriptTemplate(string type) => type switch
        {
            "C#" => "using SGE;\n\npublic class MyScript : IScript\n{\n    public void Start(GameObject self)\n    {\n        // Called once when attached\n    }\n\n    public void Update(GameObject self, float deltaTime)\n    {\n        // Called every frame\n    }\n}\n",
            "Python" => "def start(self):\n    pass\n\n\ndef update(self, deltaTime):\n    pass\n",
            "C++" => "// Compile your C++ script to a DLL with exported functions:\n// extern \"C\" __declspec(dllexport) void Start(void* self);\n// extern \"C\" __declspec(dllexport) void Update(void* self, float deltaTime);\n",
            _ => string.Empty,
        };

        private void AddEntity()
        {
            _engine.Scene.Entities.Add(new GameObject());
            RefreshEntityList();
            AppendLog("Entity added.");
        }

        private void RemoveSelectedEntity()
        {
            if (_entityList.SelectedItems.Count > 0 && _entityList.SelectedItems[0].Tag is GameObject go)
            {
                _engine.Scene.Entities.Remove(go);
                RefreshEntityList();
                AppendLog("Entity removed.");
            }
            else
            {
                AppendLog("No entity selected.");
            }
        }

        private void DetachScriptFromSelectedEntity()
        {
            if (_entityList.SelectedItems.Count > 0 && _entityList.SelectedItems[0].Tag is GameObject go)
            {
                go.Script = null;
                go.ScriptAssetName = string.Empty;
                AppendLog($"Detached script from {go.Name}.");
            }
            else
            {
                AppendLog("No entity selected.");
            }
        }

        private void ApplyAssetToSelectedEntity()
        {
            if (_assetTree.SelectedNode?.Tag is AssetItem asset && _entityList.SelectedItems.Count > 0 && _entityList.SelectedItems[0].Tag is GameObject go)
            {
                if (asset.Type == ItemType.ScriptCSharp || asset.Type == ItemType.ScriptPython || asset.Type == ItemType.ScriptCpp)
                {
                    _engine.ApplyScriptToSelected(go, asset);
                }
                else if (asset.Type == ItemType.Texture)
                {
                    _engine.ApplyTextureToSelected(go, asset);
                }
                else if (asset.Type == ItemType.Model)
                {
                    _engine.ApplyModelToSelected(go, asset);
                }
                else
                {
                    AppendLog("Selected asset cannot be applied to an entity.");
                    return;
                }

                AppendLog($"Applied asset '{asset.Name}' to {go.Name}.");
            }
            else
            {
                AppendLog("Select an asset and entity first.");
            }

            RefreshEntityList();
        }

        private void RefreshEntityList()
        {
            _entityList.Items.Clear();
            foreach (var entity in _engine.Scene.Entities)
            {
                var item = new ListViewItem(entity.Name) { Tag = entity };
                _entityList.Items.Add(item);
            }
            _entityList.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            _entityList.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void ShowAssetDetails(AssetItem asset)
        {
            AppendLog($"Asset: {asset.Name} Type: {asset.Type} Metadata: {asset.Metadata}");
        }

        private void ShowEntityDetails(GameObject go)
        {
            var scriptName = string.IsNullOrWhiteSpace(go.ScriptAssetName) ? "None" : go.ScriptAssetName;
            var textureName = string.IsNullOrWhiteSpace(go.TextureAssetName) ? "None" : go.TextureAssetName;
            var modelName = string.IsNullOrWhiteSpace(go.ModelAssetName) ? "None" : go.ModelAssetName;
            AppendLog($"Entity: {go.Name} Position: {go.Position} Scale: {go.Scale} Script: {scriptName} Texture: {textureName} Model: {modelName}");
        }

        private void SaveAssetAs()
        {
            if (_assetTree.SelectedNode?.Tag is AssetItem asset)
            {
                using var dialog = new SaveFileDialog
                {
                    FileName = GetSafeFileName(asset.Name) + ".item",
                    Filter = "SGE Asset|*.item|All Files|*.*"
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    ItemFile.Save(dialog.FileName, asset);
                    AppendLog($"Saved asset '{asset.Name}' to {dialog.FileName}.");
                }
            }
            else
            {
                AppendLog("Select an asset first.");
            }
        }

        private void RemoveAsset()
        {
            if (_assetTree.SelectedNode?.Tag is AssetItem asset)
            {
                _engine.Assets.Remove(asset);
                RefreshAssetTree();
                AppendLog($"Removed asset '{asset.Name}'.");
            }
            else
            {
                AppendLog("Select an asset first.");
            }
        }

        private void RefreshAssetTree()
        {
            _assetTree.BeginUpdate();
            _assetTree.Nodes.Clear();
            var root = _assetTree.Nodes.Add("Assets", "Assets");
            foreach (var asset in _engine.Assets)
            {
                var node = root.Nodes.Add(asset.Name);
                node.Tag = asset;
            }
            root.Expand();
            _assetTree.EndUpdate();
        }

        private void AppendLog(string message)
        {
            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        private static string GetSafeFileName(string text)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var safe = new string(text.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(safe) ? "asset" : safe;
        }
    }

    public sealed class ScenePackageData
    {
        public string Name { get; set; } = "Untitled";
        public List<EntityState> Entities { get; set; } = new();
    }

    public sealed class EntityState
    {
        public string Name { get; set; } = "NewEntity";
        public float[] Position { get; set; } = new float[3];
        public float[] Rotation { get; set; } = new float[3];
        public float[] Scale { get; set; } = new float[3];
        public string ScriptAssetName { get; set; } = string.Empty;
        public string TextureAssetName { get; set; } = string.Empty;
        public string ModelAssetName { get; set; } = string.Empty;
        public ItemType VisualizationType { get; set; } = ItemType.Texture;
    }
}