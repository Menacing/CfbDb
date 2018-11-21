using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CfbDb
{
    public class CfbDbContext : DbContext
    {
        public CfbDbContext(DbContextOptions<CfbDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Game>()
                .HasKey(c => new { c.GameDate, c.HomeTeamName, c.VisitingTeamName });

            modelBuilder.Entity<EloRecord>()
                .HasKey(c => new { c.TeamName, c.Date });
        }

        public DbSet<Game> Games { get; set; }

        public DbSet<EloRecord> EloRecords { get; set; }

    }
}
