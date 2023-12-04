using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AI_Studio
{
    internal partial class OptionsProvider
    {
        // Register the options with this attribute on your package class:
        //[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "AI_Studio", "General", 0, 0, true, SupportsProfiles = true)]
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    public class General : BaseOptionModel<General>
    {
        [Category("General")]
        [DisplayName("API URL")]
        [Description("Base url for OpenAI. For OpenAI, should be \"https://api.openai.com/{0}/{1}\"")]
        [DefaultValue("https://api.openai.com/{0}/{1})")]
        public string ApiUrl { get; set; }

        [Category("General")]
        [DisplayName("API Key")]
        [Description("AI Studio utilizes Chat GPT API, to use this extension create an API Key and add it here.")]
        public string ApiKey { get; set; }

        [Category("General")]
        [DisplayName("Infill Begin")]
        [Description("Infill start string, e.g. <|fim_begin|> for deepseek")]
        [DefaultValue("<|fim_begin|>")]
        public string InfillBeginString { get; set; } = "<｜fim▁begin｜>";

        [Category("General")]
        [DisplayName("Infill Hole")]
        [Description("Infill hole string, e.g. <|fim_hole|> for deepseek")]
        [DefaultValue("<|fim_hole|>")]
        public string InfillHoleString { get; set; } = "<｜fim▁hole｜>";

        [Category("General")]
        [DisplayName("Infill End")]
        [Description("Infill start string, e.g. <|fim_end|> for deepseek")]
        [DefaultValue("<|fim_end|>")]
        public string InfillEndString { get; set; } = "<｜fim▁end｜>";

        [Category("General")]
        [DisplayName("Format Changed Text")]
        [Description("Format text after change.")]
        [DefaultValue(true)]
        public bool FormatChangedText { get; set; } = true;

        [Category("General")]
        [DisplayName("Max tokens")]
        [Description("Maximum number of tokens to return from the completion API.")]
        [DefaultValue(200)]
        public int MaxTokens { get; set; } = 200;

        [Category("General")]
        [DisplayName("Context Above")]
        [Description("Maximum number of lines of context prior to the cursor, for infilling.")]
        [DefaultValue(100)]
        public int ContextAbove { get; set; } = 100;

        [Category("General")]
        [DisplayName("Context Below")]
        [Description("Maximum number of lines of context after to the cursor, for infilling.")]
        [DefaultValue(100)]
        public int ContextBelow { get; set; } = 20;

        [Category("General")]
        [DisplayName("Language Model")]
        [Description("Chat language model")]
        [DefaultValue(ChatLanguageModel.ChatGPTTurbo)]
        public ChatLanguageModel LanguageModel { get; set; } = ChatLanguageModel.ChatGPTTurbo;
    }
}
