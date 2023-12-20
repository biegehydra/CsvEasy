﻿using System.ComponentModel;

namespace EasyCsv.Components;
/// <summary>
/// Controls how csv headers are automatically mapped to expected headers.
/// </summary>
public enum AutoMatching
{
    [Description("Matches only when the strings are an exact match (Case Insensitive): Ex: 'First Name' and 'first name'")]
    Exact,
    [Description("Matches when the strings are a close match: Ex: 'First Name' and 'first nme'")]
    Strict,
    [Description("Matches even partial strings: Ex: 'First Name' and 'first'")]
    Lenient
}

/// <summary>
/// Represents the current state of an expected header.
/// </summary>
public enum MatchState
{
    [Description("The expected header has been matched to a csv header.")]
    Mapped,
    [Description("The expected header has been provided with a default value..")]
    ValueProvided,
    [Description("The expected header is required and is not mapped and does not have a value provided.")]
    RequiredAndMissing,
    [Description("The expected header is NOT required and is not mapped and does not have a value provided.")]
    Missing
}