using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Notes.AI.VoiceRecognition.VoiceActivity
{
    public class SlieroVadOnnxModel : IDisposable
    {
        private readonly InferenceSession session;
        private Tensor<float> h;
        private Tensor<float> c;
        private int lastSr = 0;
        private int lastBatchSize = 0;
        private static readonly List<int> SampleRates = new List<int> { 8000, 16000 };

        public SlieroVadOnnxModel()
        {
            var modelPath = $@"{AppDomain.CurrentDomain.BaseDirectory}onnx-models\whisper\silero_vad.onnx";

            var options = new SessionOptions();
            options.InterOpNumThreads = 1;
            options.IntraOpNumThreads = 1;
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
            session = new InferenceSession(modelPath, options);
            ResetStates();
        }

        public void ResetStates()
        {
            try
            {
                h = new DenseTensor<float>(new[] { 2, 1, 64 });
                c = new DenseTensor<float>(new[] { 2, 1, 64 });
                lastSr = 0;
                lastBatchSize = 0;
            }
            catch (Exception ex)
            {

            }
        }

        public void Close()
        {
            session.Dispose();
        }

        public class ValidationResult
        {
            public readonly float[][] X;
            public readonly int Sr;

            public ValidationResult(float[][] x, int sr)
            {
                X = x;
                Sr = sr;
            }
        }

        private ValidationResult ValidateInput(float[][] x, int sr)
        {
            if (x.Length == 1)
            {
                x = [x[0]];
            }
            if (x.Length > 2)
            {
                throw new ArgumentException($"Incorrect audio data dimension: {x.Length}");
            }

            if (sr != 16000 && sr % 16000 == 0)
            {
                int step = sr / 16000;
                float[][] reducedX = x.Select(row => row.Where((_, i) => i % step == 0).ToArray()).ToArray();
                x = reducedX;
                sr = 16000;
            }

            if (!SampleRates.Contains(sr))
            {
                throw new ArgumentException($"Only supports sample rates {String.Join(", ", SampleRates)} (or multiples of 16000)");
            }

            if ((float)sr / x[0].Length > 31.25)
            {
                throw new ArgumentException("Input audio is too short");
            }

            return new ValidationResult(x, sr);
        }

        public float[] Call(float[][] x, int sr)
        {
           
            var result = ValidateInput(x, sr);
            x = result.X;
            sr = result.Sr;

            int batchSize = x.Length;
            int sampleSize = x[0].Length; // Assuming all subarrays have identical length

            if (lastBatchSize == 0 || lastSr != sr || lastBatchSize != batchSize)
            {
                ResetStates();
            }

            // Flatten the jagged array and create the tensor with the correct shape
            var flatArray = x.SelectMany(inner => inner).ToArray();
            var inputTensor = new DenseTensor<float>(flatArray, [batchSize, sampleSize]);

            // Convert sr to a tensor, if the model expects a scalar as a single-element tensor, ensure matching the expected dimensions
            var srTensor = new DenseTensor<long>(new long[] { sr }, [1]);


            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor),
                NamedOnnxValue.CreateFromTensor("sr", srTensor),
                NamedOnnxValue.CreateFromTensor("h", h),
                NamedOnnxValue.CreateFromTensor("c", c)
            };

            try
            {
                using (var results = session.Run(inputs))
                {
                    var output = results.First().AsEnumerable<float>().ToArray();
                    h = results.ElementAt(1).AsTensor<float>();
                    c = results.ElementAt(2).AsTensor<float>();

                    lastSr = sr;
                    lastBatchSize = batchSize;

                    return output;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An error occurred while calling the model", ex);
            }
        }

        public static int count = 0;

        public void Dispose()
        {
            session?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}