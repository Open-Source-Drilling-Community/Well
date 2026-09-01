using OSDC.Drilling.Well.Model;
using System.Collections.Generic;

namespace OSDC.Drilling.Well.Service.Managers;

internal enum WellMutationFailureKind
{
    None,
    InvalidRequest,
    NotFound,
    Conflict,
    StorageFailure
}

internal sealed record WellMutationResult(
    WellMutationFailureKind FailureKind,
    WellMutationErrorEnvelope? Error = null)
{
    public bool Succeeded => FailureKind == WellMutationFailureKind.None;

    public static WellMutationResult Success() => new(WellMutationFailureKind.None);

    public static WellMutationResult Invalid(string property, string code, string message) =>
        Failure(WellMutationFailureKind.InvalidRequest, "invalid_request", "The mutation request is invalid.", property, code, message);

    public static WellMutationResult NotFound(string message) =>
        new(WellMutationFailureKind.NotFound, new WellMutationErrorEnvelope
        {
            Error = "not_found",
            Message = message
        });

    public static WellMutationResult AlreadyExists(string message) =>
        new(WellMutationFailureKind.Conflict, new WellMutationErrorEnvelope
        {
            Error = "already_exists",
            Message = message
        });

    public static WellMutationResult ConcurrencyConflict(string property, string message) =>
        Failure(WellMutationFailureKind.Conflict, "concurrency_conflict", "The resource was modified by another caller.",
            property, "concurrency_conflict", message);

    public static WellMutationResult ReferenceConflict(WellMutationError error) =>
        new(WellMutationFailureKind.Conflict, new WellMutationErrorEnvelope
        {
            Error = "reference_conflict",
            Message = "The mutation would break a Well-owned catalog reference.",
            Errors = [error]
        });

    public static WellMutationResult InvalidReferences(List<WellMutationError> errors) =>
        new(WellMutationFailureKind.InvalidRequest, new WellMutationErrorEnvelope
        {
            Error = "invalid_reference",
            Message = "One or more Well-owned catalog references are invalid.",
            Errors = errors
        });

    public static WellMutationResult InvalidWell(List<WellMutationError> errors) =>
        new(WellMutationFailureKind.InvalidRequest, new WellMutationErrorEnvelope
        {
            Error = "invalid_well",
            Message = "The Well document violates one or more invariants.",
            Errors = errors
        });

    public static WellMutationResult StorageFailure() =>
        new(WellMutationFailureKind.StorageFailure, new WellMutationErrorEnvelope
        {
            Error = "storage_failure",
            Message = "The mutation could not be committed. No partial change was retained."
        });

    private static WellMutationResult Failure(WellMutationFailureKind kind, string error, string summary,
        string property, string code, string message) =>
        new(kind, new WellMutationErrorEnvelope
        {
            Error = error,
            Message = summary,
            Errors = [new WellMutationError { Property = property, Code = code, Message = message }]
        });
}
