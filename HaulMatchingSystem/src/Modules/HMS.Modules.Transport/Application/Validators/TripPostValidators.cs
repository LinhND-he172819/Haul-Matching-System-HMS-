using FluentValidation;

namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed class CreateTripPostRequestValidator : AbstractValidator<CreateTripPostRequest>
{
    public CreateTripPostRequestValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty()
            .WithMessage("Trip ID không được để trống.");

        RuleFor(x => x.AcceptUntil)
            .NotEmpty()
            .WithMessage("Hạn nhận đề xuất không được để trống.")
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("H hạn nhận đề xuất phải lớn hơn thời điểm hiện tại.");

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .WithMessage("Mô tả không được vượt quá 2000 ký tự.");
    }
}

public sealed class UpdateTripPostRequestValidator : AbstractValidator<UpdateTripPostRequest>
{
    public UpdateTripPostRequestValidator()
    {
        RuleFor(x => x.AcceptUntil)
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("Hạn nhận đề xuất phải lớn hơn thời điểm hiện tại.")
            .When(x => x.AcceptUntil.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .WithMessage("Mô tả không được vượt quá 2000 ký tự.");
    }
}
