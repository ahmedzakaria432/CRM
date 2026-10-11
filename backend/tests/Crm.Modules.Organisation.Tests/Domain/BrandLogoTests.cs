using System.Text;
using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class BrandLogoTests
{
    private static byte[] WithBom(string text) => [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)];

    private static string Code(Action action) => Assert.Throws<DomainException>(action).Error.Code;

    [Fact]
    public void SniffRecognisesAPng()
    {
        Assert.Equal(LogoContentType.Png, BrandLogo.Sniff(Logos.PngBytes()));
    }

    [Fact]
    public void SniffAcceptsTheBareEightByteSignature()
    {
        // Pinned: only the signature is checked, so anything after it (even nothing) is accepted.
        Assert.Equal(LogoContentType.Png, BrandLogo.Sniff(Logos.PngBytes(8)));
    }

    [Fact]
    public void SniffRejectsAnEmptyFile()
    {
        Assert.Equal("Organisation.LogoEmpty", Code(() => BrandLogo.Sniff([])));
    }

    [Fact]
    public void SniffAcceptsExactlyTheMaximumSize()
    {
        Assert.Equal(524_288, BrandLogo.MaxBytes);
        Assert.Equal(LogoContentType.Png, BrandLogo.Sniff(Logos.PngBytes(BrandLogo.MaxBytes)));
    }

    [Fact]
    public void SniffRejectsOneByteOverTheMaximum()
    {
        byte[] tooLarge = Logos.PngBytes(BrandLogo.MaxBytes + 1);

        Assert.Equal("Organisation.LogoTooLarge", Code(() => BrandLogo.Sniff(tooLarge)));
    }

    [Fact]
    public void SniffRecognisesAnSvg()
    {
        Assert.Equal(LogoContentType.Svg, BrandLogo.Sniff(Logos.SvgBytes()));
    }

    [Fact]
    public void SniffRecognisesAnSvgWithAByteOrderMark()
    {
        Assert.Equal(LogoContentType.Svg, BrandLogo.Sniff(WithBom(Logos.SvgText)));
    }

    [Fact]
    public void SniffRecognisesAnSvgWithLeadingWhitespace()
    {
        Assert.Equal(LogoContentType.Svg, BrandLogo.Sniff(Logos.SvgBytes("  \r\n\t" + Logos.SvgText)));
    }

    [Fact]
    public void SniffRecognisesAnUpperCaseSvgElement()
    {
        Assert.Equal(LogoContentType.Svg, BrandLogo.Sniff(Logos.SvgBytes("<SVG></SVG>")));
    }

    [Fact]
    public void SniffRecognisesAnSvgBehindAnXmlDeclaration()
    {
        Assert.Equal(LogoContentType.Svg, BrandLogo.Sniff(Logos.SvgBytes("<?xml version=\"1.0\"?><svg></svg>")));
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("{\"svg\": true}")]
    [InlineData("<html><body>no vector here</body></html>")]
    [InlineData("<?xml version=\"1.0\"?><root/>")]
    [InlineData("x<svg></svg>")]
    public void SniffRejectsTextThatIsNotAnSvg(string text)
    {
        byte[] content = Encoding.UTF8.GetBytes(text);

        Assert.Equal("Organisation.LogoTypeNotAllowed", Code(() => BrandLogo.Sniff(content)));
    }

    [Fact]
    public void SniffRejectsInvalidUtf8()
    {
        byte[] content = [0xFF, 0xFE, 0x00];

        Assert.Equal("Organisation.LogoTypeNotAllowed", Code(() => BrandLogo.Sniff(content)));
    }

    [Fact]
    public void SniffRejectsAJpeg()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

        Assert.Equal("Organisation.LogoTypeNotAllowed", Code(() => BrandLogo.Sniff(jpeg)));
    }

    [Fact]
    public void SniffRejectsAGif()
    {
        byte[] gif = Encoding.ASCII.GetBytes("GIF89a......");

        Assert.Equal("Organisation.LogoTypeNotAllowed", Code(() => BrandLogo.Sniff(gif)));
    }

    [Fact]
    public void SniffRejectsAPngWithAWrongSignatureByte()
    {
        byte[] content = Logos.PngBytes();
        content[7] = 0x0B;

        Assert.Equal("Organisation.LogoTypeNotAllowed", Code(() => BrandLogo.Sniff(content)));
    }

    [Fact]
    public void SniffRejectsNull()
    {
        Func<byte[], LogoContentType> sniff = BrandLogo.Sniff;

        NullArguments.AssertThrowsArgumentNull(sniff, new object?[] { null });
    }

    [Fact]
    public void CreateOfAPngSetsTypeAndMediaType()
    {
        BrandLogo logo = Logos.Png();

        Assert.Equal(LogoContentType.Png, logo.ContentType);
        Assert.Equal("image/png", logo.MediaType);
        Assert.Equal(Ids.TenantA, logo.TenantId);
    }

    [Fact]
    public void CreateOfAnSvgSetsTypeAndMediaType()
    {
        BrandLogo logo = Logos.Svg();

        Assert.Equal(LogoContentType.Svg, logo.ContentType);
        Assert.Equal("image/svg+xml", logo.MediaType);
    }

    [Fact]
    public void CreateCopiesTheContent()
    {
        byte[] source = Logos.PngBytes();
        BrandLogo logo = BrandLogo.Create(Ids.TenantA, source);

        source[10] = 0x7F;

        Assert.Equal(0, logo.Content[10]);
        Assert.NotSame(source, logo.Content);
    }

    [Fact]
    public void CreateUsesTheSuppliedId()
    {
        var id = BrandLogoId.New();

        Assert.Equal(id, BrandLogo.Create(Ids.TenantA, Logos.PngBytes(), id).Id);
    }

    [Fact]
    public void CreateGeneratesAnIdWhenNoneIsSupplied()
    {
        BrandLogo first = Logos.Png();
        BrandLogo second = Logos.Png();

        Assert.NotEqual(Guid.Empty, first.Id.Value);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void CreateRefusesTheNoneTenant()
    {
        Assert.Equal("Organisation.TenantRequired", Code(() => BrandLogo.Create(TenantId.None, Logos.PngBytes())));
    }

    [Fact]
    public void CreateRejectsANullTenant()
    {
        Func<TenantId, byte[], BrandLogoId?, BrandLogo> create = BrandLogo.Create;

        NullArguments.AssertThrowsArgumentNull(create, null, Logos.PngBytes(), null);
    }

    [Fact]
    public void BelongsToIsTrueOnlyForTheOwningTenant()
    {
        BrandLogo logo = Logos.Png(Ids.TenantA);

        Assert.True(logo.BelongsTo(Ids.TenantA));
        Assert.False(logo.BelongsTo(Ids.TenantB));
    }

    [Fact]
    public void EveryContentSniffRejectsCreateRejectsToo()
    {
        byte[][] rejected =
        [
            [],
            [0xFF, 0xFE, 0x00],
            Encoding.UTF8.GetBytes("plain text"),
            Logos.PngBytes(BrandLogo.MaxBytes + 1),
        ];

        foreach (byte[] content in rejected)
        {
            string sniff = Code(() => BrandLogo.Sniff(content));
            string create = Code(() => BrandLogo.Create(Ids.TenantA, content));

            Assert.Equal(sniff, create);
        }
    }

    [Fact]
    public void ALogoRaisesNoDomainEvent()
    {
        Assert.Empty(Logos.Png().DomainEvents);
    }
}
