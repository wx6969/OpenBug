using System;
using System.Windows;
using Point = System.Windows.Point;

namespace OpenAnt
{
    public static class MathUtils
    {
        public static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * Math.Clamp(t, 0, 1);
        }

        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        public static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        public static Vector RotateVector(Vector v, double degrees)
        {
            double radians = DegreesToRadians(degrees);
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new Vector(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }
    }
}
