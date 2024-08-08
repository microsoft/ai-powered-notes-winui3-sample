using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Notes.AI.VoiceRecognition.VoiceActivity;
using System.Diagnostics;

namespace Notes.AI.VoiceRecognition
{
    public static class Whisper
    {
        private static InferenceSession? _inferenceSession;
        private static InferenceSession InitializeModel()
        {
            // model generated from https://github.com/microsoft/Olive/blob/main/examples/whisper/README.md
            // var modelPath = $@"{AppDomain.CurrentDomain.BaseDirectory}onnx-models\whisper\whisper_tiny.onnx";
            var modelPath = $@"{AppDomain.CurrentDomain.BaseDirectory}onnx-models\whisper\whisper_small.onnx";

            SessionOptions options = new SessionOptions();
            options.RegisterOrtExtensions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            options.EnableMemoryPattern = false;

            var session = new InferenceSession(modelPath, options);

            return session;
        }

        private static async Task<List<WhisperTranscribedChunk>> TranscribeChunkAsync(float[] pcmAudioData, string inputLanguage, WhisperTaskType taskType, int offsetSeconds = 30)
        {
            if (_inferenceSession == null)
            {
                _inferenceSession = InitializeModel();
            }

            var audioTensor = new DenseTensor<float>(pcmAudioData, [1, pcmAudioData.Length]);
            var timestampsEnableTensor = new DenseTensor<int>(new[] { 1 }, [1]);

            int task = (int)taskType;
            int langCode = WhisperUtils.GetLangId(inputLanguage);
            var decoderInputIds = new int[] { 50258, langCode, task };
            var langAndModeTensor = new DenseTensor<int>(decoderInputIds, [1, 3]);

            var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("audio_pcm", audioTensor),
                NamedOnnxValue.CreateFromTensor("min_length", new DenseTensor<int>(new int[] { 0 }, [1])),
                NamedOnnxValue.CreateFromTensor("max_length", new DenseTensor<int>(new int[] { 448 }, [1])),
                NamedOnnxValue.CreateFromTensor("num_beams", new DenseTensor<int>(new int[] {1}, [1])),
                NamedOnnxValue.CreateFromTensor("num_return_sequences", new DenseTensor<int>(new int[] { 1 }, [1])),
                NamedOnnxValue.CreateFromTensor("length_penalty", new DenseTensor<float>(new float[] { 1.0f }, [1])),
                NamedOnnxValue.CreateFromTensor("repetition_penalty", new DenseTensor<float>(new float[] { 1.2f }, [1])),
                NamedOnnxValue.CreateFromTensor("logits_processor", timestampsEnableTensor),
                NamedOnnxValue.CreateFromTensor("decoder_input_ids", langAndModeTensor)
            };

            try
            {
                using var results = _inferenceSession.Run(inputs);
                string result = results[0].AsTensor<string>().GetValue(0);
                return WhisperUtils.ProcessTranscriptionWithTimestamps(result, offsetSeconds);
            }
            catch (Exception ex)
            {
                // return empty list in case of exception
                return new List<WhisperTranscribedChunk>();
            }
        }

        public async static Task<List<WhisperTranscribedChunk>> TranscribeAsync(StorageFile audioFile, EventHandler<float>? progress = null)
        {
            var transcribedChunks = new List<WhisperTranscribedChunk>();

            var sw = Stopwatch.StartNew();

            var audioBytes = Utils.LoadAudioBytes(audioFile.Path);

            sw.Stop();
            Debug.WriteLine($"Loading took {sw.ElapsedMilliseconds} ms");
            sw.Start();

            var dynamicChunks = WhisperChunking.SmartChunking(audioBytes);

            sw.Stop();
            Debug.WriteLine($"Chunking took {sw.ElapsedMilliseconds} ms");

            for (var i = 0; i < dynamicChunks.Count; i++)
            {
                var chunk = dynamicChunks[i];

                var audioSegment = Utils.ExtractAudioSegment(audioFile.Path, chunk.Start, chunk.End - chunk.Start);

                var transcription = await TranscribeChunkAsync(audioSegment, "en", WhisperTaskType.Transcribe, (int)chunk.Start);

                transcribedChunks.AddRange(transcription);

                progress?.Invoke(null, (float)i / dynamicChunks.Count);
            }

            return transcribedChunks;
        }
    }

    internal enum WhisperTaskType
    {
        Translate = 50358,
        Transcribe = 50359
    }

    public class WhisperTranscribedChunk
    {
        public string Text { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public double Length => End - Start;

    }
}
