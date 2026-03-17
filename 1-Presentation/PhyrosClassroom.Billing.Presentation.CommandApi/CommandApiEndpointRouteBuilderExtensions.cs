using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PhyrosClassroom.Billing.Orchestration;

namespace PhyrosClassroom.Billing.Presentation.CommandApi;

public static class CommandApiEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapBillingCommandApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/command/billing");

        group.MapPost("/", async (
            RegisterBillingRequest request,
            IRegisterBillingUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(request, cancellationToken);
            return Results.Created($"/command/billing/{billing.BillingId}", billing);
        });

        group.MapGet("/{billingId:guid}", async (
            Guid billingId,
            IGetBillingByIdUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(billingId, cancellationToken);
            return billing is null ? Results.NotFound() : Results.Ok(billing);
        });

        group.MapGet("/{billingId:guid}/point-in-time", async (
            Guid billingId,
            DateTimeOffset at,
            IGetBillingAtPointInTimeUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(billingId, at, cancellationToken);
            return billing is null ? Results.NotFound() : Results.Ok(billing);
        });

        group.MapGet("/{billingId:guid}/history", async (
            Guid billingId,
            IGetBillingEventHistoryUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var history = await useCase.ExecuteAsync(billingId, cancellationToken);
            return Results.Ok(history);
        });

        group.MapPut("/{billingId:guid}/profile", async (
            Guid billingId,
            UpdateBillingProfileInput input,
            IUpdateBillingProfileUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(
                new UpdateBillingProfileRequest(
                    billingId,
                    input.BillingName,
                    input.WantsEmailNotifications,
                    input.WantsSmsNotifications,
                    input.OnboardingStatus,
                    input.Contacts.Select(contact => new Billing.Models.BillingContact(
                        contact.FullName,
                        contact.RelationshipToChildren,
                        contact.Email,
                        contact.Phone,
                        contact.IsPrimaryContact,
                        contact.WantsPortalAccess)).ToArray(),
                    input.Students.Select(student => new Billing.Models.BillingStudent(
                        student.StudentId,
                        student.StudentCode,
                        student.GivenName,
                        student.FamilyName,
                        student.GradeLevel,
                        student.BirthDate,
                        student.RelationshipToPrimaryContact)).ToArray(),
                    input.SourceRegistrationId,
                    input.SourceRegistrationStatus,
                    input.SourceRegistrationUpdatedAtUtc,
                    input.BirthDate,
                    input.GradeLevel,
                    input.PrimaryGuardianName,
                    input.PrimaryGuardianEmail,
                    input.HasMedicalAlert,
                    input.MedicalNotes,
                    input.HasIep,
                    input.Documents.Select(document => new Billing.Models.BillingDocumentReference(
                        document.DocumentType,
                        document.FileName,
                        document.UploadedAtUtc,
                        document.UploadedByUserId)).ToArray(),
                    input.Notes.Select(note => new Billing.Models.BillingRecordNote(
                        note.Category,
                        note.Body,
                        note.RecordedAtUtc,
                        note.RecordedByUserId)).ToArray()),
                cancellationToken);

            return Results.Ok(billing);
        });

        group.MapPost("/registrations/{registrationId:guid}/initialize", async (
            Guid registrationId,
            InitializeBillingLedgerInput input,
            IInitializeBillingLedgerUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(
                new InitializeBillingLedgerRequest(
                    registrationId,
                    input.SubjectId,
                    input.HouseholdName,
                    input.UpdatedByUserId),
                cancellationToken);
            return Results.Ok(billing);
        });

        group.MapPost("/registrations/{registrationId:guid}/invoices", async (
            Guid registrationId,
            CreateBillingInvoiceInput input,
            ICreateBillingInvoiceUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(
                new CreateBillingInvoiceRequest(
                    registrationId,
                    input.Description,
                    input.DueDate,
                    input.Lines.Select(line => new CreateBillingInvoiceLineItem(line.Description, line.Quantity, line.UnitAmount)).ToArray(),
                    input.CurrencyCode,
                    input.CreatedByUserId,
                    input.IdempotencyKey),
                cancellationToken);
            return Results.Ok(billing);
        });

        group.MapPost("/registrations/{registrationId:guid}/payments", async (
            Guid registrationId,
            RecordBillingPaymentInput input,
            IRecordBillingPaymentUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(
                new RecordBillingPaymentRequest(
                    registrationId,
                    input.InvoiceId,
                    input.Amount,
                    input.Method,
                    input.Reference,
                    input.Notes,
                    input.RecordedByUserId),
                cancellationToken);
            return Results.Ok(billing);
        });

        group.MapPost("/registrations/{registrationId:guid}/invoices/{invoiceId:guid}/charge", async (
            Guid registrationId,
            Guid invoiceId,
            ChargeBillingInvoiceInput input,
            IChargeBillingInvoiceUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(
                new ChargeBillingInvoiceRequest(
                    registrationId,
                    invoiceId,
                    input.Amount,
                    input.IdempotencyKey,
                    input.RequestedByUserId,
                    input.Notes),
                cancellationToken);
            return Results.Ok(billing);
        });

        group.MapPost("/registrations/{registrationId:guid}/invoices/{invoiceId:guid}/charge-attempts/{chargeAttemptId:guid}/resolve", async (
            Guid registrationId,
            Guid invoiceId,
            Guid chargeAttemptId,
            ResolveBillingChargeAttemptInput input,
            IResolveBillingChargeAttemptUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(
                new ResolveBillingChargeAttemptRequest(
                    registrationId,
                    invoiceId,
                    chargeAttemptId,
                    input.ResolutionStatus,
                    input.ResolutionNotes,
                    input.ResolvedByUserId),
                cancellationToken);
            return Results.Ok(billing);
        });

        group.MapPost("/auto-pay/run", async (
            RunBillingAutoPayBatchInput input,
            IRunBillingAutoPayBatchUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var result = await useCase.ExecuteAsync(
                new RunBillingAutoPayBatchRequest(
                    input.RunDate,
                    input.IdempotencyKey,
                    input.RequestedByUserId,
                    input.DryRun),
                cancellationToken);
            return Results.Ok(result);
        });

        group.MapPut("/registrations/{registrationId:guid}/payment-plan", async (
            Guid registrationId,
            UpdateBillingPaymentPlanInput input,
            IUpdateBillingPaymentPlanUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(
                new UpdateBillingPaymentPlanRequest(
                    registrationId,
                    input.AutoPayRequested,
                    input.RequestedChargeDayOfMonth,
                    input.DefaultPaymentMethodLabel,
                    input.UpdatedByUserId),
                cancellationToken);
            return Results.Ok(billing);
        });

        group.MapPost("/registrations/{registrationId:guid}/payment-methods", async (
            Guid registrationId,
            UpsertBillingPaymentMethodInput input,
            IUpsertBillingPaymentMethodUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var billing = await useCase.ExecuteAsync(
                new UpsertBillingPaymentMethodRequest(
                    registrationId,
                    input.PaymentMethodId,
                    input.Label,
                    input.MethodKind,
                    input.MaskedDetails,
                    input.IsDefault,
                    input.UpdatedByUserId),
                cancellationToken);
            return Results.Ok(billing);
        });

        return endpoints;
    }
}

