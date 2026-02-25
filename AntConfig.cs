using System.Windows;

namespace OpenAnt
{
    public static class AntConfig
    {
        // Body Parts Dimensions
        public static double HeadSize = 3.2;      // Head diameter
        public static double ThoraxLength = 4.0;  // Middle part length (X-axis)
        public static double ThoraxWidth = 2.5;   // Middle part width (Y-axis)
        public static double AbdomenLength = 5.0; // Rear part length (X-axis)
        public static double AbdomenWidth = 3.5;  // Rear part width (Y-axis)
        
        // Legs
        public static double LegSegment1Length = 3.0; // Femur
        public static double LegSegment2Length = 4.0; // Tibia
        public static double StepTriggerThreshold = 4.0; // Distance to trigger a step
        public static double StepDistance = 2; // Max step distance
        public static double StepHeight = 3.0; // How high the leg lifts
        public static double StepDuration = 0.01; // Time to complete a step (Slower to see it)
        public static double LegUpdateInterval = 0.083;
        public static double RenderUpdateInterval = 0.0417;

        // Movement
        public static double MaxSpeed = 30; // Pixels per second (slightly slower for small ants)
        public static double TurnSpeed = 180; // Degrees per second
        public static double WanderStrength = 50; // Random force for wandering

        // Behavior & Flocking
        public static double PerceptionRadius = 40.0;    // Reduced for performance with 500 ants
        public static double SeparationRadius = 8.0;     // Minimum distance to avoid overlap (Body size approx 6-8)
        public static double HardCollisionRadius = 6.0;  // Force push apart radius
        public static double InteractionRadius = 12.0;   // Distance to trigger "communication"
        public static double EdgePreferenceRange = 100.0;
        public static int MaxClusterSize = 10;

        // Behavior Weights
        public static double EdgeAttractionWeight = 2.0;
        public static double CohesionWeight = 1.0;
        public static double SeparationWeight = 8.0;     // Very high priority to prevent overlap
        public static double WanderWeight = 1.0;
        
        // Interaction
        public static double InteractionDuration = 0.5;  // How long to stop and "talk" (seconds)
        public static double InteractionCooldown = 5.0;  // How long before talking again
        
        // Trail Following
        public static double TrailFollowingRadius = 40.0;
        public static double TrailFollowDistance = 15.0;
        public static double TrailAlignAngle = 60.0; // Degrees
        public static double TrailFollowProbability = 0.02; // Chance per frame to start following
        public static double TrailBreakProbability = 0.01; // Chance per frame to stop following
        public static double TrailSteeringWeight = 5.0; // Strong pull to trail

        // Lazy/Cluster Behavior
        public static int LazyNeighborThreshold = 5; // How many neighbors to trigger laziness
        public static double LazyEdgeDistance = 60.0; // Distance from edge to allow laziness
        public static double LazySpeedMultiplier = 0.2; // Slow down when lazy
        public static double LazyIdleChance = 0.1; // Chance to just stop when lazy

        // Environment Interaction
        public static double CursorRepulsionRadius = 500;
        public static double CursorRepulsionStrength = 100; // Strong push
        public static double BrightnessThreshold = 0.1; // Above this is "bright"
        public static double LightAvoidanceStrength = 5000;
    }
}
