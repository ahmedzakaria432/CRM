using Crm.Modules.Organisation.Domain;
using Crm.Modules.Organisation.Tests.Support;
using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Tests.Domain;

public sealed class BrandColourTests
{
    [Fact]
    public void CreateTrimsUpperCasesAndParsesChannels()
    {
        BrandColour colour = BrandColour.Create(" #0e6655 ");

        Assert.Equal("#0E6655", colour.Hex);
        Assert.Equal(0x0E, colour.R);
        Assert.Equal(0x66, colour.G);
        Assert.Equal(0x55, colour.B);
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    [InlineData("123456")]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsInvalidHex(string hex)
    {
        var exception = Assert.Throws<DomainException>(() => BrandColour.Create(hex));

        Assert.Equal("Organisation.BrandColourInvalid", exception.Error.Code);
    }

    [Fact]
    public void CreateRejectsNull()
    {
        Func<string, BrandColour> create = BrandColour.Create;

        NullArguments.AssertThrowsArgumentNull(create, new object?[] { null });
    }

    [Fact]
    public void WhiteIsFFFFFF()
    {
        Assert.Equal("#FFFFFF", BrandColour.White.Hex);
        Assert.Equal(255, BrandColour.White.R);
        Assert.Equal(255, BrandColour.White.G);
        Assert.Equal(255, BrandColour.White.B);
    }

    [Fact]
    public void LuminanceOfBlackIsZeroAndOfWhiteIsOne()
    {
        Assert.Equal(0, BrandColour.Create("#000000").RelativeLuminance, 6);
        Assert.Equal(1, BrandColour.White.RelativeLuminance, 6);
    }

    [Fact]
    public void ContrastOfBlackAgainstWhiteIsTwentyOne()
    {
        BrandColour black = BrandColour.Create("#000000");

        Assert.Equal(21, black.ContrastWith(BrandColour.White), 6);
    }

    [Fact]
    public void ContrastIsSymmetric()
    {
        BrandColour primary = Colours.Primary;

        Assert.Equal(primary.ContrastWith(BrandColour.White), BrandColour.White.ContrastWith(primary), 10);
    }

    [Fact]
    public void ContrastOfAColourWithItselfIsOne()
    {
        Assert.Equal(1, Colours.Accent.ContrastWith(BrandColour.Create("#B7791F")), 10);
    }

    [Fact]
    public void ContrastRejectsNull()
    {
        BrandColour colour = Colours.Primary;
        Func<BrandColour, double> contrast = colour.ContrastWith;

        NullArguments.AssertThrowsArgumentNull(contrast, new object?[] { null });
    }

    [Fact]
    public void SeventySevenGreyJustFailsTheTextContrast()
    {
        double ratio = BrandColour.White.ContrastWith(BrandColour.Create("#777777"));

        Assert.Equal(4.48, Math.Round(ratio, 2));
        Assert.True(ratio < BrandPalette.TextContrast);
    }

    [Fact]
    public void SeventySixGreyJustPassesTheTextContrast()
    {
        double ratio = BrandColour.White.ContrastWith(BrandColour.Create("#767676"));

        Assert.Equal(4.54, Math.Round(ratio, 2));
        Assert.True(ratio >= BrandPalette.TextContrast);
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#0E6655")]
    [InlineData("#B7791F")]
    [InlineData("#145EC1")]
    [InlineData("#808080")]
    [InlineData("#FF0000")]
    [InlineData("#010203")]
    public void LinearRgbRoundTripsThroughFromLinearRgb(string hex)
    {
        BrandColour original = BrandColour.Create(hex);
        (double r, double g, double b) = original.ToLinearRgb();

        BrandColour back = BrandColour.FromLinearRgb(r, g, b);

        Assert.Equal(original.Hex, back.Hex);
    }

    [Fact]
    public void FromLinearRgbClampsBelowZeroAndAboveOne()
    {
        Assert.Equal("#000000", BrandColour.FromLinearRgb(-0.5, -1, -10).Hex);
        Assert.Equal("#FFFFFF", BrandColour.FromLinearRgb(1.5, 2, 10).Hex);
        Assert.Equal("#FF0000", BrandColour.FromLinearRgb(2, -1, 0).Hex);
    }

    [Fact]
    public void FromLinearRgbUsesTheLinearSegmentAtTheBoundary()
    {
        // 0.0031308 is the sRGB switch-over point: 12.92 * 0.0031308 * 255 = 10.31 -> 10.
        Assert.Equal("#0A0A0A", BrandColour.FromLinearRgb(0.0031308, 0.0031308, 0.0031308).Hex);
        // Just above it the power segment takes over: still 10 after rounding.
        Assert.Equal("#0A0A0A", BrandColour.FromLinearRgb(0.0031309, 0.0031309, 0.0031309).Hex);
    }

    [Fact]
    public void FromLinearRgbRoundsHalvesAwayFromZero()
    {
        // 0.5 / 12.92 is on the linear segment: 0.5 / 255 of an encoded step is exactly half a byte.
        double linear = 0.5 / 255 / 12.92;

        Assert.Equal(1, BrandColour.FromLinearRgb(linear, 0, 0).R);
    }

    [Fact]
    public void FromLinearRgbProducesUpperCaseHexAndChannels()
    {
        BrandColour colour = BrandColour.FromLinearRgb(0.5, 0.25, 0.1);

        Assert.Matches("^#[0-9A-F]{6}$", colour.Hex);
        Assert.Equal(colour.Hex, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");
    }

    [Fact]
    public void ColoursAreEqualByHexRegardlessOfInputCase()
    {
        Assert.Equal(BrandColour.Create("#abcdef"), BrandColour.Create("#ABCDEF"));
        Assert.NotEqual(BrandColour.Create("#abcdef"), BrandColour.Create("#ABCDEE"));
    }
}
