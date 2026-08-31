using Forge.Templates;
using Xunit;

namespace Forge.NotificationsTests;

public class TemplateTests
{
    private static Template Greeting(params (string Culture, string Body)[] bodies) => new(
        "greeting", Version: 1,
        bodies.ToDictionary(b => b.Culture, b => b.Body),
        AllowedVariables: new HashSet<string> { "name", "item" });

    [Fact]
    public void Substitutes_allow_listed_variables()
    {
        var rendered = TemplateRenderer.Render(
            Greeting(("", "Hello {{name}}, your {{item}} is ready.")),
            "en-GB",
            new Dictionary<string, string> { ["name"] = "Alice", ["item"] = "anvil" });

        Assert.Equal("Hello Alice, your anvil is ready.", rendered);
    }

    [Fact]
    public void Values_are_html_encoded_so_injection_is_inert()
    {
        var rendered = TemplateRenderer.Render(
            Greeting(("", "Hi {{name}}")),
            "en-GB",
            new Dictionary<string, string> { ["name"] = "<script>alert(1)</script>" });

        Assert.DoesNotContain("<script>", rendered, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Undeclared_variable_in_a_body_fails_validation()
    {
        var ex = Assert.Throws<TemplateValidationException>(() =>
            TemplateRenderer.Validate(Greeting(("", "Sneaky {{admin_password}}"))));

        Assert.Contains("admin_password", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_variable_value_fails_at_render_time()
    {
        Assert.Throws<TemplateValidationException>(() =>
            TemplateRenderer.Render(Greeting(("", "Hi {{name}}")), "en-GB", new Dictionary<string, string>()));
    }

    [Fact]
    public void Culture_fallback_is_exact_then_parent_then_neutral()
    {
        var template = Greeting(("", "neutral {{name}}"), ("ar", "arabic {{name}}"), ("ar-SA", "saudi {{name}}"));
        var variables = new Dictionary<string, string> { ["name"] = "x" };

        Assert.Equal("saudi x", TemplateRenderer.Render(template, "ar-SA", variables));
        Assert.Equal("arabic x", TemplateRenderer.Render(template, "ar-EG", variables));
        Assert.Equal("neutral x", TemplateRenderer.Render(template, "fr-FR", variables));
    }
}
