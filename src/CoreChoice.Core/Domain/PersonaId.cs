using System.Text.RegularExpressions;

namespace CoreChoice.Domain;

/// <summary>
/// Identifies an advisor persona. A validated slug rather than an enum, because personas are
/// database rows — adding one should be an insert, not a release.
///
/// This type validates SHAPE only. Whether a persona exists and is active is a database question,
/// answered by the prompt store. Shape validation here keeps a malformed id from reaching a query
/// at all.
/// </summary>
public readonly partial record struct PersonaId
{
    private PersonaId(string value) => Value = value;

    public string Value { get; }

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public static PersonaId From(string value)
    {
        if (!TryFrom(value, out var id))
            throw new ArgumentException(
                $"'{value}' is not a valid persona id: expect a lowercase hyphenated slug of 2-40 characters.",
                nameof(value));
        return id;
    }

    public static bool TryFrom(string? value, out PersonaId id)
    {
        id = default;
        var slug = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(slug) || slug.Length is < 2 or > 40 || !SlugPattern().IsMatch(slug))
            return false;

        id = new PersonaId(slug);
        return true;
    }

    public override string ToString() => Value;
}
