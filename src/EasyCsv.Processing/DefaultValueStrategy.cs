﻿using System;
using System.Threading.Tasks;
namespace EasyCsv.Processing;
public class DefaultValueStrategy : ICsvColumnProcessor
{
    public string ColumnName { get; }
    private readonly object? _defaultValue;
    private readonly Func<object?, bool> _shouldOverride;
    public DefaultValueStrategy(string columnName, object? defaultValue, Func<object?, bool>? shouldOverride = null)
    {
        ColumnName = columnName;
        _defaultValue = defaultValue;
        _shouldOverride = shouldOverride ?? (x => true);
    }

    public Task<OperationResult> ProcessCell<TCell>(TCell cell) where TCell : ICell
    {
        if (_shouldOverride(cell.Value))
        {
            cell.Value = _defaultValue;
        }
        return Task.FromResult(new OperationResult(true));
    }
}