using ADASAnalysisTool.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace ADASAnalysisTool.Utils
{
    public static class CsvLoader
    {
        public static List<Core> LoadCores(string folder)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                MissingFieldFound = null
            };

            List<Core> cores = new();
            using (StreamReader sr = new StreamReader($"Data/{folder}/architecture.csv"))
            using (var csv = new CsvReader(sr, config))
            {
                csv.Context.RegisterClassMap<CoreMapInput>();
                cores = csv.GetRecords<Core>().Where(c => c.SpeedFactor > 0).ToList();
                foreach (var c in cores)
                {
                    if (c.SpeedFactor <= 0)
                        Console.WriteLine($"[Warning] Core {c.Id} has non-positive SpeedFactor: {c.SpeedFactor}");
                }
            }

            Console.WriteLine($"[INFO] Loaded {cores.Count} cores from architecture.csv");
            return cores;
        }

        public static List<Component> LoadComponents(string folder)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                MissingFieldFound = null
            };

            List<Component> components = new();
            using (StreamReader sr = new StreamReader($"Data/{folder}/budgets.csv"))
            using (var csv = new CsvReader(sr, config))
            {
                csv.Context.RegisterClassMap<ComponentMapInput>();
                components = csv.GetRecords<Component>().ToList();
            }

            Console.WriteLine($"[INFO] Loaded {components.Count} components from budgets.csv");
            return components;
        }

        public static List<Tasks> LoadTasks(string folder)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                MissingFieldFound = null
            };

            List<Tasks> tasks = new();
            using (StreamReader sr = new StreamReader($"Data/{folder}/tasks.csv"))
            using (var csv = new CsvReader(sr, config))
            {
                csv.Context.RegisterClassMap<TaskMapInput>();
                var all = csv.GetRecords<Tasks>().ToList();

                foreach (var t in all)
                {
                    if (t.Period <= 0 || t.WCET < 0)
                    {
                        Console.WriteLine($"[Warning] Skipping invalid task: {t.Name}, Period={t.Period}, WCET={t.WCET}");
                        continue;
                    }
                    tasks.Add(t);
                }
            }

            Console.WriteLine($"[INFO] Loaded {tasks.Count} valid tasks from tasks.csv");
            return tasks;
        }
    }
}