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

namespace Bannerlord.PartyAI.Parties.Templates;

/// <summary>Result of importing an online template.</summary>
public sealed class TemplateImportResult
{
    public bool Success { get; }
    public string Error { get; }
    public TroopTemplate? Template { get; }

    private TemplateImportResult(bool success, string error, TroopTemplate? template)
    {
        Success = success;
        Error = error;
        Template = template;
    }

    public static TemplateImportResult Succeeded(TroopTemplate template) => new(true, string.Empty, template);

    public static TemplateImportResult Failed(string error) => new(false, error, null);
}

/// <summary>
/// Downloads a template JSON from an HTTPS URL on a background task and finishes the import on
/// the game thread via <see cref="Tick"/>. Only one download runs at a time.
/// </summary>
public sealed class TemplateImportService
{
    private const int MaximumBytes = 64 * 1024;
    private const int MaximumTroops = 64;

    private readonly HttpClient _client;
    private Task<string>? _pending;
    private Action<TemplateImportResult>? _pendingCallback;
    private string? _pendingUrl;

    public TemplateImportService()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Bannerlord.PartyAI/1.5.2");
    }

    public bool IsPending => _pending is not null;

    public static bool TryValidateUrl(string? url, out Uri? uri, out string error)
    {
        error = string.Empty;
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Enter a valid HTTPS URL.";
            return false;
        }

        return true;
    }

    public bool TryBegin(string url, Action<TemplateImportResult> callback, out string error)
    {
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
        _pending = Download(uri);
        return true;
    }

    /// <summary>Call every frame; completes a finished download on the caller's thread.</summary>
    public void Tick()
    {
        if (_pending?.IsCompleted != true)
        {
            return;
        }

        TemplateImportResult result;
        try
        {
            result = Import(_pending.GetAwaiter().GetResult(), _pendingUrl!);
        }
        catch (Exception exception)
        {
            result = TemplateImportResult.Failed(exception.GetBaseException().Message);
        }

        Action<TemplateImportResult>? callback = _pendingCallback;
        _pending = null;
        _pendingCallback = null;
        _pendingUrl = null;
        callback?.Invoke(result);
    }

    private async Task<string> Download(Uri uri)
    {
        using HttpResponseMessage response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > MaximumBytes)
        {
            throw new InvalidOperationException("Template file is larger than 64 KB.");
        }

        using Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using MemoryStream output = new();
        byte[] buffer = new byte[8192];
        int total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaximumBytes)
            {
                throw new InvalidOperationException("Template file is larger than 64 KB.");
            }

            output.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static TemplateImportResult Import(string json, string sourceUrl)
    {
        PartyRegistry registry = PartyAi.Parties;
        TemplateDefinition? definition = JsonConvert.DeserializeObject<TemplateDefinition>(json);
        if (definition is null)
        {
            return TemplateImportResult.Failed("The response is not a template JSON document.");
        }

        string name = definition.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 40)
        {
            return TemplateImportResult.Failed("Template name must contain 2 to 40 characters.");
        }

        if (!registry.IsUniqueTemplateName(name))
        {
            return TemplateImportResult.Failed($"A template named '{name}' already exists.");
        }

        string sourceId = SourceId(sourceUrl);
        if (registry.FindTemplateBySource(sourceId) is not null)
        {
            return TemplateImportResult.Failed("This online template has already been imported.");
        }

        List<string> ids = definition.Troops?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new();
        if (ids.Count == 0)
        {
            return TemplateImportResult.Failed("The template does not contain any troop IDs.");
        }

        if (ids.Count > MaximumTroops)
        {
            return TemplateImportResult.Failed($"A template can contain at most {MaximumTroops} troop IDs.");
        }

        Dictionary<string, CharacterObject> characters = CharacterObject.All
            .Where(character => character is { IsHero: false })
            .GroupBy(character => character.StringId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<string> missing = ids.Where(id => !characters.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            return TemplateImportResult.Failed("Unknown troop IDs: " + string.Join(", ", missing.Take(8)));
        }

        if (!TryParseComposition(definition.Composition, out PartyComposition composition, out string error))
        {
            return TemplateImportResult.Failed(error);
        }

        TroopRoster targets = TroopRoster.CreateDummyTroopRoster();
        foreach (string id in ids)
        {
            targets.AddToCounts(characters[id], 1);
        }

        TroopTemplate template = registry.CreateTemplate(name, targets, composition, sourceId);
        if (template.Troops.Count == 0)
        {
            registry.DeleteTemplate(template);
            return TemplateImportResult.Failed("None of the selected troops has a valid upgrade path.");
        }

        return TemplateImportResult.Succeeded(template);
    }

    private static bool TryParseComposition(CompositionDefinition? definition, out PartyComposition composition, out string error)
    {
        composition = new PartyComposition();
        error = string.Empty;
        if (definition is null)
        {
            error = "The template is missing its composition object.";
            return false;
        }

        float[] values = [definition.Infantry, definition.Ranged, definition.Cavalry, definition.HorseArcher];
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
            values = values.Select(value => value / 100f).ToArray();
        }

        float total = values.Sum();
        if (total <= 0f)
        {
            error = "Composition values must add up to more than zero.";
            return false;
        }

        composition = new PartyComposition(values[0] / total, values[1] / total, values[2] / total, values[3] / total);
        return true;
    }

    private static string SourceId(string url)
    {
        using SHA256 sha = SHA256.Create();
        return "url:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private sealed class TemplateDefinition
    {
        public string? Name { get; set; }
        public List<string>? Troops { get; set; }
        public CompositionDefinition? Composition { get; set; }
    }

    private sealed class CompositionDefinition
    {
        public float Infantry { get; set; }
        public float Ranged { get; set; }
        public float Cavalry { get; set; }
        public float HorseArcher { get; set; }
    }
}
