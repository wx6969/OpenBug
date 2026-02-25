using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace OpenAnt
{
    public class AntLeg
    {
        private Canvas _canvas;
        private Line _femur;
        private Line _tibia;

        // Configuration
        private Point _bodyAttachmentOffset; 
        private Vector _defaultFootOffset;   
        private double _segment1Length;
        private double _segment2Length;
        private bool _isRightSide;

        // State
        public Point CurrentFootPosition { get; private set; } // World space
        public Point CurrentKneePosition { get; private set; } // World space
        public Point CurrentRootPosition { get; private set; } // World space
        public bool IsStepping { get; private set; } = false;

        private Point _stepStartPos;
        private Point _stepTargetPos;
        private double _stepProgress = 0; // 0 to 1

        public AntLeg(Canvas canvas, Point bodyAttachmentOffset, Vector defaultFootOffset, bool isRightSide, Brush legColor)
        {
            _canvas = canvas;
            _bodyAttachmentOffset = bodyAttachmentOffset;
            _defaultFootOffset = defaultFootOffset;
            _isRightSide = isRightSide;
            
            _segment1Length = AntConfig.LegSegment1Length;
            _segment2Length = AntConfig.LegSegment2Length;
            
            // Initialize visuals
            _femur = new Line
            {
                Stroke = legColor,
                StrokeThickness = 0.8,
                StrokeEndLineCap = PenLineCap.Flat,
                StrokeStartLineCap = PenLineCap.Flat
            };
            _tibia = new Line
            {
                Stroke = legColor,
                StrokeThickness = 0.6,
                StrokeEndLineCap = PenLineCap.Flat,
                StrokeStartLineCap = PenLineCap.Flat
            };

            // Add behind body
            _canvas.Children.Add(_femur);
            _canvas.Children.Add(_tibia);
        }

        public void SetVisibility(bool visible)
        {
            Visibility v = visible ? Visibility.Visible : Visibility.Collapsed;
            if (_femur != null) _femur.Visibility = v;
            if (_tibia != null) _tibia.Visibility = v;
        }

        public void InitializePosition(Point bodyPos, double rotation)
        {
            Point root = GetWorldAttachment(bodyPos, rotation);
            Point target = GetIdealFootPosition(bodyPos, rotation);
            CurrentFootPosition = target;
            SolveIK(root, CurrentFootPosition);
        }

        public void Update(Point bodyPos, double bodyRotation, Vector bodyVelocity, bool canStep)
        {
            Point root = GetWorldAttachment(bodyPos, bodyRotation);
            Point idealFoot = GetIdealFootPosition(bodyPos, bodyRotation);

            if (IsStepping)
            {
                _stepProgress += (1.0 / AntConfig.StepDuration) * 0.016; // Approx delta time
                
                if (_stepProgress >= 1.0)
                {
                    _stepProgress = 1.0;
                    IsStepping = false;
                    CurrentFootPosition = _stepTargetPos;
                }
                else
                {
                    // Interpolate
                    Vector moveVec = _stepTargetPos - _stepStartPos;
                    Point horizPos = _stepStartPos + moveVec * _stepProgress;

                    CurrentFootPosition = horizPos;
                }
            }
            else
            {
                // Check if we need to step
                if (canStep)
                {
                    double distSq = (idealFoot - CurrentFootPosition).LengthSquared;
                    if (distSq > AntConfig.StepTriggerThreshold * AntConfig.StepTriggerThreshold)
                    {
                        StartStep(idealFoot, bodyVelocity);
                    }
                }
            }

            SolveIK(root, CurrentFootPosition);
        }

        private void StartStep(Point idealTarget, Vector bodyVelocity)
        {
            IsStepping = true;
            _stepProgress = 0;
            _stepStartPos = CurrentFootPosition;
            
            // Predict movement
            // Aim for where the ideal foot position will be when the stance phase is halfway?
            // Or aim ahead so that we land "in front" and let body catch up.
            // Let's aim ahead by velocity * 0.25s (approx stance time)
            // Or just use a fixed "stride ahead" factor.
            
            // If velocity is high, plant further ahead.
            _stepTargetPos = idealTarget + bodyVelocity * 0.2; 
        }

        private Point GetWorldAttachment(Point bodyPos, double rotation)
        {
            Vector offset = (Vector)_bodyAttachmentOffset;
            Vector rotated = MathUtils.RotateVector(offset, rotation);
            return bodyPos + rotated;
        }

        private Point GetIdealFootPosition(Point bodyPos, double rotation)
        {
            // The foot wants to be at Attachment + DefaultOffset (rotated)
            Vector totalOffset = (Vector)(_bodyAttachmentOffset) + _defaultFootOffset;
            Vector rotated = MathUtils.RotateVector(totalOffset, rotation);
            return bodyPos + rotated;
        }

        private void SolveIK(Point root, Point target)
        {
            // 2-Bone IK
            // Root -> Knee -> Target
            // Lengths: a = _segment1Length, b = _segment2Length
            // c = distance(Root, Target)

            Vector toTarget = target - root;
            double c = toTarget.Length;

            // Clamp reach
            if (c > _segment1Length + _segment2Length)
            {
                c = _segment1Length + _segment2Length;
                toTarget.Normalize();
                target = root + toTarget * c;
            }

            // Law of Cosines
            // a^2 + c^2 - 2ac cos(B) = b^2
            // cos(B) = (a^2 + c^2 - b^2) / (2ac)
            // B is angle at Root between Target and Knee

            double cosAngle = (_segment1Length * _segment1Length + c * c - _segment2Length * _segment2Length) / (2 * _segment1Length * c);
            
            // Clamp for safety
            if (cosAngle < -1) cosAngle = -1;
            if (cosAngle > 1) cosAngle = 1;

            double angleRad = Math.Acos(cosAngle);
            
            // Direction to target
            double targetAngleRad = Math.Atan2(toTarget.Y, toTarget.X);

            // Knee angle: TargetAngle +/- angleRad
            // For right legs, we usually want knee to bend "backward" or "outward"?
            // Insects: 
            // Front legs: knees forward?
            // Middle/Back: knees backward?
            // Let's assume standard spider/ant style: knee points UP/OUT.
            // In 2D top down, "Knee" is usually "Out".
            // If Right side, we want Knee to be "Left" of the Target vector? No, "Right" is "Down/Right" on screen.
            // Let's try adding angle for one side, subtracting for other.
            
            double kneeAngleRad = _isRightSide ? (targetAngleRad - angleRad) : (targetAngleRad + angleRad);
            
            // However, "Right Side" implies Y is larger? Or X is larger?
            // Depends on facing.
            // Let's rely on the explicit "isRightSide" flag passed in.
            // Actually, for insect legs, the "Elbow" usually points AWAY from the body.
            // So if toTarget is pointing away from body, we want the knee to be even further away?
            // Wait, triangle: Root, Knee, Target.
            // If we are looking top down.
            // Let's just try one direction (add) and if it looks crossed, flip it.
            // Usually we want the "Knee" to be distinct from the body.
            
            // Refined logic:
            // Calculate both potential knee points. Pick the one that matches "Outward" direction relative to body heading?
            // Simple heuristic: If IsRightSide, we want the knee to have a specific winding?
            
            Vector kneePos = new Vector(Math.Cos(kneeAngleRad) * _segment1Length, Math.Sin(kneeAngleRad) * _segment1Length);
            Point knee = root + kneePos;

            // Update state instead of lines
            CurrentRootPosition = root;
            CurrentKneePosition = knee;

            // Update visuals
            _femur.X1 = root.X;
            _femur.Y1 = root.Y;
            _femur.X2 = knee.X;
            _femur.Y2 = knee.Y;

            _tibia.X1 = knee.X;
            _tibia.Y1 = knee.Y;
            _tibia.X2 = target.X;
            _tibia.Y2 = target.Y;

        }
        
        public void Cleanup()
        {
            _canvas.Children.Remove(_femur);
            _canvas.Children.Remove(_tibia);
        }
    }
}
