using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using OpenAI_API.Completions;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TextManager.Interop;

namespace AI_Studio
{
    internal class AIBaseCommand<T> : BaseCommand<T> where T : class, new()
    {
        public string SystemMessage { get; set; }
        public string UserInput { get; set; }
        public List<string> AssistantInputs  { get; set; } = new List<string>();
        public ResponseBehavior ResponseBehavior { get; set; }

        protected bool _addContentTypePrefix = false;
        protected bool _stripResponseMarkdownCode = false;
        protected bool _useCompletion = false;

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var generalOptions = await General.GetLiveInstanceAsync();

            if (string.IsNullOrEmpty(generalOptions.ApiKey))
            {
                await VS.MessageBox.ShowAsync("API Key is missing, go to Tools/Options/AI Stuido/General and add the API Key created from https://platform.openai.com/account/api-keys",
                    buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);

                Package.ShowOptionPage(typeof(General));
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var fac = (IVsThreadedWaitDialogFactory)await VS.Services.GetThreadedWaitDialogAsync();
            IVsThreadedWaitDialog4 twd = fac.CreateInstance();

            twd.StartWaitDialog("AI Studio", "Working on it...", "", null, "", 1, false, true);

            var docView = await VS.Documents.GetActiveDocumentViewAsync();
            var selection = docView.TextView.Selection.SelectedSpans.FirstOrDefault();
            var text = "";
            if (selection.Length == 0)
            {
                if (_useCompletion)
                {
                    text = "<｜fim▁begin｜>";
                    var textBuffer = docView.TextView.TextBuffer;
                    int line = textBuffer.CurrentSnapshot.GetLineNumberFromPosition(selection.Start.Position);
                    int context_above_lines = 100;
                    int context_below_lines = 20;
                    for (int i = Math.Max(0,line-context_above_lines); i < line; i++)
                    {
                        var lineContents = textBuffer.CurrentSnapshot.GetLineFromLineNumber(i);
                        text += lineContents.GetText() + "\n";
                    }
                    text += textBuffer.CurrentSnapshot.GetLineFromLineNumber(line).GetText();
                    text += "<｜fim▁hole｜>";
                    for (int i = line; i < Math.Min(line+context_below_lines, textBuffer.CurrentSnapshot.LineCount); i++)
                    {
                        var lineContents = textBuffer.CurrentSnapshot.GetLineFromLineNumber(i);
                        text += lineContents.GetText() + "\n";
                    }
                    text += "<｜fim▁end｜>";
                }
                else
                {
                    var textBuffer = docView.TextView.TextBuffer;
                    var line = textBuffer.CurrentSnapshot.GetLineFromPosition(selection.Start.Position);
                    var snapshotSpan = new SnapshotSpan(line.Start, line.End);
                    docView.TextView.Selection.Select(snapshotSpan, false);
                    selection = docView.TextView.Selection.SelectedSpans.FirstOrDefault();
                    text = docView.TextView.Selection.StreamSelectionSpan.GetText();
                }
            }
            else
            {
                text = docView.TextView.Selection.StreamSelectionSpan.GetText();
            }
            int selectionStartLineNumber = docView.TextView.TextBuffer.CurrentSnapshot.GetLineNumberFromPosition(selection.Start.Position);

            if (string.IsNullOrEmpty(text))
            {
                twd.EndWaitDialog();
                await VS.MessageBox.ShowAsync("Nothing Selected!", buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }

            if (_addContentTypePrefix)
            {
                text = $"{docView.TextView.TextDataModel.ContentType.DisplayName}\n{text}";
            }

            var api = new OpenAIAPI(generalOptions.ApiKey);
            api.ApiUrlFormat = generalOptions.ApiUrl;
            var chatRequestTemplate = new ChatRequest()
            {
                Model = generalOptions.LanguageModel switch
                {
                    ChatLanguageModel.GPT4 => Model.GPT4,
                    ChatLanguageModel.GPT4_32k_Context => Model.GPT4_32k_Context,
                    _ => Model.ChatGPTTurbo
                }
            };
            var chat = api.Chat.CreateConversation(chatRequestTemplate);

            if (!string.IsNullOrEmpty(SystemMessage))
            {
                chat.AppendSystemMessage(SystemMessage);
            }
            chat.AppendUserInput(text);
            if (!string.IsNullOrEmpty(UserInput))
            {
                chat.AppendUserInput(UserInput);
            }
            foreach (var input in AssistantInputs)
            {
                chat.AppendExampleChatbotOutput(input);
            }

            var completion = new CompletionRequest();
            completion.Prompt = text;

            string response = "";
            int oldSelectionStart = selection.Start.Position;

            try
            {
                if (_useCompletion)
                {
                    response = await api.Completions.CreateAndFormatCompletion(completion);
                }
                else
                {
                    response = await chat.GetResponseFromChatbotAsync();
                }
                
                if (_stripResponseMarkdownCode)
                {
                    response = StripResponseMarkdownCode(response);
                }

                twd.EndWaitDialog();

                if (_useCompletion)
                {
                    if (response.StartsWith(text))
                    {
                        // Delete the beginning of the response, since it's redundant for the
                        // completion/infilling models I'm using (deepseek)
                        response = response.Substring(text.Length);
                    }
                }

                switch (ResponseBehavior)
                {
                    case ResponseBehavior.Insert:
                        docView.TextBuffer.Insert(selection.End, _useCompletion ? response : (Environment.NewLine + response));
                        break;
                    case ResponseBehavior.Replace:
                        docView.TextBuffer.Replace(selection, response);
                        break;
                    case ResponseBehavior.Message:
                        await VS.MessageBox.ShowAsync(response, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
                        break;
                }
            }
            catch (Exception ex)
            {
                twd.EndWaitDialog();
                await VS.MessageBox.ShowAsync(ex.Message, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }

            if (generalOptions.FormatChangedText && ResponseBehavior != ResponseBehavior.Message)
            {
                selection = docView.TextView.Selection.SelectedSpans.FirstOrDefault();
                if (selection.Length == 0)
                {
                    if (_useCompletion)
                    {
                        var snapshotSpan = new SnapshotSpan(docView.TextView.TextBuffer.CurrentSnapshot, oldSelectionStart, response.Length);
                        docView.TextView.Selection.Select(snapshotSpan, false);
                    }
                    else
                    {
                        var startLine = docView.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(selectionStartLineNumber);
                        var endLine = docView.TextView.TextBuffer.CurrentSnapshot.GetLineFromPosition(selection.End);
                        var snapshotSpan = new SnapshotSpan(startLine.Start, endLine.End);
                        docView.TextView.Selection.Select(snapshotSpan, false);
                    }
                }

                (await VS.GetServiceAsync<DTE, DTE>()).ExecuteCommand("Edit.FormatSelection");
            }
        }

        private string StripResponseMarkdownCode(string response)
        {
            var regex = new Regex(@"```.*\r?\n?");
            return regex.Replace(response, "");
        }
    }
}
