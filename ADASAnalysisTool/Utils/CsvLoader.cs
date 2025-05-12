

using ADASAnalysisTool.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace ADASAnalysisTool.Utils
{
    public static class CsvLoader
    {
        public static List<Core> LoadCores(String folder)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                MissingFieldFound = null
            };

            List<Core> cores = new List<Core>();
            using (StreamReader sr = new StreamReader($"Data/{folder}/architecture.csv"))
            {
                using (var csv = new CsvReader(sr, config))
                {
                    csv.Context.RegisterClassMap<CoreMapInput>();
                    cores = csv.GetRecords<Core>().ToList();
                }
            }

            return cores;
        }

        public static List<Component> LoadComponents(String folder)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                MissingFieldFound = null
            };

            List<Component> components = new List<Component>();
            using (StreamReader sr = new StreamReader($"Data/{folder}/budgets.csv"))
            {
                using (var csv = new CsvReader(sr, config))
                {
                    csv.Context.RegisterClassMap<ComponentMapInput>();
                    components = csv.GetRecords<Component>().ToList();
                }
            }

            return components;
        }

        public static List<Tasks> LoadTasks(String folder)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                MissingFieldFound = null
            };

            List<Tasks> tasks = new List<Tasks>();
            using (StreamReader sr = new StreamReader($"Data/{folder}/tasks.csv"))
            {
                using (var csv = new CsvReader(sr, config))
                {
                    csv.Context.RegisterClassMap<TaskMapInput>();
                    tasks = csv.GetRecords<Tasks>().ToList();
                }
            }

            return tasks;
        }
    }
}
