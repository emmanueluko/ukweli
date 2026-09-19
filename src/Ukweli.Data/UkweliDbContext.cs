using Microsoft.EntityFrameworkCore;
using Ukweli.Contracts;
using Ukweli.Data.Entities;

namespace Ukweli.Data;

/// <summary>The application's EF Core context.</summary>
public class UkweliDbContext(DbContextOptions<UkweliDbContext> options) : DbContext(options)
{
    /// <summary>The curated evidence store.</summary>
    public DbSet<Source> Sources => Set<Source>();

    /// <summary>Stored results. Holds the normalised claim only, never raw input.</summary>
    public DbSet<ClaimAnalysis> ClaimAnalyses => Set<ClaimAnalysis>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Source>(source =>
        {
            source.ToTable("sources");
            source.HasKey(s => s.Id);

            source.Property(s => s.Id).HasMaxLength(128);
            source.Property(s => s.Issuer).HasMaxLength(256).IsRequired();
            source.Property(s => s.Title).HasMaxLength(512).IsRequired();
            source.Property(s => s.Url).HasMaxLength(2048).IsRequired();
            source.Property(s => s.CollectionUrl).HasMaxLength(2048);
            source.Property(s => s.Excerpt).IsRequired();

            // Stored as their wire spelling ("primary_official", "NG-LA"), the same
            // strings the API and the curated JSON use. The table stays readable
            // in psql, and reordering an enum cannot silently change what a row
            // means — which storing the ordinal would.
            source.Property(s => s.SourceType)
                .HasConversion(EnumConversions.For<SourceType>()).HasMaxLength(32).IsRequired();
            source.Property(s => s.Jurisdiction)
                .HasConversion(EnumConversions.For<Jurisdiction>()).HasMaxLength(16).IsRequired();
            source.Property(s => s.Topic)
                .HasConversion(EnumConversions.For<Topic>()).HasMaxLength(32).IsRequired();

            source.Property(s => s.Placeholder).HasDefaultValue(false);
            source.Property(s => s.Active).HasDefaultValue(true);

            // Retrieval filters on exactly these three columns.
            source.HasIndex(s => new { s.Active, s.Jurisdiction, s.Topic });
        });

        modelBuilder.Entity<ClaimAnalysis>(analysis =>
        {
            analysis.ToTable("claim_analyses");
            analysis.HasKey(a => a.Id);

            analysis.Property(a => a.Id).HasMaxLength(16);
            analysis.Property(a => a.UserId).HasMaxLength(128);
            analysis.Property(a => a.NormalizedClaim).IsRequired();
            analysis.Property(a => a.Explanation).IsRequired();
            analysis.Property(a => a.SimpleExplanation).IsRequired();
            analysis.Property(a => a.Action).IsRequired();
            analysis.Property(a => a.SeedId).HasMaxLength(128);
            analysis.Property(a => a.ModelVersion).HasMaxLength(128).IsRequired();

            analysis.Property(a => a.Status)
                .HasConversion(EnumConversions.For<VerdictStatus>()).HasMaxLength(32).IsRequired();

            // Nullable: a claim that names no place has no jurisdiction, and
            // guessing one would be inventing scope the claim never had.
            analysis.Property(a => a.Jurisdiction)
                .HasConversion(EnumConversions.NullableFor<Jurisdiction>()).HasMaxLength(16);

            // jsonb, as the specification's data model says — not text. The
            // difference matters: Postgres validates the shape on write, and
            // these columns stay queryable.
            analysis.Property(a => a.Unknowns)
                .HasConversion(JsonConversions.ForList<string>())
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(JsonConversions.ListComparer<string>());

            analysis.Property(a => a.SourceIds)
                .HasConversion(JsonConversions.ForList<string>())
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(JsonConversions.ListComparer<string>());

            analysis.Property(a => a.SourceRelations)
                .HasConversion(JsonConversions.ForDictionary<SourceRelation>())
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(JsonConversions.DictionaryComparer<SourceRelation>());

            analysis.Property(a => a.IsSeeded).HasDefaultValue(false);

            // "My past checks", newest first (Phase 4).
            analysis.HasIndex(a => new { a.UserId, a.CreatedAt });
        });

        SnakeCaseNaming.Apply(modelBuilder);
    }

    /// <summary>
    /// A real round trip to Postgres, used by the health endpoint. Returns
    /// false rather than throwing, so a health check can report a degraded
    /// database without the endpoint itself failing.
    /// </summary>
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }
}
