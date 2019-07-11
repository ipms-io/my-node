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
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<TxIn> TxIns { get; set; }
        public DbSet<TxOut> TxOuts { get; set; }

        public Context()
        {
            _dataSourcePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bitcoin");
            base.Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dataSource = Path.Combine(_dataSourcePath, "bitcoin.db");
            optionsBuilder.UseSqlite($"Data Source={dataSource}");

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Block>(o =>
            {
                o.HasKey(b => b.Hash);
                o.HasIndex(b => b.Height)
                 .IsUnique();
                o.HasMany(b => b.Transactions)
                 .WithOne()
                 .HasForeignKey(t => t.BlockHash);
                o.HasOne(b => b.BlockHeader)
                 .WithOne()
                 .HasForeignKey<BlockHeader>(h => h.BlockHash);
            });

            builder.Entity<BlockHeader>(o =>
            {
                o.HasKey(bh => bh.BlockHash);
            });

            builder.Entity<Transaction>(o =>
            {
                o.HasKey(t => t.Hash);
                o.HasMany(t => t.In)
                 .WithOne()
                 .HasForeignKey(ti => ti.TxHash);
                o.HasMany(t => t.Out)
                 .WithOne()
                 .HasForeignKey(to => to.TxHash);
            });

            builder.Entity<TxIn>(o =>
            {
                o.HasKey(ti => new { ti.PrevHash, ti.PrevN });
                o.HasAlternateKey(ti => ti.TxHash);
                o.HasOne(ti => ti.PrevTransaction)
                 .WithOne()
                 .HasForeignKey<Transaction>(t => t.Hash)
                 .HasPrincipalKey<TxIn>(ti => ti.PrevHash);
            });

            builder.Entity<TxOut>(o =>
            {
                o.HasKey(to => to.Id);
            });

            base.OnModelCreating(builder);
        }
    }
}
