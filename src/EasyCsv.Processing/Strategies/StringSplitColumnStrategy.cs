﻿using System;
using System.Linq;
using System.Threading.Tasks;
using EasyCsv.Core;

namespace EasyCsv.Processing.Strategies;
public class StringSplitColumnStrategy : ICsvProcessor
{
    private readonly SplitColumnStrategy _splitColumnStrategy;

    public StringSplitColumnStrategy(string columnToSplit, string[] newColumnNames, string delimiter, bool removeSplitColumn, StringSplitOptions stringSplitOptions = StringSplitOptions.RemoveEmptyEntries)
    {
        if (newColumnNames.Length == 0) throw new ArgumentException("New column names must be specified.");
        if (string.IsNullOrWhiteSpace(delimiter)) throw new ArgumentException("Delimiter name must be specified");
        _splitColumnStrategy = new SplitColumnStrategy(columnToSplit, newColumnNames, removeSplitColumn,
            x => x?.ToString()?.Split([delimiter], stringSplitOptions)?.Cast<object?>().ToArray());
    }

    public async Task<OperationResult> ProcessCsv(IEasyCsv csv)
    {
        return await _splitColumnStrategy.ProcessCsv(csv);
    }
}