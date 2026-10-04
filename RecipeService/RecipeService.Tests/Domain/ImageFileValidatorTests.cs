using FluentAssertions;
using RecipeService.Domain;
using RecipeService.Exceptions;
using Xunit;

namespace RecipeService.Tests.Domain;

public class ImageFileValidatorTests
{
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] WebpHeader = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();

    [Fact]
    public void EnsureValidSignature_WhenJpeg_DoesNotThrow()
    {
        var act = () => ImageFileValidator.EnsureValidSignature(JpegHeader);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValidSignature_WhenPng_DoesNotThrow()
    {
        var act = () => ImageFileValidator.EnsureValidSignature(PngHeader);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValidSignature_WhenWebp_DoesNotThrow()
    {
        var act = () => ImageFileValidator.EnsureValidSignature(WebpHeader);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValidSignature_WhenPlainText_Throws()
    {
        var header = "Hello, this is not an image!"u8.ToArray();
        var act = () => ImageFileValidator.EnsureValidSignature(header);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureValidSignature_WhenHeaderTooShort_Throws()
    {
        var act = () => ImageFileValidator.EnsureValidSignature([0xFF, 0xD8]);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureValidSignature_WhenEmpty_Throws()
    {
        var act = () => ImageFileValidator.EnsureValidSignature(ReadOnlySpan<byte>.Empty);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureValidSignature_WhenRiffButNotWebp_Throws()
    {
        // RIFF container, but the format at offset 8 is AVI, not WEBP.
        var header = "RIFF\0\0\0\0AVI LIST"u8.ToArray();
        var act = () => ImageFileValidator.EnsureValidSignature(header);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureValidSignature_WhenExecutableMasqueradingWithImageExtension_Throws()
    {
        // The "MZ" DOS header every Windows .exe starts with -- this is the scenario a
        // filename/Content-Type-only check would miss (a renamed malicious.exe -> photo.jpg).
        var header = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };
        var act = () => ImageFileValidator.EnsureValidSignature(header);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureValidSize_WhenWithinLimit_DoesNotThrow()
    {
        var act = () => ImageFileValidator.EnsureValidSize(1024);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValidSize_WhenExactlyAtLimit_DoesNotThrow()
    {
        var act = () => ImageFileValidator.EnsureValidSize(ImageFileValidator.MaxSizeBytes);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValidSize_WhenOverLimit_Throws()
    {
        var act = () => ImageFileValidator.EnsureValidSize(ImageFileValidator.MaxSizeBytes + 1);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void EnsureValidSize_WhenZero_Throws()
    {
        var act = () => ImageFileValidator.EnsureValidSize(0);
        act.Should().Throw<ValidationException>();
    }
}
