using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Numerics.Tensors;

namespace Notes.AI.Embeddings
{
    public partial class SemanticIndex
    {
        public static SemanticIndex Instance { get; } = new SemanticIndex();

        public async Task<List<TextChunk>> Search(string searchTerm, int top = 5)
        {
            List<TextChunk> chunks = [];

            var dataContext = await AppDataContext.GetCurrentAsync();
            var storedVectors = dataContext.TextChunks.Select(chunk => chunk).ToList();

            var searchVectors = await Embeddings.Instance.GetEmbeddingsAsync(searchTerm).ConfigureAwait(false);
            var ranking = CalculateRanking(searchVectors.First(), storedVectors.Select(chunk => chunk.Vectors).ToList());

            for (var i = 0; i < ranking.Length && i < top; i++)
            {
                chunks.Add(storedVectors[ranking[i]]);
            }

            return chunks;
        }

        public static int[] CalculateRanking(float[] searchVector, List<float[]> vectors)
        {
            float[] scores = new float[vectors.Count];
            int[] indexranks = new int[vectors.Count];

            for (int i = 0; i < vectors.Count; i++)
            {
                var score = TensorPrimitives.CosineSimilarity(vectors[i], searchVector);
                scores[i] = (float)score;
            }

            var indexedFloats = scores.Select((value, index) => new { Value = value, Index = index })
              .ToArray();

            // Sort the indexed floats by value in descending order
            Array.Sort(indexedFloats, (a, b) => b.Value.CompareTo(a.Value));

            // Extract the top k indices
            indexranks = indexedFloats.Select(item => item.Index).ToArray();

            return indexranks;
        }

        private List<string> SplitParagraphInChunks(string paragraph, int maxLength)
        {
            List<string> textChunks = new();

            var sentences = paragraph.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var currentChunk = string.Empty;

            foreach (var sentence in sentences)
            {
                if (sentence.Length > maxLength)
                {
                    if (currentChunk.Length > 0)
                    {
                        textChunks.Add(currentChunk);
                        currentChunk = string.Empty;
                    }

                    sentence.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList().ForEach(word =>
                    {
                        if (currentChunk.Length + word.Length > maxLength)
                        {
                            textChunks.Add(currentChunk);
                            currentChunk = string.Empty;
                        }

                        currentChunk += word + " ";
                    });

                    continue;
                }

                if (currentChunk.Length + sentence.Length > maxLength)
                {
                    textChunks.Add(currentChunk);

                    currentChunk = string.Empty;
                }

                currentChunk += sentence + ". ";
            }

            if (!string.IsNullOrWhiteSpace(currentChunk))
            {
                textChunks.Add(currentChunk);
            }

            return textChunks;
        }

        private List<TextChunk> SplitIntoOverlappingChunks(string content, int sourceId, string contentType)
        {
            // number of maximum characters in a chunk
            var maxLength = 500;
            var text = content;
            var paragraphs = text.Split(new[] { "\r", "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            List<string> textChunks = new();

            // make sure no paragraphs are longer than maxLength
            // if they are, split them into smaller chunks

            var currentChunk = string.Empty;

            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Length > maxLength)
                {
                    if (currentChunk.Length > 0)
                    {
                        textChunks.Add(currentChunk);
                        currentChunk = string.Empty;
                    }
                    textChunks.AddRange(SplitParagraphInChunks(paragraph, maxLength));
                    continue;
                }

                if (currentChunk.Length + paragraph.Length >= maxLength)
                {
                    textChunks.Add(currentChunk);

                    currentChunk = string.Empty;
                }

                currentChunk += paragraph + "\n";
            }

            if (!string.IsNullOrWhiteSpace(currentChunk))
            {
                textChunks.Add(currentChunk);
            }

            List<TextChunk> chunks = new();

            // 3 at a time, with a sliding window of 1
            if (textChunks.Count <= 2)
            {
                chunks.Add(new TextChunk()
                {
                    SourceId = sourceId,
                    ContentType = contentType,
                    Text1 = textChunks[0],
                    Text2 = textChunks.Count > 1 ? textChunks[1] : null,
                    ChunkIndexInSource = 0
                }); ;
            }
            else
            {
                for (int i = 0; i < textChunks.Count - 2; i++)
                {
                    chunks.Add(new TextChunk()
                    {
                        SourceId = sourceId,
                        ContentType = contentType,
                        Text1 = textChunks[i],
                        Text2 = textChunks[i + 1],
                        Text3 = textChunks[i + 2],
                        ChunkIndexInSource = i
                    });
                }
            }

            return chunks;

        }

        public async Task AddOrReplaceContent(string content, int sourceId, string contentType, EventHandler<float>? progress = null)
        {
            var dataContext = await AppDataContext.GetCurrentAsync();
            dataContext.TextChunks.RemoveRange(dataContext.TextChunks.Where(chunk => chunk.SourceId == sourceId && chunk.ContentType == contentType));

            var stopwatch = Stopwatch.StartNew();

            await Task.Run(async () =>
            {
                List<TextChunk> chunks = SplitIntoOverlappingChunks(content, sourceId, contentType);

                int chunkBatchSize = 32;
                for (int i = 0; i < chunks.Count; i += chunkBatchSize)
                {
                    var chunkBatch = chunks.Skip(i).Take(chunkBatchSize).ToList();
                    var vectors = await Embeddings.Instance.GetEmbeddingsAsync(chunkBatch.Select(c => c.Text).ToArray()).ConfigureAwait(false);
                    
                    for (int j = 0; j < chunkBatch.Count; j++)
                    {
                        chunkBatch[j].Vectors = vectors[j];
                        dataContext.TextChunks.Add(chunkBatch[j]);
                    }

                    progress?.Invoke(this, (float)i / chunks.Count);
                }

                dataContext.SaveChanges();

            }).ConfigureAwait(false);

            stopwatch.Stop();
            Debug.WriteLine($"Indexing took {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}
