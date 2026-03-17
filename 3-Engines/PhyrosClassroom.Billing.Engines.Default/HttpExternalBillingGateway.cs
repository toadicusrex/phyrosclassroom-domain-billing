using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PhyrosClassroom.Billing.Engines;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Engines.Default;

public sealed class HttpExternalBillingGateway(HttpClient httpClient, IOptions<ExternalBillingGatewayOptions> options) : IExternalBillingGateway
{
    public async Task<ExternalBillingChargeResult> ChargeInvoiceAsync(
        BillingLedger ledger,
        BillingInvoice invoice,
        ExternalBillingChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        var configuredOptions = options.Value;
        var baseUrl = Normalize(configuredOptions.BaseUrl);
        var chargePath = string.IsNullOrWhiteSpace(configuredOptions.ChargePath) ? "/charges" : configuredOptions.ChargePath.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new ExternalBillingChargeResult(
                false,
                configuredOptions.ProviderName,
                "NotConfigured",
                null,
                "ExternalBillingGateway:BaseUrl must be configured before live billing can run.");
        }

        var endpoint = new Uri(new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/"), chargePath.TrimStart('/'));
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new HttpExternalBillingChargePayload
            {
                Provider = configuredOptions.ProviderName,
                MerchantKey = configuredOptions.MerchantKey,
                RegistrationId = request.RegistrationId,
                InvoiceId = request.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                SubjectId = ledger.SubjectId,
                HouseholdName = ledger.HouseholdName,
                CurrencyCode = invoice.CurrencyCode,
                Amount = request.Amount,
                IdempotencyKey = request.IdempotencyKey,
                RequestedByUserId = request.RequestedByUserId,
                Notes = request.Notes,
                PaymentMethod = ledger.PaymentMethods.FirstOrDefault(method =>
                        method.IsDefault ||
                        string.Equals(method.Label, ledger.PaymentPlan.DefaultPaymentMethodLabel, StringComparison.OrdinalIgnoreCase))
                    ?? ledger.PaymentMethods.FirstOrDefault(),
            }),
        };

        if (!string.IsNullOrWhiteSpace(configuredOptions.ApiKey))
        {
            message.Headers.TryAddWithoutValidation(configuredOptions.ApiKeyHeaderName, configuredOptions.ApiKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(configuredOptions.BearerToken))
        {
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", configuredOptions.BearerToken.Trim());
        }

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var gatewayResponse = await response.Content.ReadFromJsonAsync<HttpExternalBillingChargeResponse>(cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var failureReason = gatewayResponse?.FailureReason
                ?? $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim();
            return new ExternalBillingChargeResult(
                false,
                configuredOptions.ProviderName,
                gatewayResponse?.ResultStatus ?? "GatewayRejected",
                gatewayResponse?.ExternalReference,
                failureReason);
        }

        var resultStatus = string.IsNullOrWhiteSpace(gatewayResponse?.ResultStatus)
            ? "Approved"
            : gatewayResponse.ResultStatus.Trim();

        var succeeded = gatewayResponse?.Succeeded
            ?? string.Equals(resultStatus, "Approved", StringComparison.OrdinalIgnoreCase)
            || string.Equals(resultStatus, "Succeeded", StringComparison.OrdinalIgnoreCase);

        return new ExternalBillingChargeResult(
            succeeded,
            string.IsNullOrWhiteSpace(gatewayResponse?.ProcessorName) ? configuredOptions.ProviderName : gatewayResponse.ProcessorName.Trim(),
            resultStatus,
            gatewayResponse?.ExternalReference,
            gatewayResponse?.FailureReason);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class HttpExternalBillingChargePayload
    {
        public string Provider { get; set; } = string.Empty;
        public string? MerchantKey { get; set; }
        public Guid RegistrationId { get; set; }
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string SubjectId { get; set; } = string.Empty;
        public string HouseholdName { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = "USD";
        public decimal Amount { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public string RequestedByUserId { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public BillingPaymentMethod? PaymentMethod { get; set; }
    }

    private sealed class HttpExternalBillingChargeResponse
    {
        public bool? Succeeded { get; set; }
        public string? ProcessorName { get; set; }
        public string? ResultStatus { get; set; }
        public string? ExternalReference { get; set; }
        public string? FailureReason { get; set; }
    }
}
