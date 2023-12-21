using System.Globalization;
using System.Reflection;
using CsvHelper.Configuration;
using EasyCsv.Core;
using EasyCsv.Core.Configuration;
using FuzzySharp;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace EasyCsv.Components;

#pragma warning disable BL0007
public partial class CsvTableHeaderMatcher<T>
{
    [Inject] private ISnackbar? Snackbar { get; set; }
    [Inject] private ILogger<CsvTableHeaderMatcher<T>>? Logger { get; set; }

    private List<ExpectedHeader>? _expectedHeaders;
    /// <summary>
    /// These headers that users match their csv headers to.
    /// </summary>
    [Parameter]
    public List<ExpectedHeader>? ExpectedHeaders
    {
        get => _expectedHeaders;
        set
        {
            if (_expectedHeaders == null)
            {
                _expectedHeaders = value;
                return;
            }

            // Only want backing to change when the expected headers actually change. Without this,
            // we lose the ordering applies to the expected headers when the parameters get set even
            // if it's the same expected headers.
            if (value != null && (value.Count != _expectedHeaders.Count || _expectedHeaders.Any(x => value.All(y => y.CSharpPropertyName != x.CSharpPropertyName))))
            {
                _expectedHeaders = value;
            }
        }
    }
    /// <summary>
    /// The Csv to do the matching on.
    /// </summary>
    [Parameter] public IEasyCsv? Csv { get; set; }

    /// <summary>
    /// CsvHelper class maps to control how properties and fields in a class get converted from the csv.
    /// </summary>
    [Parameter] public List<ClassMap>? ClassMaps { get; set; }

    /// <summary>
    /// Controls how csv headers are automatically mapped to expected headers.
    /// </summary>
    [Parameter] public AutoMatching AutoMatch { get; set; } = AutoMatching.Strict;

    /// <summary>
    /// If true, the expected headers will be automatically generated in OnInitialized from
    /// the public instance properties on <typeparamref name="T"/>.
    /// </summary>
    [Parameter] public bool AutoGenerateExpectedHeaders { get; set; }

