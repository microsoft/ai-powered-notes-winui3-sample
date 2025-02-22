# AI Powered Notes app [Sample]

This sample is a simple note taking app that uses local APIs and models to provide AI powered features. The app is built using WinUI3.

![image](https://github.com/microsoft/ai-powered-notes-winui3-sample/assets/711864/19839b9a-34fe-4330-94d4-d4e0baf6c94d)

Watch the Build session: [Use AI for "Real Things" in your Windows apps](https://www.youtube.com/watch?v=st7aIx8B4Rk)

## Set Up

You will need to have Visual Studio installed with the latest workloads for WinAppSDK and WinUI 3 development. You can find instructions on how to set up your environment [here.](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment?tabs=cs-vs-community%2Ccpp-vs-community%2Cvs-2022-17-1-a%2Cvs-2022-17-1-b#install-visual-studio)

Clone the repository and open the solution in Visual Studio. Before you can get started exploring the sample, you will need to download the ML model files required for the project and place them in the `onnx-models` folder.

The final folder structure should look like this:

![image](https://github.com/user-attachments/assets/05436579-9bf9-4dc0-a30d-24b1c4006d19)


> [!NOTE]  
> Many of these models can be downloaded quickly using the [AI Dev Gallery](https://github.com/microsoft/ai-dev-gallery).

<p align="center">
<img src="https://github.com/microsoft/ai-dev-gallery/blob/main/docs/images/HeroImage1.png" alt="AI Dev Gallery" width="600"/>
</p>
<p align="center">
<a href="http://aka.ms/ai-dev-gallery-store">
	<img alt="Store badge" src="https://github.com/microsoft/ai-dev-gallery/blob/main/docs/images/storeBadge.png" width="200"/>
</a>
</p>

## Downloading Phi3.5 (or other GenAI model)

The model can be downloaded from the following link:
- [https://huggingface.co/microsoft/Phi-3.5-mini-instruct-onnx](https://huggingface.co/microsoft/Phi-3.5-mini-instruct-onnx)

Use the AI Dev Gallery linked above to download the model files. Alternatively, Huggingface models are in repositories which you can clone to get the model files. Clone the  model repository and copy the required files to this project.

> [!NOTE]  
> You can use any GenAI model by downloading the right model files and droping them in the `genai-model` folder. If using a model other than phi, make sure to also update the prompt template in the `App.xaml.cs`

## Downloading all-MiniLM-L6-v2 
The model can be downloaded from the following link:
- [https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)

This is model we use for semantic search. The two files you will need are `model.onnx` and `vocab.txt`. Create a new folder under `onnx-models` called `embedding` and place the files there. 

## Downloading Sliero VAD
The Sliero Voice Activity Detection model can be downloaded from the following link:
- [https://github.com/snakers4/silero-vad ](https://github.com/snakers4/silero-vad/tree/a9d2b591dea11451d23aa4b480eff8e55dbd9d99/files)

This is the model we use for smart chunking of audio and the only file you will need is the `sliero_vad.onnx` file. 

This should also be placed under a new folder called `whisper` under the `onnx-models` folder. 


## Downloading Whisper
The Whisper model can be downloaded from the following link:
- [https://huggingface.co/khmyznikov/whisper-int8-cpu-ort.onnx](https://huggingface.co/khmyznikov/whisper-int8-cpu-ort.onnx)

Download any of the versions on the repo or from the AI Dev Gallery and place them in the `onnx-models\whisper` folder. Make sure the path in `AI\Whisper\Whisper.cs` in the `InitializeModel` method reflects the same name:
<img width="648" alt="image" src="https://github.com/user-attachments/assets/a152f01b-f7ba-45e7-9798-989d3d973afb" />


## Troubleshooting

### TextRecognition APIs are not available
The TextRecognition APIs are not yet available in the public release of the WindowsAppSDK and their usage is commented out in this sample. We will update the app to use those APIs once they are available publicly.  

### Path name too long
You might run into an issue if you clone the repo in a location that will make the path too long to some of the generated binaries. Recomendation is to place the repo closer to the root of the drive and rename the repo folder name to something shorter. Alternatively, you can change the settings in Windows to support long paths
https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation?tabs=registry#enable-long-paths-in-windows-10-version-1607-and-later .

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
