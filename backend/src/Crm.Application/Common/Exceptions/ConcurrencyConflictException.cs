namespace Crm.Application.Common.Exceptions;

/// <summary>
/// The Application-owned translation of EF Core's DbUpdateConcurrencyException. CrmDbContext
/// catches the EF exception at the persistence boundary and rethrows this instead, so
/// TransactionBehavior can react to a concurrency conflict without Crm.Application referencing
/// EF Core.
/// </summary>
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The record was modified by someone else.", innerException);
