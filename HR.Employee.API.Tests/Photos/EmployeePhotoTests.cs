namespace HR.Employee.API.Tests.Photos;

using System.Net;
using System.Text;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Application.Employees.Photos;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Infrastructure.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Xunit;

public class EmployeePhotoTests
{
    static byte[] MakeImage(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SteelBlue);
            using var paint = new SKPaint { Color = SKColors.Gold };
            canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png)]
    public void Large_photo_becomes_256_square_webp(SKEncodedImageFormat format)
    {
        var webp = PhotoImage.ToWebpSquare(MakeImage(1200, 800, format));

        using var codec = SKCodec.Create(new SKMemoryStream(webp));
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        Assert.Equal(256, codec.Info.Width);
        Assert.Equal(256, codec.Info.Height);
        Assert.True(webp.Length < 60 * 1024, $"webp was {webp.Length} bytes");
    }

    [Fact]
    public void Small_photo_is_not_upscaled()
    {
        var webp = PhotoImage.ToWebpSquare(MakeImage(120, 200, SKEncodedImageFormat.Png));

        using var codec = SKCodec.Create(new SKMemoryStream(webp));
        Assert.Equal(120, codec.Info.Width);
        Assert.Equal(120, codec.Info.Height);
    }

    [Fact]
    public void Tiny_photo_is_rejected()
        => Assert.Throws<DomainException>(() => PhotoImage.ToWebpSquare(MakeImage(40, 40, SKEncodedImageFormat.Png)));

    [Fact]
    public void Non_image_is_rejected()
        => Assert.Throws<DomainException>(() => PhotoImage.ToWebpSquare(Encoding.UTF8.GetBytes("<svg onload=alert(1)>")));

    [Fact]
    public void Empty_and_oversized_files_are_rejected()
    {
        Assert.Throws<DomainException>(() => PhotoImage.ToWebpSquare([]));
        Assert.Throws<DomainException>(() => PhotoImage.ToWebpSquare(new byte[PhotoImage.MaxUploadBytes + 1]));
    }

    [Fact]
    public void Key_is_under_tenant_and_employee_folder_and_unique()
    {
        Guid tenant = Guid.NewGuid(), employee = Guid.NewGuid();
        var a = EmployeePhotoUrls.NewKey(tenant, employee);
        var b = EmployeePhotoUrls.NewKey(tenant, employee);

        Assert.StartsWith($"{tenant:D}/{employee:D}/", a);
        Assert.EndsWith(".webp", a);
        Assert.NotEqual(a, b);
    }

    // ───── Signed URLs ─────
    sealed class FakeStorage(bool configured = true) : IFileStorage
    {
        public int SignCalls;
        public bool IsConfigured => configured;
        public Task UploadAsync(string path, byte[] content, string contentType, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(IReadOnlyCollection<string> paths, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<string, string>> SignAsync(IReadOnlyCollection<string> paths, TimeSpan expiresIn, CancellationToken cancellationToken)
        {
            SignCalls++;
            return Task.FromResult<IReadOnlyDictionary<string, string>>(paths.ToDictionary(p => p, p => $"https://x/{p}?token={SignCalls}"));
        }
    }

    static EmployeePhotoUrls Urls(IFileStorage storage)
        => new(storage, new MemoryCache(new MemoryCacheOptions()), Options.Create(new StorageOptions()), NullLogger<EmployeePhotoUrls>.Instance);

    [Fact]
    public async Task Urls_are_signed_in_one_batch_then_cached()
    {
        var storage = new FakeStorage();
        var urls = Urls(storage);

        var first = await urls.ResolveAsync(["t/a/1.webp", "t/b/2.webp", null, "t/a/1.webp"], CancellationToken.None);
        var second = await urls.ResolveAsync(["t/a/1.webp", "t/b/2.webp"], CancellationToken.None);

        Assert.Equal(2, first.Count);
        Assert.Equal(1, storage.SignCalls);
        Assert.Equal(first["t/a/1.webp"], second["t/a/1.webp"]);   // same token = browser cache kaam karta hai
    }

    [Fact]
    public async Task No_urls_when_storage_is_not_configured()
    {
        var storage = new FakeStorage(configured: false);
        var urls = Urls(storage);

        Assert.Empty(await urls.ResolveAsync(["t/a/1.webp"], CancellationToken.None));
        Assert.Equal(0, storage.SignCalls);
        Assert.False(urls.Enabled);
    }

    sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Last;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    static SupabaseStorage Supabase(StubHandler handler, string? key = "sb_secret_test")
        => new(new HttpClient(handler), Options.Create(new StorageOptions { SupabaseUrl = "https://ref.supabase.co/", ServiceKey = key }),
               NullLogger<SupabaseStorage>.Instance);

    [Fact]
    public async Task Supabase_sign_returns_absolute_urls_and_skips_errors()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            [{"path":"t/a/1.webp","signedURL":"/object/sign/employee-photos/t/a/1.webp?token=abc","error":null},
             {"path":"t/b/2.webp","signedURL":null,"error":"Either the object does not exist or you do not have access to it"}]
            """);

        var result = await Supabase(handler).SignAsync(["t/a/1.webp", "t/b/2.webp"], TimeSpan.FromHours(1), CancellationToken.None);

        Assert.Equal("https://ref.supabase.co/storage/v1/object/sign/employee-photos/t/a/1.webp?token=abc", Assert.Single(result).Value);
        Assert.Equal("https://ref.supabase.co/storage/v1/object/sign/employee-photos", handler.Last!.RequestUri!.ToString());
        Assert.Equal("sb_secret_test", handler.Last.Headers.GetValues("apikey").Single());
    }

    [Fact]
    public async Task Supabase_errors_and_missing_key_throw_storage_exception()
    {
        var failing = Supabase(new StubHandler(HttpStatusCode.BadRequest, """{"message":"bad"}"""));
        await Assert.ThrowsAsync<FileStorageException>(() => failing.UploadAsync("t/a/1.webp", [1], "image/webp", CancellationToken.None));

        var off = Supabase(new StubHandler(HttpStatusCode.OK, "[]"), key: null);
        Assert.False(off.IsConfigured);
        var ex = await Assert.ThrowsAsync<FileStorageException>(() => off.UploadAsync("t/a/1.webp", [1], "image/webp", CancellationToken.None));
        Assert.True(ex.NotConfigured);
    }
}
