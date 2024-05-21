using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Notes.AI.VoiceRecognition.VoiceActivity
{
    public class DetectionResult
    {
        public string Type { get; set; }
        public double Seconds { get; set; }
    }

    public class WhisperChunk
    {
        public double Start { get; set; }
        public double End { get; set; }

        public WhisperChunk(double start, double end)
        {
            this.Start = start;
            this.End = end;
        }

        public double Length => End - Start;

    }
    public static class WhisperChunking
    {
        private static int SAMPLE_RATE = 16000;
        private static float START_THRESHOLD = 0.25f;
        private static float END_THRESHOLD = 0.25f;
        private static int MIN_SILENCE_DURATION_MS = 1000;
        private static int SPEECH_PAD_MS = 400;
        private static int WINDOW_SIZE_SAMPLES = 3200;

        private static double MAX_CHUNK_S = 29;
        private static double MIN_CHUNK_S = 5;

        public static List<WhisperChunk> SmartChunking(byte[] audioBytes)
        {
            SlieroVadDetector vadDetector;
            vadDetector = new SlieroVadDetector(START_THRESHOLD, END_THRESHOLD, SAMPLE_RATE, MIN_SILENCE_DURATION_MS, SPEECH_PAD_MS);

            int bytesPerSample = 2;
            int bytesPerWindow = WINDOW_SIZE_SAMPLES * bytesPerSample;

            float totalSeconds = audioBytes.Length / (SAMPLE_RATE * 2);
            var result = new List<DetectionResult>();
            var sw = Stopwatch.StartNew();
            for (int offset = 0; offset + bytesPerWindow <= audioBytes.Length; offset += bytesPerWindow)
            {
                byte[] data = new byte[bytesPerWindow];
                Array.Copy(audioBytes, offset, data, 0, bytesPerWindow);

                // Simulating the process as if data was being read in chunks
                try
                {
                    var detectResult = vadDetector.Apply(data, true);
                    // iterate over detectResult and apply the data to result:
                    foreach (var (key, value) in detectResult)
                    {
                        result.Add(new DetectionResult { Type = key, Seconds = value });
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"Error applying VAD detector: {e.Message}");
                    // Depending on the need, you might want to break out of the loop or just report the error
                }
            }
            sw.Stop();
            Debug.WriteLine($"VAD detection took {sw.ElapsedMilliseconds} ms");
            var stamps = GetTimeStamps(result, totalSeconds, MAX_CHUNK_S, MIN_CHUNK_S);
            return stamps;
        }
        private static List<WhisperChunk> GetTimeStamps(List<DetectionResult> voiceAreas, double totalSeconds, double maxChunkLength, double minChunkLength)
        {
            //const int maxLength = 30;
            //List<Chunk> chunks = new();
            //int currChunk = 1;
            //double startTime = 0;
            //for(int i=1;i<voiceAreas.Count - 1;i+=2)
            //{
            //    if (voiceAreas[i].Seconds > startTime + maxLength && chunks.Count < currChunk)
            //    {
            //        chunks.Add(new Chunk(startTime, currChunk * maxLength));
            //        currChunk++;
            //        startTime = currChunk * maxLength;
            //    }
            //    //TODO: This is a very basic check, we can check for a threshold of values instead, Amrutha will work on that
            //    if (voiceAreas[i].Seconds <= startTime + maxLength && (i == voiceAreas.Count - 1 || voiceAreas[i + 1].Seconds > startTime + maxLength)) {
            //        chunks.Add(new Chunk(startTime, voiceAreas[i].Seconds));
            //        currChunk++;
            //        startTime = voiceAreas[i].Seconds;
            //    }   
            //}

            //double j;
            ////Sometimes the last chunk is really large
            //for(j=startTime; j<totalSeconds;j+= maxLength)
            //{
            //    chunks.Add(new Chunk(j, Math.Min(j + maxLength, totalSeconds)));
            //}
            //return chunks;

            if (totalSeconds <= maxChunkLength)
            {
                return new List<WhisperChunk> { new WhisperChunk(0, totalSeconds) };
            }

            voiceAreas = voiceAreas.OrderBy(va => va.Seconds).ToList();

            List<WhisperChunk> chunks = new List<WhisperChunk>();

            double nextChunkStart = 0.0;
            while (nextChunkStart < totalSeconds)
            {
                double idealChunkEnd = nextChunkStart + maxChunkLength;
                double chunkEnd = idealChunkEnd > totalSeconds ? totalSeconds : idealChunkEnd;

                var validVoiceAreas = voiceAreas.Where(va => va.Seconds > nextChunkStart && va.Seconds <= chunkEnd).ToList();

                if (validVoiceAreas.Any())
                {
                    chunkEnd = validVoiceAreas.Last().Seconds;
                }

                chunks.Add(new WhisperChunk(nextChunkStart, chunkEnd));
                nextChunkStart = chunkEnd + 0.1;
            }

            return MergeSmallChunks(chunks, maxChunkLength, minChunkLength);
        }

        private static List<WhisperChunk> MergeSmallChunks(List<WhisperChunk> chunks, double maxChunkLength, double minChunkLength)
        {
            for (int i = 1; i < chunks.Count; i++)
            {
                // Check if current chunk is small and can be merged with previous
                if (chunks[i].Length < minChunkLength)
                {
                    double prevChunkLength = chunks[i - 1].Length;
                    double combinedLength = prevChunkLength + chunks[i].Length;

                    if (combinedLength <= maxChunkLength)
                    {
                        chunks[i - 1].End = chunks[i].End; // Merge with previous chunk
                        chunks.RemoveAt(i); // Remove current chunk
                        i--; // Adjust index to recheck current position now pointing to next chunk
                    }
                }
            }

            return chunks;
        }
    }
}
