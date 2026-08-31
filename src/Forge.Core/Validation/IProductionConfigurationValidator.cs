namespace Forge.Core.Validation;

/// <summary>
/// A check that must hold before a production host may start (ADR 18). Modules
/// contribute their own validators (e.g. the jobs provider rejects in-memory
/// stores); all failures are reported together and startup is refused.
/// </summary>
public interface IProductionConfigurationValidator
{
    IEnumerable<string> Validate();
}
