using Mdz.Core;

namespace Mdz.Tests;

public class PathValidatorTests
{
    [Theory]
    [InlineData("index.md")]
    [InlineData("assets/images/cover.png")]
    [InlineData("chapter-01.md")]
    [InlineData("assets/styles/style.css")]
    public void Validate_ValidPaths_ReturnsNull(string path)
    {
        Assert.Null(PathValidator.Validate(path));
    }

    [Fact]
    public void Validate_EmptyPath_ReturnsError()
    {
        var error = PathValidator.Validate(string.Empty);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("/absolute/path.md")]
    public void Validate_LeadingSlash_ReturnsError(string path)
    {
        var error = PathValidator.Validate(path);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("../escape.md")]
    [InlineData("assets/../../etc/passwd")]
    [InlineData("foo/../../../bar.md")]
    public void Validate_PathTraversal_ReturnsError(string path)
    {
        var error = PathValidator.Validate(path);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("file\0name.md")]
    [InlineData("file\u001fname.md")]
    [InlineData("file\u007fname.md")]
    public void Validate_ControlCharacters_ReturnsError(string path)
    {
        var error = PathValidator.Validate(path);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("file:name.md")]
    [InlineData("file*name.md")]
    [InlineData("file?name.md")]
    [InlineData("file\"name.md")]
    [InlineData("file<name.md")]
    [InlineData("file>name.md")]
    [InlineData("file|name.md")]
    [InlineData("file\\name.md")]
    public void Validate_ReservedCharacters_ReturnsError(string path)
    {
        var error = PathValidator.Validate(path);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("index.md", true)]
    [InlineData("../traversal.md", false)]
    [InlineData("/absolute.md", false)]
    public void IsValid_ReturnsExpectedResult(string path, bool expected)
    {
        Assert.Equal(expected, PathValidator.IsValid(path));
    }
}
