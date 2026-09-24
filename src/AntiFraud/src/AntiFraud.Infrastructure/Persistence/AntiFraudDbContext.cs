using AntiFraud.Domain.Repositories;
using AntiFraud.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AntiFraud.Infrastructure.Persistence;

public sealed class AntiFraudDbContext(DbContextOptions<AntiFraudDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
    public DbSet<FraudEvaluationEntity> FraudEvaluations => Set<FraudEvaluationEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tabelas/índices: docker/pgadmin/scripts/ddl.sql (sem EF migrations).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AntiFraudDbContext).Assembly);
    }
}
