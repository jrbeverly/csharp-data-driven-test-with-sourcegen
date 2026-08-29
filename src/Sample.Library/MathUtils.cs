using Testing.Abstractions;

namespace Sample.Library;

public static class MathUtils
{
    [GenerateTests("sample-math/add")]
    public static int Add(int a, int b) => a + b;

    [GenerateTests("sample-math/subtract")]
    public static int Subtract(int a, int b) => a - b;

    public static int Multiply(int a, int b) => a * b;

    public static int Divide(int a, int b) => a / b;

    [GenerateTests("sample-math/clamp")]
    public static int Clamp(int value, int min, int max)
    {
        if (min > max)
            throw new ArgumentException("min must be less than or equal to max");

        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
