using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using my_node.models;
//using Block = my_node.models.Block;
//using OutPoint = my_node.models.OutPoint;
//using Transaction = my_node.models.Transaction;
//using TxIn = my_node.models.TxIn;
//using TxOut = my_node.models.TxOut;

namespace my_node.storage
{
    public class Context : DbContext
    {
        private static string _dataSourcePath;

        public DbSet<Block> Blocks { get; set; }
        public DbSet<BlockHeader> BlockHeaders { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        //public DbSet<TxIn> TxIns { get; set; }
        //public DbSet<TxOut> TxOuts { get; set; }

        public Context(bool isUnitTest = false)
        {
            _dataSourcePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bitcoin");

            if (isUnitTest)
                _dataSourcePath = Path.Combine(Environment.CurrentDirectory, "bitcoin");

            base.Database.EnsureCreated();

            base.Database.ExecuteSqlCommand("PRAGMA foreign_keys = ON");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dataSource = Path.Combine(_dataSourcePath, "bitcoin.db");
            optionsBuilder.UseSqlite($"Data Source={dataSource};");
            optionsBuilder.EnableDetailedErrors();

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Block>(o =>
            {
                o.HasKey(b => b.Hash);
                o.HasIndex(b => b.Height)
                 .IsUnique();
                o.HasOne(b => b.BlockHeader)
                 .WithOne()
                 .HasForeignKey<BlockHeader>(h => h.BlockHash);
                o.Property(b => b.SystemVersion)
                 .IsConcurrencyToken();
            });

            builder.Entity<BlockHeader>(o =>
            {
                o.HasKey(bh => bh.BlockHash);
                o.Property(bh => bh.SystemVersion)
                 .IsConcurrencyToken();
            });

            builder.Entity<Transaction>(o =>
            {
                o.HasKey(t => t.Hash);
                o.HasOne<Block>(t => t.Block)
                 .WithMany(b => b.Transactions)
                 .HasForeignKey(b => b.BlockHash)
                 .HasPrincipalKey(t => t.Hash);
                o.Property(t => t.SystemVersion)
                 .IsConcurrencyToken();
            });

            //builder.Entity<TxIn>(o =>
            //{
            //    o.HasKey(ti => new { ti.PrevHash, ti.PrevN });
            //    o.HasAlternateKey(ti => ti.TxHash);
            //    o.HasOne<Transaction>(ti => ti.PrevTransaction)
            //     .WithOne()
            //     .HasForeignKey<Transaction>(t => t.Hash)
            //     .HasPrincipalKey<TxIn>(ti => ti.PrevHash);
            //    o.HasOne<Transaction>()
            //     .WithMany()//(ti => ti.In)
            //     .HasForeignKey(ti => ti.TxHash);
            //});

            //builder.Entity<TxOut>(o =>
            //{
            //    o.HasKey(to => to.Id);
            //    o.HasOne<Transaction>()
            //     .WithMany()//(to => to.Out)
            //     .HasForeignKey(to => to.TxHash);
            //});

            base.OnModelCreating(builder);
        }
    }
}