    /// <summary>
    /// The EasyCsvConfiguration to use when <see cref="GetRecords"/> is called
    /// </summary>
    [Parameter]
    public EasyCsvConfiguration EasyCsvConfig { get; set; } = new ()
    {
        CsvHelperConfig = new(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            ShouldUseConstructorParameters = x => true,
            GetConstructor = x => x.ClassType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 0) ?? x.ClassType.GetConstructors().FirstOrDefault()
        }
    };

    private bool _allHeadersValid;

    /// <summary>
    /// The current state of the matcher. Returns true when all <see cref="ExpectedHeaders"/> that are required either have a default value or a header matched to them.
    /// </summary>
    [Parameter]

    public bool AllHeadersValid
    {
        get => _allHeadersValid;
        set
        {
            if (_allHeadersValid == value) return;
            _allHeadersValid = value;
            AllHeadersValidChanged.InvokeAsync(value);
        }
    }
    [Parameter] public EventCallback<bool> AllHeadersValidChanged { get; set; }

    /// <summary>
    /// Controls whether users can continue editing mappings
    /// </summary>
    [Parameter] public bool Frozen { get; set; }

    /// <summary>
    /// Takes a bool representing whether <see cref="AllHeadersValid"/> and returns a style give the MudTable.
    /// By default, the table will have a green border when valid and red border when invalid.
    /// </summary>
    [Parameter] public Func<bool, string>? TableStyleFunc { get; set; } = x => x ? "border: 1px solid var(--mud-palette-success);" : "border: 1px solid var(--mud-palette-error);";

    /// <summary>
    /// Determines what gets rendered in the "Matched" column of the table.
    /// </summary>
    [Parameter] public RenderFragment<MatchState> DisplayMatchState { get; set; } = _DisplayMatchState;

    // As the tool is used, the actually headers in the easy csv will change.
    // This dict will let us track the changes and update the easy csv accordingly
    private Dictionary<string, string> _originalHeaderCurrentHeaderDict = new();
    private IEnumerable<string> OriginalHeaders => _originalHeaderCurrentHeaderDict.Keys;
    private Dictionary<string, ExpectedHeader?> _mappedDict = new();

    protected override void OnInitialized()
    {
        Reset();
        if (AutoGenerateExpectedHeaders)
        {
            CreateExpectedHeaders();
        }
    }

    private void CreateExpectedHeaders()
    {
        if (typeof(T) == typeof(object)) return;

        ExpectedHeaders = new List<ExpectedHeader>();

        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.DeclaredOnly))
        {
            if (!property.CanWrite) continue;

            var header = new ExpectedHeader(property.Name);
            ExpectedHeaders.Add(header);
        }
    }

    private IEasyCsv? _cachedCsv;
    protected override async Task OnParametersSetAsync()
    {
        if (_expectedHeaders == null) return;
        if (Csv == null || (_cachedCsv != null && _cachedCsv == Csv)) return;
        if (Csv?.GetHeaders() == null) return;
        Reset();
        _cachedCsv = Csv;
        foreach (var header in Csv.GetHeaders()!)
        {
            _originalHeaderCurrentHeaderDict[header] = header;
            _mappedDict[header] = null;
        }
        await MatchFileHeadersWithExpectedHeaders();
        _expectedHeaders = _expectedHeaders
            .OrderByDescending(x => x.Config.IsRequired)
            .ThenByDescending(x => _mappedDict.ContainsValue(x)).ToList();
        StateHasChanged();
    }

    public void Reset()
    {
        ExpectedHeaders?.ForEach(x =>
        {
            x.Value = null;
        });
        _mappedDict = new();
        _originalHeaderCurrentHeaderDict = new();
    }
    private async Task MatchFileHeadersWithExpectedHeaders()
    {
        var headers = Csv!.GetHeaders();
        if (headers == null) return;
        List<ExpectedHeader> matchedHeaders = new ();
        foreach (var header in headers)
        {
            if (DoMatching(header, matchedHeaders, out var matchedHeader))
            {
                matchedHeaders.Add(matchedHeader);
                await ReplaceColumn(header, matchedHeader);
            }
        }
        AllHeadersValid = ValidateRequiredHeaders();
    }

    public async Task ReplaceColumn(string originalHeaderName, ExpectedHeader expectedHeader)
    {
        if (Csv == null) return;
        var currentHeaderName = _originalHeaderCurrentHeaderDict[originalHeaderName];
        await Csv.MutateAsync(x => x.ReplaceColumn(currentHeaderName, expectedHeader.CSharpPropertyName));
        _originalHeaderCurrentHeaderDict[originalHeaderName] = expectedHeader.CSharpPropertyName;
        _mappedDict[originalHeaderName] = expectedHeader;
        expectedHeader.Value = null;
        AllHeadersValid = ValidateRequiredHeaders();
    }

    public async Task ResetColumn(string originalHeaderName)
    {
        if (Csv == null) return;
        var currentHeaderName = _originalHeaderCurrentHeaderDict[originalHeaderName];
        await Csv.MutateAsync(x => x.ReplaceColumn(currentHeaderName, originalHeaderName));
        _originalHeaderCurrentHeaderDict[originalHeaderName] = originalHeaderName;
        _mappedDict[originalHeaderName] = null;
        AllHeadersValid = ValidateRequiredHeaders();
    }

    private bool DoMatching(string header, List<ExpectedHeader> ignore, out ExpectedHeader? matchedExpectedHeader)
    {
        matchedExpectedHeader = null;
        return AutoMatch switch
        {
            AutoMatching.Exact => TryExactMatch(header, ignore, out matchedExpectedHeader),
            AutoMatching.Strict => TryFuzzyMatch(header, ignore, out matchedExpectedHeader),
            AutoMatching.Lenient => TryFuzzyMatch(header,ignore, out matchedExpectedHeader),
            _ => throw new NotImplementedException("AutMatching case not implemented")
        };
    }
    private bool TryExactMatch(string header, List<ExpectedHeader> ignore, out ExpectedHeader? matchedExpectedHeader)
    {
        var filteredHeaders = ExpectedHeaders?.Except(ignore);
        foreach (var expectedHeader in filteredHeaders ?? Enumerable.Empty<ExpectedHeader>())
        {
            foreach (var possibleExpectedHeader in expectedHeader.ValuesToMatch)
            {
                if (string.Compare(possibleExpectedHeader, header, StringComparison.CurrentCultureIgnoreCase) != 0) continue;
                matchedExpectedHeader = expectedHeader;
                return true;
            }
        }
        matchedExpectedHeader = null;
        return false;
    }
    private bool TryFuzzyMatch(string header, List<ExpectedHeader> ignore, out ExpectedHeader? matchedExpectedHeader)
    {
        var filteredHeaders = ExpectedHeaders?.Except(ignore);
        foreach (var expectedHeader in filteredHeaders ?? Enumerable.Empty<ExpectedHeader>())
        {
            foreach (var possibleExpectedHeader in expectedHeader.ValuesToMatch)
            {
                var ratio = Fuzz.Ratio(possibleExpectedHeader.ToLower(), header.ToLower());
                var partialRatio = Fuzz.PartialRatio(possibleExpectedHeader.ToLower(), header.ToLower());
                if (ratio > 90 || (ratio > 60 && partialRatio > 90 && AutoMatch == AutoMatching.Lenient))
                {
                    matchedExpectedHeader = expectedHeader;
                    return true;
                }
            }
        }

        matchedExpectedHeader = null;
        return false;
    }

    
    /// <summary>
    /// Attempt to convert records 
    /// </summary>
    /// <returns>A list containing the </returns>
    public async Task<List<T>?> GetRecords()
    {
        if (!ValidateRequiredHeaders() || Csv == null)
        {
            return null;
        }
        try
        {
            await AddDefaultValues();

            if (typeof(T) != typeof(object))
            {
                var csvContextProfile = new CsvContextProfile();
                if (ClassMaps != null)
                {
                    csvContextProfile.ClassMaps = ClassMaps;
                }

                try
                {
                    return await Csv.GetRecordsAsync<T>(EasyCsvConfig.CsvHelperConfig, csvContextProfile);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Error getting records. CsvTableHeaderMatcher");
                    return null;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Snackbar?.Add("Something went wrong reading the CSV. Reset and try again.", Severity.Warning);
            Logger?.LogError(ex, "CsvTableHeaderMatcher of type {Type}, exception will getting records.", typeof(T).Name);
            return null;
        }
    }
    private Task AddDefaultValues()
    {
        if (ExpectedHeaders == null || Csv == null)
        {
            return Task.CompletedTask;
        }
        var defaultValueKvps = ExpectedHeaders.Where(x => x is {Config.DefaultValueType: not DefaultValueType.None, HasValue: true} or { Config.DefaultValueRenderFragment: not null, HasValue: true })
            .ToDictionary(x => x.CSharpPropertyName, x => x.Value);
        return Csv.MutateAsync(x => x.AddColumns(defaultValueKvps));
    }

    public bool ValidateRequiredHeaders()
    {
        if (ExpectedHeaders == null) return true;
        foreach (ExpectedHeader requiredHeader in ExpectedHeaders.Where(i => i.Config.IsRequired))
        {
            bool isMapped = _mappedDict.Values.Contains(requiredHeader);
            if (requiredHeader is { Config.DefaultValueType: not DefaultValueType.None, HasValue: true } or { Config.DefaultValueRenderFragment: not null, HasValue: true }) continue;
            if (isMapped) continue;
            return false;
        }

        return true;
    }
}