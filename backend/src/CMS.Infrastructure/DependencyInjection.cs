using CMS.Application.Abstractions;
using CMS.Domain.Identity;
using CMS.Infrastructure.Auditing;
using CMS.Infrastructure.Billing;
using CMS.Infrastructure.Clients;
using CMS.Infrastructure.Compliance;
using CMS.Infrastructure.Documents;
using CMS.Infrastructure.Email;
using CMS.Infrastructure.Identity;
using CMS.Infrastructure.Settings;
using CMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CmsDb")
            ?? throw new InvalidOperationException("ConnectionStrings:CmsDb is not configured.");

        services.AddScoped<AuditingInterceptor>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IClientCodeGenerator, Clients.ClientCodeGenerator>();
        services.AddScoped<IServiceFileNumberGenerator, ServiceFiles.ServiceFileNumberGenerator>();
        services.AddScoped<IImportEngine, Imports.ImportEngine>();
        services.AddScoped<CMS.Application.Settings.ISettingsStore, DbSettingsStore>();
        services.AddScoped<IComplianceObligationGenerator, ComplianceObligationGenerator>();
        services.AddHostedService<ComplianceSchedulerHostedService>();
        services.AddScoped<CMS.Infrastructure.Notifications.IWeeklySummaryService,
                           CMS.Infrastructure.Notifications.WeeklySummaryService>();
        services.AddHostedService<CMS.Infrastructure.Notifications.WeeklySummaryHostedService>();

        services.AddOptions<ReminderOptions>()
            .Bind(configuration.GetSection(ReminderOptions.SectionName));
        services.AddScoped<IComplianceReminderService, ComplianceReminderService>();
        services.AddHostedService<ComplianceReminderHostedService>();

        services.AddOptions<DocumentStorageOptions>()
            .Bind(configuration.GetSection(DocumentStorageOptions.SectionName));
        services.AddSingleton<IDocumentStorage, LocalFileSystemStorage>();
        services.AddScoped<CMS.Application.Documents.IDocumentUrlSigner, HmacDocumentUrlSigner>();

        services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
        services.AddScoped<CMS.Infrastructure.Reconciliation.BankStatementParser>();
        services.AddScoped<CMS.Application.Letters.ILetterRenderer, CMS.Infrastructure.Letters.LetterRenderer>();
        services.AddScoped<CMS.Infrastructure.Reconciliation.IReconciliationService,
                            CMS.Infrastructure.Reconciliation.ReconciliationService>();

        services.AddOptions<EInvoicingOptions>()
            .Bind(configuration.GetSection(EInvoicingOptions.SectionName));
        services.AddOptions<DigitalSignatureOptions>()
            .Bind(configuration.GetSection(DigitalSignatureOptions.SectionName));
        services.AddScoped<CMS.Application.Billing.IInvoiceSigner, PfxInvoiceSigner>();
        services.AddHttpClient();
        services.AddScoped<CMS.Application.Billing.IEInvoiceProvider>(sp =>
        {
            var store = sp.GetRequiredService<CMS.Application.Settings.ISettingsStore>();
            var opts = store.GetAsync<EInvoicingOptions>(CMS.Application.Settings.SettingKeys.EInvoicing).GetAwaiter().GetResult();
            if (string.Equals(opts.Provider, "Nic", StringComparison.OrdinalIgnoreCase))
                return new NicIrpProvider(store, sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<NicIrpProvider>>());
            return new StubEInvoiceProvider(sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<StubEInvoiceProvider>>());
        });

        services.AddOptions<FirmProfileOptions>()
            .Bind(configuration.GetSection(FirmProfileOptions.SectionName));
        services.AddScoped<CMS.Application.Billing.IInvoicePdfRenderer, QuestPdfInvoiceRenderer>();

        services.AddOptions<MailOptions>()
            .Bind(configuration.GetSection(MailOptions.SectionName));
        services.AddScoped<CMS.Application.Email.IEmailSender>(sp =>
        {
            // Resolve the live MailOptions via the settings store so a config change at runtime
            // flips the picker between SMTP and filesystem drop on the next request.
            var store = sp.GetRequiredService<CMS.Application.Settings.ISettingsStore>();
            var opts = store.GetAsync<MailOptions>(CMS.Application.Settings.SettingKeys.Mail).GetAwaiter().GetResult();
            return string.IsNullOrWhiteSpace(opts.SmtpHost)
                ? new FileSystemEmailSender(store,
                                             sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MailOptions>>(),
                                             sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileSystemEmailSender>>())
                : (CMS.Application.Email.IEmailSender)new SmtpEmailSender(store,
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SmtpEmailSender>>());
        });

        services.AddDbContext<CmsDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(CmsDbContext).Assembly.FullName);
                // Retry-on-failure is intentionally disabled: the import engine opens explicit
                // transactions whose body is NOT idempotent — in-memory decision state (CommittedClientId,
                // tracked entities) would be stale on a retry. Transient DB failures surface as HTTP 500.
            });
            options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
        });

        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireNonAlphanumeric = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireDigit = true;
                o.User.RequireUniqueEmail = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<CmsDbContext>()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<ITotpService, OtpNetTotpService>();
        services.AddSingleton<IMfaPendingTokenService, MfaPendingTokenService>();

        return services;
    }
}
