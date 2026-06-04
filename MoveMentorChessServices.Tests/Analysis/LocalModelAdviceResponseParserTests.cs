using MoveMentorChess.Analysis;
using Xunit;

namespace MoveMentorChessServices.Tests.Analysis;

public sealed class LocalModelAdviceResponseParserTests
{
    [Fact]
    public void ParsesStructuredFieldsWithMultilineDetailedText()
    {
        const string rawResponse = """
short_text: The move slowed development and gave away initiative.
detailed_text: You brought the queen out before the minor pieces were ready.
That gave Black easy developing moves with tempo.
training_hint: Before every opening queen move, ask which minor piece could improve first.
""";

        bool parsed = LocalModelAdviceResponseParser.TryParse(rawResponse, out LocalModelAdviceResponse? response);

        Assert.True(parsed);
        Assert.NotNull(response);
        Assert.Contains("slowed development", response!.ShortText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("easy developing moves", response.DetailedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("minor piece", response.TrainingHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParsesJsonPayload()
    {
        const string rawResponse = """
{
  "short_text": "The move gave away central control.",
  "detailed_text": "You moved the queen too early and let the opponent develop with tempo.",
  "training_hint": "In the opening, develop a knight or bishop before repeating queen moves."
}
""";

        bool parsed = LocalModelAdviceResponseParser.TryParse(rawResponse, out LocalModelAdviceResponse? response);

        Assert.True(parsed);
        Assert.NotNull(response);
        Assert.Equal("The move gave away central control.", response!.ShortText);
        Assert.Contains("develop with tempo", response.DetailedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("develop a knight", response.TrainingHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParsesMarkdownWrappedJsonPayload()
    {
        const string rawResponse = """
```json
{
  "short_text": "The move lost the initiative.",
  "detailed_text": "You moved the queen too early and let Black develop freely.",
  "training_hint": "Before a queen move in the opening, check whether a knight or bishop can improve first."
}
```
""";

        bool parsed = LocalModelAdviceResponseParser.TryParse(rawResponse, out LocalModelAdviceResponse? response);

        Assert.True(parsed);
        Assert.NotNull(response);
        Assert.Contains("lost the initiative", response!.ShortText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsIncompletePayload()
    {
        const string rawResponse = """
short_text: The move lost time.
training_hint: Improve a minor piece first.
""";

        bool parsed = LocalModelAdviceResponseParser.TryParse(rawResponse, out LocalModelAdviceResponse? response);

        Assert.False(parsed);
        Assert.Null(response);
    }

    [Fact]
    public void ParsesJsonFromLlamaChatStdoutWithEchoedPrompt()
    {
        const string rawResponse = """
            Loading model...


            build      : b8837-59accc886
            model      : qwen2.5-3b-instruct-q4_k_m.gguf
            modalities : text

            available commands:
              /exit or Ctrl+C     stop or exit
              /regen              regenerate the last response
              /clear              clear the chat history
              /read <file>        add a text file
              /glob <pattern>     add text files using globbing pattern


            > Return EXACTLY this JSON object and nothing else:
            {"short_text":"ok","detailed_text":"ok","training_hint":"ok"}

            {"short_text":"ok","detailed_text":"ok","training_hint":"ok"}

            [ Prompt: 49.3 t/s | Generation: 5.2 t/s ]

            Exiting...
            """;

        bool parsed = LocalModelAdviceResponseParser.TryParse(rawResponse, out LocalModelAdviceResponse? response);

        Assert.True(parsed);
        Assert.NotNull(response);
        Assert.Equal("ok", response!.ShortText);
        Assert.Equal("ok", response.DetailedText);
        Assert.Equal("ok", response.TrainingHint);
    }

    [Fact]
    public void ExtractsHttpContentFromValidLlamaServerResponse()
    {
        const string responseJson = """
        {
          "content": "{\"short_text\":\"ok\",\"detailed_text\":\"ok\",\"training_hint\":\"ok\"}",
          "id_slot": 0,
          "stop": true,
          "model": "test.gguf"
        }
        """;

        string? content = LlamaCppHttpAdviceModel.ExtractContent(responseJson);

        Assert.NotNull(content);
        Assert.Contains("short_text", content);
        Assert.Contains("ok", content);
    }

    [Fact]
    public void ExtractHttpContentReturnsNullForEmptyContent()
    {
        const string responseJson = """
        {
          "content": "",
          "stop": true
        }
        """;

        string? content = LlamaCppHttpAdviceModel.ExtractContent(responseJson);

        Assert.Null(content);
    }

    [Fact]
    public void ExtractHttpContentReturnsNullForMissingContentField()
    {
        const string responseJson = """
        {
          "stop": true,
          "model": "test.gguf"
        }
        """;

        string? content = LlamaCppHttpAdviceModel.ExtractContent(responseJson);

        Assert.Null(content);
    }

    [Fact]
    public void ExtractHttpContentReturnsNullForInvalidJson()
    {
        string? content = LlamaCppHttpAdviceModel.ExtractContent("not json");

        Assert.Null(content);
    }

    [Fact]
    public void ExtractedHttpContentCanBeUsedByResponseParser()
    {
        const string responseJson = """
        {
          "content": "{\"short_text\":\"Move explanation\",\"detailed_text\":\"Detailed explanation\",\"training_hint\":\"Training hint\"}",
          "stop": true
        }
        """;

        string? content = LlamaCppHttpAdviceModel.ExtractContent(responseJson);
        Assert.NotNull(content);

        bool parsed = LocalModelAdviceResponseParser.TryParse(content, out LocalModelAdviceResponse? response);

        Assert.True(parsed);
        Assert.NotNull(response);
        Assert.Equal("Move explanation", response!.ShortText);
        Assert.Equal("Detailed explanation", response.DetailedText);
        Assert.Equal("Training hint", response.TrainingHint);
    }
}
