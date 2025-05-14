using System;
using CardOpsApi.Core.Dtos;
using FluentValidation;

namespace CardOpsApi.Validators
{
    public class DefinitionCreateDtoValidator : AbstractValidator<DefinitionCreateDto>
    {
        public DefinitionCreateDtoValidator()
        {
            RuleFor(d => d.AccountNumber)
                .NotNull().NotEmpty().WithMessage("Account number is required.")
                .MaximumLength(50).WithMessage("Account number cannot exceed 50 characters.");

            RuleFor(d => d.Name)
                .NotNull().NotEmpty().WithMessage("Name is required.")
                .MaximumLength(150).WithMessage("Name cannot exceed 150 characters.");
            RuleFor(d => d.CurrencyId)
                .NotNull().NotEmpty().WithMessage("Currency ID is required.")
                .GreaterThan(0).WithMessage("Currency ID must be greater than 0.");

            RuleFor(d => d.Type)
                .NotNull().NotEmpty().WithMessage("Type is required.")
                .Must(type => type.Equals("ATM", StringComparison.OrdinalIgnoreCase) ||
                              type.Equals("POS", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Type must be either ATM or POS.");
        }
    }

    public class DefinitionUpdateDtoValidator : AbstractValidator<DefinitionUpdateDto>
    {
        public DefinitionUpdateDtoValidator()
        {
            RuleFor(d => d.AccountNumber)
                .NotNull().NotEmpty().WithMessage("Account number is required.")
                .Length(13).WithMessage("FromAccount Should be 13 digits");

            RuleFor(d => d.Name)
                .NotNull().NotEmpty().WithMessage("Name is required.")
                .MaximumLength(150).WithMessage("Name cannot exceed 150 characters.");
            RuleFor(d => d.CurrencyId)
                .NotNull().NotEmpty().WithMessage("Currency ID is required.")
                .GreaterThan(0).WithMessage("Currency ID must be greater than 0.");

            RuleFor(d => d.Type)
                .NotNull().NotEmpty().WithMessage("Type is required.")
                .Must(type => type.Equals("ATM", StringComparison.OrdinalIgnoreCase) ||
                              type.Equals("POS", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Type must be either ATM or POS.");
        }
    }
}
