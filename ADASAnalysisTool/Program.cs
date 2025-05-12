using ADASAnalysisTool.Analysis;
using ADASAnalysisTool.Utils;


namespace ADASAnalysisTool
{
    internal class Program
    {
        const String folder1 = "1-tiny-test-case";
        const String folder2 = "2-small-test-case";
        const String folder3 = "3-medium-test-case";
        const String folder4 = "4-large-test-case";
        const String folder5 = "5-huge-test-case";
        const String folder6 = "6-gigantic-test-case";
        const String folder7 = "7-unschedulable-test-case";
        const String folder8 = "8-unschedulable-test-case";
        const String folder9 = "9-unschedulable-test-case";
        const String folder10 = "10-unschedulable-test-case";
        static void Main(string[] args)
        {
            Console.WriteLine("=============== ADAS Hierarchical Scheduling Analysis ===============");

            var folder = folder1;

            var cores = CsvLoader.LoadCores(folder);
            var components = CsvLoader.LoadComponents(folder);
            var tasks = CsvLoader.LoadTasks(folder);

            Analyzer.AnalyzeSystem(cores, components, tasks, $"Data/{folder}/analysis_output.csv");

            // Flatten all tasks into one list for correct export (from within component objects)
            var allTasks = components.SelectMany(c => c.Tasks).ToList();

            Console.WriteLine("========================== Analysis Complete ==========================");
        }
    }
}
