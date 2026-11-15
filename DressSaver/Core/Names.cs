using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DressSaver.Core;

/// <summary>Outils pour comparer et afficher des noms de tenues (accents, tirets, espaces).</summary>
public static class Names
{
    /// <summary>
    /// Forme simplifiée pour comparer deux noms : sans accents, tirets unifiés, espaces réduits, en minuscules.
    /// « Hikaryū – Diva » et « hikaryu - diva » donnent la même chose.
    /// </summary>
    public static string Simplify(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var sb = new StringBuilder(name.Length);
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(c is '–' or '—' or '‐' or '‑' or '−' or '_' ? '-' : char.ToLowerInvariant(c));
        }

        return string.Join(' ', sb.ToString().Split(' ', System.StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Texte affichable par la police de Dalamud, qui n'a pas toutes les lettres accentuées
    /// (« ū » s'affiche « = ») : on retire l'accent des lettres hors Latin-1.
    /// </summary>
    public static string Display(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.All(c => c <= 'ÿ' || c is '–' or '—' or '’' or '“' or '”' or '…'))
            return name ?? string.Empty;

        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (c <= 'ÿ' || c is '–' or '—' or '’' or '“' or '”' or '…')
            {
                sb.Append(c);
                continue;
            }

            foreach (var d in c.ToString().Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(d) != UnicodeCategory.NonSpacingMark)
                    sb.Append(d);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Design Glamourer qui correspond le mieux à une tenue : même nom (sans tenir compte des accents
    /// et des tirets), sinon l'unique design dont le nom commence par celui de la tenue ou l'inverse
    /// (« Hikaryu - Diva » ↔ « Hikaryū – Diva Japonaise »). Rien si c'est ambigu.
    /// </summary>
    public static Guid? BestDesign(string outfitName, IReadOnlyDictionary<Guid, string> designs)
    {
        var target = Simplify(outfitName);
        if (target.Length == 0)
            return null;

        var exact = designs.Where(d => Simplify(d.Value) == target).Select(d => d.Key).ToList();
        if (exact.Count > 0)
            return exact[0];

        static bool StartsWithWord(string longer, string shorter)
            => shorter.Length > 0 && longer.StartsWith(shorter, StringComparison.Ordinal)
               && (longer.Length == shorter.Length || !char.IsLetterOrDigit(longer[shorter.Length]));

        var partial = designs.Where(d =>
        {
            var n = Simplify(d.Value);
            return StartsWithWord(n, target) || StartsWithWord(target, n);
        }).Select(d => d.Key).ToList();

        return partial.Count == 1 ? partial[0] : null;
    }
}
