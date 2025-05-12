// Component.cs
using CsvHelper.Configuration;

namespace ADASAnalysisTool.Models
{
    public class Component
    {
        public string Id;
        public string CoreId;
        public string Scheduler;
        public int Budget; // Initial input budget (Q)
        public int Period; // Initial input period (P)
        public int? Priority; // Optional for RM (if components are scheduled by RM on a core)
        public List<Tasks> Tasks = new();

        // Calculated BDR Demand Interface parameters
        public double Alpha { get; set; }
        public double Delta { get; set; }
        public bool IsInterfaceSchedulable { get; set; } // Schedulability of this component's workload
                                                         // with the derived (Alpha, Delta)

        public Component()
        {
            Alpha = -1; // Indicate not yet calculated
            Delta = -1; // Indicate not yet calculated
            IsInterfaceSchedulable = false;
        }
    }

    public class ComponentMapInput : ClassMap<Component>
    {
        public ComponentMapInput()
        {
            Map(m => m.Id).Name("component_id");
            Map(m => m.Scheduler).Name("scheduler");
            Map(m => m.Budget).Name("budget");
            Map(m => m.Period).Name("period");
            Map(m => m.CoreId).Name("core_id");
            Map(m => m.Priority).Name("priority");
        }
    }
}