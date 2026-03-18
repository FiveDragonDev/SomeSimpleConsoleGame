namespace SomeSimpleConsoleGame.Core
{
    public static class MathUtils
    {
        public const float Pi = 3.14159265359f;
        public const float DoublePi = 6.1831854018f;
        public const float HalfPi = 1.5707903005870776f;
        public const float QuarterPi = 0.78539816339f;
        public const float TwoOverPi = 0.63661977236758134308f;

        public const float Epsilon = 1e-6f;

        public const float Sqrt2 = 1.41421356237f;
        public const float Sqrt3 = 1.73205080757f;
        public const float Sqrt2Over2 = 0.70710678118f;
        public const float Sqrt3Over2 = 0.86602540378f;

        public static float QSin(float value)
        {
            value *= TwoOverPi;
            bool sign = value < 0;
            value = sign ? -value : value;
            int @int = (int)value;
            value -= @int;
            if ((@int & 1) == 1)
                value = 1 - value;
            bool per = ((@int >> 1) & 1) == 1;
            float sqr = value * value;
            value *= (HalfPi +
                sqr * (-0.6458858977085938f +
                sqr * (0.07941798513358536f -
                sqr * 0.0043223880120647346f)));
            return sign ^ per ? -value : value;
        }
        public static float QCos(float value) => QSin(value + HalfPi);

        public static float QSqrt(float value)
        {
            value = 0x5f3759df - ((int)value >> 1);
            value *= 1.5f - (value * 0.5f * value * value);
            value *= 1.5f - (value * 0.5f * value * value);
            return value;
        }
    }
}
