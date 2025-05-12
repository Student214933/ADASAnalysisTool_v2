// Analyzer.cs
using ADASAnalysisTool.Models;
using ADASAnalysisTool.Utils;

namespace ADASAnalysisTool.Analysis
{
    public static class Analyzer
    {
        public static void AnalyzeSystem(List<Core> cores, List<Component> components, List<Tasks> tasks, string outputLocal)
        {
            // Assign tasks to components
            foreach (var task in tasks)
            {
                var comp = components.FirstOrDefault(c => c.Id == task.ComponentId);
                if (comp != null) // Check if component exists
                {
                    comp.Tasks.Add(task);
                }
                else
                {
                    Console.WriteLine($"Warning: Task {task.Name} references non-existent component {task.ComponentId}");
                }
            }
            // Assign RM priorities based on period (if not already set)
            foreach (var comp in components)
            {
                if (comp.Scheduler == "RM")
                {
                    var tasksSorted = comp.Tasks.OrderBy(t => t.Period).ToList(); // RM: shorter period = higher priority
                    for (int i = 0; i < tasksSorted.Count; i++)
                    {
                        if (!tasksSorted[i].Priority.HasValue)
                            tasksSorted[i].Priority = i;
                    }
                }
            }

            // Group components by core
            var coreGroups = components.GroupBy(c => c.CoreId);

            // Run analysis for each core
            foreach (var coreGroup in coreGroups)
            {
                var core = cores.FirstOrDefault(c => c.Id == coreGroup.Key);
                if (core == null)
                {
                    Console.WriteLine($"Warning: Components reference non-existent core {coreGroup.Key}");
                    continue;
                }

                //Console.WriteLine($"\nAnalyzing Core {core.Id} (Speed = {core.SpeedFactor})"); // Removed core.Scheduler as it's not used at this level yet
                Console.WriteLine($"\n========== Analyzing Core {core.Id} ==========");
                Console.WriteLine($"Core Speed Factor = {core.SpeedFactor:F2}, Scheduler = {core.Scheduler}\n");

                // Calculate BDR Demand Interface for each component
                foreach (var component in coreGroup)
                {
                    Console.WriteLine($"- Component: {component.Id} (Scheduler: {component.Scheduler})");

                    // Scale tasks WCET with the core's speed factor
                    foreach (var taskInComponent in component.Tasks) // Renamed to avoid conflict
                    {
                        if (core.SpeedFactor > 0)
                        {
                            taskInComponent.WCET = taskInComponent.WCET / core.SpeedFactor;
                        }
                        else
                        {
                            // Handle division by zero or invalid speed factor
                            taskInComponent.WCET = double.PositiveInfinity;
                            Console.WriteLine($"Warning: Core {core.Id} has invalid speed factor {core.SpeedFactor}. Task WCETs for component {component.Id} set to infinity.");
                        }
                    }

                    // Calculate BDR demand interface
                    switch (component.Scheduler)
                    {
                        case "EDF":
                            EDFAnalyzer.CalculateAndSetBDRDemandInterface(component);
                            if (component.IsInterfaceSchedulable)
                            {
                                EDFAnalyzer.ComputeWCRT_EDF(component); // <-- Add this
                            }
                            break;
                        case "RM":
                            RMAnalyzer.CalculateAndSetBDRDemandInterface(component);
                            if (component.IsInterfaceSchedulable)
                            {
                                RMAnalyzer.ComputeWCRT_RM(component); // <-- Add this
                            }
                            break;
                        default:
                            Console.WriteLine($"--- Unknown Scheduler '{component.Scheduler}' for component {component.Id}. Skipping interface calculation.");
                            component.IsInterfaceSchedulable = false; // Mark as not processed correctly
                            component.Alpha = double.NaN;
                            component.Delta = double.NaN;
                            break;
                    }

                    if (!component.IsInterfaceSchedulable && component.Scheduler == "RM")
                    {
                        Console.WriteLine($"Trying RTA fallback for RM Component {component.Id}...");
                        bool rtaPass = RMAnalyzer.OriginalRTAAnalysis(component);
                        Console.WriteLine($"RTA Result for {component.Id}: {(rtaPass ? "Schedulable" : "Not schedulable")}");

                        if (rtaPass)
                        {
                            component.IsInterfaceSchedulable = true;
                            component.Alpha = component.Tasks.Sum(t => t.WCET) / component.Tasks.Sum(t => t.Period); // Conservative: total utilization
                            component.Delta = component.Tasks.Max(t => t.Period); // Safe upper bound
                            Console.WriteLine($"-- Using fallback RTA: assigning Alpha={component.Alpha:F4}, Delta={component.Delta:F2}");
                            RMAnalyzer.ComputeWCRT_RM(component);
                        }
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

                // TODO: Next step would be to analyze schedulability of these component interfaces on the core
                // using the core's scheduler (core.Scheduler) and Theorem 1 / Theorem 3 from HSS chapter.
                // For now, we are just deriving the interfaces.
                // Console.WriteLine($"\n--- Core {core.Id} Component Interface Derivation Complete ---");
                Console.WriteLine($"\n=== Core {core.Id} Component Summary ===");
                foreach (var comp in coreGroup)
                {
                    string status = comp.IsInterfaceSchedulable ? "Schedulable" : "UNSCHEDULABLE";
                    Console.WriteLine($"Component {comp.Id}: {status}, α={comp.Alpha:F3}, Δ={comp.Delta:F2}");
                }
                // Example: Perform system-level schedulability for components on this core
                // This is a placeholder for the next stage of analysis



                PerformCoreLevelComponentSchedulability(core, coreGroup.ToList());

                Console.WriteLine("\n--- Task-Level WCRT Summary ---");
                foreach (var task in tasks)
                {
                    if (task.WCRT.HasValue)
                    {
                        Console.WriteLine($"Task {task.Name}: WCRT = {task.WCRT:F2}, Period = {task.Period}, Util = {task.WCET / task.Period:F3}");
                    }
                }
            }
            CsvExport.ExportAnalysisToCsv(components, outputLocal);
        }

        // Placeholder for system-level schedulability of components on a core
        private static void PerformCoreLevelComponentSchedulability(Core core, List<Component> componentsOnCore)
        {
            Console.WriteLine($"\n--- System-Level Schedulability Analysis for Core {core.Id} ({core.Scheduler}) ---");

            // Filter out components for which we couldn't derive a schedulable interface
            var schedulableComponents = componentsOnCore.Where(c => c.IsInterfaceSchedulable && c.Alpha <= 1 && c.Alpha >= 0).ToList();
            if (!schedulableComponents.Any())
            {
                Console.WriteLine("No components with valid derived interfaces to schedule on this core.");
                return;
            }


            // The components (with their derived Alpha, Delta) now act as "tasks" for the core's scheduler.
            // We need to convert these (Alpha, Delta) BDR interfaces into tasks using Theorem 3 (Half-Half Algorithm from HSS chapter)
            // C_k = Alpha_k * Delta_k / (2 * (1 - Alpha_k))
            // T_k = Delta_k / (2 * (1 - Alpha_k))
            // Note: This is if the core itself is supplying resources *as if* it's a BDR server to these components
            // OR, if the core's scheduler (EDF/RM) schedules these component "tasks" directly.
            // The project description mentions: "Theorem 3 (half-half algorithm) to transform BDR interfaces into resource supply tasks for scheduling at the parent level."

            // Let's assume we transform them into (C,P) tasks and use standard EDF/RM tests.
            List<Tasks> componentTasks = new List<Tasks>();
            foreach (var comp in schedulableComponents)
            {
                if (comp.Alpha >= 1.0 || comp.Alpha == 0) // Avoid division by zero or invalid alpha
                {
                    if (comp.Alpha == 0 && comp.Delta == 0)
                    { // Valid case for empty component
                        componentTasks.Add(new Tasks { Name = $"{comp.Id}_task", WCET = 0, Period = 1, Priority = comp.Priority }); // Effectively no load
                        continue;
                    }
                    Console.WriteLine($"Component {comp.Id} has Alpha >= 1 or Alpha = 0 (and Delta !=0), cannot transform using HSS Thm 3. Alpha: {comp.Alpha}");
                    continue;
                }

                // HSS Chapter Theorem 3: (alpha, delta) -> task (C_k, T_k)
                // C_k = alpha * delta_k / (2 * (1-alpha))
                // T_k = delta_k / (2 * (1-alpha))
                // This assumes the BDR interface (alpha, delta) is a *demand* that needs to be supplied by a periodic server.
                // The task parameters are for that server.
                double task_C = comp.Alpha * comp.Delta / (2 * (1 - comp.Alpha));
                double task_T = comp.Delta / (2 * (1 - comp.Alpha));

                // A simpler interpretation for utilization based tests at core level:
                // Each component demands comp.Alpha utilization.
                // We can use these directly for utilization tests if the core's scheduler is EDF/RM.

                componentTasks.Add(new Tasks { Name = $"{comp.Id}_interface_task", WCET = comp.Alpha * task_T, Period = task_T, Priority = comp.Priority });
                // Or more simply, for utilization based test, consider utilization = comp.Alpha
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
                // Alternative using Alpha directly: totalUtilization = schedulableComponents.Sum(c => c.Alpha);
                coreIsSchedulable = totalUtilization <= 1.0;
                Console.WriteLine($"Core EDF Utilization: {totalUtilization:F4}. Schedulable: {coreIsSchedulable}");
            }
            else if (core.Scheduler == "RM")
            {
                // For RM, we'd typically assign priorities to these component_interface_tasks
                // (e.g., based on their derived T_k or some other metric like original component period)
                // and then run RTA or Liu & Layland utilization bound.
                // Let's use Liu & Layland for simplicity here, assuming priorities are set.
                // The `budgets.csv` has a `priority` field for components, let's use that.
                // If not present, we might need to assign based on component period.

                // Assign priorities if not set (e.g., based on component period)
                int p = 0;
                foreach (var ct in componentTasks.OrderBy(c => c.Period))
                { // RM: Shorter period = higher priority
                    if (!ct.Priority.HasValue) ct.Priority = p++;
                }


                double totalUtilization = componentTasks.Sum(ct => (ct.Period > 0) ? (ct.WCET / ct.Period) : 0);
                // Alternative using Alpha directly: totalUtilization = schedulableComponents.Sum(c => c.Alpha);
                double rmBound = componentTasks.Count * (Math.Pow(2, 1.0 / componentTasks.Count) - 1);
                coreIsSchedulable = totalUtilization <= rmBound;
                Console.WriteLine($"Core RM Utilization: {totalUtilization:F4}, Bound: {rmBound:F4}. Schedulable: {coreIsSchedulable}");

                // For a more precise RM check, RTA on these componentTasks would be needed.
            }
            Console.WriteLine($"--- Core {core.Id} System-Level Schedulability: {(coreIsSchedulable ? "SCHEDULABLE" : "NOT SCHEDULABLE")} ---");
        }
    }
}