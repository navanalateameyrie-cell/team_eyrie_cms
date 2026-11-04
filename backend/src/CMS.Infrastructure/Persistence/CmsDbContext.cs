using CMS.Domain.Auditing;
using CMS.Domain.Entities;
using CMS.Domain.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CMS.Infrastructure.Persistence;

public class CmsDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<EntityType> EntityTypes => Set<EntityType>();
    public DbSet<State> States => Set<State>();
    public DbSet<ServiceDefinition> ServiceDefinitions => Set<ServiceDefinition>();
    public DbSet<ClientGroup> ClientGroups => Set<ClientGroup>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientCodeCounter> ClientCodeCounters => Set<ClientCodeCounter>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<TaxRegistration> TaxRegistrations => Set<TaxRegistration>();
    public DbSet<ServiceFile> ServiceFiles => Set<ServiceFile>();
    public DbSet<Engagement> Engagements => Set<Engagement>();
    public DbSet<StaffAssignment> StaffAssignments => Set<StaffAssignment>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<TdsEntry> TdsEntries => Set<TdsEntry>();
    public DbSet<TdsChallan> TdsChallans => Set<TdsChallan>();
    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<ChecklistTemplateItem> ChecklistTemplateItems => Set<ChecklistTemplateItem>();
    public DbSet<EngagementChecklist> EngagementChecklists => Set<EngagementChecklist>();
    public DbSet<EngagementChecklistItem> EngagementChecklistItems => Set<EngagementChecklistItem>();
    public DbSet<LetterTemplate> LetterTemplates => Set<LetterTemplate>();
    public DbSet<ComplianceObligation> ComplianceObligations => Set<ComplianceObligation>();
    public DbSet<ObligationEvent> ObligationEvents => Set<ObligationEvent>();
    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WeeklySummaryLog> WeeklySummaryLogs => Set<WeeklySummaryLog>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentLink> DocumentLinks => Set<DocumentLink>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentApproval> DocumentApprovals => Set<DocumentApproval>();
    public DbSet<FeeSchedule> FeeSchedules => Set<FeeSchedule>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<InvoiceNumberCounter> InvoiceNumberCounters => Set<InvoiceNumberCounter>();
    public DbSet<DocumentNumberCounter> DocumentNumberCounters => Set<DocumentNumberCounter>();
    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();
    public DbSet<DebitNote> DebitNotes => Set<DebitNote>();
    public DbSet<Advance> Advances => Set<Advance>();
    public DbSet<AdvanceApplication> AdvanceApplications => Set<AdvanceApplication>();
    public DbSet<BankStatementBatch> BankStatementBatches => Set<BankStatementBatch>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();
    public DbSet<EmailOutboxEntry> EmailOutbox => Set<EmailOutboxEntry>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<CMS.Domain.Identity.RefreshToken> RefreshTokens => Set<CMS.Domain.Identity.RefreshToken>();
    public DbSet<CMS.Domain.Identity.PasswordResetToken> PasswordResetTokens => Set<CMS.Domain.Identity.PasswordResetToken>();
    public DbSet<CMS.Domain.Identity.UserMfaSecret> UserMfaSecrets => Set<CMS.Domain.Identity.UserMfaSecret>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportStagingClient> ImportStagingClients => Set<ImportStagingClient>();
    public DbSet<ImportStagingServiceFile> ImportStagingServiceFiles => Set<ImportStagingServiceFile>();

    public CmsDbContext(DbContextOptions<CmsDbContext> options) : base(options) { }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreventAuditMutation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PreventAuditMutation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PreventAuditMutation()
    {
        var mutatedAuditEntries = ChangeTracker.Entries<AuditEntry>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (mutatedAuditEntries.Count > 0)
        {
            throw new InvalidOperationException("Audit records are append-only and cannot be modified or deleted.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Rename Identity tables to a cleaner schema ---
        modelBuilder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.CreatedAtUtc).IsRequired();
            b.Property(u => u.IsEnabled).HasDefaultValue(true);
            b.HasOne<Staff>()
                .WithMany()
                .HasForeignKey(u => u.StaffId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(u => u.StaffId);
        });
        modelBuilder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("roles");
            b.Property(r => r.Description).HasMaxLength(256);
        });
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("role_claims");

        // --- Staff ---
        modelBuilder.Entity<Staff>(b =>
        {
            b.ToTable("staff");
            b.HasKey(s => s.Id);
            b.Property(s => s.Code).IsRequired().HasMaxLength(32);
            b.Property(s => s.FullName).IsRequired().HasMaxLength(200);
            b.Property(s => s.Email).IsRequired().HasMaxLength(256);
            b.Property(s => s.Phone).HasMaxLength(32);
            b.Property(s => s.Role).HasMaxLength(100);
            b.HasIndex(s => s.Code).IsUnique();
            b.HasIndex(s => s.Email).IsUnique();
        });

        // --- Masters ---
        modelBuilder.Entity<EntityType>(b =>
        {
            b.ToTable("entity_types");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(32);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.HasIndex(x => x.Code).IsUnique();
        });
        modelBuilder.Entity<State>(b =>
        {
            b.ToTable("states");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(16);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.CountryCode).IsRequired().HasMaxLength(8);
            b.HasIndex(x => new { x.CountryCode, x.Code }).IsUnique();
        });
        modelBuilder.Entity<ServiceDefinition>(b =>
        {
            b.ToTable("service_definitions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(32);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Category).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.RequiredFieldsJson).HasColumnType("text");
            b.Property(x => x.OptionalFieldsJson).HasColumnType("text");
            b.Property(x => x.Frequency).HasConversion<int?>();
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.Category);
        });
        modelBuilder.Entity<ClientGroup>(b =>
        {
            b.ToTable("client_groups");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(32);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.HasIndex(x => x.Code).IsUnique();
        });

        // --- Client ---
        modelBuilder.Entity<Client>(b =>
        {
            b.ToTable("clients");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(8).IsFixedLength();
            b.Property(x => x.LegacyClientId).HasMaxLength(64);
            b.Property(x => x.LegalName).IsRequired().HasMaxLength(300);
            b.Property(x => x.TradeName).HasMaxLength(300);
            b.Property(x => x.Pan).HasMaxLength(10).IsFixedLength();
            b.Property(x => x.Version).IsConcurrencyToken();

            b.HasIndex(x => x.Code).IsUnique();
            // Both Postgres and SQLite treat NULLs as distinct in UNIQUE indexes, so no filter needed.
            b.HasIndex(x => x.LegacyClientId).IsUnique();
            b.HasIndex(x => x.LegalName);
            b.HasIndex(x => x.TradeName);
            b.HasIndex(x => x.Pan);
            b.HasIndex(x => x.IsActive);

            b.HasOne(x => x.EntityType)
                .WithMany()
                .HasForeignKey(x => x.EntityTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ClientGroup)
                .WithMany()
                .HasForeignKey(x => x.ClientGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ClientCodeCounter>(b =>
        {
            b.ToTable("client_code_counters");
            b.HasKey(x => x.Name);
            b.Property(x => x.Name).HasMaxLength(32);
            b.HasData(
                new ClientCodeCounter("client", 10_000_000),
                new ClientCodeCounter("service_file", 1_000_000_000));
        });

        // --- Branch (+ owned Address) ---
        modelBuilder.Entity<Branch>(b =>
        {
            b.ToTable("branches");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(32);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Type).HasConversion<int>().IsRequired();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.OwnsOne(x => x.Address, a =>
            {
                a.Property(p => p.Line1).HasColumnName("AddressLine1").IsRequired().HasMaxLength(200);
                a.Property(p => p.Line2).HasColumnName("AddressLine2").HasMaxLength(200);
                a.Property(p => p.City).HasColumnName("AddressCity").IsRequired().HasMaxLength(100);
                a.Property(p => p.Pincode).HasColumnName("AddressPincode").IsRequired().HasMaxLength(16);
            });
            b.HasIndex(x => new { x.ClientId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.ClientId, x.IsPrimary }); // service layer enforces the single-primary rule
            b.HasIndex(x => x.StateId);

            b.HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.State).WithMany().HasForeignKey(x => x.StateId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- Service file ---
        modelBuilder.Entity<ServiceFile>(b =>
        {
            b.ToTable("service_files");
            b.HasKey(x => x.Id);
            b.Property(x => x.FileNumber).IsRequired().HasMaxLength(10).IsFixedLength();
            b.Property(x => x.LegacyFileNumber).HasMaxLength(64);
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.MetadataJson).HasColumnType("text");
            b.Property(x => x.Version).IsConcurrencyToken();

            b.HasIndex(x => x.FileNumber).IsUnique();
            // Legacy file number is scoped unique per (client, service) — set up as raw SQL during
            // Phase-11 import run; the global unique index was dropped there. No index declared here.
            b.HasIndex(x => x.ClientId);
            b.HasIndex(x => x.ServiceDefinitionId);
            b.HasIndex(x => x.BranchId);
            b.HasIndex(x => x.Status);

            b.HasOne(x => x.Client)
                .WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ServiceDefinition)
                .WithMany().HasForeignKey(x => x.ServiceDefinitionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Branch)
                .WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- Engagement ---
        modelBuilder.Entity<Engagement>(b =>
        {
            b.ToTable("engagements");
            b.HasKey(x => x.Id);
            b.Property(x => x.FinancialYear).IsRequired().HasMaxLength(7).IsFixedLength();
            b.Property(x => x.AssessmentYear).HasMaxLength(7).IsFixedLength();
            b.Property(x => x.PeriodType).HasConversion<int>().IsRequired();
            b.Property(x => x.PeriodLabel).HasMaxLength(32);
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();

            // No overwrite: a given (file, FY, period-type, period-label) occurs at most once.
            b.HasIndex(x => new { x.ServiceFileId, x.FinancialYear, x.PeriodType, x.PeriodLabel }).IsUnique();
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.FinancialYear);

            b.HasOne(x => x.ServiceFile)
                .WithMany().HasForeignKey(x => x.ServiceFileId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Compliance obligations + append-only event log ---
        modelBuilder.Entity<ComplianceObligation>(b =>
        {
            b.ToTable("compliance_obligations");
            b.HasKey(x => x.Id);
            b.Property(x => x.PeriodKey).IsRequired().HasMaxLength(32);
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.Reference).HasMaxLength(64);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();

            // One row per (file, period) — scheduler re-runs must not duplicate.
            b.HasIndex(x => new { x.ServiceFileId, x.PeriodKey }).IsUnique();
            b.HasIndex(x => x.DueDate);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.AssignedStaffId);

            b.HasOne(x => x.ServiceFile).WithMany().HasForeignKey(x => x.ServiceFileId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.AssignedStaff).WithMany().HasForeignKey(x => x.AssignedStaffId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Notification>(b =>
        {
            b.ToTable("notifications");
            b.HasKey(x => x.Id);
            b.Property(x => x.Kind).IsRequired().HasMaxLength(40);
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.Body).HasMaxLength(2000);
            b.Property(x => x.Link).HasMaxLength(500);
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => new { x.UserId, x.ReadAtUtc });
        });
        modelBuilder.Entity<WeeklySummaryLog>(b =>
        {
            b.ToTable("weekly_summary_logs");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.IsoWeekStart).IsUnique();
        });

        modelBuilder.Entity<ReminderLog>(b =>
        {
            b.ToTable("reminder_logs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Kind).HasConversion<int>().IsRequired();
            b.Property(x => x.AssigneeEmail).HasMaxLength(256);
            // De-dupe: the daily worker uses this unique index as its idempotency guarantee.
            b.HasIndex(x => new { x.ObligationId, x.Kind, x.SentOnDate }).IsUnique();
            b.HasIndex(x => x.SentOnDate);
            b.HasOne(x => x.Obligation).WithMany().HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ObligationEvent>(b =>
        {
            b.ToTable("obligation_events");
            b.HasKey(x => x.Id);
            b.Property(x => x.Kind).HasConversion<int>().IsRequired();
            b.Property(x => x.FromStatus).HasConversion<int?>();
            b.Property(x => x.ToStatus).HasConversion<int?>();
            b.Property(x => x.Note).HasMaxLength(2000);
            b.Property(x => x.ActorUserName).HasMaxLength(256);
            b.HasIndex(x => x.ObligationId);
            b.HasIndex(x => x.CreatedAtUtc);
            b.HasOne(x => x.Obligation).WithMany().HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Documents + polymorphic links ---
        modelBuilder.Entity<Document>(b =>
        {
            b.ToTable("documents");
            b.HasKey(x => x.Id);
            b.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(260);
            b.Property(x => x.StorageKey).IsRequired().HasMaxLength(260);
            b.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
            b.Property(x => x.Sha256).IsRequired().HasMaxLength(64).IsFixedLength();
            b.Property(x => x.Category).HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.UploaderName).HasMaxLength(256);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.Sha256);
            b.HasIndex(x => x.IsDeleted);
            b.HasIndex(x => x.Category);
        });

        modelBuilder.Entity<DocumentLink>(b =>
        {
            b.ToTable("document_links");
            b.HasKey(x => x.Id);
            b.Property(x => x.OwnerType).HasConversion<int>().IsRequired();
            b.HasIndex(x => new { x.OwnerType, x.OwnerId });
            b.HasIndex(x => new { x.DocumentId, x.OwnerType, x.OwnerId }).IsUnique();
            b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentVersion>(b =>
        {
            b.ToTable("document_versions");
            b.HasKey(x => x.Id);
            b.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(260);
            b.Property(x => x.StorageKey).IsRequired().HasMaxLength(260);
            b.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
            b.Property(x => x.Sha256).IsRequired().HasMaxLength(64).IsFixedLength();
            b.Property(x => x.UploaderName).HasMaxLength(256);
            b.HasIndex(x => new { x.DocumentId, x.VersionNumber }).IsUnique();
            b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentApproval>(b =>
        {
            b.ToTable("document_approvals");
            b.HasKey(x => x.Id);
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.RequesterName).HasMaxLength(256);
            b.Property(x => x.ApproverName).HasMaxLength(256);
            b.Property(x => x.RequesterNote).HasMaxLength(2000);
            b.Property(x => x.DecisionNote).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.DocumentId);
            b.HasIndex(x => x.Status);
            b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Billing: fee schedules, invoices, line items, payments ---
        modelBuilder.Entity<FeeSchedule>(b =>
        {
            b.ToTable("fee_schedules");
            b.HasKey(x => x.Id);
            b.Property(x => x.AmountInr).HasColumnType("numeric(14,2)").IsRequired();
            b.Property(x => x.GstRatePercent).HasColumnType("numeric(5,2)").IsRequired();
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();

            b.HasIndex(x => x.ServiceDefinitionId);
            b.HasIndex(x => x.ClientId);
            b.HasIndex(x => new { x.ServiceDefinitionId, x.ClientId, x.EffectiveFrom });
            b.HasOne(x => x.ServiceDefinition).WithMany().HasForeignKey(x => x.ServiceDefinitionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(b =>
        {
            b.ToTable("invoices");
            b.HasKey(x => x.Id);
            b.Property(x => x.Number).IsRequired().HasMaxLength(32);
            b.Property(x => x.FinancialYear).IsRequired().HasMaxLength(7).IsFixedLength();
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.SubtotalInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.CgstInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.SgstInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.IgstInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.TotalInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.PaidInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.CreditedInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.EInvoiceStatus).HasConversion<int>().IsRequired();
            b.Property(x => x.Irn).HasMaxLength(64);
            b.Property(x => x.AckNo).HasMaxLength(32);
            b.Property(x => x.QrText).HasColumnType("text");
            b.Property(x => x.EInvoiceCancelReason).HasMaxLength(500);
            b.Property(x => x.Version).IsConcurrencyToken();

            b.HasIndex(x => x.Irn).IsUnique();
            b.HasIndex(x => x.EInvoiceStatus);
            b.HasIndex(x => x.Number).IsUnique();
            b.HasIndex(x => x.FinancialYear);
            b.HasIndex(x => x.ClientId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.DueDate);
            b.HasIndex(x => x.IssueDate);
            b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            // Line items are an owned collection in DDD terms, but modelled as a regular aggregated FK
            // so EF can project each item independently (reports, exports, etc.).
            b.HasMany(x => x.LineItems).WithOne().HasForeignKey(li => li.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvoiceLineItem>(b =>
        {
            b.ToTable("invoice_line_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Description).IsRequired().HasMaxLength(500);
            b.Property(x => x.Quantity).HasColumnType("numeric(10,3)");
            b.Property(x => x.UnitPriceInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.GstRatePercent).HasColumnType("numeric(5,2)");
            b.Property(x => x.LineSubtotalInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.LineGstInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.LineTotalInr).HasColumnType("numeric(14,2)");
            b.HasIndex(x => x.InvoiceId);
            b.HasIndex(x => x.ServiceFileId);
            b.HasIndex(x => x.ComplianceObligationId);
        });

        modelBuilder.Entity<Payment>(b =>
        {
            b.ToTable("payments");
            b.HasKey(x => x.Id);
            b.Property(x => x.Mode).HasConversion<int>().IsRequired();
            b.Property(x => x.AmountInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasIndex(x => x.InvoiceId);
            b.HasIndex(x => x.PaidOn);
            b.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvoiceNumberCounter>(b =>
        {
            b.ToTable("invoice_number_counters");
            b.HasKey(x => x.FinancialYear);
            b.Property(x => x.FinancialYear).HasMaxLength(7).IsFixedLength();
        });

        modelBuilder.Entity<DocumentNumberCounter>(b =>
        {
            b.ToTable("document_number_counters");
            b.HasKey(x => new { x.Kind, x.FinancialYear });
            b.Property(x => x.Kind).HasMaxLength(8);
            b.Property(x => x.FinancialYear).HasMaxLength(7).IsFixedLength();
        });

        modelBuilder.Entity<CreditNote>(b =>
        {
            b.ToTable("credit_notes");
            b.HasKey(x => x.Id);
            b.Property(x => x.Number).IsRequired().HasMaxLength(32);
            b.Property(x => x.FinancialYear).IsRequired().HasMaxLength(7).IsFixedLength();
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.SubtotalInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.CgstInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.SgstInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.IgstInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.TotalInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.Number).IsUnique();
            b.HasIndex(x => x.InvoiceId);
            b.HasIndex(x => x.ClientId);
            b.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DebitNote>(b =>
        {
            b.ToTable("debit_notes");
            b.HasKey(x => x.Id);
            b.Property(x => x.Number).IsRequired().HasMaxLength(32);
            b.Property(x => x.FinancialYear).IsRequired().HasMaxLength(7).IsFixedLength();
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.SubtotalInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.CgstInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.SgstInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.IgstInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.TotalInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.Number).IsUnique();
            b.HasIndex(x => x.InvoiceId);
            b.HasIndex(x => x.ClientId);
            b.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Advance>(b =>
        {
            b.ToTable("advances");
            b.HasKey(x => x.Id);
            b.Property(x => x.AmountInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.AmountAppliedInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.Mode).HasConversion<int>().IsRequired();
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.ClientId);
            b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BankStatementBatch>(b =>
        {
            b.ToTable("bank_statement_batches");
            b.HasKey(x => x.Id);
            b.Property(x => x.FileName).IsRequired().HasMaxLength(260);
            b.Property(x => x.UploadedByUserName).HasMaxLength(256);
            b.HasIndex(x => x.CreatedAtUtc);
        });
        modelBuilder.Entity<BankStatementLine>(b =>
        {
            b.ToTable("bank_statement_lines");
            b.HasKey(x => x.Id);
            b.Property(x => x.Description).IsRequired().HasMaxLength(500);
            b.Property(x => x.Reference).HasMaxLength(200);
            b.Property(x => x.AmountInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.MatchNote).HasMaxLength(500);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.BatchId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.MatchedInvoiceId);
            b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdvanceApplication>(b =>
        {
            b.ToTable("advance_applications");
            b.HasKey(x => x.Id);
            b.Property(x => x.AmountInr).HasColumnType("numeric(14,2)");
            b.HasIndex(x => x.AdvanceId);
            b.HasIndex(x => x.InvoiceId);
            b.HasOne(x => x.Advance).WithMany().HasForeignKey(x => x.AdvanceId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CMS.Domain.Identity.RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");
            b.HasKey(x => x.Id);
            b.Property(x => x.TokenHash).IsRequired().HasMaxLength(64).IsFixedLength();
            b.Property(x => x.RevokeReason).HasMaxLength(100);
            b.Property(x => x.ReplacedByTokenHash).HasMaxLength(64).IsFixedLength();
            b.Property(x => x.CreatedByIp).HasMaxLength(64);
            b.Property(x => x.UserAgent).HasMaxLength(512);
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.ExpiresAtUtc);
        });
        modelBuilder.Entity<CMS.Domain.Identity.PasswordResetToken>(b =>
        {
            b.ToTable("password_reset_tokens");
            b.HasKey(x => x.Id);
            b.Property(x => x.TokenHash).IsRequired().HasMaxLength(64).IsFixedLength();
            b.Property(x => x.CreatedByIp).HasMaxLength(64);
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.ExpiresAtUtc);
        });
        modelBuilder.Entity<CMS.Domain.Identity.UserMfaSecret>(b =>
        {
            b.ToTable("user_mfa_secrets");
            b.HasKey(x => x.Id);
            b.Property(x => x.Base32Secret).IsRequired().HasMaxLength(64);
            b.HasIndex(x => x.UserId).IsUnique();
        });

        modelBuilder.Entity<AppSetting>(b =>
        {
            b.ToTable("app_settings");
            b.HasKey(x => x.Id);
            b.Property(x => x.Key).IsRequired().HasMaxLength(64);
            b.Property(x => x.ValueJson).HasColumnType("text").IsRequired();
            b.Property(x => x.UpdatedByUserName).HasMaxLength(256);
            b.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<EmailOutboxEntry>(b =>
        {
            b.ToTable("email_outbox");
            b.HasKey(x => x.Id);
            b.Property(x => x.ToAddresses).IsRequired().HasMaxLength(2000);
            b.Property(x => x.CcAddresses).HasMaxLength(2000);
            b.Property(x => x.Subject).IsRequired().HasMaxLength(500);
            b.Property(x => x.BodyPlainText).HasColumnType("text");
            b.Property(x => x.FromAddress).IsRequired().HasMaxLength(256);
            b.Property(x => x.FromName).HasMaxLength(256);
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.ErrorMessage).HasMaxLength(2000);
            b.Property(x => x.SenderUserName).HasMaxLength(256);
            b.HasIndex(x => x.CreatedAtUtc);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.InvoiceId);
        });

        // --- Imports (staging) ---
        modelBuilder.Entity<ImportBatch>(b =>
        {
            b.ToTable("import_batches");
            b.HasKey(x => x.Id);
            b.Property(x => x.FileName).IsRequired().HasMaxLength(260);
            b.Property(x => x.FileSha256).IsRequired().HasMaxLength(64).IsFixedLength();
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.SourceSheet).HasMaxLength(100);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.FileSha256);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.UploadedAtUtc);
        });

        modelBuilder.Entity<ImportStagingClient>(b =>
        {
            b.ToTable("import_staging_clients");
            b.HasKey(x => x.Id);
            b.Property(x => x.RawJson).HasColumnType("text").IsRequired();
            b.Property(x => x.NormalizedPan).HasMaxLength(10);
            b.Property(x => x.NormalizedLegalName).HasMaxLength(300);
            b.Property(x => x.NormalizedTradeName).HasMaxLength(300);
            b.Property(x => x.NormalizedLegacyClientId).HasMaxLength(64);
            b.Property(x => x.EntityTypeCode).HasMaxLength(32);
            b.Property(x => x.ClientGroupCode).HasMaxLength(32);
            b.Property(x => x.ValidationErrorsJson).HasColumnType("text");
            b.Property(x => x.CandidateClientIdsJson).HasColumnType("text");
            b.Property(x => x.Decision).HasConversion<int>().IsRequired();
            b.HasIndex(x => x.BatchId);
            b.HasIndex(x => x.NormalizedPan);
            b.HasIndex(x => x.NormalizedLegacyClientId);
            b.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportStagingServiceFile>(b =>
        {
            b.ToTable("import_staging_service_files");
            b.HasKey(x => x.Id);
            b.Property(x => x.ServiceCode).HasMaxLength(32).IsRequired();
            b.Property(x => x.LegacyFileNumber).HasMaxLength(64);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.ValidationErrorsJson).HasColumnType("text");
            b.Property(x => x.Decision).HasConversion<int>().IsRequired();
            b.HasIndex(x => x.BatchId);
            b.HasIndex(x => x.SourceStagingClientId);
            b.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<ImportStagingClient>().WithMany().HasForeignKey(x => x.SourceStagingClientId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- TDS entries + challans ---
        modelBuilder.Entity<TdsChallan>(b =>
        {
            b.ToTable("tds_challans");
            b.HasKey(x => x.Id);
            b.Property(x => x.ChallanNumber).IsRequired().HasMaxLength(64);
            b.Property(x => x.BankBsrCode).HasMaxLength(32);
            b.Property(x => x.Section).IsRequired().HasMaxLength(16);
            b.Property(x => x.TotalAmountInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.ClientId);
            b.HasIndex(x => new { x.ClientId, x.ChallanNumber }).IsUnique();
            b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<TdsEntry>(b =>
        {
            b.ToTable("tds_entries");
            b.HasKey(x => x.Id);
            b.Property(x => x.Section).IsRequired().HasMaxLength(16);
            b.Property(x => x.DeducteeName).IsRequired().HasMaxLength(300);
            b.Property(x => x.DeducteePan).HasMaxLength(10).IsFixedLength();
            b.Property(x => x.GrossAmountInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.TdsRatePercent).HasColumnType("numeric(5,2)");
            b.Property(x => x.TdsAmountInr).HasColumnType("numeric(14,2)");
            b.Property(x => x.Quarter).HasConversion<int>().IsRequired();
            b.Property(x => x.FinancialYear).IsRequired().HasMaxLength(7).IsFixedLength();
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.ClientId);
            b.HasIndex(x => new { x.ClientId, x.FinancialYear, x.Quarter, x.Section });
            b.HasIndex(x => x.ChallanId);
            b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Challan).WithMany().HasForeignKey(x => x.ChallanId).OnDelete(DeleteBehavior.SetNull);
        });

        // --- Timesheet entries ---
        modelBuilder.Entity<TimeEntry>(b =>
        {
            b.ToTable("time_entries");
            b.HasKey(x => x.Id);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.StaffId, x.WorkDate });
            b.HasIndex(x => x.EngagementId);
            b.HasIndex(x => x.IsApproved);
            b.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Engagement).WithMany().HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Checklist templates (per service) + per-engagement instances ---
        modelBuilder.Entity<ChecklistTemplate>(b =>
        {
            b.ToTable("checklist_templates");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            // One active template per service — enforce via unique index on ServiceDefinitionId.
            b.HasIndex(x => x.ServiceDefinitionId).IsUnique();
            b.HasOne(x => x.ServiceDefinition).WithMany().HasForeignKey(x => x.ServiceDefinitionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ChecklistTemplateItem>(b =>
        {
            b.ToTable("checklist_template_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Text).IsRequired().HasMaxLength(500);
            b.HasIndex(x => new { x.TemplateId, x.Order });
            b.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<EngagementChecklist>(b =>
        {
            b.ToTable("engagement_checklists");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.EngagementId).IsUnique();
            b.HasOne(x => x.Engagement).WithMany().HasForeignKey(x => x.EngagementId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LetterTemplate>(b =>
        {
            b.ToTable("letter_templates");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Body).HasColumnType("text").IsRequired();
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => x.Name).IsUnique();
            b.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<EngagementChecklistItem>(b =>
        {
            b.ToTable("engagement_checklist_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Text).IsRequired().HasMaxLength(500);
            b.Property(x => x.Note).HasMaxLength(2000);
            b.HasIndex(x => new { x.ChecklistId, x.Order });
            b.HasIndex(x => new { x.ChecklistId, x.TemplateItemId }).IsUnique();
            b.HasOne(x => x.Checklist).WithMany().HasForeignKey(x => x.ChecklistId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Staff assignment (historical, append-only + End()) ---
        modelBuilder.Entity<StaffAssignment>(b =>
        {
            b.ToTable("staff_assignments");
            b.HasKey(x => x.Id);
            b.Property(x => x.Scope).HasConversion<int>().IsRequired();
            b.Property(x => x.Role).HasConversion<int>().IsRequired();
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.Version).IsConcurrencyToken();

            b.HasIndex(x => new { x.Scope, x.ScopeId, x.Role });
            b.HasIndex(x => new { x.Scope, x.ScopeId, x.EffectiveTo }); // "currently open" lookups
            b.HasIndex(x => x.StaffId);
            b.HasIndex(x => x.EffectiveFrom);

            b.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- Tax registration ---
        modelBuilder.Entity<TaxRegistration>(b =>
        {
            b.ToTable("tax_registrations");
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).HasConversion<int>().IsRequired();
            b.Property(x => x.Number).IsRequired().HasMaxLength(32);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.BranchId, x.Type, x.Number }).IsUnique();
            b.HasIndex(x => x.Type);

            b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Audit entries (append-only; app code must not UPDATE/DELETE) ---
        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.ToTable("audit_entries");
            b.HasKey(a => a.Id);
            b.Property(a => a.EntityType).HasMaxLength(200).IsRequired();
            b.Property(a => a.EntityId).HasMaxLength(200).IsRequired();
            b.Property(a => a.Action).HasConversion<int>().IsRequired();
            b.Property(a => a.ChangedFields).HasColumnType("text");
            b.Property(a => a.OldValues).HasColumnType("text");
            b.Property(a => a.NewValues).HasColumnType("text");
            b.Property(a => a.UserName).HasMaxLength(256);
            b.Property(a => a.CorrelationId).HasMaxLength(100);
            b.Property(a => a.IpAddress).HasMaxLength(64);
            b.Property(a => a.UserAgent).HasMaxLength(512);
            b.Property(a => a.Context).HasMaxLength(200);
            b.HasIndex(a => new { a.EntityType, a.EntityId });
            b.HasIndex(a => a.OccurredAtUtc);
            b.HasIndex(a => a.UserId);
            b.HasIndex(a => a.CorrelationId);
        });

        // --- Role permissions (composite PK) ---
        modelBuilder.Entity<RolePermission>(b =>
        {
            b.ToTable("role_permissions");
            b.HasKey(rp => new { rp.RoleId, rp.Permission });
            b.Property(rp => rp.Permission).HasMaxLength(100).IsRequired();
            b.HasOne<ApplicationRole>()
                .WithMany()
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(rp => rp.Permission);
        });
    }
}
