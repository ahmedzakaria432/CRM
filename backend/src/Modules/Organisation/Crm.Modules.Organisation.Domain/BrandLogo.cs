using System.Text;
using Crm.SharedKernel;
using Crm.SharedKernel.Scoping;

namespace Crm.Modules.Organisation.Domain;

/// <summary>An uploaded logo (Story 58, D3). NOT ITenantScoped on purpose (D4, like Organisation): the portal sign-in
/// page fetches it anonymously by id before any scope exists. It carries TenantId; ChangeBrandingCommandHandler
/// refuses a logo of another tenant. Immutable after Create.</summary>
public sealed class BrandLogo : AuditableEntity<BrandLogoId>, IHasRowVersion
{
    public const int MaxBytes = 512 * 1024;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public TenantId TenantId { get; private set; } = TenantId.None;

    public LogoContentType ContentType { get; private set; }

    public byte[] Content { get; private set; } = [];

    public byte[] RowVersion { get; private set; } = [];

    public string MediaType => ContentType == LogoContentType.Png ? "image/png" : "image/svg+xml";

    private BrandLogo()
    {
    }

    private BrandLogo(BrandLogoId id, TenantId tenantId, LogoContentType contentType, byte[] content) : base(id)
    {
        TenantId = tenantId;
        ContentType = contentType;
        Content = content;
    }

    /// <summary>Content sniffing (never the file name or the declared type): PNG signature, or strict UTF-8 text
    /// (BOM allowed) whose first non-whitespace char is an angle bracket and that contains "&lt;svg". Throws
    /// Organisation.LogoEmpty / LogoTooLarge (over MaxBytes) / LogoTypeNotAllowed.</summary>
    public static LogoContentType Sniff(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0)
        {
            throw new DomainException(new DomainError("Organisation.LogoEmpty", "The logo file is empty."));
        }

        if (content.Length > MaxBytes)
        {
            throw new DomainException(new DomainError("Organisation.LogoTooLarge", "The logo is larger than 512 KB."));
        }

        if (content.AsSpan().StartsWith(PngSignature))
        {
            return LogoContentType.Png;
        }

        try
        {
            string text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(content).TrimStart('﻿');
            if (text.TrimStart().StartsWith('<') && text.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            {
                return LogoContentType.Svg;
            }
        }
        catch (DecoderFallbackException)
        {
            // Not text, so not an SVG.
        }

        throw new DomainException(new DomainError("Organisation.LogoTypeNotAllowed", "The logo must be a PNG or SVG image."));
    }

    public static BrandLogo Create(TenantId tenantId, byte[] content, BrandLogoId? id = null)
    {
        ArgumentNullException.ThrowIfNull(tenantId);

        if (tenantId.Equals(TenantId.None))
        {
            throw new DomainException(new DomainError("Organisation.TenantRequired", "A logo needs a tenant."));
        }

        LogoContentType type = Sniff(content);
        return new BrandLogo(id ?? BrandLogoId.New(), tenantId, type, [.. content]);
    }

    public bool BelongsTo(TenantId tenant) => TenantId.Equals(tenant);
}
