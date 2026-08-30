using Bannerlord.PartyAI.Domain.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace Bannerlord.PartyAI.Domain;

internal static class TemplateImportService
{
    private const int MaximumTemplateBytes = 64 * 1024;
    private static readonly HttpClient Client = CreateClient();

    private static Task<string>? _pendingDownload;
    private static Action<TemplateImportResult>? _pendingCallback;
    private static string? _pendingUrl;

    internal static bool IsPending => _pendingDownload is not null;

    internal static bool TryBegin(
        string url,
        Action<TemplateImportResult> callback,
        out string error)
    {
        error = string.Empty;
        if (IsPending)
        {
            error = "A template download is already running.";
            return false;
        }

        if (!TryValidateUrl(url, out Uri? uri, out error))
        {
            return false;
        }

        _pendingUrl = uri!.AbsoluteUri;
        _pendingCallback = callback;
        _pendingDownload = Download(uri);
        return true;
    }

    internal static bool TryValidateUrl(string url, out Uri? uri, out string error)
    {
        error = string.Empty;
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Enter a valid HTTPS URL.";
            return false;
        }

        return true;
    }

    internal static void Tick()
    {
        if (_pendingDownload?.IsCompleted != true)
        {
            return;
        }

        TemplateImportResult result;
        try
        {
            string json = _pendingDownload.GetAwaiter().GetResult();
            result = Import(json, _pendingUrl!);
        }
        catch (Exception exception)
        {
            result = TemplateImportResult.Failed(exception.GetBaseException().Message);
        }

        Action<TemplateImportResult>? callback = _pendingCallback;
        _pendingDownload = null;
        _pendingCallback = null;
        _pendingUrl = null;
        callback?.Invoke(result);
    }

    private static async Task<string> Download(Uri uri)
    {
        using HttpResponseMessage response = await Client
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? length = response.Content.Headers.ContentLength;
        if (length > MaximumTemplateBytes)
        {
            throw new InvalidOperationException("Template file is larger than 64 KB.");
        }

        using Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using MemoryStream output = new();
        byte[] buffer = new byte[8192];
        int total = 0;
        while (true)
        {
            int read = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            total += read;
            if (total > MaximumTemplateBytes)
            {
                throw new InvalidOperationException("Template file is larger than 64 KB.");
            }
            output.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static TemplateImportResult Import(string json, string sourceUrl)
    {
        TemplateDefinition? definition = JsonConvert.DeserializeObject<TemplateDefinition>(json);
        if (definition is null)
        {
            return TemplateImportResult.Failed("The response is not a template JSON document.");
        }

        string name = definition.Name?.Trim() ?? string.Empty;
        if (name.Length < 2 || name.Length > 40)
        {
            return TemplateImportResult.Failed("Template name must contain 2 to 40 characters.");
        }

        if (!SubModule.PartySettingsManager.IsUniqueTemplateName(name))
        {
            return TemplateImportResult.Failed($"A template named '{name}' already exists.");
        }

        string sourceId = CreateSourceId(sourceUrl);
        if (SubModule.PartySettingsManager.AllTemplates.Any(template => template.SourceId == sourceId))
        {
            return TemplateImportResult.Failed("This online template has already been imported.");
        }

        List<string> ids = definition.Troops?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        if (ids.Count == 0)
        {
            return TemplateImportResult.Failed("The template does not contain any troop IDs.");
        }
        if (ids.Count > 64)
        {
            return TemplateImportResult.Failed("A template can contain at most 64 troop IDs.");
        }

        Dictionary<string, CharacterObject> characters = CharacterObject.All
            .Where(character => character is not null && !character.IsHero)
            .GroupBy(character => character.StringId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<string> missing = ids.Where(id => !characters.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            return TemplateImportResult.Failed(
                "Unknown troop IDs: " + string.Join(", ", missing.Take(8)));
        }

        if (!TryCreateComposition(definition.Composition, out PartyComposition composition, out string error))
        {
            return TemplateImportResult.Failed(error);
        }

        TroopRoster targets = TroopRoster.CreateDummyTroopRoster();
        foreach (string id in ids)
        {
            targets.AddToCounts(characters[id], 1);
        }

        PAICustomTemplate template = new(
            name,
            targets,
            composition,
            sourceId);

        if (template.Troops.Count == 0)
        {
            SubModule.PartySettingsManager.DeletePartyTemplate(template);
            return TemplateImportResult.Failed("None of the selected troops has a valid upgrade path.");
        }

        return TemplateImportResult.Succeeded(template);
    }

    private static bool TryCreateComposition(
        TemplateCompositionDefinition? definition,
        out PartyComposition composition,
        out string error)
    {
        composition = new PartyComposition();
        error = string.Empty;
        if (definition is null)
        {
            error = "The template is missing its composition object.";
            return false;
        }

        float[] values =
        [
            definition.Infantry,
            definition.Ranged,
            definition.Cavalry,
            definition.HorseArcher
        ];
        if (values.Any(value => value < 0f || float.IsNaN(value) || float.IsInfinity(value)))
        {
            error = "Composition values must be non-negative numbers.";
            return false;
        }
        if (values.Any(value => value > 100f))
        {
            error = "Composition values cannot be greater than 100.";
            return false;
        }

        if (values.Any(value => value > 1f))
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] /= 100f;
            }
        }

        float total = values.Sum();
        if (total <= 0f)
        {
            error = "Composition values must add up to more than zero.";
            return false;
        }

        composition = new PartyComposition(
            values[0] / total,
            values[1] / total,
            values[2] / total,
            values[3] / total);
        return true;
    }

    private static HttpClient CreateClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Bannerlord.PartyAI/1.5.2");
        return client;
    }

    private static string CreateSourceId(string sourceUrl)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sourceUrl));
        return "url:" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private sealed class TemplateDefinition
    {
        public string? Name { get; set; }
        public List<string>? Troops { get; set; }
        public TemplateCompositionDefinition? Composition { get; set; }
    }

    private sealed class TemplateCompositionDefinition
    {
        public float Infantry { get; set; }
        public float Ranged { get; set; }
        public float Cavalry { get; set; }
        public float HorseArcher { get; set; }
    }
}

internal sealed class TemplateImportResult
{
    internal bool Success { get; }
    internal string Error { get; }
    internal PAICustomTemplate? Template { get; }

    private TemplateImportResult(bool success, string error, PAICustomTemplate? template)
    {
        Success = success;
        Error = error;
        Template = template;
    }

    internal static TemplateImportResult Succeeded(PAICustomTemplate template)
        => new(true, string.Empty, template);

    internal static TemplateImportResult Failed(string error)
        => new(false, error, null);
}
