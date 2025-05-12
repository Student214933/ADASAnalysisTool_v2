using ADASAnalysisTool.Models;
using System.Globalization;

namespace ADASAnalysisTool.Utils
{
    internal class CsvExport
    {
        public static void ExportAnalysisToCsv(List<Component> components, string filePath)
        {
            var lines = new List<string>();
            lines.Add("task_name,component_schedulable,component_id,task_schedulable,WCRT");

            foreach (var component in components)
            {
                foreach (var task in component.Tasks)
                {
                    string taskName = task.Name;
                    string componentId = component.Id;
                    bool componentSchedulable = component.IsInterfaceSchedulable;
                    bool taskSchedulable = task.WCRT.HasValue && task.WCRT.Value <= task.Period;
                    string wcrt = task.WCRT.HasValue ? task.WCRT.Value.ToString("F4", CultureInfo.InvariantCulture) : "Infinity";

                    string line = $"{taskName},{componentSchedulable.ToString().ToLower()},{componentId},{taskSchedulable.ToString().ToLower()},{wcrt}";
                    lines.Add(line);
                }
            }

            File.WriteAllLines(filePath, lines);
        }
    }
}
