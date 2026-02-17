using MangaMesh.Peer.Core.Chapters;

namespace MangaMesh.Peer.Tests.Chapters;

[TestClass]
public class DefaultImageFormatProviderTests
{
    private readonly DefaultImageFormatProvider _provider = new();

    // ── IsSupported ──────────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("001.jpg",  true)]
    [DataRow("001.jpeg", true)]
    [DataRow("001.png",  true)]
    [DataRow("001.webp", true)]
    [DataRow("001.JPG",  true)]   // case insensitive
    [DataRow("001.PNG",  true)]
    [DataRow("001.WEBP", true)]
    [DataRow("001.txt",  false)]
    [DataRow("001.pdf",  false)]
    [DataRow("001.zip",  false)]
    [DataRow("noextension", false)]
    public void IsSupported_VariousExtensions(string filename, bool expected)
    {
        Assert.AreEqual(expected, _provider.IsSupported(filename));
    }

    // ── GetMimeType ──────────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("page.jpg",  "image/jpeg")]
    [DataRow("page.jpeg", "image/jpeg")]
    [DataRow("page.JPG",  "image/jpeg")]
    [DataRow("page.png",  "image/png")]
    [DataRow("page.PNG",  "image/png")]
    [DataRow("page.webp", "image/webp")]
    [DataRow("page.WEBP", "image/webp")]
    [DataRow("page.bmp",  "application/octet-stream")]
    [DataRow("page.txt",  "application/octet-stream")]
    public void GetMimeType_ReturnsCorrectMime(string filename, string expected)
    {
        Assert.AreEqual(expected, _provider.GetMimeType(filename));
    }
}
