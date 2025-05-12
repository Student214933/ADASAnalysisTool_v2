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
        private const double timeStep = 1.0; // Define timeStep with a default value of 1.0  

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
            // Max delta could be related to max D_i, or a smaller heuristic.
            // Let's use max task period as D_i for now.
            double maxDeltaToTry = sortedTasks.Any() ? sortedTasks.Max(t => t.Period) : component.Period;
            const double timeStepDelta = 0.1; // Step for iterating Delta

            for (double currentDelta = 0; currentDelta <= maxDeltaToTry; currentDelta += timeStepDelta)
            {
                double maxRequiredAlphaForComponentThisDelta = 0.0;
                bool possibleForAllTasksThisDelta = true;
               
                foreach (var task_i in sortedTasks)
                {
                    double deadline_Di = task_i.Period; // Assuming D_i = T_i

                    // Calculate demand for task_i at its deadline D_i
                    // This is dbf_RM(W, D_i, i)
                    double dbf_at_Di = CalculateDBF_RM_ForTask(sortedTasks, deadline_Di, task_i);

                    if (dbf_at_Di == 0 && deadline_Di <= currentDelta) continue;

                    if (dbf_at_Di > 0 && deadline_Di <= currentDelta) // Demand exists, but SBF is 0 up to D_i
                    {
                        possibleForAllTasksThisDelta = false;
                        break;
                    }

                    if (deadline_Di > currentDelta) // SBF can be > 0 at D_i
                    {
                        if (dbf_at_Di > 0)
                        {
                            // We need SBF(alpha, currentDelta, deadline_Di) >= dbf_at_Di
                            // alpha * (deadline_Di - currentDelta) >= dbf_at_Di
                            // alpha >= dbf_at_Di / (deadline_Di - currentDelta)
                            if ((deadline_Di - currentDelta) < 0.00001) // Avoid division by zero if D_i is very close to currentDelta
                            {
                                // If dbf_at_Di > 0, this implies infinite alpha needed
                                possibleForAllTasksThisDelta = false;
                                break;
                            }
                            double requiredAlphaForTask_i = dbf_at_Di / (deadline_Di - currentDelta);
                            maxRequiredAlphaForComponentThisDelta = Math.Max(maxRequiredAlphaForComponentThisDelta, requiredAlphaForTask_i);
                           
                        }
                    }
                }

                if (!possibleForAllTasksThisDelta)  // Try next delta
                {
    
                    continue;
                }

                    if (maxRequiredAlphaForComponentThisDelta <= 1.00001 && maxRequiredAlphaForComponentThisDelta >= 0) // Add small tolerance
                {
                    // Now, we have a candidate (alpha, delta). We must verify it for *all* t for *all* tasks.
                    // This is the more rigorous check. The above was a necessary condition based on deadlines.
                    bool verifiedForAllT = true;
                    foreach (var task_i_verify in sortedTasks)
                    {
                        // Verification loop for this task_i_verify:
                        for (double t_verify = timeStep; t_verify <= task_i_verify.Period; t_verify += timeStep) // Start from timeStep
                        {
                            double dbf_verify = CalculateDBF_RM_ForTask(sortedTasks, t_verify, task_i_verify);
                            double sbf_verify = EDFAnalyzer.SBF_BDR(maxRequiredAlphaForComponentThisDelta, currentDelta, t_verify);
                            if (dbf_verify > sbf_verify + 0.00001)
                            {
                                // Console.WriteLine($"---- RM Verification FAILED for Task {task_i_verify.Name} at t_verify={t_verify}: dbf={dbf_verify}, sbf={sbf_verify} (Alpha={maxRequiredAlphaForComponentThisDelta}, Delta={currentDelta})");
                                verifiedForAllT = false;
                                break;
                            }
                        }
                        if (!verifiedForAllT) break;

                        // Edge case: Task period is very small
                        if (task_i_verify.Period < timeStep && task_i_verify.Period > 0)
                        {
                            double dbf_at_period = CalculateDBF_RM_ForTask(sortedTasks, task_i_verify.Period, task_i_verify);
                            double sbf_at_period = EDFAnalyzer.SBF_BDR(maxRequiredAlphaForComponentThisDelta, currentDelta, task_i_verify.Period);
                            if (dbf_at_period > sbf_at_period + 0.00001)
                            {
                                verifiedForAllT = false;
                                if (!verifiedForAllT) break;
                            }
                        }
                    }

                    if (verifiedForAllT)
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
            if (!component.IsInterfaceSchedulable)
            {
                component.Alpha = double.PositiveInfinity;
                component.Delta = double.PositiveInfinity;
            }
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
    }
}