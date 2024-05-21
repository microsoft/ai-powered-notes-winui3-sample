using System;

namespace Notes.AI.Embeddings
{
    /// <summary>
    /// A chunk of text from a pdf document. Will also contain the page number and the source file.
    /// </summary>
    public class TextChunk : IVectorObject
    {
        public TextChunk()
        {
            Vectors = Array.Empty<float>();
        }

        public int Id { get; set; }
        public int SourceId { get; set; }
        public string ContentType { get; set; }
        public int ChunkIndexInSource { get; set; }
        public string? Text1 { get; set; }
        public string? Text2 { get; set; }
        public string? Text3 { get; set; }

        public string Text => string.Join(" ", new[] { Text1, Text2, Text3 });
        public float[] Vectors { get; set; }
    }
}
