// EDFAnalyzer.cs
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

            double tMax = (double)MathUtils.LCM(component.Tasks.Select(t => t.Period).ToList());
            // Heuristic for max delta to try: maximum task period within the component,
            // or component's own input period if no tasks.
            double maxDeltaToTry = component.Tasks.Any() ? component.Tasks.Max(t => t.Period) : component.Period;
            const double timeStep = 1.0; // Step for Delta and for t in DBF checking


            for (double currentDelta = 0; currentDelta <= maxDeltaToTry; currentDelta += timeStep)
            {
                double maxRequiredAlphaForThisDelta = 0.0;
                bool possibleWithThisDelta = true;

                // Iterate through all relevant time points t to find the max required alpha for this delta
                for (double t = 0; t <= tMax; t += timeStep)
                {
                    double dbfVal = CalculateDBF_EDF(component.Tasks, t);

                    if (dbfVal == 0 && t <= currentDelta)
                    {
                        // No demand at or before delta, and SBF is 0. This is fine.
                        continue;
                    }

                    if (dbfVal > 0 && t <= currentDelta)
                    {
                        // Positive demand at or before delta, but SBF is 0. This delta is not possible.
                        possibleWithThisDelta = false;
                        break;
                    }

                    if (t > currentDelta) // SBF can be > 0
                    {
                        if (dbfVal > 0) // Only need to calculate alpha if there's demand
                        {
                            // alpha * (t - currentDelta) >= dbfVal
                            // alpha >= dbfVal / (t - currentDelta)
                            // (t - currentDelta) is guaranteed to be > 0 here, and at least 'timeStep'
                            double requiredAlphaAtT = dbfVal / (t - currentDelta);
                            maxRequiredAlphaForThisDelta = Math.Max(maxRequiredAlphaForThisDelta, requiredAlphaAtT);
                        }
                        // If dbfVal is 0 and t > currentDelta, requiredAlphaAtT is 0, no change to max.
                    }
                }

                if (!possibleWithThisDelta)
                {
                    // This delta was found to be impossible, try the next one.
                    continue;
                }

                // Check if the max alpha required for this delta is acceptable
                if (maxRequiredAlphaForThisDelta <= 1.00001 && maxRequiredAlphaForThisDelta >= 0) // Add small tolerance for floating point
                {
                    // Found the smallest delta with a valid alpha <= 1
                    component.Alpha = maxRequiredAlphaForThisDelta;
                    component.Delta = currentDelta;
                    component.IsInterfaceSchedulable = true; // The component's workload is schedulable with this interface
                    Console.WriteLine($"--- Derived for EDF Component {component.Id}: Alpha={component.Alpha:F4}, Delta={component.Delta:F2}");
                    return; // Return as soon as the first (smallest delta) valid interface is found
                }
            }

            // If the loop completes, no schedulable BDR interface was found within the constraints
            component.IsInterfaceSchedulable = false;
            // Set Alpha/Delta to indicate failure or not found status
            component.Alpha = double.PositiveInfinity;
            component.Delta = double.PositiveInfinity;
            Console.WriteLine($"--- Could not find a schedulable BDR interface for EDF Component {component.Id} (alpha <= 1).");
        }

        // Supply Bound Function for a BDR interface (alpha, delta) at time t
        // HSS Chapter Eq. (6)
        public static double SBF_BDR(double alpha, double delta, double t)
        {
            if (t < 0) return 0; // Time cannot be negative
            return (t < delta) ? 0 : alpha * (t - delta);
        }

        // Demand Bound Function for EDF component's tasks at time t
        // HSS Chapter Eq. (2) - Assuming implicit deadlines (D_i = T_i)
        // If tasks can have explicit deadlines D_i != T_i, use Eq. (3)
        public static double CalculateDBF_EDF(List<Tasks> tasks, double t)
        {
            if (t < 0) return 0; // Demand for negative time interval is 0
            if (!tasks.Any()) return 0;

            double totalDemand = 0;
            foreach (var task in tasks)
            {
                if (task.Period > 0) // Avoid division by zero for invalid task period
                {
                    // Using HSS Chapter Eq. (2) for implicit deadlines (D_i = T_i)
                    totalDemand += Math.Floor(t / task.Period) * task.WCET;

                    // If explicit deadlines (D_i) are available in Tasks model as task.Deadline:
                    // Using HSS Chapter Eq. (3)
                    // totalDemand += Math.Max(0, Math.Floor((t + task.Period - task.Deadline) / task.Period)) * task.WCET;
                }
            }
            return totalDemand;
        }

        public static void ComputeWCRT_EDF(Component component)
        {
            if (!component.IsInterfaceSchedulable || component.Alpha > 1 || component.Alpha < 0)
            {
                Console.WriteLine($"Skipping WCRT computation for Component {component.Id}: Invalid or unschedulable BDR interface.");
                return;
            }

            double alpha = component.Alpha;
            double delta = component.Delta;

            foreach (var task in component.Tasks)
            {
                double D = task.Period; // Assuming implicit deadline model
                double tMax = MathUtils.LCM(component.Tasks.Select(t => t.Period).ToList());
                double timeStep = 1.0;

                double minSlack = double.PositiveInfinity;

                for (double t = D; t <= tMax; t += timeStep)
                {
                    double dbf = CalculateDBF_EDF(component.Tasks, t);
                    double sbfInput = dbf;
                    double sbfTime = SBF_BDR(alpha, delta, t);

                    // Try to find the earliest time point where the supply is enough
                    double slack = t - sbfInput;
                    if (sbfTime >= dbf)
                    {
                        minSlack = Math.Min(minSlack, slack);
                    }
                }

                task.WCRT = (minSlack == double.PositiveInfinity) ? double.PositiveInfinity : task.Period - minSlack;
                Console.WriteLine($"Task {task.Name} in Component {component.Id}: WCRT ≈ {task.WCRT:F2}");
            }
        }
    }


}