using Microsoft.AspNetCore.Http;
using PackageManager.Server;

namespace PackageManager.Tests;

public class TextTests
{
    private static readonly Text Sample = new("Không tìm thấy.", "Not found.");

    [Theory]
    [InlineData(null, "Không tìm thấy.")]
    [InlineData("vi", "Không tìm thấy.")]
    [InlineData("en", "Not found.")]
    [InlineData("en-US", "Not found.")]
    // The web UI sends "vi" explicitly; a browser-style list that prefers English still gets English.
    [InlineData("en-US,en;q=0.9,vi;q=0.8", "Not found.")]
    [InlineData("vi-VN,vi;q=0.9,en;q=0.8", "Không tìm thấy.")]
    [InlineData("vi;q=0.5,en;q=0.9", "Not found.")]
    [InlineData("fr", "Không tìm thấy.")]
    public void For_PicksLanguageFromAcceptLanguage(string? header, string expected)
    {
        var ctx = new DefaultHttpContext();
        if (header != null) ctx.Request.Headers.AcceptLanguage = header;
        Assert.Equal(expected, Sample.For(ctx.Request));
        Assert.Equal(expected, ctx.Error(Sample).Message);
    }
}
