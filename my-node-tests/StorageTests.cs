using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using my_node.models;
using my_node.storage;
using Xunit;

namespace my_node_tests
{
    public class StorageTests
    {
        private Context _context;

        public StorageTests()
        {
            _context = new Context(true);
        }

        [Fact]
        public async Task ShouldCreateData()
        {
            var block = await _context.Blocks.Include(b => b.Transactions).FirstOrDefaultAsync(b => b.Hash.Equals("b1"));
            if (block == null)
            {
                block = new Block
                {
                    Hash = "b1",
                    Height = 1
                };

                await _context.Blocks.AddAsync(block);
                await _context.SaveChangesAsync();

                block.Transactions = new List<Transaction>();
            }


            var tx = new Transaction
            {
                Hash = "t1"
            };

            //await _context.Transactions.AddAsync(tx);

            block.Transactions.Add(tx);

            _context.Blocks.Update(block);

            await _context.SaveChangesAsync();
        }
    }
}
