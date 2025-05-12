

using CsvHelper.Configuration;

namespace ADASAnalysisTool.Models
{
    public class Tasks
    {
        public string Name;
        public double WCET;
        public double Period;
        public string ComponentId;
        public int? Priority; // Only relevant for RM

        public Tasks()
        {

        }
    }
    public class TaskMapInput : ClassMap<Tasks>
    {
        public TaskMapInput()
        {
            Map(m => m.Name).Name("task_name");
            Map(m => m.WCET).Name("wcet");
            Map(m => m.Period).Name("period");
            Map(m => m.ComponentId).Name("component_id");
            Map(m => m.Priority).Name("priority");
        }
    }
}
