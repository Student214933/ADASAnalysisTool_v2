

namespace ADASAnalysisTool.Utils
{
    public static class MathUtils
    {
        public static double LCM(List<double> numbers)
        {
            double lcm = numbers[0];
            foreach (double num in numbers.Skip(1))
            {
                lcm = lcm * num / GCD(lcm, num);
            }
            return lcm;
        }
        public static double GCD(double a, double b)
        {
            while (b != 0)
            {
                double temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }
    }
}
