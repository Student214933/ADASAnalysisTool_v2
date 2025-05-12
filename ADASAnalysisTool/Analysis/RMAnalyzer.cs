using ADASAnalysisTool.Utils;
using ADASAnalysisTool.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ADASAnalysisTool.Analysis
{
    public static class RMAnalyzer
    {
        private const double timeStep = 0.5;     // DBF check step (used in fallback)
        private const double deltaStep = 0.05;   // Δ increment
        private const double epsilon = 1e-9;     // floating-point tolerance

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

            var sortedTasks = component.Tasks.OrderBy(t => t.Priority ?? int.MaxValue).ToList();
            double maxDeltaToTry = sortedTasks.Max(t => t.Period) * 2;

            double totalUtilization = sortedTasks.Sum(t => t.WCET / t.Period);
            Console.WriteLine($"[INFO] RM Component {component.Id} Total Utilization: {totalUtilization:F6}");
            if (totalUtilization > 0.9)
                Console.WriteLine($"[WARNING] Component {component.Id} has utilization near 1.0");

            for (double currentDelta = 0; currentDelta <= maxDeltaToTry; currentDelta += deltaStep)
            {
                double maxRequiredAlpha = 0.0;
                bool possible = true;

                foreach (var task_i in sortedTasks)
                {
                    double deadline_Di = task_i.Period;
                    double dbf_at_Di = CalculateDBF_RM_ForTask(sortedTasks, deadline_Di, task_i);

                    if (dbf_at_Di == 0 && deadline_Di <= currentDelta) continue;

                    if (dbf_at_Di > 0 && deadline_Di <= currentDelta)
                    {
                        possible = false;
                        break;
                    }

                    if (deadline_Di > currentDelta)
                    {
                        if ((deadline_Di - currentDelta) < epsilon)
                        {
                            possible = false;
                            break;
                        }
                        double requiredAlpha = dbf_at_Di / (deadline_Di - currentDelta);
                        maxRequiredAlpha = Math.Max(maxRequiredAlpha, requiredAlpha);
                    }
                }

                if (!possible) continue;

                if (maxRequiredAlpha <= 1.0 + epsilon && maxRequiredAlpha >= 0)
                {
                    // Final verification: dbf(t_i) ≤ sbf(t_i) for each task_i
                    bool verified = true;
                    foreach (var task_i_verify in sortedTasks)
                    {
                        double t_verify = task_i_verify.Period;
                        double dbf_verify = CalculateDBF_RM_ForTask(sortedTasks, t_verify, task_i_verify);
                        double sbf_verify = EDFAnalyzer.SBF_BDR(maxRequiredAlpha, currentDelta, t_verify);

                        if (dbf_verify > sbf_verify + epsilon)
                        {
                            verified = false;
                            Console.WriteLine($"[VIOLATION] D_i={t_verify:F2}: dbf={dbf_verify:F4} > sbf={sbf_verify:F4}");
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
                        Console.WriteLine($"[PASS] RM Component {component.Id}: dbf(D_i) ≤ sbf(D_i) ∀ i");

                        // === Max dbf - sbf gap logging ===
                        double maxGap = double.NegativeInfinity;
                        foreach (var task in sortedTasks)
                        {
                            double t = task.Period;
                            double dbf = CalculateDBF_RM_ForTask(sortedTasks, t, task);
                            double sbf = EDFAnalyzer.SBF_BDR(component.Alpha, component.Delta, t);
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

            component.IsInterfaceSchedulable = false;
            component.Alpha = double.PositiveInfinity;
            component.Delta = double.PositiveInfinity;
            Console.WriteLine($"--- Could not find a schedulable BDR interface for RM Component {component.Id} (alpha <= 1).\n");
        }

        // Demand Bound Function for RM: includes interference from higher-priority tasks
        public static double CalculateDBF_RM_ForTask(List<Tasks> sortedTasks, double t, Tasks task_i)
        {
            double demand = task_i.WCET;
            foreach (var hpTask in sortedTasks)
            {
                if (hpTask.Priority < task_i.Priority && hpTask.Period > 0)
                {
                    demand += Math.Ceiling(t / hpTask.Period) * hpTask.WCET;
                }
            }
            return demand;
        }

        // Optional fallback analysis: classical RTA
        public static bool OriginalRTAAnalysis(Component component)
        {
            var tasks = component.Tasks.OrderBy(t => t.Priority ?? int.MaxValue).ToList();

            foreach (var task in tasks)
            {
                double R = task.WCET;
                double prevR;
                int iterations = 0;
                const int MAX_ITERATIONS = 1000;

                do
                {
                    prevR = R;
                    R = task.WCET;
                    foreach (var hp in tasks)
                    {
                        if (hp.Priority < task.Priority && hp.Period > 0)
                        {
                            R += Math.Ceiling(prevR / hp.Period) * hp.WCET;
                        }
                    }

                    if (R > task.Period) return false;
                    iterations++;

                } while (Math.Abs(R - prevR) > epsilon && iterations < MAX_ITERATIONS);

                if (iterations >= MAX_ITERATIONS)
                {
                    Console.WriteLine($"Warning: RTA for task {task.Name} in component {component.Id} exceeded max iterations.");
                    return false;
                }
            }
            return true;
        }
    }
}