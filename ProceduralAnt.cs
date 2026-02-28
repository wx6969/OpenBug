using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
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

        private readonly List<AntLeg> _legs = new List<AntLeg>();
        // private Path _legsPath = null!; // Removed single path optimization
        
        // Transform
        public Point Position { get; private set; }
        public double Rotation { get; private set; } // Degrees
        public bool IsVisible => _visible;
        public double SizeScale { get; }

        public Vector Forward
        {
            get
            {
                double rad = MathUtils.DegreesToRadians(Rotation);
                return new Vector(Math.Cos(rad), Math.Sin(rad));
            }
        }
        
        private double _targetRotation;
        private static readonly Random _random = new Random();
        private static int _nextId;
        public int Id { get; }
        private readonly bool _patrolClockwise;

        // Morandi Color Palette
        private static readonly List<SolidColorBrush> _morandiColors = new List<SolidColorBrush>
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
        private readonly System.Windows.Media.Pen _femurPen;
        private readonly System.Windows.Media.Pen _tibiaPen;
        private bool _visible = true;

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
        public double PheromoneDepositScale { get; private set; } = 1.0;

        static ProceduralAnt()
        {
            for (int i = 0; i < _morandiColors.Count; i++)
            {
                if (_morandiColors[i].CanFreeze) _morandiColors[i].Freeze();
            }
        }

        public ProceduralAnt(AntBehaviorType behaviorType, double vividness = 0.0)
        {
            Id = Interlocked.Increment(ref _nextId);
            BehaviorType = behaviorType;
            _patrolClockwise = _random.NextDouble() < 0.5;
            SizeScale = PickSizeScale();
            Position = new Point(400, 225); // Center of 800x450
            Rotation = 0;

            // Pick random Morandi color
            SolidColorBrush baseBrush = _morandiColors[_random.Next(_morandiColors.Count)];
            Color baseColor = baseBrush.Color;

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

            _femurPen = new System.Windows.Media.Pen(_antColor, 0.8 * SizeScale) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            _tibiaPen = new System.Windows.Media.Pen(_antColor, 0.6 * SizeScale) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            if (_femurPen.CanFreeze) _femurPen.Freeze();
            if (_tibiaPen.CanFreeze) _tibiaPen.Freeze();
            
            // Randomize personality
            _speedModifier = 0.8 + _random.NextDouble() * 0.4; // 0.8x to 1.2x
            _turnModifier = 0.8 + _random.NextDouble() * 0.4;  // 0.8x to 1.2x
            _stateDurationModifier = 0.8 + _random.NextDouble() * 0.5; // 0.8x to 1.3x

            if (BehaviorType == AntBehaviorType.Loner)
            {
                _speedModifier *= 1.05;
                _turnModifier *= 1.15;
                _stateDurationModifier *= 0.85;
                PheromoneDepositScale = 1.6;
            }
            else if (BehaviorType == AntBehaviorType.Social)
            {
                _speedModifier *= 0.95;
                _turnModifier *= 0.9;
                _stateDurationModifier *= 1.05;
                PheromoneDepositScale = 1.0;
            }
            else
            {
                _speedModifier *= 0.9;
                _turnModifier *= 0.95;
                _stateDurationModifier *= 1.1;
                PheromoneDepositScale = 0.25;
            }

            if (SizeScale < 1.0)
            {
                _speedModifier *= 1.0 + (1.0 - SizeScale) * 0.35;
                _turnModifier *= 1.0 + (1.0 - SizeScale) * 0.20;
            }

            _moveState = AntMoveState.Moving;
            _stateTimer = 0.5 + _random.NextDouble() * 1.5;
            
            _currentSpeed = _moveState == AntMoveState.Moving ? AntConfig.MaxSpeed * _speedModifier : 0;
            _targetSpeed = _currentSpeed;
        }

        private static double PickSizeScale()
        {
            double r = _random.NextDouble();
            if (r < 0.20) return 0.70;
            if (r < 0.70) return 0.85;
            return 1.0;
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
            _visible = visible;
        }

        public void Draw(DrawingContext drawingContext)
        {
            if (!_visible) return;

            for (int i = 0; i < _legs.Count; i++)
            {
                _legs[i].Draw(drawingContext, _femurPen, _tibiaPen);
            }

            DrawBody(drawingContext);
        }

        private void DrawBody(DrawingContext drawingContext)
        {
            double headSize = AntConfig.HeadSize * SizeScale;
            double thoraxLength = AntConfig.ThoraxLength * SizeScale;
            double thoraxWidth = AntConfig.ThoraxWidth * SizeScale;
            double abdomenLength = AntConfig.AbdomenLength * SizeScale;
            double abdomenWidth = AntConfig.AbdomenWidth * SizeScale;

            double headRadius = headSize / 2;
            double thoraxHalfLength = thoraxLength / 2;
            double abdomenHalfLength = abdomenLength / 2;

            double overlap = 0.8;
            double headOffsetX = (thoraxHalfLength + headRadius) * overlap;
            double abdomenOffsetX = -(thoraxHalfLength + abdomenHalfLength) * overlap;

            Point headPos = GetWorldPosition(new Point(headOffsetX, 0));
            Point thoraxPos = Position;
            Point abdomenPos = GetWorldPosition(new Point(abdomenOffsetX, 0));

            DrawRotatedEllipse(drawingContext, headPos, headSize, headSize);
            DrawRotatedEllipse(drawingContext, thoraxPos, thoraxLength, thoraxWidth);
            DrawRotatedEllipse(drawingContext, abdomenPos, abdomenLength, abdomenWidth);
        }

        private void DrawRotatedEllipse(DrawingContext drawingContext, Point center, double width, double height)
        {
            drawingContext.PushTransform(new RotateTransform(Rotation, center.X, center.Y));
            drawingContext.DrawEllipse(_antColor, null, center, width / 2, height / 2);
            drawingContext.Pop();
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
            _legs.Add(new AntLeg(new Point(xFront, -yOffset), new Vector(reachX, -reachY), false));
            // Leg 1: Left Mid
            _legs.Add(new AntLeg(new Point(xMid, -yOffset), new Vector(0, -reachY * 1.2), false));
            // Leg 2: Left Back
            _legs.Add(new AntLeg(new Point(xBack, -yOffset), new Vector(-reachX, -reachY), false));
            
            // Leg 3: Right Front
            _legs.Add(new AntLeg(new Point(xFront, yOffset), new Vector(reachX, reachY), true));
            // Leg 4: Right Mid
            _legs.Add(new AntLeg(new Point(xMid, yOffset), new Vector(0, reachY * 1.2), true));
            // Leg 5: Right Back
            _legs.Add(new AntLeg(new Point(xBack, yOffset), new Vector(-reachX, reachY), true));

            for (int i = 0; i < _legs.Count; i++)
            {
                _legs[i].SetScale(SizeScale);
            }
        }

        private double _interactionTimer = 0;
        private double _interactionCooldownTimer = 0;
        private bool _isInteracting = false;
        private ProceduralAnt? _interactionPartner;
        private ProceduralAnt? _lastInteractionPartner;
        private double _samePartnerCooldownTimer = 0;
        private bool _swarmActive;
        
        // Trail Following State
        private ProceduralAnt? _leaderAnt;
        private double _trailCheckTimer = 0;

        // Lazy State
        private bool _isLazy = false;

        public void Update(double deltaTime, List<ProceduralAnt> neighbors, Point cursorPos, double worldWidth, double worldHeight, PheromoneField? pheromones, bool swarmActive)
        {
            _swarmActive = swarmActive && BehaviorType == AntBehaviorType.Social;
            UpdateBehavior(deltaTime, neighbors, cursorPos, worldWidth, worldHeight, pheromones);
            UpdatePhysics(deltaTime, worldWidth, worldHeight, neighbors);
            
            _legUpdateTimer += deltaTime;
            if (_legUpdateTimer >= AntConfig.LegUpdateInterval)
            {
                UpdateLegs();
                _legUpdateTimer = 0;
            }
        }

        private void UpdateBehavior(double deltaTime, List<ProceduralAnt> neighbors, Point cursorPos, double worldWidth, double worldHeight, PheromoneField? pheromones)
        {
            // Interaction Logic
            if (_interactionCooldownTimer > 0) _interactionCooldownTimer -= deltaTime;
            if (_samePartnerCooldownTimer > 0) _samePartnerCooldownTimer -= deltaTime;

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

                    if (_interactionPartner != null)
                    {
                        Vector away = Position - _interactionPartner.Position;
                        if (away.LengthSquared > 0.0001)
                        {
                            double angle = MathUtils.RadiansToDegrees(Math.Atan2(away.Y, away.X));
                            _targetRotation = angle + (_random.NextDouble() * 30 - 15);
                        }
                    }

                    _interactionPartner = null;
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
                    double stopProb = 0.12;
                    if (BehaviorType == AntBehaviorType.Loner) stopProb = 0.18;
                    else if (BehaviorType == AntBehaviorType.EdgeDweller) stopProb = 0.10;

                    if (_random.NextDouble() < stopProb)
                    {
                        _moveState = AntMoveState.Idle;
                        double idleMean = BehaviorType == AntBehaviorType.Loner ? 0.55 : 0.28;
                        _stateTimer = Clamp(SampleExp(idleMean) * _stateDurationModifier, 0.06, 1.6);
                        _targetSpeed = 0.0;
                    }
                    else
                    {
                        _moveState = AntMoveState.Moving;
                        double moveMean = BehaviorType == AntBehaviorType.Social ? 2.2 : (BehaviorType == AntBehaviorType.Loner ? 1.0 : 2.8);
                        _stateTimer = Clamp(SampleExp(moveMean) * _stateDurationModifier, 0.35, 7.5);
                        _targetSpeed = AntConfig.MaxSpeed * PickSpeedFactor() * _speedModifier;
                    }
                }
                else
                {
                    _moveState = AntMoveState.Moving;
                    double moveMean = BehaviorType == AntBehaviorType.Social ? 2.4 : (BehaviorType == AntBehaviorType.Loner ? 1.2 : 3.0);
                    _stateTimer = Clamp(SampleExp(moveMean) * _stateDurationModifier, 0.35, 8.0);
                    double speedFactor = Math.Max(0.25, PickSpeedFactor());
                    _targetSpeed = AntConfig.MaxSpeed * speedFactor * _speedModifier;
                }
            }

            _wanderTimer -= deltaTime;
            if (_wanderTimer <= 0)
            {
                double minT = 1.0;
                double maxT = 2.8;
                double baseRange = 70;
                if (BehaviorType == AntBehaviorType.Social)
                {
                    minT = 1.4; maxT = 3.8; baseRange = 45;
                }
                else if (BehaviorType == AntBehaviorType.Loner)
                {
                    minT = 0.35; maxT = 1.4; baseRange = 140;
                }
                else
                {
                    minT = 0.8; maxT = 2.2; baseRange = 55;
                }

                _wanderTimer = minT + _random.NextDouble() * (maxT - minT);
                double turnRange = baseRange * _turnModifier;
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
            ApplySteeringForces(neighbors, cursorPos, worldWidth, worldHeight, pheromones);
        }

        private static double SampleExp(double mean)
        {
            if (mean <= 0) return 0;
            double u = 1.0 - _random.NextDouble();
            return -Math.Log(u) * mean;
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private double PickSpeedFactor()
        {
            double r = _random.NextDouble();
            if (BehaviorType == AntBehaviorType.Social)
            {
                if (r < 0.06) return 0.10 + _random.NextDouble() * 0.20;
                if (r < 0.88) return 0.45 + _random.NextDouble() * 0.35;
                return 0.80 + _random.NextDouble() * 0.20;
            }

            if (BehaviorType == AntBehaviorType.Loner)
            {
                if (r < 0.18) return 0.05 + _random.NextDouble() * 0.25;
                if (r < 0.62) return 0.25 + _random.NextDouble() * 0.40;
                return 0.70 + _random.NextDouble() * 0.30;
            }

            if (r < 0.08) return 0.10 + _random.NextDouble() * 0.25;
            if (r < 0.86) return 0.40 + _random.NextDouble() * 0.40;
            return 0.75 + _random.NextDouble() * 0.25;
        }

        private void ApplySteeringForces(List<ProceduralAnt> neighbors, Point cursorPos, double worldWidth, double worldHeight, PheromoneField? pheromones)
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
            
            bool cursorPanicking = distToCursorSq < cursorRepulsionRadSq;
            if (cursorPanicking)
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

            double separationWeight = AntConfig.SeparationWeight;
            double cohesionWeight = AntConfig.CohesionWeight;
            double alignmentWeight = AntConfig.AlignmentWeight;
            double pheromoneWeightBase = AntConfig.PheromoneFollowWeight;
            double trailWeight = AntConfig.TrailSteeringWeight;
            double edgeWeight = AntConfig.EdgeAttractionWeight;
            double interactionChance = 0.004;

            bool swarmActive = _swarmActive;

            if (BehaviorType == AntBehaviorType.Social)
            {
                cohesionWeight *= 0.22;
                alignmentWeight *= 0.30;
                pheromoneWeightBase *= 0.22;
                trailWeight *= 0.22;
                separationWeight *= 1.00;
                interactionChance = 0.0012;

                if (swarmActive)
                {
                    cohesionWeight *= 3.0;
                    alignmentWeight *= 2.4;
                    pheromoneWeightBase *= 3.2;
                    trailWeight *= 3.0;
                    separationWeight *= 0.85;
                    interactionChance = 0.008;
                }
            }
            else if (BehaviorType == AntBehaviorType.Loner)
            {
                pheromoneWeightBase *= 0.35;
                separationWeight *= 1.15;
                interactionChance = 0.006;
            }
            else
            {
                cohesionWeight *= 0.5;
                alignmentWeight *= 0.55;
                trailWeight *= 0.35;
                edgeWeight *= 1.15;
                separationWeight *= 1.05;
                interactionChance = 0.004;
            }

            // 3. Edge Attraction (Only for Edge Dwellers)
            if (BehaviorType == AntBehaviorType.EdgeDweller)
            {
                Vector edgeForce = GetEdgeAttraction(worldWidth, worldHeight);
                totalForce += edgeForce * edgeWeight;
            }

            // 2. Separation & Cohesion & Interaction
            Vector separationForce = new Vector(0, 0);
            Vector cohesionForce = new Vector(0, 0);
            Vector alignmentForce = new Vector(0, 0);
            int neighborCount = 0;
            Vector centerOfMass = new Vector(0, 0);
            Vector headingSum = new Vector(0, 0);
            int movingNeighborCount = 0;

            // Optimization: neighbors list is now provided by SpatialGrid (already spatially filtered)
            
            // Check for Laziness conditions:
            // 1. Not already panicked (cursor check done above)
            // 2. Near edge/corner
            // 3. High neighbor count (cluster)
            
            double w = worldWidth;
            double h = worldHeight;
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
                        if ((_lastInteractionPartner != other || _samePartnerCooldownTimer <= 0) &&
                            (other._lastInteractionPartner != this || other._samePartnerCooldownTimer <= 0) &&
                            _random.NextDouble() < interactionChance)
                        {
                            StartInteraction(other);
                            other.StartInteraction(this);
                        }
                    }

                    // Cohesion: Accumulate position
                    centerOfMass += (Vector)other.Position;
                    neighborCount++;

                    if (other._moveState == AntMoveState.Moving && other._currentSpeed > 0.1)
                    {
                        headingSum += other.Forward;
                        movingNeighborCount++;
                    }

                    // Separation: Push away if too close
                    double sepRad = AntConfig.SeparationRadius * 0.5 * (SizeScale + other.SizeScale);
                    if (dist < sepRad)
                    {
                        // Stronger push when closer
                        Vector push = -toOther;
                        push.Normalize();
                        // Exponential separation force for "hard" collision avoidance
                        separationForce += push * (1.0 - dist / sepRad) * 2.0;
                    }
                }
            }

            // Lazy Update Logic
            if (BehaviorType == AntBehaviorType.EdgeDweller && nearEdge && neighborCount >= AntConfig.LazyNeighborThreshold)
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

            if (!cursorPanicking && BehaviorType != AntBehaviorType.EdgeDweller)
            {
                double band = 95.0;
                if (distToEdge < band)
                {
                    double t = 1.0 - distToEdge / band;
                    Vector toCenter = new Vector(worldWidth * 0.5 - Position.X, worldHeight * 0.5 - Position.Y);
                    if (toCenter.LengthSquared > 0.0001)
                    {
                        toCenter.Normalize();
                        totalForce += toCenter * (0.35 * t);
                    }
                }
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
                        double distToCenter = toCenter.Length;
                        if (distToCenter > 0.0001)
                        {
                            double desired = AntConfig.SeparationRadius * 2.6 * SizeScale;
                            double max = AntConfig.PerceptionRadius;
                            if (desired > max - 1) desired = max - 1;
                            double signed = (distToCenter - desired) / (max - desired);
                            signed = Clamp(signed, -1.0, 1.0);
                            cohesionForce = (toCenter / distToCenter) * signed;
                        }
                    }
                }
            }

            if (movingNeighborCount > 0 && headingSum.LengthSquared > 0.001)
            {
                double strength = headingSum.Length / movingNeighborCount;
                headingSum.Normalize();
                alignmentForce = headingSum * strength;
            }

            if (!nearEdge && neighborCount >= 2)
            {
                double crowdT = (neighborCount - 2) / 12.0;
                if (crowdT < 0) crowdT = 0;
                if (crowdT > 1) crowdT = 1;

                cohesionForce *= (1.0 - 0.85 * crowdT);
                alignmentForce *= (1.0 - 0.70 * crowdT);
                pheromoneWeightBase *= (1.0 - 0.75 * crowdT);
                trailWeight *= (1.0 - 0.85 * crowdT);
                separationWeight *= (1.0 + 0.45 * crowdT);
            }

            // Apply Weights
            totalForce += separationForce * separationWeight;
            if (BehaviorType == AntBehaviorType.Social)
            {
                totalForce += cohesionForce * cohesionWeight;
            }
            if (BehaviorType != AntBehaviorType.Loner)
            {
                totalForce += alignmentForce * alignmentWeight;
            }

            if (!cursorPanicking && pheromones != null && BehaviorType != AntBehaviorType.EdgeDweller)
            {
                var sample = pheromones.Sample(Position);
                if (sample.strength > 0)
                {
                    double edgeBand = 95.0;
                    double edgeScale = distToEdge < edgeBand ? (distToEdge / edgeBand) : 1.0;
                    totalForce += sample.direction * pheromoneWeightBase * edgeScale;
                }
            }

            // 4. Trail Following
            if (_leaderAnt != null)
            {
                Vector toLeader0 = _leaderAnt.Position - Position;
                double tooClose = AntConfig.TrailFollowDistance * 0.7;
                if (toLeader0.LengthSquared < tooClose * tooClose)
                {
                    _leaderAnt = null;
                }
            }

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
                    totalForce += seek * trailWeight;
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

        public void StartInteraction(ProceduralAnt partner)
        {
            _leaderAnt = null;
            _isInteracting = true;
            _interactionPartner = partner;
            _lastInteractionPartner = partner;
            _samePartnerCooldownTimer = 15.0 + _random.NextDouble() * 10.0;
            _interactionTimer = AntConfig.InteractionDuration + _random.NextDouble() * 0.5;
            _moveState = AntMoveState.Idle; // Stop moving
        }

        private Vector GetEdgeAttraction(double worldWidth, double worldHeight)
        {
            Vector force = new Vector(0, 0);

            double w = worldWidth;
            double h = worldHeight;
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
                    force.Y = _patrolClockwise ? 1 : -1;
                    if (distLeft < 20) force.X = 0.5; // Push away slightly if too close
                }
                else if (minDist == distRight)
                {
                    force.Y = _patrolClockwise ? 1 : -1;
                    if (distRight < 20) force.X = -0.5;
                }
                else if (minDist == distTop)
                {
                    force.X = _patrolClockwise ? 1 : -1;
                    if (distTop < 20) force.Y = 0.5;
                }
                else if (minDist == distBottom)
                {
                    force.X = _patrolClockwise ? 1 : -1;
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
                double collisionRad = AntConfig.HardCollisionRadius * 0.5 * (SizeScale + other.SizeScale);
                double collisionRadSq = collisionRad * collisionRad;

                if (distSq < collisionRadSq)
                {
                    double dist = Math.Sqrt(distSq);
                    if (dist < 0.1) dist = 0.1; // Avoid div by zero

                    // Calculate overlap
                    double overlap = collisionRad - dist;
                    
                    // Direction to push away
                    Vector pushDir = toOther / dist; // Normalize
                    
                    // Push SELF away
                    Position -= pushDir * (overlap * 0.5);
                }
            }
        }

        private void UpdatePhysics(double deltaTime, double worldWidth, double worldHeight, List<ProceduralAnt> neighbors)
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

            ResolveCollisions(neighbors);
            
            // Keep in bounds (Steer away from edges instead of hard bounce)
             double padding = 50; // Start steering earlier
             double width = worldWidth;
             double height = worldHeight;
             
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
            double followProb = AntConfig.TrailFollowProbability;
            double breakProb = AntConfig.TrailBreakProbability;

            if (BehaviorType == AntBehaviorType.Social)
            {
                followProb *= 0.6;
                breakProb *= 1.1;

                if (_swarmActive)
                {
                    followProb *= 6.0;
                    breakProb *= 0.55;
                }
            }
            else if (BehaviorType == AntBehaviorType.Loner)
            {
                followProb *= 0.6;
                breakProb *= 1.8;
            }
            else
            {
                followProb *= 0.25;
                breakProb *= 1.4;
            }

            if (neighbors.Count >= 8)
            {
                double crowdT = (neighbors.Count - 8) / 10.0;
                if (crowdT < 0) crowdT = 0;
                if (crowdT > 1) crowdT = 1;
                followProb *= (1.0 - 0.75 * crowdT);
            }

            // 1. Validate current leader if exists
            if (_leaderAnt != null)
            {
                 Vector toLeader = _leaderAnt.Position - Position;
                 double distSq = toLeader.LengthSquared;
                 double maxDist = AntConfig.TrailFollowingRadius * 1.5; 
                 
                 // If leader is too far, or stopped, or we randomly decide to break
                 if (distSq > maxDist * maxDist || 
                     _leaderAnt._moveState == AntMoveState.Idle ||
                     _random.NextDouble() < breakProb)
                 {
                     _leaderAnt = null; // Lost the trail
                 }
            }

            // 2. Try to find new leader if none
            if (_leaderAnt == null)
            {
                 // Probability check first to save perf
                 if (_random.NextDouble() < followProb)
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
                         if (n._leaderAnt != null) continue;
                         if (n.Id >= Id) continue;
                         
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

        private Point GetWorldPosition(Point localPos)
        {
            // Rotate localPos by Rotation
            Vector rotated = MathUtils.RotateVector((Vector)localPos, Rotation);
            return Position + rotated;
        }
    }
}
