using Sample.Library;

namespace Sample.Library.Tests;

// Handwritten coverage is retained ONLY for methods not yet opted into generated
// dataset-driven testing. Add/Subtract/Clamp carry [GenerateTests(...)] and are
// fully covered by the generated theories under datasets/sample-math/*, so their
// handwritten cases were removed as redundant. Multiply/Divide have no datasets
// yet, so they remain handwritten until migrated.
public class MathUtilsTests
{
    [Theory]
    [InlineData(2, 3, 6)]
    [InlineData(-2, 3, -6)]
    [InlineData(0, 5, 0)]
    [InlineData(-3, -4, 12)]
    public void Multiply_ReturnsExpected(int a, int b, int expected)
    {
        var result = MathUtils.Multiply(a, b);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(10, 2, 5)]
    [InlineData(0, 5, 0)]
    [InlineData(-12, 4, -3)]
    [InlineData(7, 1, 7)]
    public void Divide_WithValidDivisor_ReturnsExpected(int a, int b, int expected)
    {
        var result = MathUtils.Divide(a, b);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Divide_ByZero_ThrowsDivideByZeroException()
    {
        var ex = Assert.Throws<DivideByZeroException>(() => MathUtils.Divide(5, 0));
        Assert.NotNull(ex);
    }
}
