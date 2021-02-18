using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Storage.ValueConversion;

namespace EFCorePGIssue1716
{
    public class Program
    {
        private static void Main(string[] args)
        {
            ServiceCollection serviceCollection = new();
            serviceCollection.AddDbContext<MyContext>(options =>
            {
                options.LogTo(Console.WriteLine);

                options.UseNpgsql("Server=localhost;Port=5432;Database=test;UserID=postgres;Password=postgres;");

                options.ReplaceService<IValueConverterSelector, TypedIdValueConverterSelector>();
            });

            ServiceProvider buildServiceProvider = serviceCollection.BuildServiceProvider();

            MyContext context = buildServiceProvider.GetRequiredService<MyContext>();
            context.Database.EnsureCreated();

            // Works
            ////context.EntitiesA.Where(x => context.EntitiesB.Contains(x.EntityB)).ToList();

            ////EntityBId[] ids = context.EntitiesB.Select(x => x.Id).ToArray();
            ////context.EntitiesA.Where(x => ids.Contains(x.EntityB.Id)).ToList();

            // Does not work
            List<EntityB> list = context.EntitiesB.ToList();
            context.EntitiesA.Where(x => list.Contains(x.EntityB)).ToList();

            ////EntityB[] list = context.EntitiesB.ToArray();
            ////context.EntitiesA.Where(x => list.Contains(x.EntityB)).ToList();

            ////List<EntityBId> ids = context.EntitiesB.Select(x => x.Id).ToList();
            ////context.EntitiesA.Where(x => ids.Contains(x.EntityB.Id)).ToList();
        }
    }

    public class TypedIdValueConverterSelector : NpgsqlValueConverterSelector
    {
        private readonly ConcurrentDictionary<(Type ModelClrType, Type ProviderClrType), ValueConverterInfo> converters = new();

        public TypedIdValueConverterSelector(ValueConverterSelectorDependencies dependencies) : base(dependencies)
        {
        }

        public override IEnumerable<ValueConverterInfo> Select(Type modelClrType, Type? providerClrType = null)
        {
            foreach (var converter in base.Select(modelClrType, providerClrType)) yield return converter;

            Type underlyingModelType = Nullable.GetUnderlyingType(modelClrType) ?? modelClrType;

            if (underlyingModelType.IsAssignableTo(typeof(EntityAId)))
                yield return converters.GetOrAdd((typeof(EntityAId), typeof(int)), _ =>
                {
                    return new ValueConverterInfo(typeof(EntityAId), typeof(int),
                        valueConverterInfo => new EntityAIdValueConverter(valueConverterInfo.MappingHints));
                });

            if (underlyingModelType.IsAssignableTo(typeof(EntityBId)))
                yield return converters.GetOrAdd((typeof(EntityBId), typeof(int)), _ =>
                {
                    return new ValueConverterInfo(typeof(EntityBId), typeof(int),
                        valueConverterInfo => new EntityBIdValueConverter(valueConverterInfo.MappingHints));
                });
        }
    }

    public class MyContext : DbContext
    {
        public MyContext(DbContextOptions<MyContext> options) : base(options)
        {
        }

        public DbSet<EntityA> EntitiesA { get; set; }

        public DbSet<EntityB> EntitiesB { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Using HasConversion instead of ValueConverters works
            ////modelBuilder.Entity<EntityA>().HasKey(x => x.Id);
            ////modelBuilder.Entity<EntityA>().Property(x => x.Id)
            ////    .HasConversion(id => id.Value, value => new EntityAId {Value = value});

            ////modelBuilder.Entity<EntityB>().HasKey(x => x.Id);
            ////modelBuilder.Entity<EntityB>().Property(x => x.Id)
            ////    .HasConversion(id => id.Value, value => new EntityBId { Value = value });
        }
    }

    public class EntityA
    {
        public EntityAId Id { get; set; }

        public EntityB EntityB { get; set; }
    }

    public class EntityB
    {
        public EntityBId Id { get; set; }
    }

    public class EntityAId
    {
        public int Value { get; set; }
    }

    public class EntityBId
    {
        public int Value { get; set; }
    }

    public class EntityAIdValueConverter : ValueConverter<EntityAId, int>
    {
        public EntityAIdValueConverter(ConverterMappingHints mappingHints = null) :
            base(id => id.Value, value => new EntityAId { Value = value }, mappingHints)
        {
        }
    }

    public class EntityBIdValueConverter : ValueConverter<EntityBId, int>
    {
        public EntityBIdValueConverter(ConverterMappingHints mappingHints = null) :
            base(id => id.Value, value => new EntityBId { Value = value }, mappingHints)
        {
        }
    }
}