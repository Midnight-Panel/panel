using Microsoft.AspNetCore.DataProtection;
using OtpNet;
using QRCoder;

namespace Midnight.ApiServer.Features.Auth.Services;

public class TotpService
{
    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;

    public TotpService(IDataProtectionProvider dataProtectionProvider, TimeProvider timeProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("Midnight.Totp.v1");
        _timeProvider = timeProvider;
    }

    public string GenerateSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public string Protect(string secret) => _protector.Protect(secret);

    public string Unprotect(string protectedSecret) => _protector.Unprotect(protectedSecret);

    public static string BuildUri(string secret, string accountName, string issuer)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        return $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
    }

    public static string RenderQrCode(string uri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);

        var png = new PngByteQRCode(data).GetGraphic(8);

        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    /// <summary>
    /// Verifies a code allowing one time step of clock drift in each direction. Codes of a time step
    /// at or before <paramref name="lastUsedStep"/> are rejected to prevent replays.
    /// </summary>
    public bool Verify(string secret, string code, long lastUsedStep, out long matchedStep)
    {
        matchedStep = 0;

        code = code.Trim().Replace(" ", "");

        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
            return false;

        var totp = new Totp(Base32Encoding.ToBytes(secret));
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (!totp.VerifyTotp(now, code, out matchedStep, new VerificationWindow(previous: 1, future: 1)))
            return false;

        return matchedStep > lastUsedStep;
    }
}
