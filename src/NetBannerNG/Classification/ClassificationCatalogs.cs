using System;
using System.Collections.Generic;

namespace NetBannerNG.Classification
{
    internal sealed class ClassificationCatalog
    {
        private readonly ClassificationCatalogEntry[] _entries;
        private readonly Dictionary<int, string> _valueLabels;
        internal static readonly char[] separator = new[] { '|' };

        internal ClassificationCatalog(ClassificationCatalogEntry[] entries, Dictionary<int, string> valueLabels)
        {
            _entries = entries;
            _valueLabels = valueLabels.Count > 0 ? valueLabels : BuildOrdinalLabels(entries);
        }

        internal string CanonicalLabelForValue(int value, string fallback)
            => _valueLabels.TryGetValue(value, out var label) ? label : fallback;

        internal string ResolveBackgroundFromBannerText(string classificationText, string fallbackHex)
        {
            var tokens = ExtractCandidateTokens(classificationText);
            ClassificationCatalogEntry? winner = null;

            foreach (var entry in _entries)
            {
                foreach (var token in tokens)
                {
                    if (!entry.Matches(token))
                    {
                        continue;
                    }

                    if (winner == null || entry.Priority > winner.Priority)
                    {
                        winner = entry;
                    }
                }
            }

            return winner?.BackgroundHex ?? fallbackHex;
        }

        internal string ResolveForegroundFromBannerText(string classificationText, string fallbackHex)
        {
            var tokens = ExtractCandidateTokens(classificationText);
            ClassificationCatalogEntry? winner = null;

            foreach (var entry in _entries)
            {
                foreach (var token in tokens)
                {
                    if (!entry.Matches(token))
                    {
                        continue;
                    }

                    if (winner == null || entry.Priority > winner.Priority)
                    {
                        winner = entry;
                    }
                }
            }

            return winner?.ForegroundHex ?? fallbackHex;
        }

        private static string[] ExtractCandidateTokens(string text)
        {
            var normalized = Normalize(text);
            var pieces = normalized.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < pieces.Length; i++)
            {
                pieces[i] = pieces[i].Trim();
            }

            return pieces;
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();

        private static Dictionary<int, string> BuildOrdinalLabels(ClassificationCatalogEntry[] entries)
        {
            var sorted = (ClassificationCatalogEntry[])entries.Clone();
            Array.Sort(sorted, static (left, right) => left.Priority.CompareTo(right.Priority));

            var labels = new Dictionary<int, string>();
            for (var i = 0; i < sorted.Length; i++)
            {
                labels[i + 1] = sorted[i].Label;
            }

            return labels;
        }
    }

    internal sealed class ClassificationCatalogEntry
    {
        private readonly string[] _aliases;

        internal ClassificationCatalogEntry(string key, string label, string? backgroundHex, string? foregroundHex, int priority, params string[] aliases)
        {
            Key = key;
            Label = Normalize(label);
            BackgroundHex = string.IsNullOrWhiteSpace(backgroundHex) ? "#007A33" : backgroundHex;
            ForegroundHex = string.IsNullOrWhiteSpace(foregroundHex) ? "#FFFFFF" : foregroundHex;
            Priority = priority;
            _aliases = aliases ?? Array.Empty<string>();
            for (var i = 0; i < _aliases.Length; i++)
            {
                _aliases[i] = Normalize(_aliases[i]);
            }
        }

        internal string? BackgroundHex { get; }
        internal string? ForegroundHex { get; }
        internal string Key { get; }
        internal string Label { get; }
        internal int Priority { get; }

