using FluentAssertions;
using RecipeService.Domain;
using RecipeService.Exceptions;
using Xunit;

namespace RecipeService.Tests.Domain;

public class ImageUrlValidatorTests
{
    [Fact]
    public void EnsureValid_WhenNullOrEmpty_DoesNotThrow()
    {
        var act = () => ImageUrlValidator.EnsureValid(null);
        act.Should().NotThrow();

        var actEmpty = () => ImageUrlValidator.EnsureValid("");
        actEmpty.Should().NotThrow();
    }

    [Fact]
    public void EnsureValid_WhenHttps_DoesNotThrow()
    {
        var act = () => ImageUrlValidator.EnsureValid("https://cdn.example.com/photo.jpg");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("http://cdn.example.com/photo.jpg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    public void EnsureValid_WhenNotAbsoluteHttps_Throws(string url)
    {
        var act = () => ImageUrlValidator.EnsureValid(url);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureValid_WhenOverMaxLength_Throws()
    {
        var url = "https://example.com/" + new string('a', ImageUrlValidator.MaxLength);
        var act = () => ImageUrlValidator.EnsureValid(url);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureValid_WhenNoRestrictionConfigured_AnyHttpsHostPasses()
    {
        var act = () => ImageUrlValidator.EnsureValid(
            "https://res.cloudinary.com/someone-elses-cloud/image/upload/v1/photo.jpg", requiredCloudinaryCloudName: null);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValid_WhenRestrictionConfigured_AndUrlMatchesConfiguredCloud_DoesNotThrow()
    {
        var act = () => ImageUrlValidator.EnsureValid(
            "https://res.cloudinary.com/my-cloud/image/upload/v1/photo.jpg", requiredCloudinaryCloudName: "my-cloud");
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValid_WhenRestrictionConfigured_AndUrlIsFromAnUnrelatedHost_Throws()
    {
        var act = () => ImageUrlValidator.EnsureValid(
            "https://cdn.example.com/photo.jpg", requiredCloudinaryCloudName: "my-cloud");
        act.Should().Throw<ValidationException>();
    }

    // The edge case a naive "does the URL contain cloudinary.com" check would miss -- it must be
    // *our configured* cloud specifically, not any Cloudinary account.
    [Fact]
    public void EnsureValid_WhenRestrictionConfigured_AndUrlIsFromADifferentCloudinaryCloud_Throws()
    {
        var act = () => ImageUrlValidator.EnsureValid(
            "https://res.cloudinary.com/someone-elses-cloud/image/upload/v1/photo.jpg",
            requiredCloudinaryCloudName: "my-cloud");
        act.Should().Throw<ValidationException>();
    }
}
