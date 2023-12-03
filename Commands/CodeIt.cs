using System.Text.RegularExpressions;

namespace AI_Studio
{
    [Command(PackageIds.CodeIt)]
    internal sealed class CodeIt : AIBaseCommand<CodeIt>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            SystemMessage = "";
            ResponseBehavior = ResponseBehavior.Insert;

            var opts = await Commands.GetLiveInstanceAsync();

            UserInput = "";
            _addContentTypePrefix = false;
            _stripResponseMarkdownCode = false;
            _useCompletion = true;

            await base.ExecuteAsync(e);
        }
    }
}
