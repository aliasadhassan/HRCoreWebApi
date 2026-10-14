namespace HR.Employee.API.Application.Employees.Photos;

using HR.Employee.API.Domain.Common;
using SkiaSharp;

/// <summary>
/// Upload ki hui photo → beech se square crop → max 256px WebP (~20–40 KB).
/// Re-encode se EXIF (GPS, camera) bhi hat jata hai, aur browser ko sirf humari banayi file milti hai.
/// </summary>
public static class PhotoImage
{
    public const int OutputSize = 256;
    public const int MinSide = 64;
    public const long MaxUploadBytes = 5 * 1024 * 1024;
    private const long MaxPixels = 40_000_000;   // decompression bomb se bachao
    private const int WebpQuality = 82;

    public const string ContentType = "image/webp";

    public static byte[] ToWebpSquare(byte[] input)
    {
        if (input.Length == 0)
            throw new DomainException("Choose a photo to upload.");
        if (input.Length > MaxUploadBytes)
            throw new DomainException("The photo is too large. The limit is 5 MB.");

        using var codec = SKCodec.Create(new SKMemoryStream(input))
            ?? throw new DomainException("This file is not a photo we can read. Use JPG, PNG or WebP.");

        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            throw new DomainException("Use a JPG, PNG or WebP photo.");

        var info = codec.Info;
        if ((long)info.Width * info.Height > MaxPixels)
            throw new DomainException("The photo has too many pixels. Use one under 40 megapixels.");
        if (Math.Min(info.Width, info.Height) < MinSide)
            throw new DomainException($"The photo is too small. Use one at least {MinSide} × {MinSide} pixels.");

        using var decoded = SKBitmap.Decode(codec)
            ?? throw new DomainException("This photo could not be read. Try another file.");
        using var oriented = Orient(decoded, codec.EncodedOrigin);

        var side = Math.Min(oriented.Width, oriented.Height);
        var size = Math.Min(OutputSize, side);
        var src = SKRectI.Create((oriented.Width - side) / 2, (oriented.Height - side) / 2, side, side);

        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var image = SKImage.FromBitmap(oriented))
        {
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawImage(image, src, SKRect.Create(size, size), new SKSamplingOptions(SKCubicResampler.Mitchell));
        }

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Webp, WebpQuality)
            ?? throw new DomainException("This photo could not be processed. Try another file.");
        return data.ToArray();
    }

    /// <summary>Phone camera ki EXIF rotation lagao (warna photo tirchi dikhti hai). Mirror wale rare cases as-is.</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        var swap = origin is SKEncodedOrigin.RightTop or SKEncodedOrigin.LeftBottom;
        if (!swap && origin != SKEncodedOrigin.BottomRight)
            return source.Copy();

        var w = swap ? source.Height : source.Width;
        var h = swap ? source.Width : source.Height;
        var result = new SKBitmap(w, h, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(result);
        switch (origin)
        {
            case SKEncodedOrigin.RightTop:      // 90° clockwise
                canvas.Translate(w, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.LeftBottom:    // 90° counter-clockwise
                canvas.Translate(0, h);
                canvas.RotateDegrees(270);
                break;
            default:                            // BottomRight: 180°
                canvas.RotateDegrees(180, w / 2f, h / 2f);
                break;
        }
        canvas.DrawBitmap(source, 0, 0);
        return result;
    }
}
