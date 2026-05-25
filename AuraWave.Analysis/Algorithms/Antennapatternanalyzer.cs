using System;
using System.Collections.Generic;
using System.Linq;
using AuraWave.Core.Models;

namespace AuraWave.Analysis.Algorithms
{
    /// <summary>
    /// Full antenna pattern analysis engine.
    /// Computes HPBW, FNBW, SLL, F/B ratio, efficiency, VSWR from raw data.
    /// </summary>
    public static class AntennaPatternAnalyzer
    {
        // ── PUBLIC API ───────────────────────────────────────────────────────

        public static AntennaMetrics Analyze(MeasurementResult result)
        {
            if (result.DataPoints.Count < 3)
                return new AntennaMetrics();

            var points = result.DataPoints
                .OrderBy(p => p.AngleDegrees)
                .ToList();

            double[] angles = points.Select(p => p.AngleDegrees).ToArray();
            double[] gain = points.Select(p => p.GainDbi).ToArray();

            var metrics = new AntennaMetrics();

            // Peak gain
            int peakIdx = Array.IndexOf(gain, gain.Max());
            metrics.PeakGainDbi = gain[peakIdx];
            metrics.PeakGainAngle = angles[peakIdx];

            // HPBW (half-power = -3 dB from peak)
            (double hpbwLeft, double hpbwRight) = FindBeamwidth(angles, gain, peakIdx, -3.0);
            metrics.Hpbw = hpbwRight - hpbwLeft;

            // FNBW (first nulls)
            (double fnbwLeft, double fnbwRight) = FindFirstNulls(angles, gain, peakIdx);
            metrics.Fnbw = fnbwRight - fnbwLeft;
            metrics.FirstNullAngle = fnbwLeft;

            // Side-lobe level
            metrics.SideLobeLevel = ComputeSideLobeLevel(angles, gain, peakIdx, fnbwLeft, fnbwRight);

            // Front-to-back ratio
            metrics.FrontToBackRatio = ComputeFrontToBack(angles, gain, metrics.PeakGainAngle);

            // Beam tilt
            metrics.BeamTiltDeg = metrics.PeakGainAngle;

            // S11 → VSWR and return loss (if available)
            var s11Points = result.DataPoints.Where(p => !double.IsNaN(p.S11Magnitude)).ToList();
            if (s11Points.Count > 0)
            {
                double minS11 = s11Points.Min(p => p.S11Magnitude);
                metrics.ReturnLossDb = Math.Abs(minS11);
                metrics.Vswr = S11ToVswr(minS11);
                metrics.Efficiency = EstimateRadiationEfficiency(metrics.Vswr);
            }

            return metrics;
        }

        // ── NORMALISE PATTERN ────────────────────────────────────────────────

        public static double[] NormalizePattern(double[] gain)
        {
            double peak = gain.Max();
            return gain.Select(g => g - peak).ToArray();
        }

        // ── INTERPOLATION ────────────────────────────────────────────────────

        public static (double[] angles, double[] gain) InterpolatePattern(
            double[] srcAngles, double[] srcGain, double step = 0.1)
        {
            int n = (int)Math.Round((srcAngles.Last() - srcAngles.First()) / step) + 1;
            var outAngles = Enumerable.Range(0, n)
                .Select(i => srcAngles.First() + i * step)
                .ToArray();
            var outGain = outAngles.Select(a => LinearInterpolate(srcAngles, srcGain, a)).ToArray();
            return (outAngles, outGain);
        }

        // ── PRIVATE HELPERS ──────────────────────────────────────────────────

        private static (double left, double right) FindBeamwidth(
            double[] angles, double[] gain, int peakIdx, double dBrelative)
        {
            double threshold = gain[peakIdx] + dBrelative;
            double left = angles[peakIdx];
            double right = angles[peakIdx];

            // Search left
            for (int i = peakIdx - 1; i >= 0; i--)
            {
                if (gain[i] <= threshold)
                {
                    left = LinearInterpCross(angles[i], gain[i], angles[i + 1], gain[i + 1], threshold);
                    break;
                }
            }

            // Search right
            for (int i = peakIdx + 1; i < gain.Length; i++)
            {
                if (gain[i] <= threshold)
                {
                    right = LinearInterpCross(angles[i - 1], gain[i - 1], angles[i], gain[i], threshold);
                    break;
                }
            }

            return (left, right);
        }

        private static (double left, double right) FindFirstNulls(
            double[] angles, double[] gain, int peakIdx)
        {
            double left = angles.First();
            double right = angles.Last();

            // Find first local minimum to the left
            for (int i = peakIdx - 1; i >= 1; i--)
            {
                if (gain[i] < gain[i - 1] && gain[i] < gain[i + 1])
                {
                    left = angles[i];
                    break;
                }
            }

            // Find first local minimum to the right
            for (int i = peakIdx + 1; i < gain.Length - 1; i++)
            {
                if (gain[i] < gain[i - 1] && gain[i] < gain[i + 1])
                {
                    right = angles[i];
                    break;
                }
            }

            return (left, right);
        }

        private static double ComputeSideLobeLevel(
            double[] angles, double[] gain, int peakIdx,
            double fnbwLeft, double fnbwRight)
        {
            double sllGain = double.NegativeInfinity;

            for (int i = 0; i < gain.Length; i++)
            {
                if (angles[i] < fnbwLeft || angles[i] > fnbwRight)
                {
                    if (gain[i] > sllGain)
                        sllGain = gain[i];
                }
            }

            return double.IsNegativeInfinity(sllGain)
                ? double.NaN
                : sllGain - gain[peakIdx];  // dB below peak (negative value)
        }

        private static double ComputeFrontToBack(
            double[] angles, double[] gain, double peakAngle)
        {
            double backAngle = peakAngle + 180.0;
            if (backAngle > angles.Last()) backAngle -= 360.0;

            double backGain = LinearInterpolate(angles, gain, backAngle);
            double peakGain = gain.Max();
            return peakGain - backGain;
        }

        private static double S11ToVswr(double s11Db)
        {
            double gamma = Math.Pow(10.0, s11Db / 20.0);
            return (1 + gamma) / (1 - gamma);
        }

        private static double EstimateRadiationEfficiency(double vswr)
        {
            double gamma = (vswr - 1) / (vswr + 1);
            double mismatchLoss = 1 - gamma * gamma;
            return mismatchLoss * 100.0; // percent, ignoring ohmic losses
        }

        private static double LinearInterpCross(
            double x0, double y0, double x1, double y1, double yTarget)
        {
            if (Math.Abs(y1 - y0) < 1e-12) return x0;
            return x0 + (yTarget - y0) * (x1 - x0) / (y1 - y0);
        }

        private static double LinearInterpolate(double[] xs, double[] ys, double x)
        {
            if (x <= xs[0]) return ys[0];
            if (x >= xs[^1]) return ys[^1];

            int i = Array.BinarySearch(xs, x);
            if (i >= 0) return ys[i];
            i = ~i;

            double t = (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
            return ys[i - 1] + t * (ys[i] - ys[i - 1]);
        }
    }
}