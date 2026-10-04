using RecipeService.Exceptions;

namespace RecipeService.Domain;

// Checked against the actual bytes (the "magic number" at the start of the file), never the
// filename or the client-declared Content-Type header -- either of those can lie, the bytes
// can't.
public static class ImageFileValidator
{
    public const long MaxSizeBytes = 5 * 1024 * 1024; // 5 MB -- generous for a compressed
    // JPEG/WebP recipe photo, bounds Cloudinary free-tier usage and request cost. The web-client
    // downscales before upload so a typical phone photo lands well under this; this limit is the
    // real (server-enforced) backstop regardless of whether that happened.

    // Longest signature checked below (WebP's) needs 12 bytes.
    public const int SignatureLengthBytes = 12;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();

    public static void EnsureValidSize(long sizeBytes)
    {
        if (sizeBytes <= 0)
        {
            throw new ValidationException("File is empty");
        }

        if (sizeBytes > MaxSizeBytes)
        {
            throw new ValidationException($"File exceeds maximum size of {MaxSizeBytes / (1024 * 1024)} MB");
        }
    }

    public static void EnsureValidSignature(ReadOnlySpan<byte> header)
    {
        if (StartsWith(header, JpegSignature) || StartsWith(header, PngSignature) || IsWebp(header))
        {
            return;
        }

        throw new ValidationException("File is not a recognized JPEG, PNG, or WebP image");
    }

    private static bool StartsWith(ReadOnlySpan<byte> header, byte[] signature) =>
        header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature);

    private static bool IsWebp(ReadOnlySpan<byte> header) =>
        header.Length >= 12 && header[..4].SequenceEqual(RiffSignature) && header[8..12].SequenceEqual(WebpSignature);
}
