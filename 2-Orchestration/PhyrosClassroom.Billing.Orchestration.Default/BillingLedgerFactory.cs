using PhyrosClassroom.Billing.Models;
using PhyrosClassroom.Billing.Orchestration;

namespace PhyrosClassroom.Billing.Orchestration.Default;

internal static class BillingLedgerFactory
{
    public static BillingLedger Initialize(InitializeBillingLedgerRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        return new BillingLedger(
            Guid.NewGuid(),
            request.RegistrationId,
            request.SubjectId.Trim(),
            request.HouseholdName.Trim(),
            [],
            new BillingPaymentPlan(false, null, null, now, request.UpdatedByUserId),
            now);
    }

    public static BillingInvoice CreateInvoice(CreateBillingInvoiceRequest request, BillingLedger ledger)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        var sequence = ledger.Invoices.Count + 1;
        var lines = request.Lines
            .Select(line => new BillingInvoiceLine(
                line.Description.Trim(),
                line.Quantity,
                line.UnitAmount,
                decimal.Round(line.Quantity * line.UnitAmount, 2, MidpointRounding.AwayFromZero)))
            .ToArray();
        var amountDue = lines.Sum(line => line.LineTotal);

        return new BillingInvoice(
            Guid.NewGuid(),
            $"INV-{createdAtUtc:yyyyMMdd}-{sequence:000}",
            "Open",
            request.Description.Trim(),
            request.DueDate,
            lines,
            amountDue,
            0m,
            amountDue,
            string.IsNullOrWhiteSpace(request.CurrencyCode) ? "USD" : request.CurrencyCode.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
            createdAtUtc,
            createdAtUtc,
            [],
            []);
    }

    public static BillingInvoice ApplyPayment(BillingInvoice invoice, RecordBillingPaymentRequest request)
    {
        var recordedAtUtc = DateTimeOffset.UtcNow;
        var payment = new BillingPayment(
            Guid.NewGuid(),
            decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            request.Method.Trim(),
            request.Reference.Trim(),
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            recordedAtUtc,
            request.RecordedByUserId);
        var payments = invoice.Payments.Concat([payment]).ToArray();
        var amountPaid = payments.Sum(existing => existing.Amount);
        var balanceDue = decimal.Max(0m, decimal.Round(invoice.AmountDue - amountPaid, 2, MidpointRounding.AwayFromZero));
        var status = balanceDue == 0m ? "Paid" : amountPaid > 0m ? "PartiallyPaid" : "Open";

        return invoice with
        {
            Payments = payments,
            AmountPaid = amountPaid,
            BalanceDue = balanceDue,
            Status = status,
            UpdatedAtUtc = recordedAtUtc,
        };
    }
}
