using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;
using Notes.AI.Embeddings;
using Notes.Models;

namespace Notes
{
    public class AppDataContext : DbContext
    {
        public DbSet<TextChunk> TextChunks { get; set; }

        public DbSet<Note> Notes { get; set; }

        public DbSet<Attachment> Attachments { get; set; }

        private string _dbPath { get; set; }
        private static AppDataContext _current;
        public static async Task<AppDataContext> GetCurrentAsync()
        {
            if (_current == null)
            {
                _current = new AppDataContext($@"{(await Utils.GetStateFolderAsync()).Path}\state.db");
                await _current.Database.EnsureCreatedAsync();
            }

            return _current;
        }

        public static async Task SaveCurrentAsync()
        {
            var context = await GetCurrentAsync();
            await context.SaveChangesAsync();
        }

        private AppDataContext(string dbPath)
        {
            _dbPath = dbPath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite($"Data Source={_dbPath}");
    }
}
