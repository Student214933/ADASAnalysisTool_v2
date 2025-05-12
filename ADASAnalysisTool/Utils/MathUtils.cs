namespace ADASAnalysisTool.Utils
{
    public static class MathUtils
    {
        // Computes least common multiple of a list of periods (in integers)
        public static double LCM(List<double> numbers)
        {
            if (numbers == null || numbers.Count == 0)
                throw new ArgumentException("LCM input list must not be empty.");

            double lcm = numbers[0];
            foreach (double num in numbers.Skip(1))
            {
                lcm = lcm * num / GCD(lcm, num);
            }
            return lcm;
        }

        // Computes greatest common divisor using Euclidean algorithm
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