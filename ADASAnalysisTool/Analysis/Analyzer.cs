using ADASAnalysisTool.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ADASAnalysisTool.Analysis
{
    public static class Analyzer
    {
        public static void AnalyzeSystem(List<Core> cores, List<Component> components, List<Tasks> tasks)
        {
            // Assign tasks to components
            foreach (var task in tasks)
            {
                var comp = components.FirstOrDefault(c => c.Id == task.ComponentId);
                if (comp != null)
                {
                    comp.Tasks.Add(task);
                }
                else
                {
                    Console.WriteLine($"[Warning] Task {task.Name} references non-existent component {task.ComponentId}");
                }
            }

            // Group components by core
            var coreGroups = components.GroupBy(c => c.CoreId);

            foreach (var coreGroup in coreGroups)
            {
                var core = cores.FirstOrDefault(c => c.Id == coreGroup.Key);
                if (core == null)
                {
                    Console.WriteLine($"[Warning] Components reference non-existent core {coreGroup.Key}");
                    continue;
                }

                Console.WriteLine($"\n========== Analyzing Core {core.Id} ==========");
                Console.WriteLine($"Core Speed Factor = {core.SpeedFactor:F2}, Scheduler = {core.Scheduler}\n");

                foreach (var component in coreGroup)
                {
                    Console.WriteLine($"-- Component {component.Id} (Scheduler: {component.Scheduler}) --");

                    // Scale WCET by core speed factor
                    foreach (var taskInComponent in component.Tasks)
                    {
                        if (core.SpeedFactor > 0)
                        {
                            taskInComponent.WCET = taskInComponent.WCET / core.SpeedFactor;
                            Console.WriteLine($"[DEBUG] Effective WCETs after scaling for Component {component.Id}:");
                            foreach (var task in component.Tasks)
                            {
                                Console.WriteLine($"  Task {task.Name}: WCET={task.WCET:F4}, Period={task.Period}");
                            }
                        }
                        else
                        {
                            taskInComponent.WCET = double.PositiveInfinity;
                            Console.WriteLine($"Warning: Core {core.Id} has invalid speed factor {core.SpeedFactor}. Task WCETs for component {component.Id} set to infinity.");
                        }
                    }

                    // Determine scheduling algorithm
                    switch (component.Scheduler)
                    {
                        case "EDF":
                            EDFAnalyzer.CalculateAndSetBDRDemandInterface(component);
                            break;
                        case "RM":
                            RMAnalyzer.CalculateAndSetBDRDemandInterface(component);
                            break;
                        default:
                            Console.WriteLine($"Unknown Scheduler '{component.Scheduler}' for component {component.Id}. Skipping interface calculation.");
                            component.IsInterfaceSchedulable = false;
                            component.Alpha = double.NaN;
                            component.Delta = double.NaN;
                            break;
                    }

                    // Try RTA as fallback if BDR analysis fails for RM
                    if (!component.IsInterfaceSchedulable && component.Scheduler == "RM")
                    {
                        Console.WriteLine($"Trying RTA fallback for RM Component {component.Id}...");
                        bool rtaPass = RMAnalyzer.OriginalRTAAnalysis(component);
                        Console.WriteLine($"RTA Result for {component.Id}: {(rtaPass ? "Schedulable" : "Not schedulable")}");
                    }

                    // Post-check: catch invalid (0,0) interface on non-empty task sets
                    if (component.Tasks.Any() && component.Alpha == 0 && component.Delta == 0)
                    {
                        component.IsInterfaceSchedulable = false;
                        Console.WriteLine($"[ERROR] Component {component.Id} has tasks but was assigned (α, Δ) = (0, 0). Marking UNSCHEDULABLE.");
                    }

                    if (component.IsInterfaceSchedulable)
                    {
                        Console.WriteLine($"Derived BDR Interface: Alpha = {component.Alpha:F4}, Delta = {component.Delta:F2}");
                    }
                    else
                    {
                        Console.WriteLine($"-- Component {component.Id}: UNSCHEDULABLE");
                        Console.WriteLine($"   Final Alpha = {component.Alpha:F4}, Delta = {component.Delta:F2}");
                        Console.WriteLine("   Check if task demand is too high for any possible (α, Δ).\n");
                    }
                }

                // Print a component-level summary per core
                Console.WriteLine($"\n=== Core {core.Id} Component Summary ===");
                foreach (var comp in coreGroup)
                {
                    string status = comp.IsInterfaceSchedulable ? "Schedulable" : "UNSCHEDULABLE";
                    Console.WriteLine($"Component {comp.Id}: {status}, α={comp.Alpha:F3}, Δ={comp.Delta:F2}");
                }

                // Perform schedulability analysis at the system level (core level)
                PerformCoreLevelComponentSchedulability(core, coreGroup.ToList());
            }
        }

        // Placeholder for system-level schedulability of components on a core
        private static void PerformCoreLevelComponentSchedulability(Core core, List<Component> componentsOnCore)
        {
            Console.WriteLine($"\n--- System-Level Schedulability Analysis for Core {core.Id} ({core.Scheduler}) ---");

            // Filter out components without valid interfaces
            var schedulableComponents = componentsOnCore.Where(c => c.IsInterfaceSchedulable && c.Alpha <= 1 && c.Alpha >= 0).ToList();
            if (!schedulableComponents.Any())
            {
                Console.WriteLine("No components with valid derived interfaces to schedule on this core.");
                return;
            }

            // Convert components to tasks using Half-Half algorithm
            List<Tasks> componentTasks = new List<Tasks>();
            foreach (var comp in schedulableComponents)
            {
                if (comp.Alpha >= 1.0 || comp.Alpha == 0)
                {
                    if (comp.Alpha == 0 && comp.Delta == 0)
                    {
                        componentTasks.Add(new Tasks { Name = $"{comp.Id}_task", WCET = 0, Period = 1, Priority = comp.Priority });
                        continue;
                    }
                    Console.WriteLine($"Component {comp.Id} has Alpha >= 1 or Alpha = 0 (and Delta !=0), cannot transform using HSS Thm 3. Alpha: {comp.Alpha}");
                    continue;
                }

                double task_C = comp.Alpha * comp.Delta / (2 * (1 - comp.Alpha));
                double task_T = comp.Delta / (2 * (1 - comp.Alpha));

                componentTasks.Add(new Tasks { Name = $"{comp.Id}_interface_task", WCET = comp.Alpha * task_T, Period = task_T, Priority = comp.Priority });
            }

            if (!componentTasks.Any())
            {
                Console.WriteLine("No valid component tasks to schedule on this core.");
                return;
            }

            bool coreIsSchedulable = false;
            if (core.Scheduler == "EDF")
            {
                double totalUtilization = componentTasks.Sum(ct => (ct.Period > 0) ? (ct.WCET / ct.Period) : 0);
                coreIsSchedulable = totalUtilization <= 1.0;
                Console.WriteLine($"Core EDF Utilization: {totalUtilization:F4}. Schedulable: {coreIsSchedulable}");
            }
            else if (core.Scheduler == "RM")
            {
                // RM: Shorter period = higher priority if not assigned
                int p = 0;
                foreach (var ct in componentTasks.OrderBy(c => c.Period))
                {
                    if (!ct.Priority.HasValue) ct.Priority = p++;
                }

                double totalUtilization = componentTasks.Sum(ct => (ct.Period > 0) ? (ct.WCET / ct.Period) : 0);
                double rmBound = componentTasks.Count * (Math.Pow(2, 1.0 / componentTasks.Count) - 1);
                coreIsSchedulable = totalUtilization <= rmBound;
                Console.WriteLine($"Core RM Utilization: {totalUtilization:F4}, Bound: {rmBound:F4}. Schedulable: {coreIsSchedulable}");
            }

            Console.WriteLine($"--- Core {core.Id} System-Level Schedulability: {(coreIsSchedulable ? "SCHEDULABLE" : "NOT SCHEDULABLE")} ---\n");
        }
    }
}
