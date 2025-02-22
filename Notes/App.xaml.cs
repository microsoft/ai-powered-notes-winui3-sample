using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Notes.AI;
using System;
using System.IO;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Notes
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static IChatClient? ChatClient { get; private set; }
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();

            _ = InitializeIChatClient();
        }

        private async Task InitializeIChatClient()
        {
            // use PhiSilica
            // ChatClient = await PhiSilicaClient.CreateAsync();

            // use genai model
            ChatClient = await GenAIModel.CreateAsync(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "onnx-models", "genai-model"), 
                new LlmPromptTemplate
                {
                    System = "<|system|>\n{{CONTENT}}<|end|>\n",
                    User = "<|user|>\n{{CONTENT}}<|end|>\n",
                    Assistant = "<|assistant|>\n{{CONTENT}}<|end|>\n",
                    Stop = ["<|system|>", "<|user|>", "<|assistant|>", "<|end|>"]
                });
        }

        private Window m_window;
        public Window Window => m_window;

    }
}
