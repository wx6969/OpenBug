using System;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using System.Windows.Threading;
using System.Diagnostics;
using Microsoft.Win32;
using Point = System.Windows.Point;
using WinForms = System.Windows.Forms;

namespace OpenAnt
{
    public partial class MainWindow : Window
    {
        private readonly List<ProceduralAnt> _ants = new List<ProceduralAnt>();
        private SpatialGrid _spatialGrid = null!;
        private TimeSpan _lastRenderTime;
        private double _updateAccumulator;

        private const int MaxBugCount = 500;
        private WinForms.ToolStripMenuItem? _bugCountItem;
        private WinForms.ToolStripMenuItem? _addBugItem;
        private WinForms.ToolStripMenuItem? _removeBugItem;
        private PheromoneField? _pheromones;
        private double _lastWorldW;
        private double _lastWorldH;
        private bool _suppressBugMenuUpdates;

        private double _swarmEventTimer;
        private double _swarmEventCooldown;
        
        // New fields
        private WinForms.NotifyIcon _notifyIcon = null!;
        private DispatcherTimer _spawnTimer = null!;
        private DateTime _startTime;
        private bool _areAntsHidden = false;
        private Random _random = new Random();

        public MainWindow()
        {
            InitializeComponent();
            
            // Force Software Rendering to avoid high GPU usage on transparent windows
            // This moves the burden to CPU, which is usually negligible for this app
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            
            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _startTime = DateTime.Now;

            // Initialize Spatial Grid
            // Cell size 50 is slightly larger than PerceptionRadius (40)
            _spatialGrid = new SpatialGrid(WorldSurface.ActualWidth, WorldSurface.ActualHeight, 50);

            // Initialize NotifyIcon
            InitializeNotifyIcon();

            // Spawn Initial 5 Ants
            for (int i = 0; i < 5; i++)
            {
                SpawnAnt(isInitial: true);
            }

            // Initialize Spawn Timer
            _spawnTimer = new DispatcherTimer();
            _spawnTimer.Interval = TimeSpan.FromMinutes(10);
            _spawnTimer.Tick += OnSpawnTimerTick;
            _spawnTimer.Start();

            // Start Game Loop
            // RenderOptions.SetEdgeMode(WorldCanvas, EdgeMode.Aliased); // Revert to default (Antialiased)
            CompositionTarget.Rendering += OnRendering;

            WorldSurface.Ants = _ants;
            UpdateBugMenuItems();

            EnsurePheromoneField();
            _swarmEventCooldown = 90.0 + _random.NextDouble() * 120.0;
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new WinForms.NotifyIcon
            {
                Icon = GetAppIcon(),
                Visible = true,
                Text = "OpenBug"
            };

            var contextMenu = new WinForms.ContextMenuStrip();
            
            var autoStartItem = new WinForms.ToolStripMenuItem("Launch on boot");
            autoStartItem.CheckOnClick = true;
            autoStartItem.Checked = IsAutoStartEnabled();
            autoStartItem.CheckedChanged += (s, e) => SetAutoStart(autoStartItem.Checked);

            _bugCountItem = new WinForms.ToolStripMenuItem($"Bugs: {_ants.Count}");
            _bugCountItem.Click += (s, e) => Dispatcher.Invoke(PromptAndSetBugCount);

            _addBugItem = new WinForms.ToolStripMenuItem("Add Bug (+1)");
            _addBugItem.Click += (s, e) => Dispatcher.Invoke(() => SetBugCount(_ants.Count + 1));

            _removeBugItem = new WinForms.ToolStripMenuItem("Remove Bug (-1)");
            _removeBugItem.Click += (s, e) => Dispatcher.Invoke(() => SetBugCount(_ants.Count - 1));

            var hideItem = new WinForms.ToolStripMenuItem("Hide");
            hideItem.Click += (s, e) => Dispatcher.Invoke(() => ToggleHide(hideItem));
            var exitItem = new WinForms.ToolStripMenuItem("Quit");
            exitItem.Click += (s, e) => 
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                System.Windows.Application.Current.Shutdown();
            };
            
