using System.Runtime.CompilerServices;

namespace SomeSimpleConsoleGame.Core
{
    public static class MathUtils
    {
        public const float Pi = 3.14159265359f;
        public const float DoublePi = Pi * 2f;
        public const float HalfPi = Pi * 0.5f;
        public const float QuarterPi = Pi * 0.25f;
        public const float TwoOverPi = 2f / Pi;

        public const float Deg2Rad = Pi / 180f;
        public const float Rad2Deg = 180f / Pi;

        public static readonly float Sqrt2 = MathF.Sqrt(2) * 0.5f;
        public static readonly float Sqrt3 = MathF.Sqrt(2) * 0.5f;
        public static readonly float Sqrt2Over2 = Sqrt2 * 0.5f;
        public static readonly float Sqrt3Over2 = Sqrt3 * 0.5f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RoundToInt(float value) => (int)(value + (0.5f * (value > 0 ? 1 : -1)));

        // I will pray on  Habr
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float QSin(float value)
        {
            const float It1 = -0.6458858977085938f;
            const float It2 = 0.07941798513358536f;
            const float It3 = -0.0043223880120647346f;

            value *= TwoOverPi;
            bool sign = value < 0;
            value = sign ? -value : value;
            int asInt = (int)value;
            value -= asInt;
            if ((asInt & 1) == 1) value = 1 - value;
            bool per = ((asInt >> 1) & 1) == 1;
            float sqr = value * value;
            value *= HalfPi + (sqr * (It1 + (sqr * (It2 + (sqr * It3)))));
            return sign ^ per ? -value : value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float QCos(float value) => QSin(value + HalfPi);
    }
}
