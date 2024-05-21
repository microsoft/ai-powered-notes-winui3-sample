using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Notes.AI.Embeddings;
using Windows.Storage;

namespace Notes
{
    internal partial class Utils
    {
        public static readonly string FolderName = "MyNotes";
        public static readonly string FileExtension = ".txt";
        public static readonly string StateFolderName = ".notes";
        public static readonly string AttachmentsFolderName = "attachments";

        private static string localFolderPath = string.Empty;

        public static async Task<string> GetLocalFolderPathAsync()
        {
            if (string.IsNullOrWhiteSpace(localFolderPath))
            {
                localFolderPath = (await GetLocalFolderAsync()).Path;
            }

            return localFolderPath;
        }

        public static async Task<StorageFolder> GetLocalFolderAsync()
        {
            return await KnownFolders.DocumentsLibrary.CreateFolderAsync(FolderName, CreationCollisionOption.OpenIfExists);
        }

        public static async Task<StorageFolder> GetStateFolderAsync()
        {
            var notesFolder = await GetLocalFolderAsync();
            return await notesFolder.CreateFolderAsync(StateFolderName, CreationCollisionOption.OpenIfExists);
        }

        public static async Task<StorageFolder> GetAttachmentsFolderAsync()
        {
            var notesFolder = await GetLocalFolderAsync();
            return await notesFolder.CreateFolderAsync(AttachmentsFolderName, CreationCollisionOption.OpenIfExists);
        }

        public static async Task<StorageFolder> GetAttachmentsTranscriptsFolderAsync()
        {
            var notesFolder = await GetStateFolderAsync();
            return await notesFolder.CreateFolderAsync(AttachmentsFolderName, CreationCollisionOption.OpenIfExists);
        }

        public static async Task<List<SearchResult>> SearchAsync(string query, int top = 5)
        {
            var results = new List<SearchResult>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            // TODO: handle cancelation
            var searchVectors = await SemanticIndex.Instance.Search(query, top);
            var context = await AppDataContext.GetCurrentAsync();

            while (searchVectors.Count > 0)
            {
                var searchVector = searchVectors[0];

                var sameContent = searchVectors
                    .Where(r => r.ContentType == searchVector.ContentType && r.SourceId == searchVector.SourceId)
                    .OrderBy(r => r.ChunkIndexInSource)
                    .ToList();

                var content = new StringBuilder();

                int previousSourceIndex = sameContent.First().ChunkIndexInSource;
                content.Append(sameContent.First().Text);
                searchVectors.Remove(sameContent.First());
                sameContent.RemoveAt(0);

                while (sameContent.Count > 0)
                {
                    var currentContent = sameContent.First();

                    if (currentContent.ChunkIndexInSource == previousSourceIndex + 1)
                    {
                        content.Append(currentContent.Text3 ?? "");
                    }
                    else if (currentContent.ChunkIndexInSource == previousSourceIndex + 2)
                    {
                        content.Append(currentContent.Text2 ?? "");
                        content.Append(currentContent.Text3 ?? "");
                    }
                    else
                    {
                        content.Append(currentContent.Text);
                    }

                    previousSourceIndex = currentContent.ChunkIndexInSource;
                    searchVectors.Remove(currentContent);
                    sameContent.RemoveAt(0);
                }

                var searchResult = new SearchResult();
                searchResult.Content = content.ToString();

                if (searchVector.ContentType == "note")
                {
                    var note = await context.Notes.FindAsync(searchVector.SourceId);

                    searchResult.ContentType = ContentType.Note;
                    searchResult.SourceId = note.Id;
                    searchResult.Title = note.Title;
                }
                else if (searchVector.ContentType == "attachment")
                {
                    var attachment = await context.Attachments.FindAsync(searchVector.SourceId);

                    searchResult.ContentType = (ContentType)attachment.Type;
                    searchResult.SourceId = attachment.Id;
                    searchResult.Title = attachment.Filename;
                }

                var topSentence = await SubSearchAsync(query, searchResult.Content);
                searchResult.MostRelevantSentence = topSentence;
                results.Add(searchResult);

            }

            return results;
        }

        public static async Task<string> SubSearchAsync(string query, string text)
        {
            var sentences = text.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var vectors = await Embeddings.Instance.GetEmbeddingsAsync(sentences);
            var searchVector = await Embeddings.Instance.GetEmbeddingsAsync(new string[] { query });

            var ranking = SemanticIndex.CalculateRanking(searchVector[0], vectors.ToList());

            return sentences[ranking[0]];
        }
    }
    public record SearchResult
    {
        public string Title { get; set; }
        public string? Content { get; set; }
        public string? MostRelevantSentence { get; set; }
        public int SourceId { get; set; }
        public ContentType ContentType { get; set; }

        public static string ContentTypeToGlyph(ContentType type)
        {
            return type switch
            {
                ContentType.Note => "📝",
                ContentType.Image => "🖼️",
                ContentType.Audio => "🎙️",
                ContentType.Video => "🎞️",
                ContentType.Document => "📄"
            };
        }
    }

    public enum ContentType
    {
        Image = 0,
        Audio = 1,
        Video = 2,
        Document = 3,
        Note = 4,
    }
}
