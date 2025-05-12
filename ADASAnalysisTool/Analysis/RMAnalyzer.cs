// RMAnalyzer.cs
using ADASAnalysisTool.Utils;
using ADASAnalysisTool.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ADASAnalysisTool.Analysis
{
    public static class RMAnalyzer
    {
        private const double timeStep = 0.5; // Define timeStep with a default value of 1.0  

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

            var sortedTasks = component.Tasks.OrderBy(t => t.Priority ?? int.MaxValue).ToList();

            double maxDeltaToTry = Math.Min(
                MathUtils.LCM(sortedTasks.Select(t => t.Period).ToList()),
                1000.0
            );

            const double timeStepDelta = 0.1;
            const double timeStep = 0.5;

            for (double currentDelta = 0; currentDelta <= maxDeltaToTry; currentDelta += timeStepDelta)
            {
                double maxRequiredAlphaForComponentThisDelta = 0.0;
                bool possibleForAllTasksThisDelta = true;

                foreach (var task_i in sortedTasks)
                {
                    double deadline_Di = task_i.Period;
                    double dbf_at_Di = CalculateDBF_RM_ForTask(sortedTasks, deadline_Di, task_i);

                    Console.WriteLine($"[DEBUG]       [DBF] {task_i.Name} at t={deadline_Di:F2}: base={task_i.WCET}, total={dbf_at_Di}");
                    Console.WriteLine($"[DEBUG]     [TASK {task_i.Name}] D_i={deadline_Di}, Δ={currentDelta:F1}, dbf(D_i)={dbf_at_Di:F2}");

                    if (deadline_Di <= currentDelta)
                    {
                        if (dbf_at_Di > 0)
                        {
                            possibleForAllTasksThisDelta = false;
                            break;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (dbf_at_Di > 0)
                    {
                        double requiredAlphaForTask_i = dbf_at_Di / (deadline_Di - currentDelta);
                        maxRequiredAlphaForComponentThisDelta = Math.Max(maxRequiredAlphaForComponentThisDelta, requiredAlphaForTask_i);
                    }
                }

                Console.WriteLine($"[DEBUG] ?={currentDelta:F2}, maxRequiredAlpha={maxRequiredAlphaForComponentThisDelta:F5}, valid={maxRequiredAlphaForComponentThisDelta <= 1.0}");

                if (!possibleForAllTasksThisDelta)
                    continue;

                // === Special verification case for α = 0 ===
                if (maxRequiredAlphaForComponentThisDelta <= 0.00001)
                {
                    bool verified = true;
                    foreach (var task in sortedTasks)
                    {
                        if (task.Period <= currentDelta) continue;

                        for (double t = currentDelta + timeStep; t <= task.Period; t += timeStep)
                        {
                            double dbf = TotalDBF_RM(sortedTasks, t);
                            double sbf = EDFAnalyzer.SBF_BDR(0.0, currentDelta, t);

                            if (dbf > sbf + 0.00001)
                            {
                                Console.WriteLine($"[DEBUG]     Verification FAILED for α=0 at t={t:F2}: dbf={dbf:F2} > sbf={sbf:F2}");
                                verified = false;
                                break;
                            }
                        }

                        if (!verified) break;
                    }

                    if (verified)
                    {
                        component.Alpha = 0.0;
                        component.Delta = currentDelta;
                        component.IsInterfaceSchedulable = true;
                        Console.WriteLine($"--- Derived for RM Component {component.Id}: Alpha=0.0000, Delta={component.Delta:F2}");
                        return;
                    }
                    else
                    {
                        continue;
                    }
                }

                // === General verification for α > 0 ===
                if (maxRequiredAlphaForComponentThisDelta <= 1.00001)
                {
                    bool verifiedForAllT = true;

                    bool hasVerifiableTask = sortedTasks.Any(task => task.Period > currentDelta);
                    if (!hasVerifiableTask)
                    {
                        Console.WriteLine($"[DEBUG]     Skipping verification at Δ={currentDelta:F2}: no task deadlines > Δ");
                        continue;
                    }

                    int verificationPointsChecked = 0;

                    foreach (var task_i_verify in sortedTasks)
                    {
                        if (task_i_verify.Period <= currentDelta)
                            continue;

                        for (double t_verify = currentDelta + timeStep; t_verify <= task_i_verify.Period; t_verify += timeStep)
                        {
                            double dbf_verify = TotalDBF_RM(sortedTasks, t_verify);
                            double sbf_verify = EDFAnalyzer.SBF_BDR(maxRequiredAlphaForComponentThisDelta, currentDelta, t_verify);

                            verificationPointsChecked++;

                            if (dbf_verify > sbf_verify + 0.00001)
                            {
                                Console.WriteLine($"[DEBUG]     Verification FAILED at t={t_verify:F2}: dbf={dbf_verify:F2} > sbf={sbf_verify:F2}");
                                verifiedForAllT = false;
                                break;
                            }
                        }

                        if (!verifiedForAllT)
                            break;

                        if (task_i_verify.Period < timeStep && task_i_verify.Period > 0)
                        {
                            double dbf_at_period = TotalDBF_RM(sortedTasks, task_i_verify.Period);
                            double sbf_at_period = EDFAnalyzer.SBF_BDR(maxRequiredAlphaForComponentThisDelta, currentDelta, task_i_verify.Period);
                            verificationPointsChecked++;

                            if (dbf_at_period > sbf_at_period + 0.00001)
                            {
                                verifiedForAllT = false;
                                break;
                            }
                        }
                    }

                    if (verifiedForAllT && verificationPointsChecked > 0)
                    {
                        component.Alpha = maxRequiredAlphaForComponentThisDelta;
                        component.Delta = currentDelta;
                        component.IsInterfaceSchedulable = true;
                        Console.WriteLine($"--- Derived for RM Component {component.Id}: Alpha={component.Alpha:F4}, Delta={component.Delta:F2}");
                        return;
                    }
                }
            }

            component.IsInterfaceSchedulable = false;
            component.Alpha = double.PositiveInfinity;
            component.Delta = double.PositiveInfinity;
            Console.WriteLine($"--- Could not find a schedulable BDR interface for RM Component {component.Id} (alpha <= 1).");
        }






        // Demand Bound Function for a specific task_i in an RM component at time t
        // HSS Chapter Eq. (4)
        public static double CalculateDBF_RM_ForTask(List<Tasks> allTasksInComponentSorted, double t, Tasks task_i)
        {
            double demand = task_i.WCET;
            // Sum interference from higher priority tasks
            // Assumes allTasksInComponentSorted is already sorted by priority (lower value = higher priority)
            foreach (var hpTask in allTasksInComponentSorted)
            {
                if (hpTask.Priority < task_i.Priority) // hpTask has higher priority
                {
                    if (hpTask.Period > 0)
                    {
                        demand += Math.Ceiling(t / hpTask.Period) * hpTask.WCET;
                    }
                }
            }
            Console.WriteLine($"[DEBUG]       [DBF] {task_i.Name} at t={t:F2}: base={task_i.WCET}, total={demand}");
            return demand;
        }

        // Original RTA analysis - can be kept for reference or other purposes
        // but not directly used for BDR demand interface calculation in this new approach.
        public static bool OriginalRTAAnalysis(Component component)
        {
            var tasks = component.Tasks.OrderBy(t => t.Priority ?? int.MaxValue).ToList();

            foreach (var task in tasks)
            {
                double R = task.WCET;
                double prevR;
                int iterationLimiter = 0; // To prevent infinite loops in edge cases
                const int MAX_ITERATIONS = 1000;

                do
                {
                    prevR = R;
                    R = task.WCET;
                    foreach (var hpTask in tasks)
                    {
                        if (hpTask.Priority < task.Priority) // hpTask has higher priority
                        {
                            if (hpTask.Period > 0)
                                R += Math.Ceiling(prevR / hpTask.Period) * hpTask.WCET;
                        }
                    }

                    if (R > task.Period) return false; // Deadline miss (assuming D=T)
                    iterationLimiter++;
                    if (iterationLimiter > MAX_ITERATIONS)
                    {
                        // This might happen if periods are very small or WCETs very large, leading to slow convergence or instability
                        // Or if there's a fundamental issue with the task set making it unschedulable in a way RTA struggles with.
                        Console.WriteLine($"Warning: RTA for task {task.Name} in component {component.Id} exceeded max iterations. Current R={R}, Period={task.Period}");
                        return false; // Consider it unschedulable or handle as an error
                    }

                } while (R != prevR);
            }
            return true;
        }

        public static void ComputeWCRT_RM(Component component)
        {
            if (!component.IsInterfaceSchedulable || component.Alpha > 1 || component.Alpha < 0)
            {
                Console.WriteLine($"Skipping WCRT computation for Component {component.Id}: Invalid or unschedulable BDR interface.");
                return;
            }

            double alpha = component.Alpha;
            double delta = component.Delta;
            var tasks = component.Tasks.OrderBy(t => t.Priority ?? int.MaxValue).ToList();

            foreach (var task in tasks)
            {
                double D = task.Period;
                double R = task.WCET;
                double prevR;
                int iterationLimiter = 0;
                const int MAX_ITER = 1000;

                do
                {
                    prevR = R;
                    double interference = 0;

                    foreach (var hpTask in tasks)
                    {
                        if (hpTask.Priority < task.Priority)
                        {
                            if (hpTask.Period > 0)
                                interference += Math.Ceiling(prevR / hpTask.Period) * hpTask.WCET;
                        }
                    }

                    R = task.WCET + interference;

                    // BDR supply bound check
                    double sbf = EDFAnalyzer.SBF_BDR(alpha, delta, R);
                    if (sbf < R)
                    {
                        R = double.PositiveInfinity;
                        break;
                    }

                    iterationLimiter++;
                    if (iterationLimiter > MAX_ITER)
                    {
                        Console.WriteLine($"WCRT for task {task.Name} in {component.Id} did not converge.");
                        R = double.PositiveInfinity;
                        break;
                    }

                } while (Math.Abs(R - prevR) > 1e-3);

                task.WCRT = R;
                Console.WriteLine($"Task {task.Name} in Component {component.Id}: WCRT ≈ {R:F2}");
            }
        }

        public static double TotalDBF_RM(List<Tasks> tasks, double t)
        {
            double total = 0.0;
            foreach (var task in tasks)
            {
                if (task.Period > 0)
                {
                    total += Math.Floor((t + task.Period) / task.Period) * task.WCET;
                }
            }
            return total;
        }
    }
}