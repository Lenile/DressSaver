using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;

namespace DressSaver.Core;

public enum DependencyLevel
{
    Ok,
    Outdated, // fonctionne, mais plus ancien que la version testée : avertissement
    Missing,  // absent, désactivé ou incompatible : bloquant
}

public sealed record DependencyState(string Name, DependencyLevel Level, string Detail);

/// <summary>
/// Vérifie que Penumbra et Glamourer sont installés, chargés et compatibles.
/// Version « majeure » différente = incompatible (bloquant) ; version mineure plus ancienne
/// que celle avec laquelle DressSaver a été testé = avertissement.
/// </summary>
public static class Dependencies
{
    // Versions d'API avec lesquelles DressSaver a été écrit et testé.
    private const int PenumbraBreaking = 5;
    private const int PenumbraFeature = 20;
    private const int GlamourerMajor = 1;
    private const int GlamourerMinor = 8;

    public static List<DependencyState> Check(IDalamudPluginInterface pi)
    {
        var installed = pi.InstalledPlugins.ToList();

        DependencyState Plugin(string internalName, Func<(int, int)> apiVersion, int major, int minor)
        {
            var plugin = installed.FirstOrDefault(p => string.Equals(p.InternalName, internalName, StringComparison.OrdinalIgnoreCase));
            if (plugin == null)
                return new DependencyState(internalName, DependencyLevel.Missing, Loc.T("dep_not_installed"));
            if (!plugin.IsLoaded)
                return new DependencyState(internalName, DependencyLevel.Missing, Loc.T("dep_not_loaded"));

            (int Major, int Minor) version;
            try
            {
                version = apiVersion();
            }
            catch (Exception)
            {
                return new DependencyState(internalName, DependencyLevel.Missing, Loc.T("dep_not_ready"));
            }

            var text = $"{plugin.Version} (API {version.Major}.{version.Minor})";
            if (version.Major != major)
                return new DependencyState(internalName, DependencyLevel.Missing, Loc.T("dep_incompatible", text, $"{major}.x"));
            if (version.Minor < minor)
                return new DependencyState(internalName, DependencyLevel.Outdated, Loc.T("dep_outdated", text, $"{major}.{minor}"));
            return new DependencyState(internalName, DependencyLevel.Ok, text);
        }

        return new List<DependencyState>
        {
            Plugin("Penumbra", () => new Penumbra.Api.IpcSubscribers.ApiVersion(pi).Invoke(), PenumbraBreaking, PenumbraFeature),
            Plugin("Glamourer", () => new Glamourer.Api.IpcSubscribers.ApiVersion(pi).Invoke(), GlamourerMajor, GlamourerMinor),
        };
    }
}
