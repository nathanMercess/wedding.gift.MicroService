using System.Reflection;

namespace wedding.gift.Services.Implementations.Email;

internal static class EmailTemplateRenderer
{
    public static string Render(string templateName, IReadOnlyDictionary<string, string> values)
    {
        string template = Load(templateName);

        foreach (KeyValuePair<string, string> value in values)
            template = template.Replace($"{{{{{value.Key}}}}}", value.Value, StringComparison.Ordinal);

        return template;
    }

    private static string Load(string templateName)
    {
        Assembly assembly = typeof(EmailTemplateRenderer).Assembly;
        string resourceName = $"Email.Templates.{templateName}";

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Email template '{templateName}' was not found.");
        using StreamReader reader = new(stream);

        return reader.ReadToEnd();
    }
}
