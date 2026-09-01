using Forge.Core.Validation;
using Forge.Identity;
using Forge.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forge.SecurityTests;

/// <summary>Ephemeral token keys are a development convenience; production refuses them (ADR 18).</summary>
public class IdentityKeyMaterialTests
{
    private static List<string> ValidatorFailures(IdentityModule module)
    {
        var services = new ServiceCollection();
        module.ConfigureServices(services);
        using var provider = services.BuildServiceProvider();
        return provider.GetServices<IProductionConfigurationValidator>()
            .SelectMany(v => v.Validate())
            .Where(f => f.Contains("certificate", StringComparison.Ordinal))
            .ToList();
    }

    [Fact]
    public void Module_without_key_material_fails_production_validation()
    {
        var failures = ValidatorFailures(new IdentityModule("Server=unused;Database=unused;Encrypt=false"));

        Assert.Equal(2, failures.Count);
        Assert.Contains(failures, f => f.Contains("signing", StringComparison.Ordinal));
        Assert.Contains(failures, f => f.Contains("encryption", StringComparison.Ordinal));
    }

    [Fact]
    public void Module_with_key_material_passes_production_validation()
    {
        var failures = ValidatorFailures(new IdentityModule(
            "Server=unused;Database=unused;Encrypt=false",
            new IdentityKeyMaterial(SelfSignedPfx("km-signing")),
            new IdentityKeyMaterial(SelfSignedPfx("km-encryption"))));

        Assert.Empty(failures);
    }

    private static string SelfSignedPfx(string subject)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            $"CN={subject}", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var path = Path.Combine(Path.GetTempPath(), $"{subject}-{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(path, certificate.Export(
            System.Security.Cryptography.X509Certificates.X509ContentType.Pfx));
        return path;
    }
}
