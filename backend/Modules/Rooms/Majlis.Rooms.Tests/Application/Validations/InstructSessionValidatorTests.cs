using Majlis.Rooms.Application.Sessions.DTOs;
using Majlis.Rooms.Application.Sessions.Validations;
using Majlis.Rooms.Domain.Constants;

namespace Majlis.Rooms.Tests.Application.Validations;

public class InstructSessionValidatorTests
{
    private readonly InstructSessionValidator _validator = new();

    private static InstructSessionDto Valid => new() { SessionId = Guid.NewGuid(), Text = "لخّص العقد", Epoch = 1, ClientRequestId = Guid.NewGuid() };

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldRejectWithRequiredKey_WhenTextIsBlank(string text)
    {
        var result = _validator.Validate(Valid with { Text = text });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(InstructSessionDto.Text) && e.ErrorMessage == "General:Fields:Required");
    }

    [Fact]
    public void Validate_ShouldRejectWithMaxLengthKey_WhenTextIsTooLong()
    {
        var result = _validator.Validate(Valid with { Text = new string('أ', RoomsFieldDefinitions.MaxInstructionLength + 1) });

        Assert.Contains(result.Errors, e => e.ErrorMessage == "General:Fields:MaxLength");
    }

    [Fact]
    public void Validate_ShouldRejectUnknownLanguage_WhenLanguageIsNotArOrEn()
    {
        var result = _validator.Validate(Valid with { Language = "fr" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(InstructSessionDto.Language));
    }

    [Fact]
    public void Validate_ShouldPass_WhenInputIsValid()
    {
        Assert.True(_validator.Validate(Valid).IsValid);
    }
}