        internal bool Matches(string normalizedValue)
        {
            if (normalizedValue == Label)
            {
                return true;
            }

            foreach (var alias in _aliases)
            {
                if (normalizedValue == alias)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    internal static class ClassificationCatalogRegistry
    {
        private const string NotConfiguredCatalogKey = "NOT_CONFIGURED";

        private static readonly Dictionary<string, ClassificationCatalog> Catalogs = new()
        {
            [NotConfiguredCatalogKey] = BuildNotConfiguredCatalog(),
            ["NATO"] = BuildNatoCatalog(),
            ["US"] = BuildUsCatalog(),
            ["UK"] = BuildUkCatalog(),
            ["CA"] = BuildCanadaCatalog(),
            ["AU"] = BuildAustraliaCatalog(),
            ["DE"] = BuildGermanyCatalog(),
            ["DK"] = BuildDenmarkCatalog(),
            ["EUCI"] = BuildEuciCatalog(),
            ["EP"] = BuildEuropeanParliamentCatalog(),
            ["EE"] = BuildEstoniaCatalog(),
            ["FR"] = BuildFranceCatalog(),
            ["IT"] = BuildItalyCatalog(),
            ["PL"] = BuildPolandCatalog(),
            ["FI"] = BuildFinlandCatalog(),
            ["SE"] = BuildSwedenCatalog(),
            ["TR"] = BuildTurkeyCatalog(),
            ["UA"] = BuildUkraineCatalog(),
            ["ESA"] = BuildEsaCatalog(),
            ["OECD"] = BuildOecdCatalog(),
            ["EURATOM"] = BuildEuratomCatalog(),
            ["WASSENAAR"] = BuildWassenaarCatalog(),
            ["OSCE"] = BuildOsceCatalog(),
            ["OPCW"] = BuildOpcwCatalog(),
            ["COE"] = BuildCouncilOfEuropeCatalog(),
            ["WTO"] = BuildWtoCatalog(),
            ["ICC"] = BuildIccCatalog(),
            ["NSG"] = BuildNsgCatalog(),
            ["ICTY"] = BuildIctyCatalog(),
            ["AG"] = BuildAgCatalog(),
            ["CCEB"] = BuildCcebCatalog(),
            ["UN"] = BuildUnCatalog(),
        };

        internal static ClassificationCatalog Resolve(string? catalogName)
        {
            var key = (catalogName ?? string.Empty).Trim().ToUpperInvariant();
            return Catalogs.TryGetValue(key, out var catalog) ? catalog : Catalogs[NotConfiguredCatalogKey];
        }

        internal static string NotConfiguredLabelText => "Classification not configured";
        internal static string NotConfiguredBackgroundHex => "#FFFFFF";
        internal static string NotConfiguredForegroundHex => "#000000";

        private static ClassificationCatalog BuildNotConfiguredCatalog() => new(
            new[] { new ClassificationCatalogEntry("NOT_CONFIGURED", NotConfiguredLabelText, NotConfiguredBackgroundHex, NotConfiguredForegroundHex, 0, "NOT CONFIGURED"), },
            new Dictionary<int, string> { [0] = NotConfiguredLabelText });

        private static ClassificationCatalog BuildNatoCatalog() => new(
            new[]
            {
                new ClassificationCatalogEntry("NATO_UNCLASSIFIED", "NATO UNCLASSIFIED", "#007A33", null, 10, "UNCLASSIFIED", "UNCLASS", "PUBLIC"),
                new ClassificationCatalogEntry("NATO_RESTRICTED", "NATO RESTRICTED", "#FF671F", null, 20, "RESTRICTED"),
                new ClassificationCatalogEntry("NATO_CONFIDENTIAL", "NATO CONFIDENTIAL", "#0033A0", null, 30, "CONFIDENTIAL"),
                new ClassificationCatalogEntry("NATO_SECRET", "NATO SECRET", "#C8102E", null, 40, "SECRET"),
                new ClassificationCatalogEntry("COSMIC_TOP_SECRET", "COSMIC TOP SECRET", "#F7EA48", null, 50, "#000000", "TOP SECRET"),
            },
            new Dictionary<int, string>
            {
                [1] = "UNCLASSIFIED",
                [2] = "SECRET",
                [3] = "TOP SECRET",
                [4] = "SCI",
                [5] = "PUBLIC",
                [6] = "RESTRICTED",
                [7] = "CONFIDENTIAL",
                [8] = "SENSITIVE",
                [9] = "FOR OFFICIAL USE ONLY",
            });

        private static ClassificationCatalog BuildUsCatalog() => new(
            new[]
            {
                new ClassificationCatalogEntry("CUI", "CONTROLLED UNCLASSIFIED INFORMATION", "#502B85", null, 10, "CUI"),
                new ClassificationCatalogEntry("UNCLASSIFIED", "UNCLASSIFIED", "#007A33", null, 20, "U"),
                new ClassificationCatalogEntry("CONFIDENTIAL", "CONFIDENTIAL", "#0033A0", null, 30, "C"),
                new ClassificationCatalogEntry("SECRET", "SECRET", "#C8102E", null, 40, "S"),
                new ClassificationCatalogEntry("TOP_SECRET", "TOP SECRET", "#FF8C00", null, 50, "TS"),
                new ClassificationCatalogEntry("TS_SCI", "TOP SECRET//SENSITIVE COMPARTMENT INFORMATION", "#FCE83A", "#000000", 60, "#000000", "TS//SCI"),
            },
            new Dictionary<int, string>());

        private static ClassificationCatalog BuildUkCatalog() => new(
            new[]
            {
                new ClassificationCatalogEntry("PMNS", "PROTECTIVE MARKING NOT SET", null, null, 10),
                new ClassificationCatalogEntry("UK_OFFICIAL", "UK OFFICIAL", null, null, 20),
                new ClassificationCatalogEntry("UK_OFFICIAL_SENSITIVE", "UK OFFICIAL SENSITIVE", null, null, 30),
                new ClassificationCatalogEntry("UK_SECRET", "UK SECRET", null, null, 40),
                new ClassificationCatalogEntry("UK_TOP_SECRET", "UK TOP SECRET", null, null, 50),
            },
            new Dictionary<int, string>());

        private static ClassificationCatalog BuildCanadaCatalog() => new(
            new[]
            {
                new ClassificationCatalogEntry("PROTECTED_A", "PROTECTED A", null, null, 10),
                new ClassificationCatalogEntry("PROTECTED_B", "PROTECTED B", null, null, 20),
                new ClassificationCatalogEntry("PROTECTED_C", "PROTECTED C", null, null, 30),
                new ClassificationCatalogEntry("CONFIDENTIAL", "CONFIDENTIAL", null, null, 40),
                new ClassificationCatalogEntry("SECRET", "SECRET", null, null, 50),
                new ClassificationCatalogEntry("TOP_SECRET", "TOP SECRET", null, null, 60),
            },
            new Dictionary<int, string>());

        private static ClassificationCatalog BuildAustraliaCatalog() => new(
            new[]
            {
                new ClassificationCatalogEntry("UNOFFICIAL", "UNOFFICIAL", null, null, 10),
                new ClassificationCatalogEntry("OFFICIAL", "OFFICIAL", null, null, 20),
                new ClassificationCatalogEntry("OFFICIAL_SENSITIVE", "OFFICIAL: SENSITIVE", null, null, 30, "OFFICIAL SENSITIVE"),
                new ClassificationCatalogEntry("CONFIDENTIAL", "CONFIDENTIAL", null, null, 40),
                new ClassificationCatalogEntry("SECRET", "SECRET", "#FFA500", "#000000", 50, "#000000"),
                new ClassificationCatalogEntry("TOP_SECRET", "TOP SECRET", null, null, 60),
            },
            new Dictionary<int, string>());

        private static ClassificationCatalog BuildGermanyCatalog() => new(
            new[]
            {
                new ClassificationCatalogEntry("VS_NFD", "VS-NUR FÜR DEN DIENSTGEBRAUCH", null, null, 10),
                new ClassificationCatalogEntry("VS_VERTRAULICH", "VS-VERTRAULICH", null, null, 20),
                new ClassificationCatalogEntry("GEHEIM", "GEHEIM", null, null, 30),
                new ClassificationCatalogEntry("STRENG_GEHEIM", "STRENG GEHIM", null, null, 40),
            },
            new Dictionary<int, string>());

        private static ClassificationCatalog BuildEuciCatalog() => new(
            new[]
            {
                new ClassificationCatalogEntry("EU_TS", "TRÈS SECRET UE / EU TOP SECRET", null, null, 40, "TS-UE / EU-TS"),
                new ClassificationCatalogEntry("EU_S", "SECRET UE / EU SECRET", null, null, 30, "S-UE / EU-S"),
                new ClassificationCatalogEntry("EU_C", "CONFIDENTIEL UE / EU CONFIDENTIAL", null, null, 20, "C-UE / EU-C"),
                new ClassificationCatalogEntry("EU_R", "RESTREINT UE / EU RESTRICTED", null, null, 10, "R-UE / EU-R"),
            },
            new Dictionary<int, string>());

        private static ClassificationCatalog BuildEuropeanParliamentCatalog() => BuildEuciCatalog();

        private static ClassificationCatalog BuildEstoniaCatalog() => new(new[] { new ClassificationCatalogEntry("EE_TS", "TÄIESTI SALAJANE", null, null, 40), new ClassificationCatalogEntry("EE_S", "SALAJANE", null, null, 30), new ClassificationCatalogEntry("EE_C", "KONFIDENTSIAALNE", null, null, 20), new ClassificationCatalogEntry("EE_R", "PIIRATUD", null, null, 10), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildFranceCatalog() => new(new[] { new ClassificationCatalogEntry("FR_TS", "TRÈS SECRET DÉFENSE", null, null, 40), new ClassificationCatalogEntry("FR_S", "SECRET DÉFENSE", null, null, 30), new ClassificationCatalogEntry("FR_C", "CONFIDENTIEL DÉFENSE", null, null, 20), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildItalyCatalog() => new(new[] { new ClassificationCatalogEntry("IT_TS", "SEGRETISSIMO", null, null, 40), new ClassificationCatalogEntry("IT_S", "SEGRETO", null, null, 30), new ClassificationCatalogEntry("IT_C", "RISERVATISSIMO", null, null, 20), new ClassificationCatalogEntry("IT_R", "RISERVATO", null, null, 10), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildPolandCatalog() => new(new[] { new ClassificationCatalogEntry("PL_TS", "ŚCIŚLE TAJNE", null, null, 40), new ClassificationCatalogEntry("PL_S", "TAJNE", null, null, 30), new ClassificationCatalogEntry("PL_C", "POUFNE", null, null, 20), new ClassificationCatalogEntry("PL_R", "ZASTRZEŻONE", null, null, 10), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildFinlandCatalog() => new(new[] { new ClassificationCatalogEntry("FI_TS", "ERITTÄIN SALAINEN / YTTERST HEMLIG", null, null, 40), new ClassificationCatalogEntry("FI_S", "SALAINEN / HEMLIG", null, null, 30), new ClassificationCatalogEntry("FI_C", "LUOTTAMUKSELLINEN / KONFIDENTIELL", null, null, 20), new ClassificationCatalogEntry("FI_R", "KÄYTTÖ RAJOITETTU / BEGRÄNSAD TILLGÅNG", null, null, 10), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildSwedenCatalog() => new(new[] { new ClassificationCatalogEntry("SE_TS", "HEMLIG/TOP SECRET", null, null, 40), new ClassificationCatalogEntry("SE_S", "HEMLIG/SECRET", null, null, 30), new ClassificationCatalogEntry("SE_C", "HEMLIG/CONFIDENTIAL", null, null, 20), new ClassificationCatalogEntry("SE_R", "HEMLIG/RESTRICTED", null, null, 10), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildTurkeyCatalog() => new(new[] { new ClassificationCatalogEntry("TR_HIZMETE_OZEL", "HİZMETE ÖZEL", null, null, 10), new ClassificationCatalogEntry("TR_TD", "TASNİF DIŞI", null, null, 20), new ClassificationCatalogEntry("TR_GIZLI", "GİZLİ", null, null, 30), new ClassificationCatalogEntry("TR_COK_GIZLI", "ÇOK GİZLİ", null, null, 40), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildUkraineCatalog() => new(new[] { new ClassificationCatalogEntry("UA_DSK", "ДЛЯ СЛУЖБОВОГО КОРИСТУВАННЯ", null, null, 10), new ClassificationCatalogEntry("UA_TAYEMNO", "ТАЄМНО", null, null, 20), new ClassificationCatalogEntry("UA_CILKOM", "ЦІЛКОМ ТАЄМНО", null, null, 30), new ClassificationCatalogEntry("UA_OSOB", "ОСОБЛИВОЇ ВАЖЛИВОСТІ", null, null, 40), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildEsaCatalog() => new(new[] { new ClassificationCatalogEntry("ESA_R", "ESA RESTRICTED", null, null, 10), new ClassificationCatalogEntry("ESA_C", "ESA CONFIDENTIAL", null, null, 20), new ClassificationCatalogEntry("ESA_S", "ESA SECRET", null, null, 30), new ClassificationCatalogEntry("ESA_TS", "ESA TOP SECRET", null, null, 40), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildOecdCatalog() => new(new[] { new ClassificationCatalogEntry("OECD_C", "OECD CONFIDENTIAL", null, null, 20, "CONFIDENTIAL"), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildEuratomCatalog() => new(new[] { new ClassificationCatalogEntry("EURA_R", "EURA-RESTRICTED", null, null, 10), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildWassenaarCatalog() => new(new[] { new ClassificationCatalogEntry("WASSENAAR_R", "RESTRICTED", null, null, 10), new ClassificationCatalogEntry("WASSENAAR_C", "CONFIDENTIAL", null, null, 20), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildOsceCatalog() => new(new[] { new ClassificationCatalogEntry("OSCE_R", "RESTRICTED", null, null, 10), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildOpcwCatalog() => new(new[] { new ClassificationCatalogEntry("OPCW_R", "OPCW RESTRICTED", null, null, 10), new ClassificationCatalogEntry("OPCW_P", "OPCW PROTECTED", null, null, 20), new ClassificationCatalogEntry("OPCW_HP", "OPCW HIGHLY PROTECTED", null, null, 30), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildCouncilOfEuropeCatalog() => new(new[] { new ClassificationCatalogEntry("COE_R", "RESTRICTED", null, null, 10), new ClassificationCatalogEntry("COE_C", "CONFIDENTIAL", null, null, 20), new ClassificationCatalogEntry("COE_S", "SECRET", null, null, 30), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildWtoCatalog() => new(new[] { new ClassificationCatalogEntry("WTO_R", "RESTRICTED", null, null, 10), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildIccCatalog() => new(new[] { new ClassificationCatalogEntry("ICC_R", "RESTRICTED", null, null, 10), new ClassificationCatalogEntry("ICC_C", "CONFIDENTIAL", null, null, 20), new ClassificationCatalogEntry("ICC_S", "SECRET", null, null, 30), new ClassificationCatalogEntry("ICC_US", "UNDER SEAL", null, null, 40), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildNsgCatalog() => new(new[] { new ClassificationCatalogEntry("NSG_C", "NSG CONFIDENTIAL", null, null, 20), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildIctyCatalog() => new(new[] { new ClassificationCatalogEntry("ICTY_C", "CONFIDENTIAL", null, null, 20), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildAgCatalog() => new(new[] { new ClassificationCatalogEntry("AG_IC", "AG-IN-CONFIDENCE", null, null, 10), new ClassificationCatalogEntry("AG_C", "AG CONFIDENTIAL", null, null, 20), new ClassificationCatalogEntry("AG_S", "AG SECRET", null, null, 30), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildCcebCatalog() => new(new[] { new ClassificationCatalogEntry("CCEB_R", "RESTRICTED", null, null, 10), new ClassificationCatalogEntry("CCEB_C", "CONFIDENTIAL", null, null, 20), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildUnCatalog() => new(new[] { new ClassificationCatalogEntry("UN_C", "CONFIDENTIAL", null, null, 20), new ClassificationCatalogEntry("UN_SC", "STRICTLY CONFIDENTIAL", null, null, 30), }, new Dictionary<int, string>());

        private static ClassificationCatalog BuildDenmarkCatalog() => new(
            new[]
            {
                new ClassificationCatalogEntry("TIL_TJENESTEBRUG", "TIL TJENESTEBRUG", null, null, 10),
                new ClassificationCatalogEntry("FORTROLIGT", "FORTROLIGT", null, null, 20),
                new ClassificationCatalogEntry("HEMMELIGT", "HEMMELIGT", null, null, 30),
                new ClassificationCatalogEntry("YDERST_HEMMELIGT", "YDERST HEMMELIGT", null, null, 40),
            },
            new Dictionary<int, string>());
    }
}
