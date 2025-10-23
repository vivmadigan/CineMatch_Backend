using Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.Data.Context
{
    public class ApplicationDbContext : IdentityDbContext<UserEntity>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // JSON converter for List<int>
            var intListJson = new ValueConverter<List<int>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<int>>(v, (JsonSerializerOptions?)null) ?? new List<int>());

            // Comparer so EF can track changes to the list
            var intListComparer = new ValueComparer<List<int>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),   // equality
                v => v.Aggregate(0, (acc, i) => HashCode.Combine(acc, i)),// hash
                v => v == null ? new List<int>() : v.ToList());           // snapshot/clone

            builder.Entity<UserPreference>()
                .Property(p => p.GenreIds)
                .HasConversion(intListJson);

            // Attach the comparer (EF needs this for collections with converters)
            builder.Entity<UserPreference>()
                .Property(p => p.GenreIds)
                .Metadata.SetValueComparer(intListComparer);

            // (Optional but clear) enforce the 1:1 FK + cascade
            builder.Entity<UserPreference>()
                .HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<UserPreference>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
