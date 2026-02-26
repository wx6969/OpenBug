using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace OpenAnt
{
    public enum AntBehaviorType
    {
        EdgeDweller, // Loves edges (60%)
        Social,      // Loves groups (30%)
        Loner        // Loves solitude (10%)
    }

    public class ProceduralAnt
    {
        private enum AntMoveState
        {
            Idle,
            Moving
        }

        private Canvas _canvas;
        private Ellipse _head = null!;
        private Ellipse _thorax = null!;
        private Ellipse _abdomen = null!;
        private List<AntLeg> _legs = new List<AntLeg>();
        // private Path _legsPath = null!; // Removed single path optimization
        
        // Transform
        public Point Position { get; private set; }
        public double Rotation { get; private set; } // Degrees
        
        private double _targetRotation;
        private static readonly Random _random = new Random();

        // Morandi Color Palette
        private static readonly List<Brush> _morandiColors = new List<Brush>
        {
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A4B7C9")), // Blue Grey
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8FA899")), // Sage Green
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4B2AA")), // Dusty Pink
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DED0B6")), // Sand/Beige
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9BA88D")), // Olive Green
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C6B5A6")), // Taupe
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8DA399")), // Slate Green
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B5A398")), // Warm Grey
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A99D98")), // Pewter
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0CDB6")), // Cream
        };
        private Brush _antColor;

        private double _wanderTimer = 0;
        private AntMoveState _moveState = AntMoveState.Moving;
        private double _stateTimer;
        private double _currentSpeed;
        private double _targetSpeed;

        // Personality traits
        private double _speedModifier;
        private double _turnModifier;
        private double _stateDurationModifier;
        
        private double _gaitTimer = 0;
        private int _gaitPhase = 0;
        private double _legUpdateTimer = 0;

        public AntBehaviorType BehaviorType { get; private set; }

        public ProceduralAnt(Canvas canvas, AntBehaviorType behaviorType, double vividness = 0.0)
        {
            _canvas = canvas;
            BehaviorType = behaviorType;
            Position = new Point(400, 225); // Center of 800x450
            Rotation = 0;

            // Pick random Morandi color
            Brush baseBrush = _morandiColors[_random.Next(_morandiColors.Count)];
            Color baseColor = ((SolidColorBrush)baseBrush).Color;

            // Apply vividness (0.0 to 1.0)
            if (vividness > 0)
            {
                _antColor = ApplyVividness(baseColor, vividness);
            }
            else
            {
                _antColor = baseBrush;
            }

            // Freeze the brush to be thread-safe/performant
            if (_antColor.CanFreeze) _antColor.Freeze();

            InitializeLegs();
            InitializeBody();
            
            // Randomize personality
            _speedModifier = 0.8 + _random.NextDouble() * 0.4; // 0.8x to 1.2x
            _turnModifier = 0.8 + _random.NextDouble() * 0.4;  // 0.8x to 1.2x
            _stateDurationModifier = 0.8 + _random.NextDouble() * 0.5; // 0.8x to 1.3x

            // Loner ants are faster and more restless
            if (BehaviorType == AntBehaviorType.Loner)
            {
                _speedModifier *= 1.2;
                _stateDurationModifier *= 0.7;
            }

            // Randomize initial state
            _moveState = _random.NextDouble() > 0.5 ? AntMoveState.Moving : AntMoveState.Idle;
            _stateTimer = _random.NextDouble() * 2.0;
            
            _currentSpeed = _moveState == AntMoveState.Moving ? AntConfig.MaxSpeed * _speedModifier : 0;
            _targetSpeed = _currentSpeed;
        }

        private Brush ApplyVividness(Color color, double factor)
        {
            // Simple HSL adjustment simulation
            // Factor 0.0 = original, 1.0 = max vividness

            // 1. Increase Saturation: Move channels away from gray (average)
            double r = color.R;
            double g = color.G;
            double b = color.B;
            
            double avg = (r + g + b) / 3.0;
            
            // Push values away from average to increase saturation
            double saturationBoost = 1.0 + factor * 1.5; // Up to 2.5x saturation
            
            r = avg + (r - avg) * saturationBoost;
            g = avg + (g - avg) * saturationBoost;
            b = avg + (b - avg) * saturationBoost;

            // 2. Increase Brightness: Add light
            double brightnessBoost = factor * 40.0; // Add up to 40 to RGB
            r += brightnessBoost;
            g += brightnessBoost;
            b += brightnessBoost;

            // Clamp
            byte newR = (byte)Math.Min(255, Math.Max(0, r));
            byte newG = (byte)Math.Min(255, Math.Max(0, g));
            byte newB = (byte)Math.Min(255, Math.Max(0, b));

            return new SolidColorBrush(Color.FromRgb(newR, newG, newB));
        }

        public void SetTransform(Point position, double rotationDegrees)
        {
            Position = position;
            Rotation = rotationDegrees;
            foreach (var leg in _legs)
            {
                leg.InitializePosition(Position, Rotation);
            }
        }

        public void SetVisibility(bool visible)
        {
            Visibility v = visible ? Visibility.Visible : Visibility.Collapsed;
            if (_head != null) _head.Visibility = v;
            if (_thorax != null) _thorax.Visibility = v;
            if (_abdomen != null) _abdomen.Visibility = v;
            foreach (var leg in _legs)
            {
                leg.SetVisibility(visible);
            }
        }

        private void InitializeBody()
        {
            _head = CreateBodyPart(AntConfig.HeadSize, AntConfig.HeadSize, _antColor);
            _thorax = CreateBodyPart(AntConfig.ThoraxLength, AntConfig.ThoraxWidth, _antColor);
            _abdomen = CreateBodyPart(AntConfig.AbdomenLength, AntConfig.AbdomenWidth, _antColor);
        }

        private Ellipse CreateBodyPart(double width, double height, Brush fill)
        {
            var ellipse = new Ellipse
            {
                Width = width,
                Height = height,
                Fill = fill
            };
            _canvas.Children.Add(ellipse);
            return ellipse;
        }

        private void InitializeLegs()
        {
            // Attach points on Thorax
            // 3 pairs of legs
            // Adjust offsets and reach to prevent overlapping
            
            double xFront = 1.2;
            double xMid = 0.0;
            double xBack = -1.2;
            
            double yOffset = 0.8; 
            
            // Reach (Ideal foot distance)
            // Front legs reach forward and out
            // Mid legs reach strictly out
            // Back legs reach back and out
            double reachX = 4.0;
            double reachY = 6.0;

            // Leg 0: Left Front
            _legs.Add(new AntLeg(_canvas, new Point(xFront, -yOffset), new Vector(reachX, -reachY), false, _antColor));
            // Leg 1: Left Mid
            _legs.Add(new AntLeg(_canvas, new Point(xMid, -yOffset), new Vector(0, -reachY * 1.2), false, _antColor));
            // Leg 2: Left Back
            _legs.Add(new AntLeg(_canvas, new Point(xBack, -yOffset), new Vector(-reachX, -reachY), false, _antColor));
            
            // Leg 3: Right Front
            _legs.Add(new AntLeg(_canvas, new Point(xFront, yOffset), new Vector(reachX, reachY), true, _antColor));
            // Leg 4: Right Mid
            _legs.Add(new AntLeg(_canvas, new Point(xMid, yOffset), new Vector(0, reachY * 1.2), true, _antColor));
            // Leg 5: Right Back
            _legs.Add(new AntLeg(_canvas, new Point(xBack, yOffset), new Vector(-reachX, reachY), true, _antColor));
        }

        private double _interactionTimer = 0;
        private double _interactionCooldownTimer = 0;
        private bool _isInteracting = false;
        
        // Trail Following State
        private ProceduralAnt? _leaderAnt;
        private double _trailCheckTimer = 0;

        // Lazy State
        private bool _isLazy = false;

        public void Update(double deltaTime, List<ProceduralAnt> neighbors, Point cursorPos)
        {
            UpdateBehavior(deltaTime, neighbors, cursorPos);
            UpdatePhysics(deltaTime);
            UpdateVisuals(deltaTime);
            
            _legUpdateTimer += deltaTime;
            if (_legUpdateTimer >= AntConfig.LegUpdateInterval)
            {
                UpdateLegs();
                _legUpdateTimer = 0;
            }
        }

        private void UpdateBehavior(double deltaTime, List<ProceduralAnt> neighbors, Point cursorPos)
        {
            // Interaction Logic
            if (_interactionCooldownTimer > 0) _interactionCooldownTimer -= deltaTime;

            if (_isInteracting)
            {
                _interactionTimer -= deltaTime;
                if (_interactionTimer <= 0)
                {
                    // Interaction over
                    _isInteracting = false;
                    _interactionCooldownTimer = AntConfig.InteractionCooldown + _random.NextDouble() * 2.0;
                    
                    // Separate after interaction
                    _moveState = AntMoveState.Moving;
                    _targetSpeed = AntConfig.MaxSpeed * _speedModifier;
                    
                    // Turn away randomly
                    _targetRotation += 180 + (_random.NextDouble() * 60 - 30);
                }
                else
                {
                    // Stop moving while interacting
                    _currentSpeed = 0;
                    _targetSpeed = 0;
                    return; // Skip other movement logic
                }
            }

            _stateTimer -= deltaTime;
            if (_stateTimer <= 0)
            {
                if (_moveState == AntMoveState.Moving)
                {
                    _moveState = AntMoveState.Idle;
                    // Personalized wait duration
                    _stateTimer = (0.3 + _random.NextDouble() * 1.5) * _stateDurationModifier;
                    _targetSpeed = 0.0;
                }
                else
                {
                    _moveState = AntMoveState.Moving;
                    // Personalized move duration
                    _stateTimer = (0.8 +  _random.NextDouble() * 2.5) * _stateDurationModifier;
                    // Personalized target speed
                    _targetSpeed = AntConfig.MaxSpeed * (0.6 + _random.NextDouble() * 0.4) * _speedModifier;
                }
            }

            _wanderTimer -= deltaTime;
            if (_wanderTimer <= 0)
            {
                _wanderTimer = 1.0 + _random.NextDouble() * 2.0;
                
                // Base random turn
                double turnRange = 90 * _turnModifier;
                _targetRotation = Rotation + (_random.NextDouble() * 2 - 1) * turnRange;
            }

            // Trail Check Logic
            _trailCheckTimer -= deltaTime;
            if (_trailCheckTimer <= 0)
            {
                _trailCheckTimer = 0.5 + _random.NextDouble() * 0.5;
                UpdateTrailLeader(neighbors);
            }

            // Apply social/environmental influences to target rotation every frame
            ApplySteeringForces(neighbors, cursorPos);
        }

        private void ApplySteeringForces(List<ProceduralAnt> neighbors, Point cursorPos)
        {
            Vector totalForce = new Vector(0, 0);

            // 1. Cursor Repulsion (Highest Priority fear)
            // Need to convert cursor position from Screen coordinates to Window/Canvas coordinates
            // Since window is maximized and covers screen, they should be close, but let's be precise
            // Actually cursorPos passed in is already screen coordinates.
            // If the window is full screen transparent, PointFromScreen might be needed if DPI scaling is an issue.
            // But usually for overlay windows, direct mapping works if DPI aware.
            // Let's assume cursorPos is correct relative to the canvas for now.
            
            Vector toCursor = Position - cursorPos; // Vector FROM cursor TO ant
            double distToCursorSq = toCursor.LengthSquared;
            double cursorRepulsionRadSq = AntConfig.CursorRepulsionRadius * AntConfig.CursorRepulsionRadius;
            
            if (distToCursorSq < cursorRepulsionRadSq)
            {
                // Panic!
                _isLazy = false;
                
                double dist = Math.Sqrt(distToCursorSq);
                Vector fleeDir = toCursor; // Flee direction is AWAY from cursor
                fleeDir.Normalize();
                
                // Strong exponential repulsion
                // Force grows stronger as distance decreases
                double repulsionStrength = AntConfig.CursorRepulsionStrength * (1.0 - dist / AntConfig.CursorRepulsionRadius);
                totalForce += fleeDir * repulsionStrength * 10.0; // Reduced multiplier from 20.0 to 10.0
                
                // If very close to cursor, force move state and high speed
                if (dist < AntConfig.CursorRepulsionRadius * 0.8)
                {
                    _moveState = AntMoveState.Moving;
                    _currentSpeed = AntConfig.MaxSpeed * 1.7; // Reduced panic speed from 2.0 to 1.7
                    _targetSpeed = _currentSpeed;
                    _stateTimer = 0.5; // Maintain panic for a bit
                }
            }

            // 3. Edge Attraction (Only for Edge Dwellers)
            if (BehaviorType == AntBehaviorType.EdgeDweller)
            {
                Vector edgeForce = GetEdgeAttraction();
                double weight = AntConfig.EdgeAttractionWeight;
                totalForce += edgeForce * weight;
            }

            // 2. Separation & Cohesion & Interaction
            Vector separationForce = new Vector(0, 0);
            Vector cohesionForce = new Vector(0, 0);
            int neighborCount = 0;
            Vector centerOfMass = new Vector(0, 0);

            // Optimization: neighbors list is now provided by SpatialGrid (already spatially filtered)
            
            // Check for Laziness conditions:
            // 1. Not already panicked (cursor check done above)
            // 2. Near edge/corner
            // 3. High neighbor count (cluster)
            
            double w = _canvas.ActualWidth;
            double h = _canvas.ActualHeight;
            double distLeft = Position.X;
            double distRight = w - Position.X;
            double distTop = Position.Y;
            double distBottom = h - Position.Y;
            double distToEdge = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

            bool nearEdge = distToEdge < AntConfig.LazyEdgeDistance;
            
            foreach (var other in neighbors)
            {
                if (other == this) continue;

                Vector toOther = other.Position - this.Position;
                // Quick distance check (squared) to avoid Sqrt
                double distSq = toOther.LengthSquared;
                double perceptionSq = AntConfig.PerceptionRadius * AntConfig.PerceptionRadius;

                if (distSq < perceptionSq)
                {
                    double dist = Math.Sqrt(distSq);

                    // Interaction Check
                    if (!_isInteracting && other._isInteracting == false && 
                        _interactionCooldownTimer <= 0 && other._interactionCooldownTimer <= 0 &&
                        dist < AntConfig.InteractionRadius)
                    {
                        // Reduced chance to stop and interact: from 5% to 1% per frame
                        if (_random.NextDouble() < 0.01)
                        {
                            StartInteraction();
                            other.StartInteraction();
                        }
                    }

                    // Cohesion: Accumulate position
                    centerOfMass += (Vector)other.Position;
                    neighborCount++;

                    // Separation: Push away if too close
                    if (dist < AntConfig.SeparationRadius)
                    {
                        // Stronger push when closer
                        Vector push = -toOther;
                        push.Normalize();
                        // Exponential separation force for "hard" collision avoidance
                        separationForce += push * (1.0 - dist / AntConfig.SeparationRadius) * 2.0;
                    }
                }
            }

            // Lazy Update Logic
            if (nearEdge && neighborCount >= AntConfig.LazyNeighborThreshold)
            {
                // Enter lazy state with high probability
                 if (!_isLazy && _random.NextDouble() < 0.1) 
                 {
                     _isLazy = true;
                     _moveState = AntMoveState.Idle; // Stop moving immediately
                     _stateTimer = 2.0 + _random.NextDouble() * 3.0; // Long nap
                 }
            }
            else if (_isLazy && (neighborCount < AntConfig.LazyNeighborThreshold / 2 || !nearEdge))
            {
                // Break lazy state if cluster disperses or moved away from edge
                _isLazy = false;
                _moveState = AntMoveState.Moving;
            }

            // Apply Lazy modifiers
            if (_isLazy)
            {
                // Reduce movement desire
                if (_moveState == AntMoveState.Moving)
                {
                    _targetSpeed *= AntConfig.LazySpeedMultiplier;
                    
                    // High chance to stop again
                    if (_random.NextDouble() < AntConfig.LazyIdleChance)
                    {
                         _moveState = AntMoveState.Idle;
                         _stateTimer = 1.0 + _random.NextDouble() * 2.0;
                    }
                }
                
                // Reduce steering forces except separation (still don't want to overlap)
                cohesionForce *= 0.1; 
                // Don't separate too much, stay cozy
                separationForce *= 0.5; 
            }

            if (neighborCount > 0)
            {
                // Cohesion Logic
                if (BehaviorType != AntBehaviorType.Loner)
                {
                    // Only flock if not overcrowded
                    if (neighborCount < AntConfig.MaxClusterSize)
                    {
                        centerOfMass /= neighborCount;
                        Vector toCenter = centerOfMass - (Vector)Position;
                        toCenter.Normalize();
                        cohesionForce = toCenter;
                    }
                }
            }

            // Apply Weights
            totalForce += separationForce * AntConfig.SeparationWeight;
            if (BehaviorType == AntBehaviorType.Social)
            {
                totalForce += cohesionForce * AntConfig.CohesionWeight;
            }

            // 4. Trail Following
            if (_leaderAnt != null)
            {
                double leaderRad = MathUtils.DegreesToRadians(_leaderAnt.Rotation);
                Vector leaderForward = new Vector(Math.Cos(leaderRad), Math.Sin(leaderRad));
                Point targetPos = _leaderAnt.Position - leaderForward * AntConfig.TrailFollowDistance;
                
                Vector seek = targetPos - Position;
                double distToTarget = seek.Length;
                
                if (distToTarget > 1.0)
                {
                    seek.Normalize();
                    totalForce += seek * AntConfig.TrailSteeringWeight;
                }
                
                // Match speed
                if (_leaderAnt._moveState == AntMoveState.Moving)
                {
                    _targetSpeed = _leaderAnt._currentSpeed * (0.9 + _random.NextDouble() * 0.2);
                    _moveState = AntMoveState.Moving; // Ensure we keep moving
                    _stateTimer = 1.0; // Keep state valid
                }
            }

            // Convert total force to rotation adjustment
            if (totalForce.Length > 0.1)
            {
                double targetAngle = MathUtils.RadiansToDegrees(Math.Atan2(totalForce.Y, totalForce.X));
                
                // Blend current target rotation with new steering target
                double angleDiff = targetAngle - _targetRotation;
                while (angleDiff > 180) angleDiff -= 360;
                while (angleDiff < -180) angleDiff += 360;

                // Stronger influence to avoid collision
                _targetRotation += angleDiff * 1.0;
            }
        }

        public void StartInteraction()
        {
            _isInteracting = true;
            _interactionTimer = AntConfig.InteractionDuration + _random.NextDouble() * 0.5;
            _moveState = AntMoveState.Idle; // Stop moving
        }

        private Vector GetEdgeAttraction()
        {
            Vector force = new Vector(0, 0);

            double w = _canvas.ActualWidth;
            double h = _canvas.ActualHeight;
            double range = AntConfig.EdgePreferenceRange;

            // Check distance to 4 edges
            double distLeft = Position.X;
            double distRight = w - Position.X;
            double distTop = Position.Y;
            double distBottom = h - Position.Y;

            // If we are FAR from edges, pull towards nearest edge
            double minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

            if (minDist > range)
            {
                // Find nearest edge direction
                if (minDist == distLeft) force.X = -1;
                else if (minDist == distRight) force.X = 1;
                else if (minDist == distTop) force.Y = -1;
                else if (minDist == distBottom) force.Y = 1;
            }
            else
            {
                // We are near edge!
                // Instead of just doing nothing, let's walk ALONG the edge (Tangent)
                
                // Determine which edge we are near
                if (minDist == distLeft)
                {
                    // Near Left Edge. Tangent is (0, -1) or (0, 1). 
                    // Let's try to maintain current direction's sign
                    double headingY = Math.Sin(MathUtils.DegreesToRadians(Rotation));
                    force.Y = headingY > 0 ? 1 : -1;
                    if (distLeft < 20) force.X = 0.5; // Push away slightly if too close
                }
                else if (minDist == distRight)
                {
                    // Near Right Edge.
                    double headingY = Math.Sin(MathUtils.DegreesToRadians(Rotation));
                    force.Y = headingY > 0 ? 1 : -1;
                    if (distRight < 20) force.X = -0.5;
                }
                else if (minDist == distTop)
                {
                    // Near Top Edge.
                    double headingX = Math.Cos(MathUtils.DegreesToRadians(Rotation));
                    force.X = headingX > 0 ? 1 : -1;
                    if (distTop < 20) force.Y = 0.5;
                }
                else if (minDist == distBottom)
                {
                    // Near Bottom Edge.
                    double headingX = Math.Cos(MathUtils.DegreesToRadians(Rotation));
                    force.X = headingX > 0 ? 1 : -1;
                    if (distBottom < 20) force.Y = -0.5;
                }
                
                // Boost the tangent force
                force *= 2.0;
            }
            return force;
        }

        private void ResolveCollisions(List<ProceduralAnt> allAnts)
        {
            // Simple hard collision resolution
            // We check against other ants and if too close, we push away directly

            foreach (var other in allAnts)
            {
                if (other == this) continue;

                Vector toOther = other.Position - this.Position;
                double distSq = toOther.LengthSquared;
                double collisionRadSq = AntConfig.HardCollisionRadius * AntConfig.HardCollisionRadius;

                if (distSq < collisionRadSq)
                {
                    double dist = Math.Sqrt(distSq);
                    if (dist < 0.1) dist = 0.1; // Avoid div by zero

                    // Calculate overlap
                    double overlap = AntConfig.HardCollisionRadius - dist;
                    
                    // Direction to push away
                    Vector pushDir = toOther / dist; // Normalize
                    
                    // Push SELF away
                    Position -= pushDir * (overlap * 0.5);
                }
            }
        }

        private void UpdatePhysics(double deltaTime)
        {
            // Update rotation
            // Lerp rotation to target
            double angleDiff = _targetRotation - Rotation;
            // Normalize angle to -180 to 180
            while (angleDiff > 180) angleDiff -= 360;
            while (angleDiff < -180) angleDiff += 360;
            
            // Turn speed limit
            double turnAmount = AntConfig.TurnSpeed * deltaTime * _turnModifier;
            
            if (Math.Abs(angleDiff) < turnAmount)
            {
                Rotation = _targetRotation;
            }
            else
            {
                Rotation += Math.Sign(angleDiff) * turnAmount;
            }
            
            // Update speed
            // Accelerate/Decelerate
            double accel = AntConfig.MaxSpeed * 2.0 * deltaTime;
            if (_currentSpeed < _targetSpeed)
            {
                _currentSpeed += accel;
                if (_currentSpeed > _targetSpeed) _currentSpeed = _targetSpeed;
            }
            else if (_currentSpeed > _targetSpeed)
            {
                _currentSpeed -= accel;
                if (_currentSpeed < _targetSpeed) _currentSpeed = _targetSpeed;
            }
            
            // Move
            double rad = Rotation * Math.PI / 180.0;
            Vector velocity = new Vector(Math.Cos(rad), Math.Sin(rad)) * _currentSpeed;
            
            Position += velocity * deltaTime;
            
            // Keep in bounds (Steer away from edges instead of hard bounce)
             double padding = 50; // Start steering earlier
             double width = _canvas.ActualWidth;
             double height = _canvas.ActualHeight;
             
             // Soft boundary steering
             if (Position.X < padding)
             {
                 _targetRotation += 5.0; // Turn right
             }
             else if (Position.X > width - padding)
             {
                 _targetRotation += 5.0; // Turn right
             }
             
             if (Position.Y < padding)
             {
                 _targetRotation += 5.0;
             }
             else if (Position.Y > height - padding)
             {
                 _targetRotation += 5.0;
             }

             // Hard boundary clamp (failsafe)
             if (Position.X < 0) Position = new Point(0, Position.Y);
             if (Position.X > width) Position = new Point(width, Position.Y);
             if (Position.Y < 0) Position = new Point(Position.X, 0);
             if (Position.Y > height) Position = new Point(Position.X, height);
         }

        private void UpdateTrailLeader(List<ProceduralAnt> neighbors)
        {
            // 1. Validate current leader if exists
            if (_leaderAnt != null)
            {
                 Vector toLeader = _leaderAnt.Position - Position;
                 double distSq = toLeader.LengthSquared;
                 double maxDist = AntConfig.TrailFollowingRadius * 1.5; 
                 
                 // If leader is too far, or stopped, or we randomly decide to break
                 if (distSq > maxDist * maxDist || 
                     _leaderAnt._moveState == AntMoveState.Idle ||
                     _random.NextDouble() < AntConfig.TrailBreakProbability)
                 {
                     _leaderAnt = null; // Lost the trail
                 }
            }

            // 2. Try to find new leader if none
            if (_leaderAnt == null)
            {
                 // Probability check first to save perf
                 if (_random.NextDouble() < AntConfig.TrailFollowProbability)
                 {
                     double bestDistSq = double.MaxValue;
                     ProceduralAnt? bestCandidate = null;
                     
                     double rad = MathUtils.DegreesToRadians(Rotation);
                     Vector myForward = new Vector(Math.Cos(rad), Math.Sin(rad));
                     
                     foreach(var n in neighbors)
                     {
                         if (n == this) continue;
                         if (n._moveState == AntMoveState.Idle) continue;
                         if (n._leaderAnt == this) continue; // Don't follow someone following me (simple cycle break)
                         
                         Vector toN = n.Position - Position;
                         double distSq = toN.LengthSquared;
                         if (distSq > AntConfig.TrailFollowingRadius * AntConfig.TrailFollowingRadius) continue;
                         
                         // Must be in front of me (dot product > 0)
                         Vector dirToN = toN;
                         dirToN.Normalize();
                         if (Vector.Multiply(myForward, dirToN) < 0.5) continue; // Within ~60 deg cone in front
                         
                         // Must be moving in similar direction
                         double nRad = MathUtils.DegreesToRadians(n.Rotation);
                         Vector nForward = new Vector(Math.Cos(nRad), Math.Sin(nRad));
                         if (Vector.Multiply(myForward, nForward) < 0.5) continue; // Parallel-ish
                         
                         // Pick closest suitable leader
                         if (distSq < bestDistSq)
                         {
                             bestDistSq = distSq;
                             bestCandidate = n;
                         }
                     }
                     
                     if (bestCandidate != null)
                     {
                         _leaderAnt = bestCandidate;
                     }
                 }
            }
        }

        private void UpdateLegs()
        {
            if (_legs.Count == 0) return;

            double rad = Rotation * Math.PI / 180.0;
            Vector velocity = new Vector(Math.Cos(rad), Math.Sin(rad)) * _currentSpeed;

            // Gait
            // Tripod Gait: Legs 0, 4, 2 move together; 1, 3, 5 move together
            // Group A: 0, 4, 2 (Left Front, Right Mid, Left Back)
            // Group B: 1, 3, 5 (Left Mid, Right Front, Right Back)
            
            // Only update gait if moving
            if (_currentSpeed > 0.1)
            {
                // Smoother gait at slow speeds:
                // Increase multiplier to make legs move faster relative to body speed
                // This prevents "sliding" feeling
                _gaitTimer += AntConfig.LegUpdateInterval * (_currentSpeed / AntConfig.MaxSpeed) * 8.0; 
                
                // Add base speed so legs don't freeze at very low speeds
                if (_currentSpeed < AntConfig.MaxSpeed * 0.5)
                {
                    _gaitTimer += AntConfig.LegUpdateInterval * 0.8; // Increased base cadence
                }
                
                // Boost leg speed when panicking (high speed)
                if (_currentSpeed > AntConfig.MaxSpeed * 1.2)
                {
                    _gaitTimer += AntConfig.LegUpdateInterval * 2.0; // Extra boost for panic run
                }

                if (_gaitTimer > 1.0)
                {
                    _gaitTimer = 0;
                    _gaitPhase = 1 - _gaitPhase; // Toggle 0 or 1
                }
            }

            // Update each leg
            // Leg indices: 0:LF, 1:LM, 2:LB, 3:RF, 4:RM, 5:RB
            
            for (int i = 0; i < _legs.Count; i++)
            {
                bool canStep = false;
                
                // Determine if this leg belongs to the current active gait group
                // Group A (Phase 0): 0, 4, 2
                // Group B (Phase 1): 1, 3, 5
                
                if (_gaitPhase == 0)
                {
                    if (i == 0 || i == 4 || i == 2) canStep = true;
                }
                else
                {
                    if (i == 1 || i == 3 || i == 5) canStep = true;
                }
                
                _legs[i].Update(Position, Rotation, velocity, canStep);
            }
        }

        private void UpdateVisuals(double deltaTime)
        {
            // Position
            Canvas.SetLeft(_head, Position.X - AntConfig.HeadSize/2);
            Canvas.SetTop(_head, Position.Y - AntConfig.HeadSize/2);
            
            Canvas.SetLeft(_thorax, Position.X - AntConfig.ThoraxLength/2);
            Canvas.SetTop(_thorax, Position.Y - AntConfig.ThoraxWidth/2);
            
            Canvas.SetLeft(_abdomen, Position.X - AntConfig.AbdomenLength/2);
            Canvas.SetTop(_abdomen, Position.Y - AntConfig.AbdomenWidth/2);

            // Rotation
            var rotateTransform = new RotateTransform(Rotation, AntConfig.ThoraxLength/2, AntConfig.ThoraxWidth/2);
            // We need to rotate around the center of the ant (Thorax center)
            // But individual parts need local rotation + global position
            // Simpler: Just rotate the whole group if we had one.
            // Since we don't, we update RenderTransforms of parts.
            
            // Actually, we need to position parts relative to body center, then rotate.
            // Simplified for now: Just rotate parts around their own centers? No.
            // We need to calculate world positions of parts based on Body Rotation.
            
            UpdateBodyParts();
        }

        private void UpdateBodyParts()
        {
            // Calculate part positions based on body position and rotation
            // Center is Position
            
            // Re-calculate offsets based on actual sizes to ensure proper connection and overlap
            double headRadius = AntConfig.HeadSize / 2;
            double thoraxHalfLength = AntConfig.ThoraxLength / 2;
            double abdomenHalfLength = AntConfig.AbdomenLength / 2;
            
            // Overlap factor (0.8 means 20% overlap to hide gaps)
            double overlap = 0.8; 
            
            // Relative X positions (Head is +X, Abdomen is -X)
            double headOffsetX = (thoraxHalfLength + headRadius) * overlap;
            double abdomenOffsetX = -(thoraxHalfLength + abdomenHalfLength) * overlap;

            // Head is forward (positive X relative to body)
            Point headPos = GetWorldPosition(new Point(headOffsetX, 0));
            // Thorax is center
            Point thoraxPos = Position;
            // Abdomen is back
            Point abdomenPos = GetWorldPosition(new Point(abdomenOffsetX, 0));

            UpdatePart(_head, headPos, Rotation, AntConfig.HeadSize, AntConfig.HeadSize);
            UpdatePart(_thorax, thoraxPos, Rotation, AntConfig.ThoraxLength, AntConfig.ThoraxWidth);
            UpdatePart(_abdomen, abdomenPos, Rotation, AntConfig.AbdomenLength, AntConfig.AbdomenWidth);
        }

        private void UpdatePart(Ellipse part, Point center, double rotation, double width, double height)
        {
            if (part == null) return;
            
            // Position top-left
            Canvas.SetLeft(part, center.X - width/2);
            Canvas.SetTop(part, center.Y - height/2);
            
            // Rotation
            part.RenderTransform = new RotateTransform(rotation, width/2, height/2);
        }

        private Point GetWorldPosition(Point localPos)
        {
            // Rotate localPos by Rotation
            Vector rotated = MathUtils.RotateVector((Vector)localPos, Rotation);
            return Position + rotated;
        }

        // All leg logic removed for now.
    }
}
