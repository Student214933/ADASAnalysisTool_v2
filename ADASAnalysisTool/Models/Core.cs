

using CsvHelper.Configuration;
using System.Globalization;

namespace ADASAnalysisTool.Models
{
    public class Core
    {
        public string Id;
        public double SpeedFactor;
        public string Scheduler; // "EDF" or "RM"

        public Core() {
        
        }
    }

    public class CoreMapInput : ClassMap<Core>
    {
        public CoreMapInput()
        {
            Map(m => m.Id).Name("core_id");
            Map(m => m.SpeedFactor).Name("speed_factor").TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
            Map(m => m.Scheduler).Name("scheduler");
        }
    }
}
