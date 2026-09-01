using System.Text.RegularExpressions;

namespace Forge.Templates;

/// <summary>
/// A versioned, localised template (ADR 38). Rendering is a constrained,
/// non-code sandbox: only <c>{{variable}}</c> substitution against the
/// allow-listed set, with HTML-encoded values. There is no way to execute
/// logic — by construction, not by policy.
/// </summary>
public sealed record Template(
    string Id,
    int Version,
    IReadOnlyDictionary<string, string> BodiesByCulture,
    IReadOnlySet<string> AllowedVariables)
{
    public const string NeutralCulture = "";
}

/// <summary>A template uses a construct the constrained renderer refuses (ADR 38).</summary>
public sealed class TemplateValidationException(string message) : Exception(message);

/// <summary>Constrained renderer and validator (ADR 38).</summary>
public static partial class TemplateRenderer
{
    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex VariableToken();

    /// <summary>Every token in every body must be allow-listed; call at registration time.</summary>
    public static void Validate(Template template)
    {
        foreach (var (culture, body) in template.BodiesByCulture)
        {
            foreach (Match match in VariableToken().Matches(body))
            {
                if (!template.AllowedVariables.Contains(match.Groups[1].Value))
                {
                    throw new TemplateValidationException(
                        $"template '{template.Id}' v{template.Version} ({culture}): variable '{match.Groups[1].Value}' is not allow-listed");
                }
            }
        }
    }

    /// <summary>Renders with culture fallback (exact → parent → neutral) and HTML-encoded values.</summary>
    public static string Render(Template template, string culture, IReadOnlyDictionary<string, string> variables)
    {
        Validate(template);

        var body = ResolveBody(template, culture);
        return VariableToken().Replace(body, match =>
        {
            var name = match.Groups[1].Value;
            if (!variables.TryGetValue(name, out var value))
            {
                throw new TemplateValidationException($"template '{template.Id}': no value supplied for variable '{name}'");
            }

            return System.Net.WebUtility.HtmlEncode(value); // sanitised output, always
        });
    }

    private static string ResolveBody(Template template, string culture)
    {
        if (template.BodiesByCulture.TryGetValue(culture, out var exact))
        {
            return exact;
        }

        var parent = culture.Contains('-', StringComparison.Ordinal) ? culture.Split('-')[0] : null;
        if (parent is not null && template.BodiesByCulture.TryGetValue(parent, out var parentBody))
        {
            return parentBody;
        }

        return template.BodiesByCulture.TryGetValue(Template.NeutralCulture, out var neutral)
            ? neutral
            : throw new TemplateValidationException($"template '{template.Id}' has no body for '{culture}' and no neutral fallback");
    }
}
