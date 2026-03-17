using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using PhyrosClassroom.Billing.Engines;
using PhyrosClassroom.Billing.Models;

namespace PhyrosClassroom.Billing.Engines.Default;

public sealed class UsaEpayExternalBillingGateway(HttpClient httpClient, IOptions<ExternalBillingGatewayOptions> options) : IExternalBillingGateway
{
    private static readonly XNamespace SoapEnvelopeNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace SoapEncodingNs = "http://schemas.xmlsoap.org/soap/encoding/";
    private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace XsdNs = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace UsaEpayNs = "urn:usaepay";

    public async Task<ExternalBillingChargeResult> ChargeInvoiceAsync(
        BillingLedger ledger,
        BillingInvoice invoice,
        ExternalBillingChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        var configuredOptions = options.Value;
        var endpoint = Normalize(configuredOptions.BaseUrl);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new ExternalBillingChargeResult(
                false,
                configuredOptions.ProviderName,
                "NotConfigured",
                null,
                "ExternalBillingGateway:BaseUrl must be configured to the UsaEpay SOAP gate before live billing can run.");
        }

        if (string.IsNullOrWhiteSpace(configuredOptions.SourceKey) || string.IsNullOrWhiteSpace(configuredOptions.Pin))
        {
            return new ExternalBillingChargeResult(
                false,
                configuredOptions.ProviderName,
                "NotConfigured",
                null,
                "ExternalBillingGateway:SourceKey and ExternalBillingGateway:Pin must be configured for UsaEpay.");
        }

        var paymentMethod = ResolvePaymentMethod(ledger);
        if (paymentMethod is null)
        {
            return new ExternalBillingChargeResult(
                false,
                configuredOptions.ProviderName,
                "NotConfigured",
                null,
                "No default billing payment method is configured for this ledger.");
        }

        if (string.IsNullOrWhiteSpace(paymentMethod.ExternalCustomerId) || string.IsNullOrWhiteSpace(paymentMethod.ExternalPaymentMethodId))
        {
            return new ExternalBillingChargeResult(
                false,
                configuredOptions.ProviderName,
                "NotConfigured",
                null,
                $"Payment method '{paymentMethod.Label}' is missing the stored processor customer id or payment method id.");
        }

        var envelope = BuildRequestEnvelope(configuredOptions, ledger, invoice, request, paymentMethod);
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml"),
        };
        message.Headers.TryAddWithoutValidation("SOAPAction", "\"urn:ueSoapServerAction\"");

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ExternalBillingChargeResult(
                false,
                configuredOptions.ProviderName,
                "GatewayRejected",
                null,
                $"{(int)response.StatusCode} {response.ReasonPhrase}: {TrimMessage(content)}".Trim());
        }

        return ParseResponse(content, configuredOptions.ProviderName);
    }

    private static BillingPaymentMethod? ResolvePaymentMethod(BillingLedger ledger) =>
        ledger.PaymentMethods.FirstOrDefault(method =>
            method.IsDefault ||
            string.Equals(method.Label, ledger.PaymentPlan.DefaultPaymentMethodLabel, StringComparison.OrdinalIgnoreCase));

    private static XDocument BuildRequestEnvelope(
        ExternalBillingGatewayOptions configuredOptions,
        BillingLedger ledger,
        BillingInvoice invoice,
        ExternalBillingChargeRequest request,
        BillingPaymentMethod paymentMethod)
    {
        var seed = Guid.NewGuid().ToString("N");
        var hashValue = ComputeMd5Hash($"{configuredOptions.SourceKey}{seed}{configuredOptions.Pin}");
        var clientIp = string.IsNullOrWhiteSpace(configuredOptions.ClientIpAddress) ? "127.0.0.1" : configuredOptions.ClientIpAddress.Trim();
        var clerkName = string.IsNullOrWhiteSpace(configuredOptions.ClerkName) ? "PhyrosClassroom" : configuredOptions.ClerkName.Trim();

        var token = new XElement("Token",
            new XAttribute(XsiNs + "type", "ns1:ueSecurityToken"),
            new XElement("ClientIP", clientIp),
            new XElement("PinHash",
                new XAttribute(XsiNs + "type", "ns1:ueHash"),
                new XElement("HashValue", hashValue),
                new XElement("Seed", seed),
                new XElement("Type", "md5")),
            new XElement("SourceKey", configuredOptions.SourceKey!.Trim()));

        var details = new XElement("Details",
            new XAttribute(XsiNs + "type", "ns1:TransactionDetail"),
            new XElement("Amount", request.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
            new XElement("Clerk", clerkName),
            new XElement("Description", string.IsNullOrWhiteSpace(request.Notes) ? invoice.Description : request.Notes!.Trim()),
            new XElement("Comments", $"Invoice {invoice.InvoiceNumber} / {ledger.HouseholdName}"),
            new XElement("Invoice", invoice.InvoiceNumber),
            new XElement("OrderID", request.RegistrationId.ToString("N")));

        var parameters = new XElement("Parameters",
            new XAttribute(XsiNs + "type", "ns1:CustomerTransactionRequest"),
            new XElement("Command", "Sale"),
            details,
            new XElement("Software", "PhyrosClassroom"),
            new XElement("ClientIP", clientIp),
            new XElement("isRecurring", ledger.PaymentPlan.AutoPayRequested));

        return new XDocument(
            new XElement(SoapEnvelopeNs + "Envelope",
                new XAttribute(XNamespace.Xmlns + "SOAP-ENV", SoapEnvelopeNs),
                new XAttribute(XNamespace.Xmlns + "SOAP-ENC", SoapEncodingNs),
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNs),
                new XAttribute(XNamespace.Xmlns + "xsd", XsdNs),
                new XAttribute(XNamespace.Xmlns + "ns1", UsaEpayNs),
                new XElement(SoapEnvelopeNs + "Body",
                    new XAttribute(SoapEnvelopeNs + "encodingStyle", SoapEncodingNs),
                    new XElement(UsaEpayNs + "runCustomerTransaction",
                        token,
                        new XElement("CustNum", paymentMethod.ExternalCustomerId!.Trim()),
                        new XElement("PaymentMethodID", paymentMethod.ExternalPaymentMethodId!.Trim()),
                        parameters))));
    }

    private static ExternalBillingChargeResult ParseResponse(string content, string providerName)
    {
        try
        {
            var document = XDocument.Parse(content);
            var result = FindFirstValue(document, "Result");
            var reference = FindFirstValue(document, "RefNum");
            var error = FindFirstValue(document, "Error");

            if (!string.IsNullOrWhiteSpace(result) && char.ToUpperInvariant(result[0]) == 'A')
            {
                return new ExternalBillingChargeResult(true, providerName, "Approved", reference, null);
            }

            return new ExternalBillingChargeResult(
                false,
                providerName,
                string.IsNullOrWhiteSpace(result) ? "Declined" : result.Trim(),
                reference,
                string.IsNullOrWhiteSpace(error) ? "UsaEpay declined the transaction." : error.Trim());
        }
        catch (Exception ex)
        {
            return new ExternalBillingChargeResult(
                false,
                providerName,
                "GatewayParseFailure",
                null,
                $"Unable to parse UsaEpay response: {ex.Message}");
        }
    }

    private static string? FindFirstValue(XContainer document, string localName) =>
        document.Descendants().FirstOrDefault(element => string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string ComputeMd5Hash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = MD5.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string TrimMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var trimmed = content.Trim();
        return trimmed.Length <= 200 ? trimmed : trimmed[..200];
    }
}
