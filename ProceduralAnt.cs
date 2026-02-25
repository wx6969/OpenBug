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
            // Thorax Length 4.0, Width 2.5
            // 3 pairs
            // Offsets relative to body center (which is center of Thorax)
            
            double xFront = 1.0;
            double xMid = 0.0;
            double xBack = -1.0;
            
            double yOffset = 0.5; // Attach slightly off center
            
            // Feet targets (ideal)
            // Front: Forward + Out
            // Mid: Out
            // Back: Back + Out
            double reachX = 3.5;
            double reachY = 5.0;

            // Leg 0: Left Front
            _legs.Add(new AntLeg(_canvas, new Point(xFront, -yOffset), new Vector(reachX, -reachY), false, _antColor));
            // Leg 1: Left Mid
            _legs.Add(new AntLeg(_canvas, new Point(xMid, -yOffset), new Vector(0, -reachY * 1.1), false, _antColor));
            // Leg 2: Left Back
            _legs.Add(new AntLeg(_canvas, new Point(xBack, -yOffset), new Vector(-reachX * 0.8, -reachY * 1.2), false, _antColor));
            
            // Leg 3: Right Front
            _legs.Add(new AntLeg(_canvas, new Point(xFront, yOffset), new Vector(reachX, reachY), true, _antColor));
            // Leg 4: Right Mid
            _legs.Add(new AntLeg(_canvas, new Point(xMid, yOffset), new Vector(0, reachY * 1.1), true, _antColor));
            // Leg 5: Right Back
            _legs.Add(new AntLeg(_canvas, new Point(xBack, yOffset), new Vector(-reachX * 0.8, reachY * 1.2), true, _antColor));
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
            UpdateLegs(deltaTime);
            UpdateBodyParts();
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

            double rotDiff = _targetRotation - Rotation;
            while (rotDiff > 180) rotDiff -= 360;
            while (rotDiff < -180) rotDiff += 360;

            // Apply turn speed modifier
            double currentTurnSpeed = AntConfig.TurnSpeed * _turnModifier;
            double turnAmount = Math.Sign(rotDiff) * Math.Min(Math.Abs(rotDiff), currentTurnSpeed * deltaTime);
            Rotation += turnAmount;

            // Sharper speed changes: big interpolation factor, clamped to 1
            double t = deltaTime * 12.0;
            if (t > 1.0) t = 1.0;
            _currentSpeed = MathUtils.Lerp(_currentSpeed, _targetSpeed, t);

            double speed = _currentSpeed;
            double rad = MathUtils.DegreesToRadians(Rotation);
            Vector forward = new Vector(Math.Cos(rad), Math.Sin(rad));
            Position += forward * speed * deltaTime;

            // Boundary Check: Bounce
            HandleBoundaries();
        }

        private void ApplySteeringForces(List<ProceduralAnt> neighbors, Point cursorPos)
        {
            Vector totalForce = new Vector(0, 0);

            // 1. Cursor Repulsion (Highest Priority fear)
            Vector toCursor = cursorPos - Position;
            double distToCursorSq = toCursor.LengthSquared;
            double cursorRepulsionRadSq = AntConfig.CursorRepulsionRadius * AntConfig.CursorRepulsionRadius;
            
            if (distToCursorSq < cursorRepulsionRadSq)
            {
                // Panic!
                _isLazy = false;
                
                double dist = Math.Sqrt(distToCursorSq);
                Vector fleeDir = -toCursor;
                fleeDir.Normalize();
                // Strong exponential repulsion
                totalForce += fleeDir * AntConfig.CursorRepulsionStrength * (1.0 - dist / AntConfig.CursorRepulsionRadius) * 5.0;
                
                // If very close to cursor, force move state
                if (dist < AntConfig.CursorRepulsionRadius * 0.5)
                {
                    _moveState = AntMoveState.Moving;
                    _currentSpeed = AntConfig.MaxSpeed * 1.5; // Panic speed
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
                        // 5% chance to stop and interact per frame if close
                        if (_random.NextDouble() < 0.05)
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

        private void HandleBoundaries()
        {
            bool bounced = false;
            if (Position.X < 0) 
            {
                Position = new Point(0, Position.Y);
                double rad = MathUtils.DegreesToRadians(Rotation);
                double dx = Math.Cos(rad);
                double dy = Math.Sin(rad);
                dx = Math.Abs(dx); // Bounce right
                Rotation = MathUtils.RadiansToDegrees(Math.Atan2(dy, dx));
                bounced = true;
            }
            else if (Position.X > _canvas.ActualWidth)
            {
                Position = new Point(_canvas.ActualWidth, Position.Y);
                double rad = MathUtils.DegreesToRadians(Rotation);
                double dx = Math.Cos(rad);
                double dy = Math.Sin(rad);
                dx = -Math.Abs(dx); // Bounce left
                Rotation = MathUtils.RadiansToDegrees(Math.Atan2(dy, dx));
                bounced = true;
            }

            if (Position.Y < 0)
            {
                Position = new Point(Position.X, 0);
                double rad = MathUtils.DegreesToRadians(Rotation);
                double dx = Math.Cos(rad);
                double dy = Math.Sin(rad);
                dy = Math.Abs(dy); // Bounce down
                Rotation = MathUtils.RadiansToDegrees(Math.Atan2(dy, dx));
                bounced = true;
            }
            else if (Position.Y > _canvas.ActualHeight)
            {
                Position = new Point(Position.X, _canvas.ActualHeight);
                double rad = MathUtils.DegreesToRadians(Rotation);
                double dx = Math.Cos(rad);
                double dy = Math.Sin(rad);
                dy = -Math.Abs(dy); // Bounce up
                Rotation = MathUtils.RadiansToDegrees(Math.Atan2(dy, dx));
                bounced = true;
            }

            if (bounced)
            {
                _targetRotation = Rotation;
            }
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

        private void UpdateLegs(double deltaTime)
        {
            _legUpdateTimer += deltaTime;
            if (_legUpdateTimer < AntConfig.LegUpdateInterval)
            {
                return;
            }
            double legDeltaTime = _legUpdateTimer;
            _legUpdateTimer = 0;

            if (_moveState == AntMoveState.Moving)
            {
                _gaitTimer += legDeltaTime;
                double phaseDuration = AntConfig.StepDuration * 1.5; 
                
                if (_gaitTimer > phaseDuration)
                {
                    _gaitTimer = 0;
                    _gaitPhase = 1 - _gaitPhase;
                }
            }

            double rad = MathUtils.DegreesToRadians(Rotation);
            Vector velocity = new Vector(Math.Cos(rad), Math.Sin(rad)) * _currentSpeed;

            for (int i = 0; i < _legs.Count; i++)
            {
                bool allowed = false;
                
                if (_moveState == AntMoveState.Idle)
                {
                    // Allow small readjustments if really needed
                    allowed = true; 
                }
                else
                {
                    if (_gaitPhase == 0)
                    {
                        if (i == 0 || i == 4 || i == 2) allowed = true;
                    }
                    else
                    {
                        if (i == 3 || i == 1 || i == 5) allowed = true;
                    }
                }

                _legs[i].Update(Position, Rotation, velocity, allowed);
            }
        }

        private void UpdateBodyParts()
        {
            // Calculate offsets based on actual sizes to ensure proper connection
            double headRadius = AntConfig.HeadSize / 2;
            double thoraxHalfLength = AntConfig.ThoraxLength / 2;
            double abdomenHalfLength = AntConfig.AbdomenLength / 2;
            
            // Overlap factor (0.8 means 20% overlap)
            double overlap = 0.8;
            
            double headOffset = (thoraxHalfLength + headRadius) * overlap;
            double abdomenOffset = -(thoraxHalfLength + abdomenHalfLength) * overlap;

            UpdatePart(_head, new Point(headOffset, 0));
            UpdatePart(_thorax, new Point(0, 0));
            UpdatePart(_abdomen, new Point(abdomenOffset, 0));
        }

        private void UpdatePart(Ellipse part, Point localOffset)
        {
            Point worldPos = GetWorldPosition(localOffset);
            
            // Rotate part itself
            part.RenderTransform = new RotateTransform(Rotation, part.Width/2, part.Height/2);
            
            Canvas.SetLeft(part, worldPos.X - part.Width / 2);
            Canvas.SetTop(part, worldPos.Y - part.Height / 2);
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
