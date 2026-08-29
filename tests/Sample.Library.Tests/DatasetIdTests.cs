using Testing.Abstractions;

namespace Sample.Library.Tests;

public class DatasetIdTests
{
    [Theory]
    [InlineData("sample-math/add")]
    [InlineData("simple")]
    [InlineData("a/b/c")]
    [InlineData("with-dash/under_score/and123")]
    [InlineData("ABCDE/FGHIJ")]
    public void Normalize_ValidIds_ReturnsLowercase(string id)
    {
        var result = DatasetId.Normalize(id);

        Assert.NotNull(result);
        Assert.Equal(id.ToLowerInvariant(), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Normalize_NullOrEmpty_ReturnsNull(string? id)
    {
        var result = DatasetId.Normalize(id!);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("/starts-with-slash")]
    [InlineData("ends-with-slash/")]
    [InlineData("/both/")]
    public void Normalize_LeadingOrTrailingSlash_ReturnsNull(string id)
    {
        var result = DatasetId.Normalize(id);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("a//b")]
    [InlineData("empty//segments")]
    [InlineData("///triple")]
    public void Normalize_EmptySegments_ReturnsNull(string id)
    {
        var result = DatasetId.Normalize(id);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("a/./b")]
    [InlineData("./starts-with-dot-slash")]
    [InlineData("a/b/.")]
    [InlineData("a/../b")]
    [InlineData("..")]
    [InlineData("a/b/..")]
    public void Normalize_DotSegments_ReturnsNull(string id)
    {
        var result = DatasetId.Normalize(id);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("back\\slash")]
    [InlineData("a*b")]
    [InlineData("special#char")]
    [InlineData("emoji☺")]
    public void Normalize_InvalidCharacters_ReturnsNull(string id)
    {
        var result = DatasetId.Normalize(id);
        Assert.Null(result);
    }

    [Fact]
    public void Normalize_SameIdDifferentCase_ProducesSameNormalizedForm()
    {
        var lower = DatasetId.Normalize("some-dataset/math");
        var upper = DatasetId.Normalize("SOME-DATASET/MATH");

        Assert.NotNull(lower);
        Assert.NotNull(upper);
        Assert.Equal(lower, upper);
    }
}