public sealed class InitializeBillingLedgerInput
{
    public string SubjectId { get; set; } = string.Empty;
    public string HouseholdName { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
}

public sealed class CreateBillingInvoiceInput
{
    public string Description { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public List<CreateBillingInvoiceLineInput> Lines { get; set; } = [];
}

public sealed class CreateBillingInvoiceLineInput
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitAmount { get; set; }
}

public sealed class RecordBillingPaymentInput
{
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
}

public sealed class ChargeBillingInvoiceInput
{
    public decimal? Amount { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class RunBillingAutoPayBatchInput
{
    public DateOnly RunDate { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;
    public bool DryRun { get; set; } = true;
}

public sealed class ResolveBillingChargeAttemptInput
{
    public string ResolutionStatus { get; set; } = string.Empty;
    public string? ResolutionNotes { get; set; }
    public string ResolvedByUserId { get; set; } = string.Empty;
}

public sealed class UpdateBillingPaymentPlanInput
{
    public bool AutoPayRequested { get; set; }
    public int? RequestedChargeDayOfMonth { get; set; }
    public string? DefaultPaymentMethodLabel { get; set; }
    public string UpdatedByUserId { get; set; } = string.Empty;
}

public sealed class UpsertBillingPaymentMethodInput
{
    public Guid? PaymentMethodId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string MethodKind { get; set; } = string.Empty;
    public string MaskedDetails { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string UpdatedByUserId { get; set; } = string.Empty;
}

public sealed class UpdateBillingProfileInput
{
    public string BillingName { get; set; } = string.Empty;
    public bool WantsEmailNotifications { get; set; }
    public bool WantsSmsNotifications { get; set; }
    public string? OnboardingStatus { get; set; }
    public Guid? SourceRegistrationId { get; set; }
    public string? SourceRegistrationStatus { get; set; }
    public DateTimeOffset? SourceRegistrationUpdatedAtUtc { get; set; }
    public List<BillingContactInput> Contacts { get; set; } = [];
    public List<BillingStudentInput> Students { get; set; } = [];
    public DateOnly? BirthDate { get; set; }
    public string? GradeLevel { get; set; }
    public string? PrimaryGuardianName { get; set; }
    public string? PrimaryGuardianEmail { get; set; }
    public bool HasMedicalAlert { get; set; }
    public string? MedicalNotes { get; set; }
    public bool HasIep { get; set; }
    public List<BillingDocumentInput> Documents { get; set; } = [];
    public List<BillingNoteInput> Notes { get; set; } = [];
}

public sealed class BillingContactInput
{
    public string FullName { get; set; } = string.Empty;
    public string RelationshipToChildren { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsPrimaryContact { get; set; }
    public bool WantsPortalAccess { get; set; }
}

public sealed class BillingStudentInput
{
    public Guid? StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string GivenName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string RelationshipToPrimaryContact { get; set; } = string.Empty;
}

public sealed class BillingDocumentInput
{
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
}

public sealed class BillingNoteInput
{
    public string Category { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
}
