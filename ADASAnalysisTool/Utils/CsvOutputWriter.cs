using ADASAnalysisTool.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace ADASAnalysisTool.Utils
{
    internal class CsvOutputWriter
    {
        public static void WriteSolutionCSV(List<Component> components, string folderName)
        {
            var records = new List<SolutionRecord>();

            foreach (var comp in components)
            {
                foreach (var task in comp.Tasks)
                {
                    records.Add(new SolutionRecord
                    {
                        TaskName = task.Name,
                        ComponentId = comp.Id,
                        CoreId = comp.CoreId,
                        Scheduler = comp.Scheduler,
                        WCET = task.WCET,
                        Period = task.Period,
                        Alpha = comp.Alpha,
                        Delta = comp.Delta,
                        Schedulable = comp.IsInterfaceSchedulable ? "Yes" : "No"
                    });
                }
            }

            using var writer = new StreamWriter($"Data/{folderName}/analysis_output.csv");
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.Context.RegisterClassMap<SolutionRecordMap>();
            csv.WriteRecords(records);
            Console.WriteLine($"[INFO] Analysis results saved to Data/{folderName}/analysis_output.csv");
        }

        public class SolutionRecord
        {
            public string TaskName { get; set; }
            public string ComponentId { get; set; }
            public string CoreId { get; set; }
            public string Scheduler { get; set; }
            public double WCET { get; set; }
            public double Period { get; set; }
            public double Alpha { get; set; }
            public double Delta { get; set; }
            public string Schedulable { get; set; }
        }

        public sealed class SolutionRecordMap : ClassMap<SolutionRecord>
        {
            public SolutionRecordMap()
            {
                Map(m => m.TaskName).Name("task_name");
                Map(m => m.ComponentId).Name("component_id");
                Map(m => m.CoreId).Name("core_id");
                Map(m => m.Scheduler).Name("scheduler");
                Map(m => m.WCET).Name("wcet");
                Map(m => m.Period).Name("period");
                Map(m => m.Alpha).Name("alpha");
                Map(m => m.Delta).Name("delta");
                Map(m => m.Schedulable).Name("schedulable");
            }
        }
    }
}
