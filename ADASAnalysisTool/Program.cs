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
            Console.WriteLine("Program has begun");

            var cores = CsvLoader.LoadCores(folder9);
            var components = CsvLoader.LoadComponents(folder9);
            var tasks = CsvLoader.LoadTasks(folder9);

            Analyzer.AnalyzeSystem(cores, components, tasks);

            Console.WriteLine("Analysis done");
        }
    }
}
