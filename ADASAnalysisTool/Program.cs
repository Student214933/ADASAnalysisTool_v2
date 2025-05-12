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
            Console.WriteLine("=============== ADAS Hierarchical Scheduling Analysis ===============\n");

            // Select test case folder (change as needed)
            const string folder = folder9;

            // Load system input from CSVs
            Console.WriteLine($"[INFO] Loading architecture, budgets, and tasks from folder: {folder}\n");
            var cores = CsvLoader.LoadCores(folder);
            var components = CsvLoader.LoadComponents(folder);
            var tasks = CsvLoader.LoadTasks(folder);

            if (!cores.Any() || !components.Any() || !tasks.Any())
            {
                Console.WriteLine("[ERROR] One or more CSV inputs are empty. Please check your input files.");
                return;
            }

            // Perform analysis
            Analyzer.AnalyzeSystem(cores, components, tasks);

            // Write output file
            CsvOutputWriter.WriteSolutionCSV(components, folder);

            Console.WriteLine("\n========================== Analysis Complete ==========================");
        }
    }
}
