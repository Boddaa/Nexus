using Nexus.Application.Features.AI.Services;
using Xunit;

namespace Nexus.Application.Tests;

public class PromptBuilderTests
{
    private readonly PromptBuilder _builder = new();

    [Fact]
    public void BuildSystemPrompt_ContainsGroundingAndCitationRules()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("NEXUS AI", prompt);
        Assert.Contains("grounded", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not hallucinate", prompt);
        Assert.Contains("[SOURCE 1]", prompt);
        Assert.Contains("insufficient", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildUserPrompt_WithContext_IncludesContextAndQuestion()
    {
        var context = "[SOURCE 1]\nTitle: Safety\nContent: Always wear safety goggles.";
        var question = "What gear is required?";

        var prompt = _builder.BuildUserPrompt(question, context);

        Assert.Contains("Retrieved Workspace Knowledge:", prompt);
        Assert.Contains(context, prompt);
        Assert.Contains("User Question:", prompt);
        Assert.Contains("What gear is required?", prompt);
    }

    [Fact]
    public void BuildUserPrompt_WithoutContext_OmitsContextSection()
    {
        var question = "Hello assistant";
        var prompt = _builder.BuildUserPrompt(question, "");

        Assert.DoesNotContain("Retrieved Workspace Knowledge:", prompt);
        Assert.Contains("User Question:", prompt);
        Assert.Contains("Hello assistant", prompt);
    }
}
