using System;
using System.Collections.Generic;

namespace MarketNest.Admin.Infrastructure;

public record CreateTestRequest(
    string Name,
    string ValueCode,
    decimal ValueAmount,
    IEnumerable<string>? SubTitles = null);

public record UpdateTestRequest(
    string Name,
    string ValueCode,
    decimal ValueAmount,
    IEnumerable<string>? SubTitles = null);

