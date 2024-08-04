using System;
using Microsoft.ML.OnnxRuntimeGenAI;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Notes.AI.Phi
{
    public partial class Phi3 : IDisposable
    {
        // model from https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx
        private readonly string ModelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "onnx-models", "phi3");

        private Model? model = null;
        public event EventHandler? ModelLoaded = null;

        [MemberNotNullWhen(true, nameof(model))]
        public bool IsReady => model != null;

        public static Phi3 Instance { get; } = new Phi3();

        public void Dispose()
        {
            model?.Dispose();
        }

        public IAsyncEnumerable<string> InferStreaming(string systemPrompt, string userPrompt, [EnumeratorCancellation] CancellationToken ct = default)
        {
            var prompt = $@"<|system|>{systemPrompt}<|end|><|user|>{userPrompt}<|end|><|assistant|>";
            return InferStreaming(prompt, ct);
        }

        public Task InitializeAsync()
        {
            return Task.Run(() =>
            {
                model = new Model(ModelDir);
                ModelLoaded?.Invoke(this, EventArgs.Empty);
            });
        }

        public async IAsyncEnumerable<string> InferStreaming(string prompt, [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (!IsReady)
            {
                throw new InvalidOperationException("Model is not ready");
            }

            using var generatorParams = new GeneratorParams(model);
            using var tokenizer = new Tokenizer(model);
            using var sequences = tokenizer.Encode(prompt);
            using var tokenizerStream = tokenizer.CreateStream();

            var maxLength = Math.Max(1024, sequences[0].Length + 300);
            Debug.WriteLine($"Max length: {maxLength}");

            generatorParams.SetInputSequences(sequences);
            generatorParams.TryGraphCaptureWithMaxBatchSize(1);
            generatorParams.SetSearchOption("max_length", maxLength);

            using var generator = new Generator(model, generatorParams);

            StringBuilder stringBuilder = new();
            bool firstPartial = true;
            while (!generator.IsDone())
            {
                string part;
                try
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    await Task.Delay(0, ct).ConfigureAwait(false);
                    generator.ComputeLogits();
                    generator.GenerateNextToken();
                    part = tokenizerStream.Decode(generator.GetSequence(0)[^1]);

                    if (firstPartial)
                    {
                        part = part.TrimStart();
                        firstPartial = false;
                    }

                    stringBuilder.Append(part);
                    if (stringBuilder.ToString().Contains("<|end|>")
                        || stringBuilder.ToString().Contains("<|user|>")
                        || stringBuilder.ToString().Contains("<|system|>"))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    break;
                }

                yield return part;
            }
        }

    }
}