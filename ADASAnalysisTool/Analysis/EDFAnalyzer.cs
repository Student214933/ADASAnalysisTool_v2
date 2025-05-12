using ADASAnalysisTool.Utils;
using ADASAnalysisTool.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ADASAnalysisTool.Analysis
{
    public static class EDFAnalyzer
    {
        // Calculates the BDR Demand Interface (alpha, delta) for the component
        public static void CalculateAndSetBDRDemandInterface(Component component)
        {
            if (!component.Tasks.Any())
            {
                component.Alpha = 0;
                component.Delta = 0;
                component.IsInterfaceSchedulable = true;
                Console.WriteLine($"--- Component {component.Id} has no tasks. Alpha=0, Delta=0.");
                return;
            }

            double tMax = MathUtils.LCM(component.Tasks.Select(t => t.Period).ToList());
            double maxDeltaToTry = component.Tasks.Max(t => t.Period);
            const double timeStep = 0.1; // Tightened timestep
            const double epsilon = 1e-9;

            double totalUtilization = component.Tasks.Sum(t => t.WCET / t.Period);
            Console.WriteLine($"[INFO] EDF Component {component.Id} Total Utilization: {totalUtilization:F6}");
            if (totalUtilization > 0.9)
                Console.WriteLine($"[WARNING] Component {component.Id} has utilization near 1.0");

            for (double currentDelta = 0; currentDelta <= maxDeltaToTry; currentDelta += timeStep)
            {
                double maxRequiredAlpha = 0.0;
                bool possible = true;

                for (double t = 0; t <= tMax; t += timeStep)
                {
                    double dbfVal = CalculateDBF_EDF(component.Tasks, t);

                    if (dbfVal == 0 && t <= currentDelta) continue;

                    if (dbfVal > 0 && t <= currentDelta)
                    {
                        possible = false;
                        break;
                    }

                    if (t > currentDelta && dbfVal > 0)
                    {
                        double requiredAlpha = dbfVal / (t - currentDelta);
                        maxRequiredAlpha = Math.Max(maxRequiredAlpha, requiredAlpha);
                    }
                }

                if (!possible) continue;

                if (maxRequiredAlpha <= 1.0 + epsilon && maxRequiredAlpha >= 0)
                {
                    // Final full verification
                    bool verified = true;
                    for (double tVerify = 0; tVerify <= tMax; tVerify += timeStep)
                    {
                        double dbf = CalculateDBF_EDF(component.Tasks, tVerify);
                        double sbf = SBF_BDR(maxRequiredAlpha, currentDelta, tVerify);

                        if (dbf > sbf + epsilon)
                        {
                            verified = false;
                            Console.WriteLine($"[VIOLATION] t={tVerify:F2}: dbf={dbf:F4} > sbf={sbf:F4}");
                            break;
                        }
                    }

                    if (verified)
                    {
                        component.Alpha = maxRequiredAlpha;
                        component.Delta = currentDelta == 0 ? 1e-6 : currentDelta;

                        if (maxRequiredAlpha == 0 && currentDelta == 0)
                        {
                            Console.WriteLine($"[FAIL] Component {component.Id} Alpha = {component.Alpha:F4} and Delta = 0");
                            component.IsInterfaceSchedulable = false;
                            return;
                        }

                        if (component.Alpha < 0.05)
                        {
                            Console.WriteLine($"[FAIL] Component {component.Id} has unacceptably small Alpha = {component.Alpha:F4}");
                            component.IsInterfaceSchedulable = false;
                            return;
                        }

                        component.IsInterfaceSchedulable = true;
                        Console.WriteLine($"[PASS] EDF Component {component.Id}: dbf(t) ≤ sbf(t) ∀ t");

                        // === Max dbf - sbf gap logging ===
                        double maxGap = double.NegativeInfinity;
                        foreach (var task in component.Tasks)
                        {
                            double t = task.Period;
                            double dbf = CalculateDBF_EDF(component.Tasks, t);
                            double sbf = SBF_BDR(component.Alpha, component.Delta, t);
                            double gap = dbf - sbf;
                            if (gap > maxGap) maxGap = gap;
                        }

                        Console.WriteLine($"[DEBUG] Max dbf - sbf gap for Component {component.Id}: {maxGap:F6}");
                        if (Math.Abs(maxGap) < 1e-6)
                            Console.WriteLine($"[WARNING] Component {component.Id} is on the edge of schedulability.");

                        return;
                    }
                }
            }

            // Mark unschedulable if no valid alpha/delta was found
            component.Alpha = double.PositiveInfinity;
            component.Delta = double.PositiveInfinity;
            component.IsInterfaceSchedulable = false;
            Console.WriteLine($"[FAIL] EDF Component {component.Id}: Could not derive schedulable BDR interface (α ≤ 1).");
        }

        // HSS Chapter Eq. (6) - Supply Bound Function for a BDR interface (alpha, delta) at time t
        public static double SBF_BDR(double alpha, double delta, double t)
        {
            if (t < 0) return 0;
            return (t < delta) ? 0 : alpha * (t - delta);
        }

        // HSS Chapter Eq. (2) - Demand Bound Function for EDF (implicit deadlines)
        public static double CalculateDBF_EDF(List<Tasks> tasks, double t)
        {
            if (t < 0 || !tasks.Any()) return 0;

            double totalDemand = 0;
            foreach (var task in tasks)
            {
                if (task.Period > 0)
                {
                    totalDemand += Math.Floor(t / task.Period) * task.WCET;
                }
            }
            return totalDemand;
        }
    }
}