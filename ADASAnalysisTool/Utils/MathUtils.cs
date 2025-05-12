namespace ADASAnalysisTool.Utils
{
    public static class MathUtils
    {
        /// <summary>
        /// Computes the Greatest Common Divisor (GCD) of two integers using Euclid's algorithm.
        /// </summary>
        public static long GCD(long a, long b)
        {
            a = Math.Abs(a);
            b = Math.Abs(b);
            while (b != 0)
            {
                long temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

        /// <summary>
        /// Computes the Least Common Multiple (LCM) of two integers.
        /// </summary>
        public static long LCM(long a, long b)
        {
            if (a == 0 || b == 0)
                return 0;

            return Math.Abs(a * b) / GCD(a, b);
        }

        /// <summary>
        /// Computes the LCM of a list of numbers. Input values are rounded to nearest integers.
        /// </summary>
        public static long LCM(List<double> numbers)
        {
            if (numbers == null || numbers.Count == 0)
                throw new ArgumentException("Number list is empty or null.");

            List<long> roundedInts = numbers.Select(n => (long)Math.Round(n)).ToList();

            long lcm = roundedInts[0];
            foreach (var num in roundedInts.Skip(1))
            {
                lcm = LCM(lcm, num);
            }
            return lcm;
        }
    }
}