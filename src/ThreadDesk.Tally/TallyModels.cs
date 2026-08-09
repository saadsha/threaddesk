using System;

namespace ThreadDesk.Tally;

public record TallyCompany(string Name, string? Id = null);

public record TallyLedger(string Id, string Name, string? Group = null);

public record TallyVoucher(string Id, DateTime Date, string Narration, decimal Amount, string? Type = null);
