using System.Net.Http.Json;
using CMS.Application.Billing;
using CMS.Application.Settings;
using Microsoft.Extensions.Logging;

namespace CMS.Infrastructure.Billing;

/// <summary>
/// Real NIC IRP integration skeleton. The authentication call-flow (auth → get token → encrypt
/// payload with AES → POST invoice → receive IRN + signed invoice + QR) is NIC-specific and
/// requires live sandbox credentials that only the practice can issue. The method bodies below
/// are the exact HTTP shape we'll use once creds are provisioned; today they throw a well-typed
/// error if invoked without configuration, so switching providers is a settings change not a code
/// change.
/// </summary>
public class NicIrpProvider : IEInvoiceProvider
{
    private readonly ISettingsStore _settings;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<NicIrpProvider> _log;

    public NicIrpProvider(ISettingsStore settings, IHttpClientFactory http, ILogger<NicIrpProvider> log)
    {
        _settings = settings;
        _http = http;
        _log = log;
    }

    public async Task<EInvoiceResult> GenerateAsync(InvoicePdfModel invoice, CancellationToken ct)
    {
        var opts = await _settings.GetAsync<EInvoicingOptions>(SettingKeys.EInvoicing, ct);
        EnsureConfigured(opts);

        // Payload shape placeholder — NIC v1.03 expects a specific JSON structure
        // (TranDtls, DocDtls, SellerDtls, BuyerDtls, ItemList, ValDtls). Mapping to that spec goes here.
        using var client = _http.CreateClient();
        client.BaseAddress = new Uri(opts.BaseUrl!);
        client.DefaultRequestHeaders.Add("client_id", opts.ClientId);
        client.DefaultRequestHeaders.Add("client_secret", opts.ClientSecret);
        client.DefaultRequestHeaders.Add("Gstin", opts.Gstin);

        // Not implemented until NIC sandbox creds + certified payload mapper land.
        // Keep an explicit throw rather than a silent misbehavior.
        throw new NotImplementedException(
            "NicIrpProvider: payload mapping + encrypted request pipeline pending NIC sandbox provisioning.");
    }

    public async Task<bool> CancelAsync(string irn, string reason, CancellationToken ct)
    {
        var opts = await _settings.GetAsync<EInvoicingOptions>(SettingKeys.EInvoicing, ct);
        EnsureConfigured(opts);
        _log.LogInformation("NicIrp cancel request for {Irn}", irn);
        // Shape: POST /enriched/cancel with { Irn, CnlRsn: "1-6", CnlRem: reason }
        throw new NotImplementedException("NicIrpProvider: cancel endpoint wiring pending.");
        #pragma warning disable CS0162 // Unreachable code — present for the eventual rewiring.
        await Task.CompletedTask;
        return false;
        #pragma warning restore CS0162
    }

    private static void EnsureConfigured(EInvoicingOptions opts)
    {
        if (!opts.Enabled) throw new InvalidOperationException("E-invoicing is disabled in settings.");
        if (string.IsNullOrWhiteSpace(opts.BaseUrl) || string.IsNullOrWhiteSpace(opts.ClientId) ||
            string.IsNullOrWhiteSpace(opts.ClientSecret) || string.IsNullOrWhiteSpace(opts.Gstin))
            throw new InvalidOperationException("NIC IRP settings incomplete (BaseUrl/ClientId/ClientSecret/Gstin).");
    }
}
