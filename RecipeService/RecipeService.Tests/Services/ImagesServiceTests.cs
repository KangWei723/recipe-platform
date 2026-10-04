using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using RecipeService.Domain;
using RecipeService.Exceptions;
using RecipeService.Services;
using Xunit;

namespace RecipeService.Tests.Services;

public class ImagesServiceTests
{
    private readonly Mock<IImageUploadService> _uploadService = new();
    private readonly ImagesService _service;

    public ImagesServiceTests()
    {
        _service = new ImagesService(_uploadService.Object);
    }

    private static byte[] MakeJpegBytes(int totalLength = 200)
    {
        var bytes = new byte[totalLength];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        for (var i = 3; i < totalLength; i++)
        {
            bytes[i] = (byte)(i % 256);
        }
        return bytes;
    }

    private static IFormFile MakeFormFile(byte[] content, string fileName = "photo.jpg") =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName);

    [Fact]
    public async Task UploadAsync_WithValidImage_ReturnsUrlFromUploadService()
    {
        var file = MakeFormFile(MakeJpegBytes());
        _uploadService
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), "photo.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://res.cloudinary.com/demo/image/upload/v1/photo.jpg");

        var result = await _service.UploadAsync(file);

        result.Url.Should().Be("https://res.cloudinary.com/demo/image/upload/v1/photo.jpg");
    }

    [Fact]
    public async Task UploadAsync_WhenOversized_ThrowsWithoutCallingUploadService()
    {
        var oversized = MakeJpegBytes((int)ImageFileValidator.MaxSizeBytes + 1);
        var file = MakeFormFile(oversized);

        var act = () => _service.UploadAsync(file);

        await act.Should().ThrowAsync<ValidationException>();
        _uploadService.Verify(
            s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_WhenNotAnImage_ThrowsWithoutCallingUploadService()
    {
        var file = MakeFormFile(Encoding.UTF8.GetBytes("not an image, just text content here"));

        var act = () => _service.UploadAsync(file);

        await act.Should().ThrowAsync<ValidationException>();
        _uploadService.Verify(
            s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // This is the test that would fail if the stream rewind/reopen between the signature check
    // and the Cloudinary upload were missing or wrong -- it doesn't just check that
    // IImageUploadService was called, it checks exactly what bytes it received, including the
    // first 12 the signature check already consumed.
    [Fact]
    public async Task UploadAsync_PassesTheCompleteFileToUploadService_IncludingTheBytesTheSignatureCheckConsumed()
    {
        var originalBytes = MakeJpegBytes();
        var file = MakeFormFile(originalBytes);
        byte[]? capturedBytes = null;

        _uploadService
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream stream, string _, CancellationToken ct) =>
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                capturedBytes = buffer.ToArray();
                return "https://res.cloudinary.com/demo/image/upload/v1/photo.jpg";
            });

        await _service.UploadAsync(file);

        capturedBytes.Should().NotBeNull();
        capturedBytes.Should().Equal(originalBytes);
    }
}