            contextMenu.Items.Add(autoStartItem);
            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
            contextMenu.Items.Add(_bugCountItem);
            contextMenu.Items.Add(_addBugItem);
            contextMenu.Items.Add(_removeBugItem);
            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
            contextMenu.Items.Add(hideItem);
            contextMenu.Items.Add(exitItem);
            
            _notifyIcon.ContextMenuStrip = contextMenu;
            UpdateBugMenuItems();
        }

        private static string? GetExecutablePath()
        {
            return Process.GetCurrentProcess().MainModule?.FileName;
        }

        private static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                var value = key?.GetValue("OpenBug") as string;
                var path = GetExecutablePath();
                return !string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(path) && string.Equals(value, path, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;
                if (enable)
                {
                    var path = GetExecutablePath();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        key.SetValue("OpenBug", path);
                    }
                }
                else
                {
                    key.DeleteValue("OpenBug", false);
                }
            }
            catch
            {
            }
        }

        private System.Drawing.Icon GetAppIcon()
        {
            try
            {
                var path = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                    if (icon != null) return icon;
                }
            }
            catch
            {
            }

            return System.Drawing.SystemIcons.Application;
        }

        private void ToggleHide(WinForms.ToolStripMenuItem menuItem)
        {
            _areAntsHidden = !_areAntsHidden;
            
            if (_areAntsHidden)
            {
                // Hide all ants
                foreach (var ant in _ants)
                {
                    ant.SetVisibility(false);
                }
                // Pause spawning
                _spawnTimer.Stop();
                WorldSurface.Ants = Array.Empty<ProceduralAnt>();
                WorldSurface.InvalidateVisual();
                menuItem.Text = "Show";
            }
            else
            {
                // Show all ants
                foreach (var ant in _ants)
                {
                    ant.SetVisibility(true);
                }
                // Resume spawning
                _spawnTimer.Start();
                WorldSurface.Ants = _ants;
                WorldSurface.InvalidateVisual();
                menuItem.Text = "Hide";
            }

            UpdateBugMenuItems();
        }

        private void OnSpawnTimerTick(object? sender, EventArgs e)
        {
            // Spawn 1 ant per interval
            if (_ants.Count >= MaxBugCount) return;
            SpawnAnt(isInitial: false);
            UpdateBugMenuItems();
        }

        private void SetBugCount(int targetCount)
        {
            if (targetCount < 0) targetCount = 0;
            if (targetCount > MaxBugCount) targetCount = MaxBugCount;
            if (targetCount == _ants.Count) return;

            _suppressBugMenuUpdates = true;
            try
            {
                while (_ants.Count < targetCount)
                {
                    SpawnAnt(isInitial: false);
                }

                while (_ants.Count > targetCount)
                {
                    _ants.RemoveAt(_ants.Count - 1);
                }
            }
            finally
            {
                _suppressBugMenuUpdates = false;
            }

            if (!_areAntsHidden)
            {
                WorldSurface.Ants = _ants;
                WorldSurface.InvalidateVisual();
            }

            UpdateBugMenuItems();
        }

        private void PromptAndSetBugCount()
        {
            int? desired = PromptForBugCount(_ants.Count);
            if (desired == null) return;
            int value = desired.Value;
            if (value > MaxBugCount) value = MaxBugCount;
            if (value < 0) value = 0;
            SetBugCount(value);
        }

        private int? PromptForBugCount(int currentCount)
        {
            using var form = new WinForms.Form();
            form.Text = "Set Bugs";
            form.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowIcon = false;
            form.ShowInTaskbar = false;
            form.StartPosition = WinForms.FormStartPosition.CenterScreen;
            form.Width = 260;
            form.Height = 140;

            var label = new WinForms.Label();
            label.Left = 12;
            label.Top = 12;
            label.Width = 220;
            label.Text = $"0 - {MaxBugCount}";

            var input = new WinForms.NumericUpDown();
            input.Left = 12;
            input.Top = 36;
            input.Width = 220;
            input.Minimum = 0;
            input.Maximum = MaxBugCount;
            input.Value = Math.Min(MaxBugCount, Math.Max(0, currentCount));

            var ok = new WinForms.Button();
            ok.Text = "OK";
            ok.Left = 72;
            ok.Top = 72;
            ok.Width = 70;
            ok.DialogResult = WinForms.DialogResult.OK;

            var cancel = new WinForms.Button();
            cancel.Text = "Cancel";
            cancel.Left = 156;
            cancel.Top = 72;
            cancel.Width = 70;
            cancel.DialogResult = WinForms.DialogResult.Cancel;

            form.Controls.Add(label);
            form.Controls.Add(input);
            form.Controls.Add(ok);
            form.Controls.Add(cancel);
            form.AcceptButton = ok;
            form.CancelButton = cancel;

            var result = form.ShowDialog();
            if (result != WinForms.DialogResult.OK) return null;
            return (int)input.Value;
        }

        private void UpdateBugMenuItems()
        {
            if (_bugCountItem == null || _addBugItem == null || _removeBugItem == null) return;
            _bugCountItem.Text = $"Bugs: {_ants.Count}";
            _addBugItem.Enabled = _ants.Count < MaxBugCount;
            _removeBugItem.Enabled = _ants.Count > 0;
        }

        private void SpawnAnt(bool isInitial)
        {
            // Calculate vividness
            // 0 at start, increases with time
            // Max vividness (1.0) after ~4 hours (reached 50 ants)
            double elapsedHours = (DateTime.Now - _startTime).TotalHours;
            double vividness = Math.Min(1.0, elapsedHours / 4.0); 
            if (isInitial) vividness = 0.0;

            // Behavior Type
            double roll = _random.NextDouble();
            AntBehaviorType type;
            if (roll < 0.15) type = AntBehaviorType.EdgeDweller;
            else if (roll < 0.85) type = AntBehaviorType.Social;
            else type = AntBehaviorType.Loner;

            var ant = new ProceduralAnt(type, vividness);

            // Position
            Point pos;
            double rotation;
            
            if (isInitial)
            {
                // Random position within bounds
                double x = _random.NextDouble() * (WorldSurface.ActualWidth - 20) + 10;
                double y = _random.NextDouble() * (WorldSurface.ActualHeight - 20) + 10;
                pos = new Point(x, y);
                rotation = _random.NextDouble() * 360.0;
            }
            else
            {
                // Spawn at edge
                // Pick a side: 0=Top, 1=Right, 2=Bottom, 3=Left
                int side = _random.Next(4);
                double offset = 20; // Spawn slightly outside or on edge
                
                // Canvas dimensions might be large (virtual), but we use ActualWidth/Height
                double w = WorldSurface.ActualWidth;
                double h = WorldSurface.ActualHeight;

                switch (side)
                {
                    case 0: // Top
                        pos = new Point(_random.NextDouble() * w, -offset);
                        rotation = 90 + (_random.NextDouble() * 90 - 45); // Down
                        break;
                    case 1: // Right
                        pos = new Point(w + offset, _random.NextDouble() * h);
                        rotation = 180 + (_random.NextDouble() * 90 - 45); // Left
                        break;
                    case 2: // Bottom
                        pos = new Point(_random.NextDouble() * w, h + offset);
                        rotation = 270 + (_random.NextDouble() * 90 - 45); // Up
                        break;
                    case 3: // Left
                        pos = new Point(-offset, _random.NextDouble() * h);
                        rotation = 0 + (_random.NextDouble() * 90 - 45); // Right
                        break;
                    default:
                        pos = new Point(w/2, h/2);
                        rotation = 0;
                        break;
                }
            }

            ant.SetTransform(pos, rotation);
            
            // If we spawn while hidden (shouldn't happen due to timer pause, but good safety)
            if (_areAntsHidden)
            {
                ant.SetVisibility(false);
            }
            
            _ants.Add(ant);
            if (!_suppressBugMenuUpdates) UpdateBugMenuItems();
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            // If paused/hidden, we can skip updates to save resources
            if (_areAntsHidden) return;

            var args = (RenderingEventArgs)e;
            
            if (_lastRenderTime == TimeSpan.Zero)
            {
                _lastRenderTime = args.RenderingTime;
                return;
            }

            var deltaTime = (args.RenderingTime - _lastRenderTime).TotalSeconds;
            // Cap at 60fps maximum
            if (deltaTime < 0.016) return;
            
            _lastRenderTime = args.RenderingTime;
            _updateAccumulator += deltaTime;
            if (_updateAccumulator < AntConfig.RenderUpdateInterval)
            {
                return;
            }
            deltaTime = _updateAccumulator; // Use accumulated time for physics step
            _updateAccumulator = 0;

            // Update Spatial Grid
            _spatialGrid.Clear();
            foreach (var ant in _ants)
            {
                _spatialGrid.Add(ant);
            }

            // Get global cursor position and convert to local coordinates
            var screenCursor = WinForms.Cursor.Position;
            var pointFromScreen = this.PointFromScreen(new Point(screenCursor.X, screenCursor.Y));
            var cursorPos = pointFromScreen;

            EnsurePheromoneField();
            _pheromones?.Update(deltaTime);
            bool swarmActive = UpdateSwarmDirector(deltaTime);

            foreach (var ant in _ants)
            {
                // Query neighbors from grid (O(1) lookup + small local search)
                // Radius 50 covers PerceptionRadius (40)
                var neighbors = _spatialGrid.Query(ant.Position, 50);
                ant.Update(deltaTime, neighbors, cursorPos, WorldSurface.ActualWidth, WorldSurface.ActualHeight, _pheromones, swarmActive);
            }

            if (_pheromones != null)
            {
                for (int i = 0; i < _ants.Count; i++)
                {
                    if (!_ants[i].IsVisible) continue;
                    Vector dir = _ants[i].Forward;
                    double amount = AntConfig.PheromoneDepositPerSecond * deltaTime * _ants[i].PheromoneDepositScale;
                    _pheromones.Deposit(_ants[i].Position, dir, amount);
                }
            }

            WorldSurface.InvalidateVisual();
        }

        private bool UpdateSwarmDirector(double deltaTime)
        {
            if (deltaTime <= 0) return _swarmEventTimer > 0;

            if (_swarmEventTimer > 0)
            {
                _swarmEventTimer -= deltaTime;
                if (_swarmEventTimer <= 0)
                {
                    _swarmEventTimer = 0;
                    _swarmEventCooldown = 300.0 + _random.NextDouble() * 420.0;
                }

                return _swarmEventTimer > 0;
            }

            if (_swarmEventCooldown > 0)
            {
                _swarmEventCooldown -= deltaTime;
                return false;
            }

            if (_pheromones == null) return false;
            if (_ants.Count < 20) return false;

            ProceduralAnt? candidate = null;
            for (int i = 0; i < 6; i++)
            {
                int idx = _random.Next(_ants.Count);
                var a = _ants[idx];
                if (!a.IsVisible) continue;
                if (a.BehaviorType != AntBehaviorType.Social) continue;
                candidate = a;
                break;
            }

            if (candidate == null) return false;

            var s = _pheromones.Sample(candidate.Position);
            double extra = Math.Max(0, _ants.Count - 50) / 150.0;
            if (extra > 3) extra = 3;
            double requiredStrength = 5.0 + extra;
            if (s.strength < requiredStrength) return false;

            double triggerProb = deltaTime / 480.0;
            if (triggerProb <= 0) return false;
            if (_random.NextDouble() >= triggerProb) return false;

            _swarmEventTimer = 6.0 + _random.NextDouble() * 8.0;
            return true;
        }

        private void EnsurePheromoneField()
        {
            double w = WorldSurface.ActualWidth;
            double h = WorldSurface.ActualHeight;
            if (w < 1 || h < 1) return;

            if (_pheromones == null || Math.Abs(_lastWorldW - w) > 1 || Math.Abs(_lastWorldH - h) > 1)
            {
                _pheromones = new PheromoneField(w, h, AntConfig.PheromoneCellSize);
                _lastWorldW = w;
                _lastWorldH = h;
            }
        }
    }
}
