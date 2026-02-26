using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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
            _spatialGrid = new SpatialGrid(WorldCanvas.ActualWidth, WorldCanvas.ActualHeight, 50);

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

            var hideItem = new WinForms.ToolStripMenuItem("Hide");
            hideItem.Click += (s, e) => ToggleHide(hideItem);
            var exitItem = new WinForms.ToolStripMenuItem("Quit");
            exitItem.Click += (s, e) => 
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                System.Windows.Application.Current.Shutdown();
            };
            
            contextMenu.Items.Add(autoStartItem);
            contextMenu.Items.Add(hideItem);
            contextMenu.Items.Add(exitItem);
            
            _notifyIcon.ContextMenuStrip = contextMenu;
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
                menuItem.Text = "Hide";
            }
        }

        private void OnSpawnTimerTick(object? sender, EventArgs e)
        {
            // Spawn 1 ant per interval
            if (_ants.Count >= 50) return;
            SpawnAnt(isInitial: false);
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
            if (roll < 0.1) type = AntBehaviorType.EdgeDweller; // 10% (was 60%)
            else if (roll < 0.7) type = AntBehaviorType.Social; // 60% (was 30%)
            else type = AntBehaviorType.Loner;                  // 30% (was 10%)

            var ant = new ProceduralAnt(WorldCanvas, type, vividness);

            // Position
            Point pos;
            double rotation;
            
            if (isInitial)
            {
                // Random position within bounds
                double x = _random.NextDouble() * (WorldCanvas.ActualWidth - 20) + 10;
                double y = _random.NextDouble() * (WorldCanvas.ActualHeight - 20) + 10;
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
                double w = WorldCanvas.ActualWidth;
                double h = WorldCanvas.ActualHeight;

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

            foreach (var ant in _ants)
            {
                // Query neighbors from grid (O(1) lookup + small local search)
                // Radius 50 covers PerceptionRadius (40)
                var neighbors = _spatialGrid.Query(ant.Position, 50);
                ant.Update(deltaTime, neighbors, cursorPos);
            }
        }
    }
}
