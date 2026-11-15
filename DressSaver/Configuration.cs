using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Configuration;

namespace DressSaver;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Langue de l'interface ("fr", "en", "es", "de"). Vide = langue de Dalamud.</summary>
    public string? Language { get; set; }

    /// <summary>Dossier où sont écrits les .pmp et les designs Glamourer.</summary>
    public string OutputFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DressSaver");

    /// <summary>Préfixe ajouté au nom des mods (ex. « Hikaryū – »).</summary>
    public string ModPrefix { get; set; } = string.Empty;

    /// <summary>Auteur inscrit dans les mods créés.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Installe automatiquement le .pmp dans Penumbra après création.</summary>
    public bool AutoInstall { get; set; } = true;

    /// <summary>Active le nouveau mod et désactive (sans les supprimer) les mods d'origine de la tenue.</summary>
    public bool DisableSourceMods { get; set; } = false;

    /// <summary>Conflits : comparer avec tous les mods installés (sinon seulement les tenues DressSaver).</summary>
    public bool CompareAllMods { get; set; } = true;

    /// <summary>Les mods d'où vient la tenue ne comptent pas comme conflits (ils seront remplacés par le nouveau mod).</summary>
    public bool IgnoreSourceModsInConflicts { get; set; } = true;

    /// <summary>Enregistre le design de la tenue directement dans la liste de Glamourer.</summary>
    public bool SaveDesignToGlamourer { get; set; } = true;

    /// <summary>Applique le design sur le personnage une fois le mod installé et activé.</summary>
    public bool ApplyDesignAfterCreate { get; set; } = true;

    /// <summary>Design Glamourer associé à chaque tenue : dossier du mod Penumbra → identifiant du design.</summary>
    public Dictionary<string, Guid> OutfitDesigns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
